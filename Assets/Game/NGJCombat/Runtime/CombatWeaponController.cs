// 武器执行器：外部输入调用 TryFire / TryWeaponSkill，不直接绑定按键。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGJ.Combat
{
    [RequireComponent(typeof(PlayerCombatStats), typeof(SkillRunner))]
    public sealed class CombatWeaponController : MonoBehaviour
    {
        // 所属玩家属性：连接玩家根物体上的 PlayerCombatStats。
        [Tooltip("连接玩家根物体上的 PlayerCombatStats。")]
        public PlayerCombatStats owner;
        // 技能执行组件：连接同一玩家的 SkillRunner。
        [Tooltip("连接同一玩家的 SkillRunner。")]
        public SkillRunner skills;
        // 普攻瞄准／发射点：正 Z 方向为开火方向。
        [Tooltip("正 Z 方向为开火方向。")]
        public Transform fireOrigin;
        [Tooltip("实际弹体出生点；瞄准仍由 fireOrigin 决定。")]
        public Transform muzzle;
        // 当前装备武器：可直接拖入武器配置测试。
        [Tooltip("可直接拖入武器配置测试。")]
        public WeaponDefinition equipped;
        // 普攻射线检测层：同时包含环境和目标层。
        [Tooltip("同时包含环境和目标层。")]
        public LayerMask hitMask = ~0;
        public event Action<WeaponDefinition> Fired;
        public event Action<WeaponDefinition> Equipped;
        readonly Dictionary<WeaponDefinition, float> nextShots = new Dictionary<WeaponDefinition, float>();
        // 重开关卡时清理各武器射击冷却。
        public void ResetWeapons() { nextShots.Clear(); }
        // 初始化组件引用和资源范围。
        void Awake()
        {
            if (!owner) owner = GetComponent<PlayerCombatStats>();
            if (!skills) skills = GetComponent<SkillRunner>();
        }
        // 切换当前武器，通知界面或装备表现。
        public void Equip(WeaponDefinition weapon)
        {
            equipped = weapon;
            Equipped?.Invoke(weapon);
        }
        // 尝试释放当前武器关联的技能。
        public bool TryWeaponSkill() => equipped && skills && skills.TryCast(equipped.skill);
        // 按射速检查后发出一次普攻，再增加自身噪值。
        public bool TryFire()
        {
            if (!isActiveAndEnabled || !equipped || !owner || !owner.CanAct ||
                (nextShots.TryGetValue(equipped, out float next) && Time.time < next)) return false;
            if ((equipped.type == WeaponType.BaoMingGrenade || equipped.type == WeaponType.RocketLauncher) &&
                (!equipped.projectilePrefab || !equipped.projectilePrefab.explosionPrefab)) return false;
            Transform origin = fireOrigin ? fireOrigin : transform;
            HitInfo hit = new HitInfo { source = owner, noise = equipped.targetNoise * owner.DamageMultiplier,
                penetration = equipped.penetration };
            Vector3 spawnPosition = muzzle ? muzzle.position : origin.position;
            if (equipped.projectilePrefab && Physics.Linecast(origin.position, spawnPosition, CombatLayers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            nextShots[equipped] = Time.time + Mathf.Max(.01f, equipped.fireInterval);
            // 本次已被接受的攻击会发出，即使开火产生的自身噪值随后导致死亡。
            if (equipped.projectilePrefab)
            {
                Vector3 aim = origin.position + origin.forward * equipped.range;
                if (Physics.Raycast(origin.position, origin.forward, out var contact, equipped.range, hitMask, QueryTriggerInteraction.Ignore)) aim = contact.point;
                Vector3 direction = (aim - spawnPosition).normalized;
                Instantiate(equipped.projectilePrefab, spawnPosition, Quaternion.LookRotation(direction))
                    .Initialize(hit, direction * equipped.projectileSpeed);
            }
            else
                for (int pellet = 0; pellet < Mathf.Max(1, equipped.pelletCount); pellet++)
                {
                    Vector2 spread = UnityEngine.Random.insideUnitCircle * Mathf.Tan(equipped.spreadAngle * Mathf.Deg2Rad);
                    Vector3 direction = (origin.forward + origin.right * spread.x + origin.up * spread.y).normalized;
                    FireRay(origin.position, direction, hit);
                }
            owner.RegisterShot(equipped.selfNoise);
            Fired?.Invoke(equipped);
            return true;
        }
        // 按距离处理射线命中；墙体阻挡，按配置穿透多个目标。
        void FireRay(Vector3 origin, Vector3 direction, HitInfo hit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, equipped.range, hitMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            var visited = new HashSet<CombatTarget>();
            int count = 0;
            foreach (var contact in hits)
            {
                CombatTarget target = CombatUtility.Resolve(contact.collider, out bool weakPoint);
                // 非受击目标的实体碰撞体视为墙体；普通触发器不挡子弹。
                if (!target) { if (!contact.collider.isTrigger) break; else continue; }
                if (!CombatUtility.CanHit(owner, target) || !visited.Add(target)) continue;
                hit.point = contact.point;
                hit.direction = direction;
                if (target.ReceiveHit(hit, weakPoint) && ++count >= Mathf.Max(1, equipped.maxTargetsPerPellet)) break;
            }
        }
    }
}
