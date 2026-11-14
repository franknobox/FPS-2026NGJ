// 主动查询的命中区域：负责范围检测、遮挡过滤与每目标一次结算。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System.Collections.Generic;
using UnityEngine;

namespace NGJ.Combat
{
    // 使用主动查询而非 Trigger 回调；出生时已经重叠的目标也能命中。
    public sealed class CombatHitbox : MonoBehaviour
    {
        // 判定形状：球形或盒形，均使用主动物理查询。
        [Tooltip("球形或盒形，均使用主动物理查询。")]
        public HitboxShape shape;
        // 判定半径（米）：球形命中区域或投射物碰撞半径。
        [Tooltip("球形命中区域或投射物碰撞半径。")]
        [Min(.01f)] public float radius = 3;
        // 盒形半尺寸（米）：盒形区域的 X/Y/Z 半尺寸。
        [Tooltip("盒形区域的 X/Y/Z 半尺寸。")]
        public Vector3 halfExtents = new Vector3(3, 2, 3);
        // 扇形半角（度）：180 为全方向，90 为前方半球。
        [Tooltip("180 为全方向，90 为前方半球。")]
        [Range(0, 180)] public float halfAngle = 180;
        // 存活时间（秒）：Hitbox 到期销毁；投射物到期爆炸。
        [Tooltip("Hitbox 到期销毁；投射物到期爆炸。")]
        [Min(.01f)] public float lifetime = .15f;
        // 声波速度（米／秒）：设为 0 表示原地范围判定。
        [Tooltip("设为 0 表示原地范围判定。")]
        [Min(0)] public float speed;
        // 可命中物理层：应包含敌人受击碰撞体所在层。
        [Tooltip("应包含敌人受击碰撞体所在层。")]
        public LayerMask targetMask = ~0;
        // 墙体遮挡层：仅包含墙和地形，排除角色层。
        [Tooltip("仅包含墙和地形，排除角色层。")]
        public LayerMask obstructionMask;
        // 最多命中目标数：每个 Hitbox 对同一目标只结算一次。
        [Tooltip("每个 Hitbox 对同一目标只结算一次。")]
        [Min(1)] public int maxTargets = 64;
        readonly HashSet<CombatTarget> hitTargets = new HashSet<CombatTarget>();
        HitInfo hit;
        float expiresAt;
        bool initialized;

        // 写入本次攻击数据并开始运行；同时处理出生时的重叠。
        public void Initialize(HitInfo info)
        {
            hit = info;
            hitTargets.Clear();
            expiresAt = Time.time + Mathf.Max(.01f, lifetime);
            initialized = true;
            Scan(transform.position, 0, transform.position);
        }

        // 按物理时间步推进移动与碰撞检测。
        void FixedUpdate()
        {
            if (!initialized || hitTargets.Count >= Mathf.Max(1, maxTargets)) return;
            if (Time.time >= expiresAt) { Destroy(gameObject); return; }
            Vector3 start = transform.position;
            float travel = Mathf.Max(0, speed) * Time.fixedDeltaTime;
            bool blocked = false;
            if (travel > 0 && Physics.Raycast(start, transform.forward, out RaycastHit wall, travel,
                obstructionMask, QueryTriggerInteraction.Ignore))
            {
                travel = Mathf.Max(0, wall.distance - .01f);
                blocked = true;
            }
            Vector3 end = start + transform.forward * travel;
            Scan(start, travel, end);
            transform.position = end;
            if (blocked) Destroy(gameObject);
        }

        // 查询完整移动路径内的碰撞体，并过滤阵营、角度、遮挡和重复目标。
        void Scan(Vector3 start, float travel, Vector3 end)
        {
            Collider[] colliders;
            if (shape == HitboxShape.Sphere)
                colliders = Physics.OverlapCapsule(start, end, Mathf.Max(.01f, radius), targetMask, QueryTriggerInteraction.Collide);
            else
            {
                Vector3 extents = new Vector3(Mathf.Max(.01f, halfExtents.x), Mathf.Max(.01f, halfExtents.y),
                    Mathf.Max(.01f, halfExtents.z) + travel * .5f);
                colliders = Physics.OverlapBox((start + end) * .5f, extents, transform.rotation, targetMask, QueryTriggerInteraction.Collide);
            }
            // 弱点优先，避免同一目标的躯干碰撞体先被处理后吞掉弱点命中。
            System.Array.Sort(colliders, (a, b) => IsWeak(b).CompareTo(IsWeak(a)));
            foreach (Collider collider in colliders)
            {
                CombatTarget target = CombatUtility.Resolve(collider, out bool weakPoint);
                if (!CombatUtility.CanHit(hit.source, target) || hitTargets.Contains(target)) continue;
                Vector3 point = collider.ClosestPoint(end);
                Vector3 delta = point - start;
                if (delta.sqrMagnitude > .001f && Vector3.Angle(transform.forward, delta) > halfAngle) continue;
                if (!CombatUtility.Visible(start, point, obstructionMask)) continue;
                HitInfo info = hit;
                info.point = point;
                info.direction = transform.forward;
                if (!target.ReceiveHit(info, weakPoint)) continue;
                hitTargets.Add(target);
                if (hitTargets.Count >= Mathf.Max(1, maxTargets)) { Destroy(gameObject); return; }
            }
        }

        // 判断碰撞体是否标记为弱点，供命中排序使用。
        static bool IsWeak(Collider collider)
        {
            CombatUtility.Resolve(collider, out bool weak);
            return weak;
        }
        // 选中物体时在 Scene 窗口显示判定范围。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            if (shape == HitboxShape.Sphere) Gizmos.DrawWireSphere(Vector3.zero, radius);
            else Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2);
        }
    }
}
