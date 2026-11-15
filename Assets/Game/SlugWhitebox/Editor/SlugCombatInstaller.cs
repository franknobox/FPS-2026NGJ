using System;
using System.IO;
using NGJ.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SlugWhitebox.Editor
{
    // 菜单可重复接线，不重建地图；每次先保存一份场景备份。
    [InitializeOnLoad]
    public static class SlugCombatInstaller
    {
        const string Request="Temp/NGJCombat.request";
        static SlugCombatInstaller(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request))return;
            string action=File.ReadAllText(Request).Trim();File.Delete(Request);
            try{Install();if(action=="build")SlugWhiteboxBuilder.Build();if(action=="play")EditorApplication.isPlaying=true;File.WriteAllText("Temp/NGJCombat.done","OK "+DateTime.Now);}
            catch(Exception error){Debug.LogException(error);File.WriteAllText("Temp/NGJCombat.done",error.ToString());}
        }
        static T Load<T>(string sub) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>("Assets/Game/NGJCombat/Generated/"+sub);
        [MenuItem("循声虫族/接入新战斗模块（保留当前白盒）")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new Exception("请先停止运行再接线");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/Game/SlugWhitebox/Scenes/SlugWhitebox.unity")throw new Exception("当前场景不是 SlugWhitebox，未修改");
            var g=UnityEngine.Object.FindFirstObjectByType<SlugGame>();if(!g)throw new Exception("场景缺少 SlugGame");
            Directory.CreateDirectory("../Verification/SceneBackups");
            EditorSceneManager.SaveScene(scene,Path.GetFullPath("../Verification/SceneBackups/SlugWhitebox-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity"),true);
            g.weaponDefinitions=new[]{Load<WeaponDefinition>("Weapons/SiMing.asset"),Load<WeaponDefinition>("Weapons/JianXiao.asset"),Load<WeaponDefinition>("Weapons/HanYin.asset"),Load<WeaponDefinition>("Weapons/BaoMing.asset")};
            g.glandSkills=new[]{Load<SkillDefinition>("Skills/SiMing.asset"),Load<SkillDefinition>("Skills/HanDi.asset"),Load<SkillDefinition>("Skills/GongMing.asset")};
            g.enemyPrefabs=new[]{Load<GameObject>("Prefabs/EnemyChanYin.prefab").GetComponent<EnemyCombatant>(),Load<GameObject>("Prefabs/EnemyMiYin.prefab").GetComponent<EnemyCombatant>(),Load<GameObject>("Prefabs/EnemyZhenYin.prefab").GetComponent<EnemyCombatant>()};
            g.stats=g.player.GetComponent<PlayerCombatStats>();bool fresh=!g.stats;
            if(fresh)g.stats=g.player.gameObject.AddComponent<PlayerCombatStats>();
            g.stats.team=0;
            // 默认每十秒恢复一声腺；数值可在 PlayerCombatStats 调整。
            if(fresh){g.stats.maxNoise=100;g.stats.maxGlands=2;g.stats.glandRegenPerSecond=.1f;g.stats.noiseDecayPerSecond=8;g.stats.noiseDecayDelay=2;}
            CombatLayers.SetActorLayer(g.player.gameObject,CombatLayers.Player);
            g.runner=g.player.GetComponent<SkillRunner>();if(!g.runner)g.runner=g.player.gameObject.AddComponent<SkillRunner>();
            g.weapons=g.player.GetComponent<CombatWeaponController>();if(!g.weapons)g.weapons=g.player.gameObject.AddComponent<CombatWeaponController>();
            g.runner.owner=g.stats;g.runner.castOrigin=g.view.transform;
            g.weapons.owner=g.stats;g.weapons.skills=g.runner;g.weapons.fireOrigin=g.view.transform;g.weapons.hitMask=CombatLayers.PlayerAttackMask;
            if(!g.ConfigureLoadout(g.weaponLoadout))g.ConfigureLoadout(2,4,0,1);
            if(g.weaponModels[3] && g.weaponModels[3].name!="爆鸣体 · 榴弹白盒")
            {
                UnityEngine.Object.DestroyImmediate(g.weaponModels[3]);
                var launcher=new GameObject("爆鸣体 · 榴弹白盒");launcher.transform.SetParent(g.weaponPivot,false);
                Piece(launcher.transform,new Vector3(.28f,-.28f,.55f),new Vector3(.18f,.22f,.55f),g.surfaceMaterial);
                Piece(launcher.transform,new Vector3(.28f,-.22f,.85f),new Vector3(.25f,.25f,.35f),g.cyanMaterial);
                g.weaponModels[3]=launcher;launcher.SetActive(false);
            }
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/NGJCombat/Generated/Prefabs"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var prefab=PrefabUtility.LoadPrefabContents(path);
                if((prefab.GetComponent<CombatProjectile>() || prefab.GetComponent<CombatHitbox>()) && !prefab.GetComponent<SlugCombatEffect>())
                {prefab.AddComponent<SlugCombatEffect>();PrefabUtility.SaveAsPrefabAsset(prefab,path);}
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            NGJ.Combat.Editor.PlayableArtSetup.BindCurrentScene();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
            EditorUtility.SetDirty(g);EditorUtility.SetDirty(g.stats);EditorUtility.SetDirty(g.runner);EditorUtility.SetDirty(g.weapons);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("NGJ_WHITEBOX_INSTALLED: 4 weapons / 7 skills / 3 enemies / HUD / drops / restart");
        }
        static void Piece(Transform parent,Vector3 pos,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
