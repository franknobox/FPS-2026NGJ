using System.IO;
using UnityEditor;
using UnityEngine;

namespace NGJ.Combat.Editor
{
    public static class CombatAssetBuilder
    {
        const string Root = "Assets/Game/NGJCombat/Generated";

        [MenuItem("NGJ/Combat/生成缺少的配置和预制体")]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Weapons");
            Directory.CreateDirectory(Root + "/Skills");
            Directory.CreateDirectory(Root + "/Enemies");
            AssetDatabase.Refresh();
            var sniperWave = Hitbox("SniperWave", .8f, 45, .8f, 180);
            var closeWave = Hitbox("CloseStunWave", 5, 0, .15f, 75);
            var wideWave = Hitbox("WideStunWave", 12, 0, .15f, 90);
            var groundWave = Hitbox("GroundWave", 5, 0, .15f, 180);
            var resonanceWave = Hitbox("ResonanceWave", 12, 0, .15f, 180);
            var explosion = Hitbox("GrenadeExplosion", 4, 0, .1f, 180);
            var stickyExplosion = Hitbox("StickyExplosion", 6, 0, .1f, 180);
            var grenade = Projectile("Grenade", explosion, false);
            var sticky = Projectile("StickyBomb", stickyExplosion, true);

            var sniperSkill = Skill("SniperSkill", "尖啸体·强力声波", ResourceType.Noise, 20, SkillEffect.Hitbox,
                new HitInfo { noise = 90, penetration = ArmorGrade.High }, sniperWave);
            var rifleSkill = Skill("RifleSkill", "嘶鸣体·增伤", ResourceType.Noise, 15, SkillEffect.DamageBuff, default);
            var shotgunSkill = Skill("ShotgunSkill", "撼音体·震晕声波", ResourceType.Noise, 20, SkillEffect.Hitbox,
                new HitInfo { penetration = ArmorGrade.Medium, stunDuration = 2 }, closeWave);
            var grenadeSkill = Skill("GrenadeSkill", "爆鸣体·黏质炸弹", ResourceType.Noise, 25, SkillEffect.Projectile,
                new HitInfo { penetration = ArmorGrade.High, vulnerabilityDuration = 4, vulnerabilityMultiplier = 1.5f },
                projectile: sticky);
            Skill("SiMing", "嘶鸣", ResourceType.Gland, 1, SkillEffect.Hitbox,
                new HitInfo { penetration = ArmorGrade.Low, stunDuration = 2 }, wideWave);
            Skill("HanDi", "撼地", ResourceType.Gland, 1, SkillEffect.Hitbox,
                new HitInfo { penetration = ArmorGrade.High, stunDuration = 2, groundOnly = true }, groundWave);
            Skill("GongMing", "共鸣", ResourceType.Gland, 1, SkillEffect.Resonance,
                new HitInfo { penetration = ArmorGrade.Medium }, resonanceWave);

            Weapon("JianXiao", "尖啸体", WeaponType.JianXiaoSniper, 60, 20, .9f, 100, ArmorGrade.High, sniperSkill, 1, 0, 32);
            Weapon("SiMing", "嘶鸣体", WeaponType.SiMingRifle, 8, 2, .12f, 40, ArmorGrade.Medium, rifleSkill);
            Weapon("HanYin", "撼音体", WeaponType.HanYinShotgun, 10, 15, .7f, 12, ArmorGrade.Low, shotgunSkill, 6, 8);
            Weapon("BaoMing", "爆鸣体", WeaponType.BaoMingGrenade, 85, 25, 1.2f, 35, ArmorGrade.High, grenadeSkill,
                projectile: grenade);
            PlayerPrefab();
            EnemyAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NGJCombat：配置与预制体已生成。所有数值均为测试占位值，已有资源不会覆盖。");
        }

        static T Existing<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        // 定性特性来自策划表“敌人”第 2~4 行；所有具体数值仅供白盒测试。
        static void EnemyAssets()
        {
            Enemy("ChanYin", "颤音虫", EnemyKind.ChanYin, 35, ArmorGrade.Low, 5, 3, .6f,
                EnemyQuantity.Many, true, EnemyAttackMode.Intermittent, "大量低质敌人，骚扰；飞行、较快、难以命中；被击晕时坠落地面。高频、低伤害间隙攻击。");
            Enemy("MiYin", "觅音虫", EnemyKind.MiYin, 100, ArmorGrade.Medium, 3, 10, 1.5f,
                EnemyQuantity.Normal, false, EnemyAttackMode.Intermittent, "最普通的敌人；地面、速度适中；攻击和眩晕时暴露低介质弱点。频率和伤害适中的间隙攻击。");
            Enemy("ZhenYin", "震音虫", EnemyKind.ZhenYin, 240, ArmorGrade.High, 1.5f, 12, 3,
                EnemyQuantity.Few, false, EnemyAttackMode.ContinuousBeam, "重型单位，少量高质；地面、较慢；攻击和眩晕时暴露低介质弱点。低频、高伤害持续射线攻击。");
        }

