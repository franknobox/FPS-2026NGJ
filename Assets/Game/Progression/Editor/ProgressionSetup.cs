using System;
using NGJ.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace NGJ.Progression.Editor
{
    public static class ProgressionSetup
    {
        public const string SettingsPath="Assets/Game/Progression/Settings/局内成长.asset";
        const string Combat="Assets/Game/NGJCombat/Generated/";
        [MenuItem("NGJ/成长/应用策划数值与成长配置")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("请先停止运行");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("请先保存当前场景");
            if(!AssetDatabase.IsValidFolder("Assets/Game/Progression/Settings"))AssetDatabase.CreateFolder("Assets/Game/Progression","Settings");
            var config=AssetDatabase.LoadAssetAtPath<ProgressionSettings>(SettingsPath);
            if(!config){config=ScriptableObject.CreateInstance<ProgressionSettings>();AssetDatabase.CreateAsset(config,SettingsPath);}
            Weapon("JianXiao",50,15,"尖啸体");Weapon("SiMing",10,2,"嘶鸣体");Weapon("HanYin",5,10,"撼声体");
            Weapon("RocketLauncher",60,30,"爆鸣体");
            Skill("SniperSkill",40,15);Skill("RifleSkill",30,15);Skill("ShotgunSkill",35,20);Skill("GrenadeSkill",20,8);
            var rocket=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Combat+"Weapons/RocketLauncher.asset");
            rocket.skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(Combat+"Skills/GrenadeSkill.asset");EditorUtility.SetDirty(rocket);
            var shotgun=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Combat+"Weapons/HanYin.asset");shotgun.pelletCount=8;EditorUtility.SetDirty(shotgun);
            Enemy("ChanYin",15,5);Enemy("MiYin",30,15);Enemy("ZhenYin",80,5);
            var boss=AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Combat+"Enemies/ZhenYin.asset");boss.beamDuration=3;boss.beamTickInterval=.3f;EditorUtility.SetDirty(boss);
            foreach(string name in new[]{"EnemyChanYin","EnemyMiYin","EnemyMiYinPurple","EnemyZhenYin"})
            {
                string path=Combat+"Prefabs/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{root.GetComponent<EnemyCombatant>().ApplyDefinition();PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            string original=EditorSceneManager.GetActiveScene().path;
            foreach(var path in new[]{Wasteland.Editor.WastelandLevelBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);var game=UnityEngine.Object.FindFirstObjectByType<SlugWhitebox.SlugGame>();
                game.progressionSettings=config;game.stats.noiseDecayPerSecond=2;game.stats.noiseDecayDelay=2;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
        }
        static void Weapon(string id,float damage,float noise,string label)
        {
            var value=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Combat+"Weapons/"+id+".asset");value.targetNoise=damage;value.selfNoise=noise;value.displayName=label;EditorUtility.SetDirty(value);
        }
        static void Skill(string id,float cost,float cooldown)
        {
            var value=AssetDatabase.LoadAssetAtPath<SkillDefinition>(Combat+"Skills/"+id+".asset");value.cost=cost;value.cooldown=cooldown;EditorUtility.SetDirty(value);
        }
        static void Enemy(string id,float limit,float attack)
        {
            var value=AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Combat+"Enemies/"+id+".asset");value.maxNoise=limit;value.attackNoise=attack;EditorUtility.SetDirty(value);
        }
    }
}
