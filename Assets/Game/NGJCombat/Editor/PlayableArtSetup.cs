using System;
using System.IO;
using System.Linq;
using SlugWhitebox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NGJ.Combat.Editor
{
    public static class PlayableArtSetup
    {
        const string Root = "Assets/Game/NGJCombat/Generated";
        const string ScenePath = "Assets/Game/SlugWhitebox/Scenes/SlugWhitebox.unity";
        const string Views = "Assets/Game/Prefabs/Weapons";

        [MenuItem("NGJ/美术/接入四只怪物与两把武器")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateRocket();
            Actor("EnemyChanYin", "ChanYin", "翼怪", 2.16f, .48f, .24f, "SoundSocket", Vector3.zero);
            Actor("EnemyZhenYin", "ZhenYin", "甲壳怪", 2.2f, .65f, .24f, "SoundSocket", Vector3.zero);
            Actor("EnemyMiYin", "MiYin", "黄色眼虫", .75f, .3f, .13f, "Eye", Vector3.forward * .25f);
            Actor("EnemyMiYinPurple", "MiYin", "紫色眼虫", .75f, .3f, .13f, "Eye", Vector3.forward * .25f);
            WeaponView("Shotgun", "霰弹枪", new Vector3(.26f, -.26f, .42f));
            WeaponView("ClawRocket", "爪型枪", new Vector3(.22f, -.25f, .35f));
            EditorSceneManager.OpenScene(ScenePath);
            BindCurrentScene();
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYABLE_ART_READY enemies=4 weaponViews=2 default=撼音体 rocket=火箭筒");
        }

        static void Actor(string id, string definition, string art, float height, float radius, float weakRadius, string weakSocket, Vector3 offset)
        {
            string path = Root + "/Prefabs/" + id + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(File.Exists(path) ? path : Root + "/Prefabs/EnemyMiYin.prefab");
            try
            {
                root.name = id; // Internal ID. Player-facing names come from EnemyDefinition.
                var combat = root.GetComponent<EnemyCombatant>();
                combat.definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Root + "/Enemies/" + definition + ".asset");
                combat.ApplyDefinition();
                if(id == "EnemyZhenYin")combat.underPenetrationMultiplier = 1;
                combat.attackMask = CombatLayers.AttackMask;
                var binding = root.GetComponent<EnemyVisualBinding>() ?? root.AddComponent<EnemyVisualBinding>();
                if (binding.preparedVisual) UnityEngine.Object.DestroyImmediate(binding.preparedVisual.gameObject);
                binding.preparedVisual = null;
                binding.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ART/Prefabs/Enemies/" + art + ".prefab");
                binding.localScale = id == "EnemyChanYin" ? Vector3.one * 1.2f : Vector3.one;
                binding.collisionHeight = height;
                binding.collisionRadius = radius;
                binding.weakPointRadius = weakRadius;
                binding.weakPointSocket = weakSocket;
                binding.weakPointOffset = offset;
                var previousOrigin = combat.attackOrigin;
                var visual = binding.CreateVisual(combat);
                if (!visual || !combat.attackOrigin.IsChildOf(visual)) throw new Exception("Missing authored visual or socket: " + id);
                if (previousOrigin && previousOrigin.parent == root.transform && previousOrigin.childCount == 0)
                    UnityEngine.Object.DestroyImmediate(previousOrigin.gameObject);
                root.GetComponent<CharacterController>().stepOffset = Mathf.Min(.3f, height * .2f);
                var adapter = root.GetComponent<SlugEnemy>() ?? root.AddComponent<SlugEnemy>();
                adapter.combat = combat;
                adapter.kind = (int)combat.definition.kind;
                adapter.body = visual;
                adapter.shell = visual.GetComponentInChildren<Renderer>();
                CombatLayers.SetActorLayer(root, CombatLayers.Enemy);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void WeaponView(string id, string art, Vector3 position)
        {
            EnsureFolder(Views);
            var root = new GameObject(id);
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ART/Prefabs/Weapons/" + art + ".prefab");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = position;
                visual.transform.localRotation = Quaternion.identity;
                var view = root.AddComponent<SlugWeaponView>();
                view.muzzle = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Muzzle");
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("Weapons");
                PrefabUtility.SaveAsPrefabAsset(root, Views + "/" + id + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void CreateRocket()
        {
            EnsureFolder("Assets/Game/Materials");
            string materialPath = "Assets/Game/Materials/RocketPlaceholder.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", Color.white);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string blastPath = Root + "/Prefabs/RocketExplosion.prefab";
            if (!File.Exists(blastPath))
            {
                var blast = new GameObject("RocketExplosion");
                var hitbox = blast.AddComponent<CombatHitbox>();
                hitbox.radius = 3; hitbox.halfAngle = 180; hitbox.lifetime = .15f;
                hitbox.targetMask = CombatLayers.ActorMask; hitbox.obstructionMask = CombatLayers.WorldMask;
                blast.AddComponent<SlugCombatEffect>();
                PrefabUtility.SaveAsPrefabAsset(blast, blastPath);
                UnityEngine.Object.DestroyImmediate(blast);
            }
            string projectilePath = Root + "/Prefabs/RocketProjectile.prefab";
            if (!File.Exists(projectilePath))
            {
                var rocket = new GameObject("RocketProjectile");
                var projectile = rocket.AddComponent<CombatProjectile>();
                projectile.radius = .12f; projectile.gravity = 0; projectile.lifetime = 3;
                projectile.collisionMask = CombatLayers.AttackMask;
                projectile.explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(blastPath).GetComponent<CombatHitbox>();
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "WhiteboxBody"; body.transform.SetParent(rocket.transform, false);
                body.transform.localScale = new Vector3(.12f, .22f, .12f);
                body.transform.localRotation = Quaternion.Euler(90, 0, 0);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(rocket, projectilePath);
                UnityEngine.Object.DestroyImmediate(rocket);
            }
            string weaponPath = Root + "/Weapons/RocketLauncher.asset";
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath);
            if (!weapon)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                weapon.type = WeaponType.RocketLauncher;
                weapon.displayName = "火箭筒";
                weapon.targetNoise = 100; weapon.selfNoise = 20; weapon.fireInterval = 1.2f;
                weapon.penetration = ArmorGrade.High; weapon.range = 75; weapon.projectileSpeed = 25;
                weapon.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath).GetComponent<CombatProjectile>();
                AssetDatabase.CreateAsset(weapon, weaponPath);
            }
        }

        public static void BindCurrentScene()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<SlugGame>();
            if (!game || game.gameObject.scene.path != ScenePath || !File.Exists(Views + "/ClawRocket.prefab")) return;
            game.weaponDefinitions = new[] { "SiMing", "JianXiao", "HanYin", "BaoMing", "RocketLauncher" }
                .Select(n => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "/Weapons/" + n + ".asset")).ToArray();
            game.enemyPrefabs = new[] { "EnemyChanYin", "EnemyMiYin", "EnemyZhenYin" }
                .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + n + ".prefab").GetComponent<EnemyCombatant>()).ToArray();
            game.miYinVariants = new[] { "EnemyMiYin", "EnemyMiYinPurple" }
                .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + n + ".prefab").GetComponent<EnemyCombatant>()).ToArray();
            Array.Resize(ref game.weaponModels, 5);
            foreach (var pair in new[] { (index: 2, id: "Shotgun"), (index: 4, id: "ClawRocket") })
            {
                if (game.weaponModels[pair.index]) UnityEngine.Object.DestroyImmediate(game.weaponModels[pair.index]);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Views + "/" + pair.id + ".prefab"), game.weaponPivot);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                game.weaponModels[pair.index] = instance;
            }
            game.ConfigureLoadout(2, 4);
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.weapons);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
        }

        public static void BuildAndVerify()
        {
            Build();
            ArtIntegrationSetup.ApplyAndVerify();
            var game = UnityEngine.Object.FindFirstObjectByType<SlugGame>();
            if (!game.weaponLoadout.SequenceEqual(new[] { 2, 4 }) || game.weapon != 2 || game.WeaponDisplayName != "撼音体")
                throw new Exception("Default shotgun/loadout/player-facing name incorrect");
            if (game.miYinVariants.Length != 2 || game.miYinVariants.Any(p => p.definition.displayName != "觅音虫"))
                throw new Exception("Eye bug variants must share the design name");
            foreach (var id in new[] { "EnemyChanYin", "EnemyMiYin", "EnemyMiYinPurple", "EnemyZhenYin" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + id + ".prefab");
                var binding = prefab.GetComponent<EnemyVisualBinding>();
                if (!binding.preparedVisual || !prefab.GetComponent<EnemyCombatant>().attackOrigin || prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 1)
                    throw new Exception("Incomplete enemy prefab: " + id);
                if (prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new Exception("Missing script: " + id);
            }
            var rocket = game.weaponDefinitions[4];
            if (rocket.displayName != "火箭筒" || rocket.projectilePrefab.gravity != 0 || !rocket.projectilePrefab.explosionPrefab)
                throw new Exception("Invalid whitebox rocket");
            game.SelectWeaponSlot(1);
            if (game.WeaponDisplayName != "火箭筒" || !game.weapons.muzzle || !game.weapons.muzzle.IsChildOf(game.weaponModels[4].transform))
                throw new Exception("Rocket view/muzzle/player-facing name incorrect");
            game.SelectWeaponSlot(0);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene);
            ArtTools.ArtShowcaseSetup.UpdateAndVerify();
            Debug.Log("PLAYABLE_ART_VERIFIED enemies=4 weapons=2 publicNames=颤音虫/觅音虫/震音虫/撼音体/火箭筒 PlayMode=" + EditorApplication.isPlaying);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
