using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NGJ.Combat.Editor
{
    [InitializeOnLoad]
    public static class CombatVerification
    {
        const string Pending = "NGJCombat.Verification.Pending";
        static CombatVerification() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

        [MenuItem("NGJ/Combat/验证核心结算")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("请先退出 Play 模式再验证。"); return; }
            CombatAssetBuilder.Build();
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            int exitCode = 0;
            try { Verify(); Debug.Log("NGJ_COMBAT_VERIFICATION_PASSED"); }
            catch (Exception exception) { Debug.LogException(exception); exitCode = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else EditorApplication.isPlaying = false;
        }

        static void Verify()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.CreateScene("NGJCombat_Verification");
            SceneManager.SetActiveScene(scene);
            var transient = new List<Object>();
            try
            {
                var player = new GameObject("Test Player").AddComponent<PlayerCombatStats>();
                player.team = 0;
                player.maxNoise = 1000;
                player.AddNoise(40);
                Require(player.TryConsumeNoise(20) && player.Noise == 20, "消耗噪值");
                Require(!player.TryConsumeNoise(21) && player.Noise == 20, "不足时不扣资源");
                Require(!player.TryConsumeGland(-1), "拒绝负数消耗");
                var enemy = new GameObject("Test Enemy").AddComponent<CombatTarget>();
                enemy.transform.position = Vector3.forward * 3;
                enemy.maxNoise = 1000;
                enemy.armor = ArmorGrade.High;
                var collider = enemy.gameObject.AddComponent<SphereCollider>();
                collider.radius = .5f;
                enemy.gameObject.AddComponent<BoxCollider>();
                var hit = new HitInfo { source = player, noise = 20, penetration = ArmorGrade.Low, stunDuration = 1 };
                enemy.ReceiveHit(hit);
                Require(enemy.Noise == 5 && !enemy.IsStunned, "穿透不足减伤并阻挡控制");
                hit.penetration = ArmorGrade.High;
                enemy.ReceiveHit(hit);
                Require(enemy.Noise == 25 && enemy.IsStunned, "穿透足够眩晕");
                enemy.ResetCombat();
                enemy.weakPointOpen = true;
                hit.penetration = ArmorGrade.Low;
                enemy.ReceiveHit(hit, true);
                Require(enemy.Noise == 20, "暴露弱点使用弱点装甲");
                enemy.ResetCombat();
                enemy.airborne = true;
                hit.groundOnly = true;
                Require(!enemy.ReceiveHit(hit), "地面技能过滤飞行目标");
                hit.groundOnly = false;
                hit.source = enemy;
                Require(!enemy.ReceiveHit(hit), "过滤自己和同阵营");

                hit.source = player;
                hit.penetration = ArmorGrade.High;
                hit.stunDuration = 0;
                Physics.SyncTransforms();
                var box = new GameObject("Test Hitbox").AddComponent<CombatHitbox>();
                box.radius = 5;
                box.Initialize(hit);
                Require(enemy.Noise == 20, "出生重叠且多个碰撞体只结算一次");
                box.SendMessage("FixedUpdate");
                Require(enemy.Noise == 20, "持续检测不重复命中");

                var wall = new GameObject("Wall");
                wall.layer = 0;
                wall.transform.position = Vector3.forward * 1.5f;
                wall.AddComponent<BoxCollider>();
                var blocked = new GameObject("Blocked Hitbox").AddComponent<CombatHitbox>();
                blocked.radius = 5;
                blocked.obstructionMask = 1;
                Physics.SyncTransforms();
                blocked.Initialize(hit);
                Require(enemy.Noise == 20, "范围攻击不穿墙");
                Object.DestroyImmediate(wall);

                var runner = player.gameObject.AddComponent<SkillRunner>();
                runner.owner = player;
                var invalid = ScriptableObject.CreateInstance<SkillDefinition>();
                transient.Add(invalid);
                invalid.cost = 1;
                Require(!runner.TryCast(invalid) && player.Glands == 2, "缺少预制体不扣资源");
                var buff = ScriptableObject.CreateInstance<SkillDefinition>();
                transient.Add(buff);
                buff.effect = SkillEffect.DamageBuff;
                buff.resource = ResourceType.Noise;
                buff.cost = 5;
                buff.damageMultiplier = 2;
                Require(runner.TryCast(buff) && player.Noise == 15 && player.DamageMultiplier == 2, "步枪增伤和扣资源");
                Require(!runner.TryCast(buff) && player.Noise == 15, "技能冷却不重复扣费");

                var resonance = ScriptableObject.CreateInstance<SkillDefinition>();
                transient.Add(resonance);
                resonance.effect = SkillEffect.Resonance;
                resonance.hitboxPrefab = box;
                resonance.hit.penetration = ArmorGrade.High;
                Require(runner.TryCast(resonance), "共鸣可以释放");
                hit.source = enemy;
                player.ReceiveHit(hit);
                Require(player.Noise == 15 && player.AbsorbedNoise == 20, "共鸣无效化并吸收攻击");
                float before = enemy.Noise;
                typeof(SkillRunner).GetField("resonanceEndsAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runner, -1f);
                runner.SendMessage("Update");
                Require(!player.IsAbsorbing && enemy.Noise == before + 20, "共鸣到期按吸收量反击");
                runner.ResetSkills();
                player.AddGland(2);
                Require(runner.TryCast(resonance), "共鸣重置后可再次释放");
                runner.Cancel();
                Require(!player.IsAbsorbing && player.AbsorbedNoise == 0, "取消清理吸收状态");

                enemy.ResetCombat();
                hit.source = player;
                var grenade = new GameObject("Fast Grenade").AddComponent<CombatProjectile>();
                grenade.gravity = 0;
                grenade.explosionPrefab = box;
                grenade.Initialize(hit, Vector3.forward * 500);
                grenade.SendMessage("FixedUpdate");
                Require(enemy.Noise == 20, "高速榴弹碰撞并爆炸结算");

                var sticky = new GameObject("Sticky Bomb").AddComponent<CombatProjectile>();
                sticky.transform.position = enemy.transform.position;
                sticky.sticky = true;
                sticky.detonationDelay = 1;
                sticky.explosionPrefab = box;
                hit.vulnerabilityDuration = 2;
                hit.vulnerabilityMultiplier = 1.5f;
                sticky.Initialize(hit, Vector3.zero);
                Require(enemy.Noise == 20, "黏附炸弹不会提前爆炸");
                enemy.transform.position += Vector3.right;
                sticky.SendMessage("FixedUpdate");
                Require(sticky.transform.position == enemy.transform.position, "黏附炸弹跟随目标");
                typeof(CombatProjectile).GetField("detonateAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sticky, -1f);
                Physics.SyncTransforms();
                sticky.SendMessage("FixedUpdate");
                Require(enemy.Noise == 40 && enemy.IsVulnerable, "黏附到期爆炸并施加易伤");
                enemy.transform.position = Vector3.forward * 3;

                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Game/NGJCombat/Generated/Weapons/JianXiao.asset");
                Require(weapon && weapon.skill && weapon.skill.hitboxPrefab, "生成资源引用完整");
                var weapons = player.gameObject.AddComponent<CombatWeaponController>();
                weapons.owner = player;
                weapons.skills = runner;
                weapons.Equip(weapon);
                enemy.ResetCombat();
                var second = new GameObject("Second Enemy").AddComponent<CombatTarget>();
                second.maxNoise = 1000;
                second.transform.position = Vector3.forward * 6;
                second.gameObject.AddComponent<SphereCollider>();
                Physics.SyncTransforms();
                Require(weapons.TryFire() && enemy.Noise > 0 && second.Noise > 0, "狙击普攻穿透多个目标");
                Require(!weapons.TryFire(), "普攻射速限制");

                VerifyEnemies(transient);

                int deaths = 0;
                second.Died += () => deaths++;
                second.AddNoise(1000);
                second.AddNoise(1000);
                Require(!second.IsAlive && deaths == 1, "到上限死亡且事件只触发一次");
            }
            finally
            {
                foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
                foreach (var obj in transient) Object.DestroyImmediate(obj);
                SceneManager.SetActiveScene(previous);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
        // 强制推进私有计时器，验证真实物理射线和状态切换，不依赖帧率。
        static void VerifyEnemies(List<Object> transient)
        {
            const string folder = "Assets/Game/NGJCombat/Generated/";
            var fly = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(folder + "Enemies/ChanYin.asset");
            var normal = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(folder + "Enemies/MiYin.asset");
            var heavy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(folder + "Enemies/ZhenYin.asset");
            Require(fly && fly.flying && fly.fallsWhenStunned && fly.quantity == EnemyQuantity.Many, "颤音虫飞行坠落配置");
            Require(normal && normal.armor == ArmorGrade.Medium && normal.exposeDuringAttack, "觅音虫中甲和攻击弱点配置");
            Require(heavy && heavy.armor == ArmorGrade.High && heavy.attackMode == EnemyAttackMode.ContinuousBeam, "震音虫重甲持续射线配置");
            foreach (string name in new[] { "ChanYin", "MiYin", "ZhenYin" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Prefabs/Enemy" + name + ".prefab");
                var actor = prefab ? prefab.GetComponent<EnemyCombatant>() : null;
                var hurtbox = prefab ? prefab.GetComponentInChildren<CombatHurtbox>() : null;
                Require(actor && actor.definition && hurtbox && hurtbox.target == actor && hurtbox.weakPoint, name + "预制体引用完整");
            }
            var victim = new GameObject("Enemy Attack Victim").AddComponent<PlayerCombatStats>();
            victim.transform.position = new Vector3(50, 0, 5);
            victim.team = 0;
            victim.maxNoise = 1000;
            victim.armor = ArmorGrade.Low;
            victim.gameObject.AddComponent<CapsuleCollider>().center = Vector3.up;
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Prefabs/EnemyMiYin.prefab"), new Vector3(50, 0, 0), Quaternion.identity);
            var enemy = instance.GetComponent<EnemyCombatant>();
            var tuning = Object.Instantiate(normal);
            transient.Add(tuning);
            tuning.windup = 0;
            enemy.definition = tuning;
            enemy.attackTarget = victim;
            enemy.ResetCombat();
            Physics.SyncTransforms();
            enemy.SendMessage("Update");
            Require(enemy.IsAttacking && enemy.WeakPointExposed && victim.Noise == tuning.attackNoise, "间隙攻击命中并暴露弱点");
            enemy.SendMessage("Update");
            Require(victim.Noise == tuning.attackNoise, "间隙攻击动作期间不重复伤害");
            enemy.ReceiveHit(new HitInfo { source = victim, noise = 10, penetration = ArmorGrade.Low }, true);
            Require(enemy.Noise == 10, "攻击暴露弱点接受低穿透全额伤害");
            enemy.ReceiveHit(new HitInfo { source = victim, penetration = ArmorGrade.High, stunDuration = 1 });
            Require(enemy.IsStunned && !enemy.IsAttacking && enemy.WeakPointExposed, "眩晕打断攻击且保持弱点暴露");
            tuning.attackMode = EnemyAttackMode.ContinuousBeam;
            enemy.ResetCombat();
            float before = victim.Noise;
            enemy.SendMessage("Update");
            typeof(EnemyCombatant).GetField("nextTickAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, -1f);
            enemy.SendMessage("Update");
            Require(victim.Noise == before + tuning.attackNoise * 2, "持续射线按跳数造成伤害");
            var wall = new GameObject("Enemy Attack Wall");
            wall.transform.position = new Vector3(50, 1, 2.5f);
            wall.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            before = victim.Noise;
            typeof(EnemyCombatant).GetField("nextTickAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, -1f);
            enemy.SendMessage("Update");
            Require(victim.Noise == before, "敌人射线被墙体阻挡");
            Object.DestroyImmediate(wall);
            enemy.ReceiveHit(new HitInfo { source = victim, penetration = ArmorGrade.High, stunDuration = 1 });
            Require(!enemy.IsAttacking, "眩晕中断持续射线");
            enemy.ResetCombat();
            tuning.windup = 2;
            enemy.SendMessage("Update");
            Require(enemy.IsWindingUp && !enemy.WeakPointExposed, "前摇期间不提前暴露弱点");
            enemy.ReceiveHit(new HitInfo { source = victim, penetration = ArmorGrade.High, stunDuration = 1 });
            Require(!enemy.IsWindingUp, "眩晕中断前摇");
            var flyerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Prefabs/EnemyChanYin.prefab"), new Vector3(80, 4, 0), Quaternion.identity);
            var flyer = flyerObject.GetComponent<EnemyCombatant>();
            flyer.findPlayerAutomatically = false;
            var ground = new GameObject("Flying Enemy Test Ground");
            ground.transform.position = new Vector3(80, -.5f, 0);
            ground.transform.localScale = new Vector3(10, 1, 10);
            ground.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Require(!flyer.ReceiveHit(new HitInfo { source = victim, groundOnly = true }), "颤音虫在空中免疫地面技能");
            flyer.ReceiveHit(new HitInfo { source = victim, penetration = ArmorGrade.High, stunDuration = 60 });
            float initialHeight = flyer.transform.position.y;
            for (int i = 0; i < 1000 && flyer.airborne; i++) flyer.SendMessage("Update");
            Require(flyer.transform.position.y < initialHeight && !flyer.airborne, "颤音虫眩晕下落并识别地面");
            Require(flyer.ReceiveHit(new HitInfo { source = victim, groundOnly = true }), "颤音虫坠地后接受地面技能");
            typeof(CombatTarget).GetField("stunnedUntil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flyer, -1f);
            flyer.attackTarget = victim;
            flyer.definition = Object.Instantiate(fly);
            transient.Add(flyer.definition);
            flyer.definition.detectionRange = 100;
            float groundHeight = flyer.transform.position.y;
            flyer.SendMessage("Update");
            Require(flyer.airborne && flyer.transform.position.y > groundHeight, "眩晕结束恢复飞行");

        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("NGJCombat 验证失败：" + message);
            Debug.Log("NGJCombat PASS: " + message);
        }
    }
}
