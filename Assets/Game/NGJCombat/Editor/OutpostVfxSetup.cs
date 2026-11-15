using System;
using System.IO;
using System.Linq;
using NGJ.Combat;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NGJ.EditorTools
{
    // 增量接入：只更新特效引用，不重建关卡、模型或战斗数值。
    public static class OutpostVfxSetup
    {
        const string Root = "Assets/ART/VFX/Outpost";
        const string Gen = "Assets/Game/NGJCombat/Generated";
        static readonly Color Amber = new Color(1,.48f,.12f,.85f);
        static readonly Color Cyan = new Color(.25f,.85f,1,.7f);
        static readonly Color Violet = new Color(.7f,.36f,1,.7f);
        [MenuItem("NGJ/特效/接入荒漠前哨特效")]
        public static void Apply()
        {
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/ThirdParty/KenneyParticlePack"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Default; imp.alphaIsTransparency=true;
                imp.mipmapEnabled=false; imp.wrapMode=TextureWrapMode.Clamp; imp.SaveAndReimport();
            }
            foreach(var id in new[]{"SiMing","JianXiao","HanYin","BaoMing","RocketLauncher"})
            {
                var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Gen+"/Weapons/"+id+".asset");
                bool sniper=id=="JianXiao", shotgun=id=="HanYin";
                Color color=sniper?Violet:Amber;
                var muzzle=New(id+"_Muzzle",.22f);
                Burst(muzzle,"枪口核心","muzzle_01",color,1,shotgun?.95f:.6f,.075f,0,.01f);
                Burst(muzzle,"侧向火星","spark_01",color,shotgun?16:7,.045f,.18f,shotgun?5:3,.03f);
                Burst(muzzle,"淡烟","smoke_04",new Color(.4f,.32f,.28f,.18f),3,.25f,.2f,.5f,.05f,false);
                weapon.muzzleEffect=Save(muzzle);
                var impact=New(id+"_Impact",.5f);
                Burst(impact,"命中火星","spark_01",color,shotgun?12:8,.06f,.25f,4,.08f);
                Burst(impact,"冲击闪光","light_01",color,1,.42f,.12f,0,0);
                Burst(impact,"尘屑","smoke_01",new Color(.48f,.38f,.29f,.32f),5,.4f,.45f,.5f,.15f,false);
                weapon.impactEffect=Save(impact);
                var trailRoot=new GameObject(id+"_Tracer");
                var trail=trailRoot.AddComponent<TrailRenderer>();
                trail.sharedMaterial=Material("light_01",true); trail.time=sniper?.18f:.08f;
                trail.startWidth=sniper?.035f:.018f;trail.endWidth=.003f;
                trail.startColor=color; trail.endColor=new Color(color.r,color.g,color.b,0);
                trail.minVertexDistance=.1f;trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                weapon.bulletTrail=Save(trailRoot).GetComponent<TrailRenderer>();
                EditorUtility.SetDirty(weapon);
            }
            foreach(string id in new[]{"WideStunWave","CloseStunWave","GroundWave","ResonanceWave","SniperWave","GrenadeExplosion","StickyExplosion","RocketExplosion"})
            {
                EditPrefab(Gen+"/Prefabs/"+id+".prefab",root=>
                {
                    var hit=root.GetComponent<CombatHitbox>();
                    bool explosion=id.Contains("Explosion"),ground=id=="GroundWave";
                    bool weaponWave=id=="CloseStunWave"||id=="SniperWave";
                    Color color=explosion||weaponWave?Amber:id=="ResonanceWave"?Violet:Cyan;
                    var fx=New(id+"_VFX",explosion?1.4f:hit.speed>0?hit.lifetime:.65f);
                    var motion=fx.GetComponent<CombatVfxMotion>();
                    motion.expandRing=!weaponWave;motion.radius=hit.radius;motion.halfAngle=hit.halfAngle;
                    motion.directionalBurst=weaponWave;
                    motion.echoCount=explosion?1:3;
                    motion.verticalOffset=explosion?.05f:-1.25f;
                    motion.groundRing=ground||explosion||id=="ResonanceWave";motion.tint=color;motion.ringMaterial=Material(null,true);
                    if(explosion)
                    {
                        Burst(fx,"火光","flame_01",color,16,1.8f,.4f,3,.3f);
                        Burst(fx,"爆炸核心","light_01",color,2,hit.radius*.8f,.2f,0,.1f);
                        Burst(fx,"飞溅火花","spark_01",Amber,32,.09f,.7f,8,.3f);
                        Burst(fx,"烟尘","smoke_01",new Color(.28f,.23f,.19f,.55f),18,1.7f,1.25f,2,.6f,false);
                        // 爆心抬离地面，避免火球有一半被地形遮住。
                        foreach(Transform emitter in fx.transform) emitter.localPosition=Vector3.up*.65f;
                    }
                    else if(weaponWave)
                    {
                        // 武器技能用短促前向冲击束，不再复用声腺扩散圆环。
                        Burst(fx,"定向冲击火花","spark_01",Amber,20,.10f,.35f,6,.2f);
                        Burst(fx,"压缩闪光","light_01",Amber,1,.8f,.18f,0,0);
                    }
                    else
                    {
                        Burst(fx,"声波光环","circle_03",color,2,Mathf.Min(3,hit.radius*.6f),.45f,0,.05f);
                        Burst(fx,"共振微粒","light_01",color,10,.035f,.5f,ground?2:1,.3f);
                        if(ground) Burst(fx,"扬尘","smoke_04",new Color(.64f,.43f,.23f,.42f),24,.9f,.6f,5,.6f,false);
                    }
                    hit.visualEffect=Save(fx);
                    var contact=New(id+"_Contact",.5f);
                    Burst(contact,"命中核心","light_01",color,1,.85f,.18f,0,0);
                    Burst(contact,"命中碎光","spark_01",color,12,.1f,.35f,3,.12f);
                    hit.impactEffect=Save(contact);
                    var old=root.GetComponent<SlugWhitebox.SlugCombatEffect>();if(old)Object.DestroyImmediate(old);
                });
            }
            foreach(var id in new[]{"Grenade","StickyBomb","RocketProjectile"})
                EditPrefab(Gen+"/Prefabs/"+id+".prefab",root=>
                {
                    var old=root.GetComponent<SlugWhitebox.SlugCombatEffect>();if(old)Object.DestroyImmediate(old);
                    var previous=root.transform.Find("OutpostVFX");if(previous)Object.DestroyImmediate(previous.gameObject);
                    var visual=new GameObject("OutpostVFX");visual.transform.SetParent(root.transform,false);
                    var core=Loop(visual,"弹芯","light_01",id=="StickyBomb"?Violet:Amber,.25f,.1f,25);
                    core.transform.localPosition=Vector3.zero;
                    var smoke=Loop(visual,"尾烟","smoke_04",new Color(.45f,.32f,.25f,.3f),.4f,.6f,18,false);
                    var main=smoke.main;main.simulationSpace=ParticleSystemSimulationSpace.World;
                    var size=smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.2f,1,1.6f));
                });
            foreach(var skill in AssetDatabase.FindAssets("t:SkillDefinition",new[]{Gen+"/Skills"}).Select(g=>AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(g))))
            {
                bool gland=skill.name=="SiMing"||skill.name=="HanDi"||skill.name=="GongMing";
                var cast=New(skill.name+"_Cast",.35f);
                if(gland)
                {
                    Burst(cast,"声腺呼吸脉冲","circle_03",Cyan,2,.65f,.3f,0,0);
                    Burst(cast,"声腺微光","light_01",Violet,6,.03f,.3f,.6f,.15f);
                }
                else
                {
                    Burst(cast,"武器激活火星","spark_01",Amber,16,.09f,.28f,2,.12f);
                    Burst(cast,"武器激活闪光","light_01",Amber,1,.65f,.16f,0,0);
                }
                skill.castEffect=Save(cast);
                skill.activeEffect=null;
                if(skill.effect==SkillEffect.Resonance||skill.effect==SkillEffect.DamageBuff)
                {
                    var aura=new GameObject(skill.name+"_Active");
                    var ps=Loop(aura,gland?"持续共振":"武器过载火星",gland?"circle_03":"spark_01",gland?Cyan:Amber,gland?.18f:.09f,gland?.6f:.35f,gland?4:18);
                    var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=.65f;
                    ps.transform.localPosition=new Vector3(0,-.25f,.8f);
                    skill.activeEffect=Save(aura);
                }
                EditorUtility.SetDirty(skill);
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("OUTPOST_VFX_READY: 5 weapons, 7 skills, 8 hitboxes, 3 projectiles. Kenney CC0.");
        }
        static GameObject New(string name,float duration)
        {
            var go=new GameObject(name);go.AddComponent<CombatVfxMotion>().duration=duration;return go;
        }
        static Material Material(string texture,bool additive)
        {
            string path=Root+"/"+(texture??"Solid")+(additive?"_Add":"_Alpha")+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("ART/CombatParticle"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetFloat("_DstBlend",additive?1:10);
            if(texture!=null)mat.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/KenneyParticlePack/Textures/"+texture+".png"));
            EditorUtility.SetDirty(mat);return mat;
        }
        static ParticleSystem Base(GameObject parent,string name,string texture,Color tint,float size,float life,float speed,bool additive)
        {
            var go=new GameObject(name);go.transform.SetParent(parent.transform,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.duration=1;main.loop=false;main.playOnAwake=true;main.startLifetime=life;
            main.startSize=new ParticleSystem.MinMaxCurve(size*.7f,size);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.4f,speed);
            main.startColor=tint;main.maxParticles=128;main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var fade=ps.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var sizeOver=ps.sizeOverLifetime;sizeOver.enabled=true;sizeOver.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,1.3f));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Material(texture,additive);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        static void Burst(GameObject parent,string name,string texture,Color tint,int count,float size,float life,float speed,float radius,bool additive=true)
        {
            var ps=Base(parent,name,texture,tint,size,life,speed,additive);
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=ps.shape;shape.enabled=radius>0;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=Mathf.Max(.001f,radius);
        }
        static ParticleSystem Loop(GameObject parent,string name,string texture,Color tint,float size,float life,float rate,bool additive=true)
        {
            var ps=Base(parent,name,texture,tint,size,life,0,additive);
            var main=ps.main;main.loop=true;var emission=ps.emission;emission.rateOverTime=rate;
            var shape=ps.shape;shape.enabled=false;return ps;
        }
        static GameObject Save(GameObject go)
        {
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/"+go.name+".prefab");Object.DestroyImmediate(go);return prefab;
        }
        static void EditPrefab(string path,Action<GameObject> action)
        {
            var go=PrefabUtility.LoadPrefabContents(path);
            try{action(go);PrefabUtility.SaveAsPrefabAsset(go,path);}finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        public static void Validate()
        {
            int weapons=0,skills=0;
            foreach(var guid in AssetDatabase.FindAssets("t:WeaponDefinition",new[]{Gen+"/Weapons"}))
            {var w=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));if(!w.muzzleEffect||!w.impactEffect||!w.bulletTrail)throw new Exception("Missing weapon VFX: "+w.name);weapons++;}
            foreach(var guid in AssetDatabase.FindAssets("t:SkillDefinition",new[]{Gen+"/Skills"}))
            {var s=AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));if(!s.castEffect)throw new Exception("Missing cast VFX: "+s.name);if(s.hitboxPrefab&&!s.hitboxPrefab.visualEffect)throw new Exception("Missing hitbox VFX");skills++;}
            if(weapons!=5||skills!=7)throw new Exception("Configuration count mismatch");
            foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root}))
            {var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(!mat.shader||!mat.shader.isSupported)throw new Exception("Unsupported VFX shader");}
            Debug.Log("OUTPOST_VFX_VALIDATE_PASS");
        }
        public static void Build()
        {
            Validate();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                locationPathName="Builds/OutpostVFX/OutpostVFX.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("VFX build failed: "+report.summary.result);
            Debug.Log("OUTPOST_VFX_BUILD_PASS");
        }
        public static void BuildDifferentiated()
        {
            Apply();
            Build();
        }
    }
}
