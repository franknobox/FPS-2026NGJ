using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SlugWhitebox
{
    public class SlugGame : MonoBehaviour
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
        public enum Mode { Briefing, Playing, Paused, Result }
        public Mode mode;
        public bool Running => mode == Mode.Playing;
        public Vector3 PlayerPosition => player.position;
        public float glands, health, maxHealth, maxGlands;
        public int weapon, skill, sector = -1, kills, runBiomass;
        public float[] heat = new float[4];
        public bool[] locked = new bool[4];
        public readonly List<SlugEnemy> enemies = new List<SlugEnemy>();
        public string notice = "", resultTitle = "";
        public float noticeUntil, hitUntil, damageUntil;
        public bool criticalHit;
        public readonly string[] WeaponNames = { "中频 · 脉冲步枪", "高频 · 重击左轮", "低频 · 震爆霰弹", "低频 · 武士刀" };
        public readonly string[] SkillNames = { "裂甲脉冲", "迟滞共振", "震荡爆鸣" };
        public readonly string[] SkillDescriptions = { "前方锥形 · 易伤 5 秒", "周身范围 · 减速 6 秒 + 短硬直", "周身范围 · 易伤 + 硬直 2.5 秒" };
        public readonly string[] SectorNames = { "01 / 接驳平台", "02 / 冷却中庭", "03 / 虫巢回收站" };
        CharacterController controller;
        AudioSource sound;
        AudioClip[] skillSounds;
        float yaw, pitch, shotAt, recoil, footstepAt;
        bool settled, smoke;
        int runGeneration;
        float runTime;
        public int Remaining => enemies.FindAll(e => e && !e.dead).Count;
        public float RunTime => runTime;
        public bool IsSmoke => smoke;
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
            if(fireSounds==null || fireSounds.Length!=4)Array.Resize(ref fireSounds,4);
            for(int i=0;i<4;i++)if(!fireSounds[i])fireSounds[i]=MakeFeedback(i);
            if(!pickupSound)pickupSound=MakeFeedback(4);
            if(!killSound)killSound=MakeFeedback(5);
            if(!hitSound)hitSound=MakeFeedback(6);
            hud = gameObject.AddComponent<SlugHud>(); hud.game = this; hud.Build();
            BeginRun();
            if (smoke) StartCoroutine(SlugSmoke.Run(this));
        }

        public void BeginRun()
        {
            runGeneration++;
            Time.timeScale=1;
            foreach(var enemy in enemies) if(enemy) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach(var drop in drops) if(drop.body) Destroy(drop.body.gameObject);
            drops.Clear();
            maxHealth = tuning.health;
            maxGlands = tuning.glandMax;
            health = maxHealth; glands = maxGlands;
            for(int i=0;i<4;i++) { heat[i]=0; locked[i]=false; }
            kills=runBiomass=0; sector=-1; runTime=0; settled=false;
            weapon=skill=0; shotAt=0; yaw=pitch=0;
            Warp(new Vector3(0,.15f,-2)); player.rotation=Quaternion.identity;
            view.transform.localPosition=new Vector3(0,1.62f,0);view.transform.localRotation = Quaternion.identity;
            mode=Mode.Playing; SetCursor(true); SelectWeapon(0);
            hud.Refresh();
        }

        public void Warp(Vector3 position)
        {
            controller.enabled = false; player.position = position; controller.enabled = true;
        }

        void Update()
        {
            var kb=Keyboard.current; var mouse=Mouse.current;
            if(kb != null && kb.rKey.wasPressedThisFrame) BeginRun();
            if(kb != null && kb.escapeKey.wasPressedThisFrame)
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
                if(mouse.rightButton.wasPressedThisFrame) CastSkill();
                if(mouse.leftButton.isPressed) Fire();
                if(kb.digit1Key.wasPressedThisFrame) SelectWeapon(0);
                if(kb.digit2Key.wasPressedThisFrame) SelectWeapon(1);
                if(kb.digit3Key.wasPressedThisFrame) SelectWeapon(2);
                if(kb.digit4Key.wasPressedThisFrame) SelectWeapon(3);
                if(kb.zKey.wasPressedThisFrame) skill=0;
                if(kb.xKey.wasPressedThisFrame) skill=1;
                if(kb.cKey.wasPressedThisFrame) skill=2;
                float scroll=mouse.scroll.ReadValue().y;
                if(scroll!=0) SelectWeapon((weapon+(scroll>0?3:1))%4);
            }
            else controller.Move(Vector3.down*4*Time.deltaTime);
            recoil=Mathf.Lerp(recoil,0,Time.deltaTime*12);
            weaponPivot.localPosition=new Vector3(0,0,-recoil*.04f);
            weaponPivot.localRotation=Quaternion.Euler(-recoil*8,0,Mathf.Sin(Time.time*3)*.5f);
            UpdateDrops(Time.deltaTime);
            UpdateFlow();
        }

        public void TickResources(float dt)
        {
            glands=Mathf.Min(maxGlands,glands+tuning.glandRegen*dt);
            for(int i=0;i<4;i++)
            {
                // A brief delay after a shot makes sustained fire reach overheat.
                if(i!=weapon || Time.time > shotAt+.15f) heat[i]=Mathf.Max(0,heat[i]-tuning.heatCoolRate*dt);
                if(locked[i] && heat[i]<=tuning.overheatRecovery) locked[i]=false;
            }
        }

        public bool CastSkill()
        {
            if(!Running) return false;
            int cost=tuning.skillCosts[skill];
            if(glands+.001f < cost) { Message("声腺不足 · 需要 "+cost+"，击杀拾取或等待回复",2); hud.GlandFlash(false); return false; }
            glands-=cost; hud.GlandFlash(false);
            int tagged=0;
            foreach(var enemy in enemies)
            {
                if(!enemy || enemy.dead) continue;
                Vector3 vector=enemy.AimPoint-view.transform.position;
                float range=skill==0?tuning.skillRange:skill==1?12:10;
                if(vector.magnitude>range || (skill==0 && Vector3.Angle(view.transform.forward,vector)>48)) continue;
                if(!ClearLine(view.transform.position,enemy.AimPoint)) continue;
                enemy.ApplySkill(skill); tagged++;
            }
            Pulse(player.position+Vector3.up*.8f,skill==0?tuning.skillRange:skill==1?12:10,Color.white,.5f);
            Play(skillSounds[skill],.65f);
            Message(SkillNames[skill]+"  −"+cost+" 声腺 · 影响 "+tagged+" 只",2.2f);
            return true;
        }

        public void SelectWeapon(int index)
        {
            weapon=index;
            for(int i=0;i<weaponModels.Length;i++) if(weaponModels[i]) weaponModels[i].SetActive(i==index);
        }

        public bool Fire(bool bypassInterval=false)
        {
            if(!Running || locked[weapon] || (!bypassInterval && Time.time<shotAt)) return false;
            shotAt=Time.time+tuning.fireIntervals[weapon];
            heat[weapon]=Mathf.Min(100,heat[weapon]+tuning.heatPerShot[weapon]);
            if(heat[weapon]>=100) { locked[weapon]=true; Message("武器过热 · 切换武器继续输出",2); }
            recoil=weapon==1?1.5f:weapon==2?1.8f:1;
            var weaponView=weaponModels[weapon].GetComponent<SlugWeaponView>();
            if(weaponView)weaponView.Fire();
            Play(fireSounds.Length>weapon?fireSounds[weapon]:null,weapon==0?.28f:.42f);
            if(weapon==3)
            {
                SlugEnemy nearest=null; float distance=3.2f;
                foreach(var e in enemies)
                {
                    if(!e || e.dead) continue;
                    Vector3 diff=e.AimPoint-view.transform.position;
                    if(diff.magnitude<distance && Vector3.Angle(view.transform.forward,diff)<65 && ClearLine(view.transform.position,e.AimPoint)) {nearest=e;distance=diff.magnitude;}
                }
                if(nearest) nearest.Hit(tuning.weaponDamage[weapon]);
                return true;
            }
            int pellets=weapon==2?8:1;
            for(int i=0;i<pellets;i++)
            {
                Vector3 dir=view.transform.forward;
                if(weapon==2) dir=(dir+view.transform.right*UnityEngine.Random.Range(-.065f,.065f)+view.transform.up*UnityEngine.Random.Range(-.05f,.05f)).normalized;
                Vector3 end=view.transform.position+dir*100;
                if(Physics.Raycast(view.transform.position,dir,out RaycastHit hit,100,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))
                {
                    end=hit.point;
                    var enemy=hit.collider.GetComponentInParent<SlugEnemy>();
                    if(enemy) enemy.Hit(tuning.weaponDamage[weapon]);
                    Spark(hit.point,enemy?cyanMaterial:orangeMaterial);
                }
                Trail(weaponView&&weaponView.muzzle?weaponView.muzzle.position:view.transform.TransformPoint(.17f,-.14f,.88f),end,Color.white,.075f);
            }
            Vector3 muzzlePoint=weaponView&&weaponView.muzzle?view.transform.InverseTransformPoint(weaponView.muzzle.position):new Vector3(.17f,-.14f,.88f);
            var muzzle=Visual(PrimitiveType.Sphere,view.transform,muzzlePoint,Vector3.one*(weapon==2?.17f:.1f),orangeMaterial);
            Destroy(muzzle,.045f);
            return true;
        }

        void UpdateFlow()
        {
            int next=sector+1;
            if(Remaining==0 && next<3 && player.position.z >= (next==0?3:next*30+1)) ActivateSector(next);
            if(sector==2 && Remaining==0 && Vector3.Distance(player.position,exit.position)<3.5f) EndRun("回收完成 / 任务成功");
        }

        public void ActivateSector(int index)
        {
            sector=index;
            int count=tuning.sectorCounts[index];
            var available=Array.FindAll(spawns,s=>s && s.sector==index);
            for(int i=0;i<count && available.Length>0;i++)
            {
                var marker=available[i%available.Length];
                Vector3 pos=marker.transform.position;
                if(UnityEngine.AI.NavMesh.SamplePosition(pos,out var hit,3,UnityEngine.AI.NavMesh.AllAreas)) pos=hit.position;
                var go=new GameObject("虫族_"+(i+1)); go.transform.position=pos;
                var e=go.AddComponent<SlugEnemy>(); e.Initialize(this,marker.kind); enemies.Add(e);
            }
            Message(SectorNames[index]+" · 清除虫族 "+count+" 只",4);
        }

        public bool ClearLine(Vector3 from, Vector3 to) => !Physics.Linecast(from,to,1<<0,QueryTriggerInteraction.Ignore);
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
            if(Remaining==0) Message(sector<2?"区域肃清 · 推进下一区":"区域肃清 · 前往 EXIT",4);
        }

        public Drop SpawnDrop(Vector3 position)
        {
            var item=new GameObject("声腺 + 生物质"); item.layer=10; item.transform.position=position;
            Visual(PrimitiveType.Cube,item.transform,Vector3.zero,Vector3.one*.4f,cyanMaterial);
            var d=new Drop{body=item.transform,origin=position,gland=tuning.glandDrop,bio=tuning.biomassDrop};
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
            float actual=Mathf.Min(gland,maxGlands-glands);
            glands=Mathf.Min(maxGlands,glands+gland); runBiomass+=bio;
            hud.GlandFlash(true); Play(pickupSound,.35f);
            Message("摄入 +"+actual.ToString("0")+" 声腺 / +"+bio+" 生物质"+(actual<gland?" · 溢出丢弃":""),1.7f);
        }

        public void Hurt(float amount)
        {
            if(!Running) return;
            health=Mathf.Max(0,health-amount); damageUntil=Time.time+.3f;
            if(health<=0)
            {
                EndRun("死亡");
                view.transform.localPosition=Vector3.up*.5f;
                if(!smoke)StartCoroutine(RestartAfterDeath(runGeneration));
            }
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
                if(Physics.Linecast(projectile.transform.position,next,1<<0)) break;
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
            foreach(var e in enemies) if(e && e.agent.isOnNavMesh) e.agent.isStopped=true;
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
            Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial=material;
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