        static void Enemy(string name, string label, EnemyKind kind, float health, ArmorGrade armor,
            float speed, float damage, float interval, EnemyQuantity quantity, bool flying,
            EnemyAttackMode mode, string description)
        {
            string assetPath = Root + "/Enemies/" + name + ".asset";
            var definition = Existing<EnemyDefinition>(assetPath);
            if (!definition)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                definition.kind = kind;
                definition.displayName = label;
                definition.description = description;
                definition.quantity = quantity;
                definition.maxNoise = health;
                definition.armor = armor;
                definition.moveSpeed = speed;
                definition.attackNoise = damage;
                definition.attackInterval = interval;
                definition.flying = flying;
                definition.fallsWhenStunned = flying;
                definition.exposeDuringWindup = !flying;
                definition.exposeDuringAttack = !flying;
                definition.attackMode = mode;
                AssetDatabase.CreateAsset(definition, assetPath);
            }
            string prefabPath = Root + "/Prefabs/Enemy" + name + ".prefab";
            if (Existing<GameObject>(prefabPath)) return;
            var go = new GameObject("Enemy" + name);
            go.layer = CombatLayers.Enemy;
            var motor = go.AddComponent<CharacterController>();
            motor.height = 2;
            motor.radius = .5f;
            motor.center = Vector3.up;
            var enemy = go.AddComponent<EnemyCombatant>();
            enemy.definition = definition;
            enemy.attackMask = CombatLayers.AttackMask;
            go.AddComponent<EnemyVisualBinding>();
            enemy.ApplyDefinition();
            var origin = new GameObject("AttackOrigin").transform;
            origin.SetParent(go.transform, false);
            origin.localPosition = Vector3.up * 1.2f;
            enemy.attackOrigin = origin;
            var weak = new GameObject("WeakPoint");
            weak.layer = CombatLayers.Enemy;
            weak.transform.SetParent(go.transform, false);
            weak.transform.localPosition = new Vector3(0, 1.2f, .55f);
            weak.AddComponent<SphereCollider>().radius = .22f;
            var hurtbox = weak.AddComponent<CombatHurtbox>();
            hurtbox.target = enemy;
            hurtbox.weakPoint = true;
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
        }
        static CombatHitbox Hitbox(string name, float radius, float speed, float lifetime, float angle)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var existing = Existing<CombatHitbox>(path);
            if (existing) return existing;
            var go = new GameObject(name);
            var hitbox = go.AddComponent<CombatHitbox>();
            hitbox.radius = radius;
            hitbox.speed = speed;
            hitbox.lifetime = lifetime;
            hitbox.halfAngle = angle;
            // 环境遮挡与角色目标分开，支持 ART 场景的 Ground 层。
            hitbox.obstructionMask = CombatLayers.WorldMask;
            hitbox.targetMask = CombatLayers.ActorMask;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<CombatHitbox>();
        }
        static CombatProjectile Projectile(string name, CombatHitbox explosion, bool sticky)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var existing = Existing<CombatProjectile>(path);
            if (existing) return existing;
            var go = new GameObject(name);
            var projectile = go.AddComponent<CombatProjectile>();
            projectile.collisionMask = CombatLayers.AttackMask;
            projectile.explosionPrefab = explosion;
            projectile.sticky = sticky;
            projectile.detonationDelay = .6f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<CombatProjectile>();
        }
        static SkillDefinition Skill(string name, string label, ResourceType resource, float cost,
            SkillEffect effect, HitInfo hit, CombatHitbox hitbox = null, CombatProjectile projectile = null)
        {
            string path = Root + "/Skills/" + name + ".asset";
            var existing = Existing<SkillDefinition>(path);
            if (existing) return existing;
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.displayName = label;
            skill.resource = resource;
            skill.cost = cost;
            skill.effect = effect;
            skill.hit = hit;
            skill.hitboxPrefab = hitbox;
            skill.projectilePrefab = projectile;
            skill.cooldown = 2;
            skill.duration = 2;
            AssetDatabase.CreateAsset(skill, path);
            return skill;
        }
        static void Weapon(string name, string label, WeaponType type, float damage, float selfNoise,
            float interval, float range, ArmorGrade penetration, SkillDefinition skill, int pellets = 1,
            float spread = 0, int maxTargets = 1, CombatProjectile projectile = null)
        {
            string path = Root + "/Weapons/" + name + ".asset";
            if (Existing<WeaponDefinition>(path)) return;
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.type = type;
            weapon.displayName = label;
            weapon.targetNoise = damage;
            weapon.selfNoise = selfNoise;
            weapon.fireInterval = interval;
            weapon.range = range;
            weapon.penetration = penetration;
            weapon.skill = skill;
            weapon.pelletCount = pellets;
            weapon.spreadAngle = spread;
            weapon.maxTargetsPerPellet = maxTargets;
            weapon.projectilePrefab = projectile;
            AssetDatabase.CreateAsset(weapon, path);
        }
        static void PlayerPrefab()
        {
            string path = Root + "/Prefabs/PlayerCombatRig.prefab";
            if (Existing<GameObject>(path)) return;
            var go = new GameObject("PlayerCombatRig");
            go.layer = CombatLayers.Player;
            var stats = go.AddComponent<PlayerCombatStats>();
            stats.team = 0;
            var origin = new GameObject("CastOrigin").transform;
            origin.SetParent(go.transform, false);
            origin.localPosition = Vector3.up * 1.6f;
            var runner = go.AddComponent<SkillRunner>();
            runner.owner = stats;
            runner.castOrigin = origin;
            var controller = go.AddComponent<CombatWeaponController>();
            controller.owner = stats;
            controller.hitMask = CombatLayers.PlayerAttackMask;
            controller.skills = runner;
            controller.fireOrigin = origin;
            controller.equipped = Existing<WeaponDefinition>(Root + "/Weapons/SiMing.asset");
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
