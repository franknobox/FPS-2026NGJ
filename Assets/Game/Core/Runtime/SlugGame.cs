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
        [Tooltip("死亡掉落的声腺外观；仅表现，收益与吸附仍由本组件处理。")]
        public GameObject glandDropPrefab;
        public SlugHud hud;
        public SlugLevelFlow levelFlow;
        public NGJ.Progression.ProgressionSettings progressionSettings;
        [HideInInspector] public NGJ.Progression.RunProgression progression;
        [HideInInspector] public float moveSpeedMultiplier=1;
        ClientFlow.GameOverlay clientOverlay;
        public enum Mode { Briefing, Playing, Paused, Result, Evolution }
        public Mode mode;
        public bool Running => mode == Mode.Playing;
        public Vector3 PlayerPosition => player.position;
        public float EyeHeight => Mathf.Max(.1f,(controller?controller:player.GetComponent<CharacterController>()).height-.12f);
        public int weapon, skill, sector = -1, kills, runBiomass;
        public readonly List<SlugEnemy> enemies = new List<SlugEnemy>();
        public string notice = "", resultTitle = "";
        public float noticeUntil, hitUntil, damageUntil;
        public bool criticalHit;
        public string WeaponDisplayName => weapons && weapons.equipped ? weapons.equipped.displayName : "未装备";
        public readonly string[] SkillNames = { "嘶鸣", "撼地", "共鸣" };
        public readonly string[] SkillDescriptions = { "低穿透声波 · 击晕飞虫", "高穿透震波 · 仅地面", "吸收攻击 · 声波反击" };
        CharacterController controller;
        AudioSource sound;
        public AudioClip[] skillSounds;
        float yaw, pitch, recoil, footstepAt;
        bool settled;
        float runTime;
        public OutpostCameraShake cameraFeedback;
        float cameraShake;
        bool sniperAiming;
        bool ignoreHeldFire;
        public bool IsSniperEquipped => weapons && weapons.equipped && weapons.equipped.type==NGJ.Combat.WeaponType.JianXiaoSniper;
        public bool IsSniperAiming => Running && sniperAiming && IsSniperEquipped;
        float AimFov => IsSniperAiming?30f:80f;
        public int Remaining { get { int count=0; foreach(var enemy in enemies) if(enemy && !enemy.dead) count++; return count; } }
        public float RunTime => runTime;
        public bool HasStarted { get; private set; }
        public class Drop { public Transform body; public Vector3 origin; public int gland, bio; }
        public readonly List<Drop> drops = new List<Drop>();

        void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 120;
            controller = player.GetComponent<CharacterController>();
            sound = view.gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 0;
            if(fireSounds==null || fireSounds.Length!=weaponDefinitions.Length)Array.Resize(ref fireSounds,weaponDefinitions.Length);
            InitializeCombat();
            cameraFeedback=GetComponent<OutpostCameraShake>();
            if(!cameraFeedback)cameraFeedback=gameObject.AddComponent<OutpostCameraShake>();
            cameraFeedback.game=this;
            progression=gameObject.AddComponent<NGJ.Progression.RunProgression>();progression.Initialize(this,progressionSettings);
            hud = gameObject.AddComponent<SlugHud>(); hud.game = this; hud.Build();
            ClientFlow.ClientSettings.Apply();
            if(levelFlow) { clientOverlay=gameObject.AddComponent<ClientFlow.GameOverlay>(); clientOverlay.Initialize(this); }
            BeginRun();
            HasStarted = true;
        }

        public void BeginRun()
        {
            var attacks=GetComponent<EnemyAttackCoordinator>();if(attacks)attacks.ResetSchedule();
            Time.timeScale=1;
            foreach(var enemy in enemies) if(enemy) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach(var drop in drops) if(drop.body) Destroy(drop.body.gameObject);
            drops.Clear();
            progression?.BeginRun();ignoreHeldFire=true;
            ResetNewCombat();
            kills=runBiomass=0; sector=-1; runTime=0; settled=false;
            weapon=skill=0; yaw=pitch=0;
            cameraShake=0; view.fieldOfView=80;
            Warp(levelFlow ? levelFlow.StartPosition : new Vector3(0,.15f,-2)); player.rotation=Quaternion.identity;
            view.transform.localPosition=new Vector3(0,EyeHeight,0);view.transform.localRotation = Quaternion.identity;
            mode=Mode.Playing; SetCursor(true);
            if(!ConfigureLoadout(weaponLoadout)) ConfigureLoadout(2,4,0,1);
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
            if(kb != null && kb.rKey.wasPressedThisFrame && (Running || mode==Mode.Result && (!clientOverlay || clientOverlay.CanQuickRestart)))
            {
                if(Running && clientOverlay)clientOverlay.RequestRestart();else BeginRun();
            }
            if(kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if(clientOverlay && clientOverlay.HandleEscape()) { }
                else if(Running) Pause();
                else if(mode==Mode.Paused) Resume();
            }
            if(!Running) {sniperAiming=false;return;}
            runTime += Time.deltaTime;
            if(mouse != null && kb != null)
            {
                {
                    sniperAiming=IsSniperEquipped && mouse.rightButton.isPressed;
                    Vector2 delta=mouse.delta.ReadValue()*(IsSniperAiming?.35f:1f);
                    yaw += delta.x*tuning.mouseSensitivity*ClientFlow.ClientSettings.Sensitivity; pitch=Mathf.Clamp(pitch-delta.y*tuning.mouseSensitivity*ClientFlow.ClientSettings.Sensitivity,-75,75);
                    player.rotation=Quaternion.Euler(0,yaw,0);
                    view.transform.localRotation=Quaternion.Euler(pitch,0,0);
                }
                Vector2 move=new Vector2((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0));
                Vector3 direction=(player.right*move.x+player.forward*move.y).normalized;
                controller.Move((direction*tuning.moveSpeed*moveSpeedMultiplier + Vector3.down*4)*Time.deltaTime);
                if(!IsSniperEquipped && mouse.rightButton.wasPressedThisFrame || IsSniperEquipped && kb.eKey.wasPressedThisFrame) CastWeaponSkill();
                if(kb.qKey.wasPressedThisFrame) CastSkill();
                if(!mouse.leftButton.isPressed)ignoreHeldFire=false;
                if(mouse.leftButton.isPressed && !ignoreHeldFire && (!weapons.RequiresPress||mouse.leftButton.wasPressedThisFrame)) Fire();
                if(kb.digit1Key.wasPressedThisFrame) SelectWeaponSlot(0);
                if(kb.digit2Key.wasPressedThisFrame) SelectWeaponSlot(1);
                if(kb.digit3Key.wasPressedThisFrame) SelectWeaponSlot(2);
                if(kb.digit4Key.wasPressedThisFrame) SelectWeaponSlot(3);
                if(kb.zKey.wasPressedThisFrame) skill=0;
                if(kb.xKey.wasPressedThisFrame) skill=1;
                if(kb.cKey.wasPressedThisFrame) skill=2;
                float scroll=mouse.scroll.ReadValue().y;
                if(scroll!=0 && weaponLoadout!=null && weaponLoadout.Length>0)
                    SelectWeaponSlot((Array.IndexOf(weaponLoadout,weapon)+(scroll<0?1:weaponLoadout.Length-1))%weaponLoadout.Length);
            }
            else controller.Move(Vector3.down*4*Time.deltaTime);
            recoil=Mathf.Lerp(recoil,0,Time.deltaTime*12);
            weaponPivot.localPosition=new Vector3(0,0,-recoil*.04f);
            weaponPivot.localRotation=Quaternion.Euler(-recoil*8,0,Mathf.Sin(Time.time*3)*.5f);
            cameraShake=Mathf.Lerp(cameraShake,0,Time.deltaTime*16);
            view.transform.localPosition=new Vector3(0,EyeHeight,0)+UnityEngine.Random.insideUnitSphere*cameraShake;
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,AimFov,Time.deltaTime*14);
            if(weaponModels[weapon])weaponModels[weapon].GetComponent<SlugWeaponView>()?.SetAiming(IsSniperAiming);
            UpdateDrops(Time.deltaTime);
            UpdateFlow();
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
            if(weaponLoadout==null || Array.IndexOf(weaponLoadout,index)<0 ||
                weaponDefinitions==null || index<0 || index>=weaponDefinitions.Length || !weaponDefinitions[index])return false;
            weapon=index;
            sniperAiming=false;
            weapons.Equip(weaponDefinitions[index]);
            if(weaponModels!=null)for(int i=0;i<weaponModels.Length;i++) if(weaponModels[i]) weaponModels[i].SetActive(i==index);
            var selectedView=weaponModels!=null && index<weaponModels.Length && weaponModels[index] ? weaponModels[index].GetComponent<SlugWeaponView>() : null;
            weapons.muzzle=selectedView?selectedView.muzzle:null;
            return true;
        }

        public bool Fire(bool bypassInterval=false)
        {
            return Running && weapons.TryFire();
        }
        void PresentWeaponShot(NGJ.Combat.WeaponDefinition firedWeapon)
        {
            var weaponView=weaponModels[weapon].GetComponent<SlugWeaponView>();
            recoil=weaponView && weaponView.feedbackSource ? 0 : weapon==1?1.5f:weapon==2?1.8f:1;
            if(weaponView)weaponView.Fire();
            if(cameraFeedback)cameraFeedback.WeaponImpulse(weapon);
            if(weaponView && weaponView.feedbackSource)
            {
                cameraShake=weaponView.feedbackSource.camShakeAmount;
                if(weaponView.feedbackSource.applyFOVEffectOnShooting)view.fieldOfView=AimFov-weaponView.feedbackSource.FOVValueToSubtract;
            }
            else Play(fireSounds[weapon],weapon==2?.075f:.15f);
            Vector3 start=weaponView && weaponView.muzzle ? weaponView.muzzle.position : view.transform.TransformPoint(.17f,-.14f,.88f);
            if(!weaponDefinitions[weapon].projectilePrefab && !weaponDefinitions[weapon].bulletTrail)
            {
                Vector3 end=view.transform.position+view.transform.forward*weaponDefinitions[weapon].range;
                if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,weaponDefinitions[weapon].range,CombatLayers.PlayerAttackMask,QueryTriggerInteraction.Ignore)) end=hit.point;
                Trail(start,end,Color.cyan,.08f);
            }
            if(!weaponDefinitions[weapon].muzzleEffect){var flash=Visual(PrimitiveType.Sphere,null,start,Vector3.one*.12f,orangeMaterial);Destroy(flash,.05f);}
        }

        void UpdateFlow(){levelFlow?.Tick(this);}

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
            var item=glandDropPrefab?Instantiate(glandDropPrefab,position,Quaternion.identity):new GameObject("声腺 + 生物质");
            item.layer=LayerMask.NameToLayer("Interactable"); item.transform.position=position;
            if(!glandDropPrefab)Visual(PrimitiveType.Sphere,item.transform,Vector3.zero,new Vector3(.22f,.32f,.22f),orangeMaterial);
            var d=new Drop{body=item.transform,origin=position,gland=1,bio=progression?progression.Settings.biomassPerEnemy:tuning.biomassDrop};
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
            float gained=stats.Glands-before;
            if(gained>0||bio>0)Message((gained>0?$"声腺 +{gained:0.#}  ":"")+(bio>0?$"生物质 +{bio}":""),1.7f);
            progression?.AddBiomass(bio);
        }
        public void CollectUnclaimedDrops()
        {
            int glands=0,biomass=0;
            foreach(var drop in drops)if(drop.body){glands+=drop.gland;biomass+=drop.bio;Destroy(drop.body.gameObject);}
            drops.Clear();if(glands>0||biomass>0)Collect(glands,biomass);
        }

        public void Hurt(float amount)
        {
            if(Running) stats.AddNoise(amount);
        }

        public void Pause() { if(!Running)return;sniperAiming=false;view.fieldOfView=80;mode=Mode.Paused;Time.timeScale=0;SetCursor(false); }
        public void EnterEvolution(){if(!Running)return;sniperAiming=false;view.fieldOfView=80;mode=Mode.Evolution;Time.timeScale=0;SetCursor(false);}
        public void Resume() { ignoreHeldFire=true;Time.timeScale=1;mode=Mode.Playing; SetCursor(true); }
        public void EndRun(string title)
        {
            if(settled) return;
            progression?.CancelPending();
            sniperAiming=false;view.fieldOfView=80;Time.timeScale=1;settled=true;resultTitle=title;mode=Mode.Result;SetCursor(false);
            foreach(var e in enemies) if(e && e.agent && e.agent.isOnNavMesh) e.agent.isStopped=true;
            foreach(var e in enemies) if(e && e.combat) e.combat.enabled=false;
            runner.Cancel(); stats.enabled=false; ClearEffects();
            if(levelFlow)levelFlow.StopRun();
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
    }
}
