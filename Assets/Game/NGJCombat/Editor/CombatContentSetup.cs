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
    public static class CombatContentSetup
    {
        const string Root = "Assets/Game/NGJCombat/Generated/";
        const string Views = "Assets/Game/Prefabs/Weapons/";
        const string Audio = "Assets/ART/Audio/Enemies/";
        [MenuItem("NGJ/美术/接入四武器与虫族音效")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("请先停止运行");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("当前场景有未保存修改，请先保存");
            var original=EditorSceneManager.GetActiveScene().path;
            PrepareGun("Shotgun");PrepareGun("ClawRocket");
            Rifle("Rifle", "SiMing");Rifle("Sniper", "JianXiao");
            foreach(var name in new[]{"EnemyMiYin","EnemyMiYinPurple","EnemyChanYin","EnemyZhenYin"})
            {
                string path=Root+"Prefabs/"+name+".prefab";
                var go=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var p=go.GetComponent<EnemyPresentation>();
                    bool heavy=name=="EnemyZhenYin",wing=name=="EnemyChanYin";
                    p.windupSound=Clip(heavy?"roar_01":wing?"bug_03":"bug_01");
                    p.shotSound=Clip(heavy?"monster_02":wing?"spit_02":"spit_01");
                    p.hurtSounds=(heavy?new[]{"grunt_01","grunt_02"}:wing?new[]{"hurt_02","bug_04"}:new[]{"hurt_01","hurt_03"}).Select(Clip).ToArray();
                    p.deathSounds=(heavy?new[]{"monster_01","roar_01"}:wing?new[]{"scream_02","bug_02"}:new[]{"scream_01","bug_04"}).Select(Clip).ToArray();
                    p.voicePitch=heavy?.7f:wing?1.15f:name=="EnemyMiYinPurple"?1.05f:1.2f;
                    go.GetComponent<EnemyCombatant>().ApplyDefinition();
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            foreach(string path in new[]{SlugWhitebox.Editor.SlugWhiteboxBuilder.ScenePath,Wasteland.Editor.WastelandLevelBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);
                var g=Object.FindFirstObjectByType<SlugGame>();if(!g)continue;
                Array.Resize(ref g.weaponModels,5);Array.Resize(ref g.fireSounds,5);
                foreach(var pair in new[]{(0,"Rifle"),(1,"Sniper"),(2,"Shotgun"),(4,"ClawRocket")})
                {
                    if(g.weaponModels[pair.Item1])Object.DestroyImmediate(g.weaponModels[pair.Item1]);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Views+pair.Item2+".prefab"),g.weaponPivot);
                    instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;
                    instance.SetActive(pair.Item1==2);g.weaponModels[pair.Item1]=instance;
                    g.fireSounds[pair.Item1]=instance.GetComponent<SlugWeaponView>().feedbackSource.audioSFX.shooting[0];
                }
                g.ConfigureLoadout(2,4,0,1);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
            if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ART/Prefabs/Weapons/狙击枪.prefab"))NewWeaponModelsSetup.Apply();
        }
        static AudioClip Clip(string name)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+name+".ogg");
            if(!clip)throw new Exception("缺少音效 "+name);return clip;
        }
        static void PrepareGun(string name)
        {
            string path=Views+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<SlugWeaponView>();
                var binding=root.GetComponent<WeaponHandBinding>();
                var motion=root.transform.Find("原厂开火运动/枪体运动");
                if(binding && binding.gun)
                {
                    binding.gun.SetParent(motion,false);
                    binding.gun.localPosition=new Vector3(.22f,-.23f,.5f);
                    binding.gun.localRotation=Quaternion.identity;
                    Object.DestroyImmediate(binding);
                }
                var arms=root.transform.Find("原厂手臂");if(arms)Object.DestroyImmediate(arms.gameObject);
                view.rig=root.transform.Find("原厂开火运动").GetComponent<Animator>();
                if(!view.rig)view.rig=root.transform.Find("原厂开火运动").gameObject.AddComponent<Animator>();
                string id=name=="Shotgun"?"Shotgun":"RocketLauncher";
                view.idleClip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ART/Animations/FPSWeapons/"+id+"Idle.anim");
                view.fireClip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ART/Animations/FPSWeapons/"+id+"Fire.anim");
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Rifle(string name,string definition)
        {
            var root=SlugWhitebox.Editor.SlugWeaponAssets.Create(0,null);
            try
            {
                root.name=name;var view=root.GetComponent<SlugWeaponView>();
                var source=AssetDatabase.LoadAssetAtPath<cowsins.Weapon_SO>("Assets/FPS Engine/ScriptableObjects/Weapons/Rifle.asset");
                view.feedbackSource=source;
                view.feedbackWeapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root+"Weapons/"+definition+".asset");
                view.feedbackWeapon.muzzleEffect=FpsWeaponFeedbackSetup.CopyEffect(source.muzzleVFX,name+"Muzzle");
                view.feedbackWeapon.impactEffect=FpsWeaponFeedbackSetup.CopyEffect(source.bulletHoleImpact.defaultImpact,name+"Impact");
                view.feedbackWeapon.bulletTrail=FpsWeaponFeedbackSetup.CopyEffect(source.bulletTrail.gameObject,name+"Trail").GetComponent<TrailRenderer>();
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer.name=="Mesh" || renderer.name=="Arms_Mesh"){renderer.enabled=false;continue;}
                    var authored=source.weaponObject.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name==renderer.name);
                    if(authored)renderer.sharedMaterials=authored.sharedMaterials.Select(FpsWeaponFeedbackSetup.ConvertMaterial).ToArray();
                }
                foreach(var kind in new[]{"Idle","Fire"})
                {
                    var clip=Object.Instantiate(kind=="Idle"?view.idleClip:view.fireClip);clip.events=Array.Empty<AnimationEvent>();
                    string clipPath="Assets/ART/Animations/FPSWeapons/"+name+kind+".anim";
                    var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if(existing){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;}else AssetDatabase.CreateAsset(clip,clipPath);
                    if(kind=="Idle")view.idleClip=clip;else view.fireClip=clip;
                }
                var muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(root.transform,false);muzzle.localPosition=new Vector3(.1f,-.12f,.9f);view.muzzle=muzzle;
                EditorUtility.SetDirty(view.feedbackWeapon);
                PrefabUtility.SaveAsPrefabAsset(root,Views+name+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
