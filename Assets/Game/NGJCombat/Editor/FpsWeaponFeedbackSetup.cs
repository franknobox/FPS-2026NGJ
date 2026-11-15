using System;
using System.Linq;
using NGJ.Combat;
using SlugWhitebox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NGJ.EditorTools
{
    public static class FpsWeaponFeedbackSetup
    {
        const string Fx = "Assets/ART/VFX/FPSWeapons";
        const string Anim = "Assets/ART/Animations/FPSWeapons";
        const string Generated = "Assets/Game/NGJCombat/Generated";
        [MenuItem("NGJ/美术/接入原厂霰弹与火箭反馈")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("请先停止运行");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            Folder(Fx); Folder(Anim);
            foreach(var id in new[]{"Shotgun","RocketLauncher"})
            {
                var source=AssetDatabase.LoadAssetAtPath<cowsins.Weapon_SO>("Assets/FPS Engine/ScriptableObjects/Weapons/"+id+".asset");
                var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Generated+"/Weapons/"+(id=="Shotgun"?"HanYin":"RocketLauncher")+".asset");
                weapon.muzzleEffect=CopyEffect(source.muzzleVFX,id+"Muzzle");
                weapon.impactEffect=CopyEffect(source.bulletHoleImpact.defaultImpact,id+"Impact");
                weapon.bulletTrail=source.bulletTrail?CopyEffect(source.bulletTrail.gameObject,id+"Trail").GetComponent<TrailRenderer>():null;
                var shot=Motion(id);
                var idle=Idle(id);
                string path="Assets/Game/Prefabs/Weapons/"+(id=="Shotgun"?"Shotgun":"ClawRocket")+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view=root.GetComponent<SlugWeaponView>();
                    var motion=root.transform.Find("原厂开火运动");
                    if(!motion)
                    {
                        var children=root.transform.Cast<Transform>().ToArray();
                        motion=new GameObject("原厂开火运动").transform;motion.SetParent(root.transform,false);
                        foreach(var child in children)child.SetParent(motion,false);
                    }
                    view.rig=motion.GetComponent<Animator>();if(!view.rig)view.rig=motion.gameObject.AddComponent<Animator>();
                    if(!motion.Find("枪体运动"))
                    {
                        var children=motion.Cast<Transform>().ToArray();
                        var animated=new GameObject("枪体运动").transform;animated.SetParent(motion,false);
                        foreach(var child in children)child.SetParent(animated,false);
                    }
                    view.rig.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    view.idleClip=idle;view.fireClip=shot;view.feedbackSource=source;view.feedbackWeapon=weapon;
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                if(id=="RocketLauncher")
                {
                    var explosion=CopyEffect(source.explosionVFX,"RocketExplosion");
                    string blastPath=Generated+"/Prefabs/RocketExplosion.prefab";
                    var blast=PrefabUtility.LoadPrefabContents(blastPath);
                    try
                    {
                        blast.GetComponent<CombatHitbox>().visualEffect=explosion;
                        var old=blast.GetComponent<SlugCombatEffect>();if(old)Object.DestroyImmediate(old);
                        PrefabUtility.SaveAsPrefabAsset(blast,blastPath);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(blast);}
                    var visual=CopyEffect(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS Engine/Prefabs/Projectiles/Rocket.prefab"),"RocketVisual");
                    string rocketPath=Generated+"/Prefabs/RocketProjectile.prefab";
                    var rocket=PrefabUtility.LoadPrefabContents(rocketPath);
                    try
                    {
                        foreach(var child in rocket.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                        var model=(GameObject)PrefabUtility.InstantiatePrefab(visual,rocket.transform);
                        model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
                        var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>!(r is ParticleSystemRenderer)&&!(r is TrailRenderer)).ToArray();
                        if(renderers.Length==0)throw new Exception("Rocket mesh missing");
                        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                        var center=model.transform.InverseTransformPoint(bounds.center);
                        float scale=.13f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                        model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-center*scale;
                        WeaponFeelSetup.ConfigureRocketParticles(model);
                        var audio=rocket.GetComponent<AudioSource>();if(!audio)audio=rocket.AddComponent<AudioSource>();
                        audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS Engine/SFX/Weapons/RocketLauncher/Rocket_AirTravel_Loop_SFX.wav");
                        audio.loop=true;audio.playOnAwake=true;audio.spatialBlend=1;audio.volume=.5f;audio.minDistance=2;audio.maxDistance=35;
                        // Speed follows the original rocket; damage, range limit and blast radius remain game tuning.
                        weapon.projectileSpeed=source.speed;
                        PrefabUtility.SaveAsPrefabAsset(rocket,rocketPath);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(rocket);}
                }
                EditorUtility.SetDirty(weapon);
            }
            AssetDatabase.SaveAssets();
            foreach(string scene in new[]{Wasteland.Editor.WastelandLevelBuilder.ScenePath})
            {
                EditorSceneManager.OpenScene(scene);
                var game=Object.FindFirstObjectByType<SlugGame>();
                Array.Resize(ref game.fireSounds,game.weaponDefinitions.Length);
                foreach(int index in new[]{2,4})
                {
                    // Existing nested weapon views inherit the updated prefab; explicit sound references also stay reusable.
                    game.fireSounds[index]=game.weaponModels[index].GetComponent<SlugWeaponView>().feedbackSource.audioSFX.shooting[0];
                }
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            }
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
            Debug.Log("FPS_WEAPON_FEEDBACK_READY original audio, muzzle, pellet trails, impacts, rocket mesh/smoke/explosion, baked fire motion, FOV/shake; damage remains NGJ");
        }
        public static GameObject CopyEffect(GameObject source,string name)
        {
            if(!source)throw new Exception("Missing source effect "+name);
            string path=AssetDatabase.GetAssetPath(source);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
                foreach(var col in root.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(col);
                foreach(var rb in root.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(rb);
                foreach(var audio in root.GetComponentsInChildren<AudioSource>(true)){audio.playOnAwake=true;audio.spatialBlend=1;}
                if(name=="RocketExplosion")
                {
                    var audio=root.GetComponent<AudioSource>();if(!audio)audio=root.AddComponent<AudioSource>();
                    audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS Engine/SFX/Others/Explosion.wav");
                    audio.playOnAwake=true;audio.loop=false;audio.spatialBlend=1;audio.minDistance=3;audio.maxDistance=60;
                }
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=2;
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>ConvertMaterial(m)).ToArray();
                return PrefabUtility.SaveAsPrefabAsset(root,Fx+"/"+name+".prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static Material ConvertMaterial(Material source)
        {
            if(!source||source.shader.name.StartsWith("Universal Render Pipeline")||source.shader.name.StartsWith("ART/"))return source;
            string path=Fx+"/"+source.name+"_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)).Substring(0,8)+".mat";
            var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result)return result;
            bool particle=source.shader.name.Contains("Particle");
            result=new Material(Shader.Find(particle?"Universal Render Pipeline/Particles/Unlit":"Universal Render Pipeline/Lit"));
            if(source.HasProperty("_MainTex"))result.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
            result.SetColor("_BaseColor",source.HasProperty("_TintColor")?source.GetColor("_TintColor"):source.HasProperty("_Color")?source.color:Color.white);
            if(particle)
            {
                result.SetFloat("_Surface",1);result.SetFloat("_SrcBlend",5);result.SetFloat("_DstBlend",10);result.SetFloat("_ZWrite",0);
                result.SetOverrideTag("RenderType","Transparent");result.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");result.renderQueue=3000;
            }
            AssetDatabase.CreateAsset(result,path);return result;
        }
        static AnimationClip Idle(string id)
        {
            string path=Anim+"/"+id+"Idle.anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
            clip.ClearCurves();
            foreach(string axis in new[]{"x","y","z"})clip.SetCurve("枪体运动",typeof(Transform),"localPosition."+axis,AnimationCurve.Constant(0,1,0));
            foreach(string axis in new[]{"x","y","z","w"})clip.SetCurve("枪体运动",typeof(Transform),"localRotation."+axis,AnimationCurve.Constant(0,1,axis=="w"?1:0));
            EditorUtility.SetDirty(clip);return clip;
        }
        static AnimationClip Motion(string id)
        {
            string modelPath="Assets/FPS Engine/Models/Weapons/"+id+".fbx";
            var source=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__")&&c.name.Contains("Fire"));
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            try
            {
                var bone=model.transform.Find(id=="Shotgun"?"FPSEngine_Rig/body":"FPSEngine_Rig/Weapon 1");
                if(!bone)throw new Exception("Gun animation bone missing "+id);
                source.SampleAnimation(model,0);var origin=bone.position;var rotation=bone.rotation;
                var curves=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
                int frames=Mathf.CeilToInt(source.length*60);
                for(int i=0;i<=frames;i++)
                {
                    float t=source.length*i/frames;source.SampleAnimation(model,t);
                    var p=bone.position-origin;var q=bone.rotation*Quaternion.Inverse(rotation);
                    float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w};
                    for(int j=0;j<7;j++)curves[j].AddKey(t,values[j]);
                }
                string path=Anim+"/"+id+"Fire.anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}
                clip.ClearCurves();clip.frameRate=60;
                string[] properties={"localPosition.x","localPosition.y","localPosition.z","localRotation.x","localRotation.y","localRotation.z","localRotation.w"};
                for(int j=0;j<7;j++)clip.SetCurve("枪体运动",typeof(Transform),properties[j],curves[j]);
                clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);return clip;
            }
            finally{Object.DestroyImmediate(model);}
        }
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int i=path.LastIndexOf('/');Folder(path.Substring(0,i));AssetDatabase.CreateFolder(path.Substring(0,i),path.Substring(i+1));}
    }
}
