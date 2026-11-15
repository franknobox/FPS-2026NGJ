using System;
using NGJ.Combat;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SlugWhitebox
{
    public partial class SlugGame : MonoBehaviour
    {
        public SlugTuning tuning;
        public Material surfaceMaterial, cyanMaterial, orangeMaterial, lineMaterial;
        public Transform player, weaponPivot, exit;
        public Camera view;
        public GameObject[] weaponModels;
        public AudioClip[] fireSounds;
        public AudioClip pickupSound, killSound, hitSound;
        public SlugSpawn[] spawns;
        public SlugHud hud;
        public SlugLevelFlow levelFlow;
        public enum Mode { Briefing, Playing, Paused, Result }
        public Mode mode;
        public bool Running => mode == Mode.Playing;
        public Vector3 PlayerPosition => player.position;
        public float EyeHeight => Mathf.Max(.1f,(controller?controller:player.GetComponent<CharacterController>()).height-.12f);
        [HideInInspector] public float glands, health, maxHealth, maxGlands;
        public int weapon, skill, sector = -1, kills, runBiomass;
        [HideInInspector] public float[] heat = new float[4];
        [HideInInspector] public bool[] locked = new bool[4];
        public readonly List<SlugEnemy> enemies = new List<SlugEnemy>();
        public string notice = "", resultTitle = "";
        public float noticeUntil, hitUntil, damageUntil;
        public bool criticalHit;
        public string WeaponDisplayName => weapons && weapons.equipped ? weapons.equipped.displayName : "未装备";
        public readonly string[] SkillNames = { "嘶鸣", "撼地", "共鸣" };
        public readonly string[] SkillDescriptions = { "低穿透声波 · 击晕飞虫", "高穿透震波 · 仅地面", "吸收攻击 · 声波反击" };
        public readonly string[] SectorNames = { "01 / 接驳平台", "02 / 冷却中庭", "03 / 虫巢回收站" };
        CharacterController controller;
        AudioSource sound;
        AudioClip[] skillSounds;
        float yaw, pitch, shotAt, recoil, footstepAt;
        bool settled, smoke;
        int runGeneration;
        float runTime;
        float cameraShake;
        public int Remaining => enemies.FindAll(e => e && !e.dead).Count;
        public float RunTime => runTime;
        public bool IsSmoke => smoke;
        public bool HasStarted { get; private set; }
        public class Drop { public Transform body; public Vector3 origin; public int gland, bio; }
        public readonly List<Drop> drops = new List<Drop>();

        void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 120;
            smoke = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--slug-smoke");
            controller = player.GetComponent<CharacterController>();
            sound = view.gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 0;
            skillSounds = new AudioClip[3];
            for(int i=0;i<3;i++) skillSounds[i] = MakePulse(i);
            if(fireSounds==null || fireSounds.Length!=weaponDefinitions.Length)Array.Resize(ref fireSounds,weaponDefinitions.Length);
            for(int i=0;i<fireSounds.Length;i++)if(!fireSounds[i])fireSounds[i]=MakeFeedback(Mathf.Min(i,3));
            if(!pickupSound)pickupSound=MakeFeedback(4);
            if(!killSound)killSound=MakeFeedback(5);
            if(!hitSound)hitSound=MakeFeedback(6);
            InitializeCombat();
            hud = gameObject.AddComponent<SlugHud>(); hud.game = this; hud.Build();
            BeginRun();
            HasStarted = true;
            if (smoke) StartCoroutine(SlugSmoke.Run(this));
        }

        public void BeginRun()
        {
            runGeneration++;
            var attacks=GetComponent<EnemyAttackCoordinator>();if(attacks)attacks.ResetSchedule();
            Time.timeScale=1;
            foreach(var enemy in enemies) if(enemy) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach(var drop in drops) if(drop.body) Destroy(drop.body.gameObject);
            drops.Clear();
            ResetNewCombat();
            maxHealth = stats.maxNoise;
            maxGlands = stats.maxGlands;
            health = maxHealth; glands = maxGlands;
            for(int i=0;i<4;i++) { heat[i]=0; locked[i]=false; }
            kills=runBiomass=0; sector=-1; runTime=0; settled=false;
            weapon=skill=0; shotAt=0; yaw=pitch=0;
            cameraShake=0; view.fieldOfView=80;
            Warp(levelFlow ? levelFlow.StartPosition : new Vector3(0,.15f,-2)); player.rotation=Quaternion.identity;
            view.transform.localPosition=new Vector3(0,EyeHeight,0);view.transform.localRotation = Quaternion.identity;
            mode=Mode.Playing; SetCursor(true);
            if(weaponLoadout==null || weaponLoadout.Length!=2 || !ConfigureLoadout(weaponLoadout[0],weaponLoadout[1])) ConfigureLoadout(2,weaponDefinitions.Length>4?4:0);
            if(levelFlow)levelFlow.ResetRun(this);
            hud.Refresh();
        }

        public void Warp(Vector3 position)
        {
            controller.enabled = false; player.position = position; controller.enabled = true;
        }

        void Update()
        {
            var kb=Keyboard.current; var mouse=Mouse.current;
            if(!smoke && kb != null && kb.rKey.wasPressedThisFrame) BeginRun();
            if(!smoke && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if(Running) { mode=Mode.Paused; Time.timeScale=0; SetCursor(false); }
                else if(mode==Mode.Paused) Resume();
            }
            if(!Running) return;
            runTime += Time.deltaTime;
            TickResources(Time.deltaTime);
            if(!smoke && mouse != null && kb != null)
            {
                {
                    Vector2 delta=mouse.delta.ReadValue();
                    yaw += delta.x*tuning.mouseSensitivity; pitch=Mathf.Clamp(pitch-delta.y*tuning.mouseSensitivity,-75,75);
                    player.rotation=Quaternion.Euler(0,yaw,0);
                    view.transform.localRotation=Quaternion.Euler(pitch,0,0);
                }
                Vector2 move=new Vector2((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0));
                Vector3 direction=(player.right*move.x+player.forward*move.y).normalized;
                controller.Move((direction*tuning.moveSpeed + Vector3.down*4)*Time.deltaTime);
                if(mouse.rightButton.wasPressedThisFrame) CastWeaponSkill();
                if(kb.qKey.wasPressedThisFrame) CastSkill();
                if(mouse.leftButton.isPressed) Fire();
                if(kb.digit1Key.wasPressedThisFrame) SelectWeaponSlot(0);
                if(kb.digit2Key.wasPressedThisFrame) SelectWeaponSlot(1);
                if(kb.zKey.wasPressedThisFrame) skill=0;
                if(kb.xKey.wasPressedThisFrame) skill=1;
                if(kb.cKey.wasPressedThisFrame) skill=2;
                float scroll=mouse.scroll.ReadValue().y;
                if(scroll!=0) SelectWeaponSlot(weapon==weaponLoadout[0]?1:0);
            }
            else controller.Move(Vector3.down*4*Time.deltaTime);
            recoil=Mathf.Lerp(recoil,0,Time.deltaTime*12);
            weaponPivot.localPosition=new Vector3(0,0,-recoil*.04f);
            weaponPivot.localRotation=Quaternion.Euler(-recoil*8,0,Mathf.Sin(Time.time*3)*.5f);
            cameraShake=Mathf.Lerp(cameraShake,0,Time.deltaTime*16);
            view.transform.localPosition=new Vector3(0,EyeHeight,0)+UnityEngine.Random.insideUnitSphere*cameraShake;
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,80,Time.deltaTime*10);
            UpdateDrops(Time.deltaTime);
            UpdateFlow();
        }

        public void TickResources(float dt)
        {
            glands=stats.Glands; maxGlands=stats.maxGlands;
            maxHealth=stats.maxNoise; health=maxHealth-stats.Noise;
        }

        // Q 释放选中的声腺技能；实际扣费、冷却和命中由 NGJ 模块负责。
        public bool CastSkill()
        {
            if(!Running) return false;
            bool ok=runner.TryCast(glandSkills[skill]);
            if(!ok) Message("声腺不足、技能冷却或共鸣尚未结束",2);
            return ok;
        }

        public bool SelectWeapon(int index)
        {
            if(weaponLoadout==null || weaponLoadout.Length!=2 || Array.IndexOf(weaponLoadout,index)<0 ||
                weaponDefinitions==null || index<0 || index>=weaponDefinitions.Length || !weaponDefinitions[index])return false;
            weapon=index;
            weapons.Equip(weaponDefinitions[index]);
            if(weaponModels!=null)for(int i=0;i<weaponModels.Length;i++) if(weaponModels[i]) weaponModels[i].SetActive(i==index);
            var selectedView=weaponModels!=null && index<weaponModels.Length && weaponModels[index] ? weaponModels[index].GetComponent<SlugWeaponView>() : null;
            weapons.muzzle=selectedView?selectedView.muzzle:null;
            return true;
        }

        public bool Fire(bool bypassInterval=false)
        {
            if(!Running || !weapons.TryFire()) return false;
            var weaponView=weaponModels[weapon].GetComponent<SlugWeaponView>();
            recoil=weaponView && weaponView.feedbackSource ? 0 : weapon==1?1.5f:weapon==2?1.8f:1;
            if(weaponView)weaponView.Fire();
            if(weaponView && weaponView.feedbackSource)
            {
                cameraShake=weaponView.feedbackSource.camShakeAmount;
                if(weaponView.feedbackSource.applyFOVEffectOnShooting)view.fieldOfView=80-weaponView.feedbackSource.FOVValueToSubtract;
            }
            else Play(fireSounds[weapon],.3f);
            Vector3 start=weaponView && weaponView.muzzle ? weaponView.muzzle.position : view.transform.TransformPoint(.17f,-.14f,.88f);
            if(!weaponDefinitions[weapon].projectilePrefab && !weaponDefinitions[weapon].bulletTrail)
            {
                Vector3 end=view.transform.position+view.transform.forward*weaponDefinitions[weapon].range;
                if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,weaponDefinitions[weapon].range,CombatLayers.PlayerAttackMask,QueryTriggerInteraction.Ignore)) end=hit.point;
                Trail(start,end,Color.cyan,.08f);
            }
            if(!weaponDefinitions[weapon].muzzleEffect){var flash=Visual(PrimitiveType.Sphere,null,start,Vector3.one*.12f,orangeMaterial);Destroy(flash,.05f);}
            return true;
        }

        void UpdateFlow()
        {
            if(levelFlow){levelFlow.Tick(this);return;}
            int next=sector+1;
            if(!smoke && Remaining==0 && next<3 && player.position.z >= (next==0?3:next*30+1)) ActivateSector(next);
            if(sector==2 && Remaining==0 && Vector3.Distance(player.position,exit.position)<3.5f) EndRun("回收完成 / 任务成功");
        }

        public void ActivateSector(int index)
        {
            sector=index;
            int count=tuning.sectorCounts[index];
            var available=Array.FindAll(spawns,s=>s && s.sector==index);
            int miVariantIndex=0;
            for(int i=0;i<count && available.Length>0;i++)
            {
                var marker=available[i%available.Length];
                Vector3 pos=marker.transform.position;
                if(UnityEngine.AI.NavMesh.SamplePosition(pos,out var hit,3,UnityEngine.AI.NavMesh.AllAreas)) pos=hit.position;
                var prefab=enemyPrefabs[Mathf.Clamp(marker.kind,0,2)];
                if(marker.kind==1 && miYinVariants!=null && miYinVariants.Length>0)
                {
                    var variant=miYinVariants[miVariantIndex++%miYinVariants.Length];
                    if(variant)prefab=variant;
                }
                var go=Instantiate(prefab,pos,Quaternion.identity).gameObject;
                var e=go.GetComponent<SlugEnemy>() ?? go.AddComponent<SlugEnemy>(); e.Initialize(this,marker.kind); enemies.Add(e);
            }
            Message(SectorNames[index]+" · 清除虫族 "+count+" 只",4);
        }

        public bool ClearLine(Vector3 from, Vector3 to) => !Physics.Linecast(from,to,CombatLayers.WorldMask,QueryTriggerInteraction.Ignore);
        public void HitFeedback(bool critical)
        {
            hitUntil=Time.time+.13f; criticalHit=critical;
            if(Time.time>footstepAt) {Play(hitSound,.16f);footstepAt=Time.time+.08f;}
        }
        public void EnemyKilled(SlugEnemy enemy)
        {
            kills++; Play(killSound,.25f);
            SpawnDrop(enemy.transform.position+Vector3.up*.5f);
            Pulse(enemy.AimPoint,1.8f,Color.white,.2f);
            if(levelFlow){levelFlow.OnEnemyKilled(this,enemy);return;}
            if(Remaining==0) Message(sector<2?"区域肃清 · 推进下一区":"区域肃清 · 前往 EXIT",4);
        }

        public Drop SpawnDrop(Vector3 position)
        {
            var item=new GameObject("声腺 + 生物质"); item.layer=LayerMask.NameToLayer("Interactable"); item.transform.position=position;
            Visual(PrimitiveType.Cube,item.transform,Vector3.zero,Vector3.one*.4f,cyanMaterial);
            var d=new Drop{body=item.transform,origin=position,gland=1,bio=tuning.biomassDrop};
            drops.Add(d); return d;
        }
        void UpdateDrops(float dt)
        {
            for(int i=drops.Count-1;i>=0;i--)
            {
                var d=drops[i]; if(!d.body) {drops.RemoveAt(i);continue;}
                float dist=Vector3.Distance(player.position+Vector3.up*.8f,d.body.position);
                bool clear=ClearLine(d.body.position,player.position+Vector3.up*.8f);
                if(dist<tuning.pickupRadius && clear) d.body.position=Vector3.MoveTowards(d.body.position,player.position+Vector3.up*.8f,dt*10);
                else d.body.position=d.origin+Vector3.up*Mathf.Sin(Time.time*2+i)*.1f;
                d.body.Rotate(Vector3.up,70*dt);
                if(dist<1.1f && clear) { Collect(d.gland,d.bio); Destroy(d.body.gameObject); drops.RemoveAt(i); }
            }
        }
        public void Collect(int gland,int bio)
        {
            float before=stats.Glands; stats.AddGland(gland); runBiomass+=bio;
            hud.GlandFlash(true); Play(pickupSound,.35f);
            Message("摄入 +"+(stats.Glands-before).ToString("0")+" 声腺 / +"+bio+" 生物质",1.7f);
        }

        public void Hurt(float amount)
        {
            if(Running) stats.AddNoise(amount);
        }

        public void SpawnSpit(Vector3 from,Vector3 target) { StartCoroutine(Spit(from,target)); }
        IEnumerator Spit(Vector3 from,Vector3 target)
        {
            int generation=runGeneration;
            var projectile=Visual(PrimitiveType.Sphere,null,from,Vector3.one*.33f,orangeMaterial);
            projectile.name="酸液弹";
            Vector3 dir=(target-from).normalized;
            for(float t=0;t<3 && projectile;t+=Time.deltaTime)
            {
                if(generation!=runGeneration || mode==Mode.Result || mode==Mode.Briefing)break;
                if(!Running) {yield return null; t-=Time.deltaTime; continue;}
                Vector3 next=projectile.transform.position+dir*11*Time.deltaTime;
                if(Physics.Linecast(projectile.transform.position,next,CombatLayers.WorldMask)) break;
                projectile.transform.position=next;
                if(Vector3.Distance(next,player.position+Vector3.up)<.7f) {Hurt(tuning.enemyDamage);break;}
                yield return null;
            }
            if(projectile) Destroy(projectile);
        }

        public void Resume() { Time.timeScale=1;mode=Mode.Playing; SetCursor(true); }
        public void EndRun(string title)
        {
            if(settled) return;
            Time.timeScale=1;settled=true;resultTitle=title;mode=Mode.Result;SetCursor(false);
            foreach(var e in enemies) if(e && e.agent && e.agent.isOnNavMesh) e.agent.isStopped=true;
            foreach(var e in enemies) if(e && e.combat) e.combat.enabled=false;
            runner.Cancel(); stats.enabled=false; ClearEffects();
            if(levelFlow)levelFlow.StopRun();
        }
        IEnumerator RestartAfterDeath(int generation)
        {
            yield return new WaitForSeconds(1.5f);
            if(generation==runGeneration && mode==Mode.Result)BeginRun();
        }
        public void Message(string text,float duration=2) {notice=text;noticeUntil=Time.time+duration;}
        void SetCursor(bool capture) { Cursor.lockState=capture?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!capture; }
        public void AimAt(Vector3 point)
        {
            view.transform.rotation=Quaternion.LookRotation(point-view.transform.position);
        }

        public GameObject Visual(PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type); go.layer=2;
            if(parent) go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=scale;
            go.GetComponent<Collider>().enabled=false; Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial=material;
            return go;
        }
        void Spark(Vector3 point,Material mat) { var go=Visual(PrimitiveType.Sphere,null,point,Vector3.one*.14f,mat);Destroy(go,.15f); }
        public void Trail(Vector3 a,Vector3 b,Color color,float lifetime)
        {
            var go=new GameObject("曳光"); go.layer=2;
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=lineMaterial;
            line.positionCount=2;line.SetPosition(0,a);line.SetPosition(1,b);line.startWidth=.025f;line.endWidth=.008f;line.startColor=line.endColor=color;
            Destroy(go,lifetime);
        }
        public void Pulse(Vector3 center,float radius,Color color,float duration) {StartCoroutine(PulseRoutine(center,radius,color,duration));}
        IEnumerator PulseRoutine(Vector3 center,float radius,Color color,float duration)
        {
            var go=new GameObject("声波扩散"); go.layer=2;
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.loop=true;line.positionCount=80;line.startWidth=line.endWidth=.06f;
            for(float t=0;t<duration;t+=Time.deltaTime)
            {
                float f=t/duration; color.a=1-f;line.startColor=line.endColor=color;
                for(int i=0;i<80;i++) {float a=i*Mathf.PI*2/80;line.SetPosition(i,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*Mathf.Lerp(.4f,radius,f));}
                yield return null;
            }
            Destroy(go);
        }
        void Play(AudioClip clip,float volume) {if(clip)sound.PlayOneShot(clip,volume);}
        // Some supplied audio files are LFS pointers. Always provide audible prototype feedback.
        AudioClip MakeFeedback(int index)
        {
            int rate=22050;float length=index==2?.36f:index==3?.22f:index==4?.24f:.15f;
            var data=new float[(int)(rate*length)];var noise=new System.Random(900+index);
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,f=t/length,n=(float)noise.NextDouble()*2-1;
                float envelope=Mathf.Min(1,t*1200)*Mathf.Exp(-f*(index<4?7:4));
                float frequency=index==0?130:index==1?390:index==2?62:index==3?900:index==4?600+f*1100:index==5?230:1200;
                float tone=Mathf.Sin(2*Mathf.PI*frequency*t);
                data[i]=(index<3?(n*.65f+tone*.35f):index==3?n*.6f:tone*.55f)*envelope*.8f;
            }
            var clip=AudioClip.Create("白盒反馈_"+index,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        AudioClip MakePulse(int index)
        {
            int rate=22050; float length=.6f; var data=new float[(int)(rate*length)];
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate, f=t/length;
                data[i]=(Mathf.Sin(2*Mathf.PI*(440-index*100)*t*(1-f*.65f))*.45f+Mathf.Sin(2*Mathf.PI*65*t)*.3f)*Mathf.Sin(Mathf.PI*f)*Mathf.Exp(-f*3);
            }
            var clip=AudioClip.Create("拟声脉冲_"+index,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
    }
}
