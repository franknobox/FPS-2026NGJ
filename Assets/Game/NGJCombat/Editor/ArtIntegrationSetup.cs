using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SlugWhitebox;
using Unity.AI.Navigation;

namespace NGJ.Combat.Editor
{
    public static class ArtIntegrationSetup
    {
        const string Root = "Assets/Game/NGJCombat/Generated";
        const string ScenePath = "Assets/Game/SlugWhitebox/Scenes/SlugWhitebox.unity";
        static int checks;

        [MenuItem("NGJ/Combat/准备美术接入配置")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("请停止运行后整理配置");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { Root }))
            {
                var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                definition.exposeDuringWindup = !definition.flying;
                EditorUtility.SetDirty(definition);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var target in prefab.GetComponentsInChildren<CombatTarget>(true))
                    {
                        CombatLayers.SetActorLayer(target.gameObject, target is PlayerCombatStats ? CombatLayers.Player : CombatLayers.Enemy);
                        if (target is PlayerCombatStats stats) stats.glandRegenPerSecond = .1f;
                    }
                    foreach (var enemy in prefab.GetComponentsInChildren<EnemyCombatant>(true))
                    {
                        enemy.attackMask = CombatLayers.AttackMask;
                        if (!enemy.GetComponent<EnemyVisualBinding>()) enemy.gameObject.AddComponent<EnemyVisualBinding>();
                    }
                    foreach (var weapon in prefab.GetComponentsInChildren<CombatWeaponController>(true)) weapon.hitMask = CombatLayers.PlayerAttackMask;
                    foreach (var hitbox in prefab.GetComponentsInChildren<CombatHitbox>(true))
                    {
                        hitbox.obstructionMask = CombatLayers.WorldMask;
                        hitbox.targetMask = CombatLayers.ActorMask;
                    }
                    foreach (var projectile in prefab.GetComponentsInChildren<CombatProjectile>(true)) projectile.collisionMask = CombatLayers.AttackMask;
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var game = UnityEngine.Object.FindFirstObjectByType<SlugGame>();
            CombatLayers.SetActorLayer(game.player.gameObject, CombatLayers.Player);
            game.stats.glandRegenPerSecond = .1f;
            game.weapons.hitMask = CombatLayers.PlayerAttackMask;
            foreach (var surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
            {
                surface.layerMask = CombatLayers.WorldMask;
                surface.collectObjects = CollectObjects.All;
                EditorUtility.SetDirty(surface);
            }
            var rocket = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "/Weapons/RocketLauncher.asset");
            if (rocket && game.weaponDefinitions.Length == 4) game.weaponDefinitions = game.weaponDefinitions.Concat(new[] { rocket }).ToArray();
            Require(game.ConfigureLoadout(2, rocket ? 4 : 0), "默认两武器配置");
            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(game.stats);
            EditorUtility.SetDirty(game.weapons);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("ART_INTEGRATION_CONFIGURED noPlayMode=" + !EditorApplication.isPlaying);
        }

        // Editor-only behavioral checks: Physics queries are valid without Play Mode.
        public static void ApplyAndVerify()
        {
            Apply();
            checks = 0;
            Require((CombatLayers.WorldMask & (1 << LayerMask.NameToLayer("Ground"))) != 0, "Ground 环境遮挡");
            Require((CombatLayers.WorldMask & CombatLayers.ActorMask) == 0, "角色不进入遮挡层");
            Require(!Physics.GetIgnoreLayerCollision(CombatLayers.Player, LayerMask.NameToLayer("Ground")), "玩家与 Ground 物理碰撞");
            Require(!Physics.GetIgnoreLayerCollision(CombatLayers.Enemy, LayerMask.NameToLayer("Ground")), "敌人与 Ground 物理碰撞");
            var game = UnityEngine.Object.FindFirstObjectByType<SlugGame>();
            Require(game.player.gameObject.layer == CombatLayers.Player && Mathf.Approximately(game.stats.glandRegenPerSecond, .1f), "玩家层与声腺回复");
            Require(game.weaponLoadout.SequenceEqual(new[] { 2, 4 }), "两武器出战配置持久化");
            var navigation = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            Require(navigation && navigation.layerMask.value == CombatLayers.WorldMask && navigation.collectObjects == CollectObjects.All,
                "新场景素材纳入导航烘焙范围");
            var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = new GameObject("Test Player").AddComponent<PlayerCombatStats>(); source.team = 0;
            var enemy = new GameObject("Test Enemy").AddComponent<EnemyCombatant>();
            enemy.definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Root + "/Enemies/MiYin.asset");
            typeof(EnemyCombatant).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
            typeof(EnemyCombatant).GetMethod("BeginWindup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
            Require(enemy.IsWindingUp && enemy.WeakPointExposed, "普通怪前摇暴露弱点");
            enemy.ReceiveHit(new HitInfo { source = source, penetration = ArmorGrade.Low, stunDuration = 2 }, true);
            Require(enemy.IsStunned && !enemy.IsWindingUp && !enemy.IsAttacking, "低穿透弱点控制打断前摇");
            enemy.transform.position = new Vector3(0, 0, 4);
            CombatLayers.SetActorLayer(enemy.gameObject, CombatLayers.Enemy);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerMask.NameToLayer("Ground"); wall.transform.position = new Vector3(0, 1, 2); wall.transform.localScale = new Vector3(4, 4, .5f);
            Physics.SyncTransforms();
            var hit = new HitInfo { source = source, noise = 10, penetration = ArmorGrade.High };
            var wavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/GroundWave.prefab");
            var wave = UnityEngine.Object.Instantiate(wavePrefab, Vector3.up, Quaternion.identity);
            wave.GetComponent<CombatHitbox>().Initialize(hit);
            Require(enemy.Noise == 0, "Ground 墙体阻挡真实范围命中");
            UnityEngine.Object.DestroyImmediate(wave);
            wall.transform.position += Vector3.right * 20; Physics.SyncTransforms();
            wave = UnityEngine.Object.Instantiate(wavePrefab, Vector3.up, Quaternion.identity);
            wave.GetComponent<CombatHitbox>().Initialize(hit);
            Require(enemy.Noise == 10, "移除遮挡后命中 Enemy 层");
            UnityEngine.Object.DestroyImmediate(wave);
            var rig = new GameObject("Loadout Check");
            var controller = rig.AddComponent<CombatWeaponController>();
            var testGame = rig.AddComponent<SlugGame>(); testGame.weapons = controller;
            testGame.weaponDefinitions = new[] { "SiMing", "JianXiao", "HanYin", "BaoMing" }
                .Select(n => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "/Weapons/" + n + ".asset")).ToArray();
            Require(testGame.ConfigureLoadout(0, 2) && testGame.SelectWeaponSlot(1) && testGame.weapon == 2, "允许切换出战武器");
            Require(!testGame.SelectWeapon(1) && testGame.weapon == 2, "禁止切到背包外武器");
            Require(!testGame.ConfigureLoadout(2, 2) && !testGame.ConfigureLoadout(-1, 2) && !testGame.SelectWeaponSlot(2), "拒绝重复和非法槽位");
            foreach (var name in new[] { "ChanYin", "MiYin", "ZhenYin" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Enemy" + name + ".prefab");
                Require(prefab.layer == CombatLayers.Enemy && prefab.GetComponent<EnemyVisualBinding>(), name + "美术接入口与层配置");
            }
            var sample = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/EnemyChanYin.prefab"));
            var sampleCombat = sample.GetComponent<EnemyCombatant>();
            var binding = sample.GetComponent<EnemyVisualBinding>();
            binding.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ART/Prefabs/Enemies/翼怪.prefab");
            var visual = binding.CreateVisual(sampleCombat);
            Require(visual && sampleCombat.attackOrigin.IsChildOf(visual), "正式翼怪模型与发射挂点接入");
            var weak = sample.GetComponentsInChildren<CombatHurtbox>(true).Single(h => h.weakPoint);
            Require(Vector3.Distance(weak.transform.position, sampleCombat.attackOrigin.position) < .001f, "弱点跟随模型挂点");
            Require(weak.GetComponent<Collider>().enabled && Vector3.Distance(weak.transform.lossyScale, Vector3.one) < .001f,
                "弱点碰撞不继承导入骨骼缩放");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(".utmp/combat-art-integration");
            File.WriteAllText(".utmp/combat-art-integration/verification.txt", "PASS checks=" + checks + " PlayMode=" + EditorApplication.isPlaying);
            Debug.Log("ART_INTEGRATION_VERIFIED checks=" + checks + " PlayMode=" + EditorApplication.isPlaying);
        }

        static void Require(bool ok, string message)
        {
            if (!ok) throw new Exception("Art integration: " + message);
            checks++;
        }
    }
}
