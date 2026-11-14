// 手动弹道投射物：高速扫掠检测，支持碰撞爆炸或黏附延时爆炸。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using UnityEngine;

namespace NGJ.Combat
{
    // SphereCast 防止高速穿透薄碰撞体；爆炸伤害由独立 Hitbox 结算。
    public sealed class CombatProjectile : MonoBehaviour
    {
        // 判定半径（米）：球形命中区域或投射物碰撞半径。
        [Tooltip("球形命中区域或投射物碰撞半径。")]
        [Min(.01f)] public float radius = .15f;
        // 存活时间（秒）：Hitbox 到期销毁；投射物到期爆炸。
        [Tooltip("Hitbox 到期销毁；投射物到期爆炸。")]
        [Min(.01f)] public float lifetime = 5;
        // 重力加速度：设为 0 可直线飞行。
        [Tooltip("设为 0 可直线飞行。")]
        [Min(0)] public float gravity = 9.81f;
        // 命中后黏附：启用后附着在命中物体上，等待延时爆炸。
        [Tooltip("启用后附着在命中物体上，等待延时爆炸。")]
        public bool sticky;
        // 黏附引爆延时（秒）：黏附后等待时间，设为 0 立即爆炸。
        [Tooltip("黏附后等待时间，设为 0 立即爆炸。")]
        [Min(0)] public float detonationDelay = .5f;
        // 爆炸区域预制体：爆炸时生成的 Hitbox，负责伤害和附加效果。
        [Tooltip("爆炸时生成的 Hitbox，负责伤害和附加效果。")]
        public CombatHitbox explosionPrefab;
        // 投射物碰撞层：应包含环境和目标层。
        [Tooltip("应包含环境和目标层。")]
        public LayerMask collisionMask = ~0;
        HitInfo hit;
        Vector3 velocity;
        float expiresAt, detonateAt;
        bool initialized, attached, detonated;
        Transform attachedTo;
        Vector3 attachedLocalPoint;
        bool hadAttachment;

        // 写入本次攻击数据并开始运行；同时处理出生时的重叠。
        public void Initialize(HitInfo info, Vector3 initialVelocity)
        {
            hit = info;
            velocity = initialVelocity;
            expiresAt = Time.time + Mathf.Max(.01f, lifetime);
            initialized = true;
            // 处理出生点已在碰撞体内的情况。
            foreach (var collider in Physics.OverlapSphere(transform.position, radius, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (Ignore(collider)) continue;
                Contact(collider, transform.position);
                break;
            }
        }

        // 忽略自身及同阵营目标，避免投射物刚出生就击中玩家。
        bool Ignore(Collider collider)
        {
            CombatTarget target = CombatUtility.Resolve(collider, out _);
            return collider.transform.IsChildOf(transform) || (target && hit.source &&
                (target == hit.source || target.team == hit.source.team));
        }

        // 按物理时间步推进移动与碰撞检测。
        void FixedUpdate()
        {
            if (!initialized || detonated) return;
            if (attached)
            {
                if (attachedTo) transform.position = attachedTo.TransformPoint(attachedLocalPoint);
                if (Time.time >= detonateAt || (hadAttachment && !attachedTo)) Detonate();
                return;
            }
            if (Time.time >= expiresAt) { Detonate(); return; }
            velocity += Vector3.down * gravity * Time.fixedDeltaTime;
            Vector3 step = velocity * Time.fixedDeltaTime;
            if (step.sqrMagnitude <= .000001f) return;
            var contacts = Physics.SphereCastAll(transform.position, radius, step.normalized, step.magnitude,
                collisionMask, QueryTriggerInteraction.Ignore);
            Array.Sort(contacts, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var contact in contacts)
            {
                if (Ignore(contact.collider)) continue;
                Vector3 center = transform.position + step.normalized * Mathf.Max(0, contact.distance - .01f);
                Contact(contact.collider, center);
                return;
            }
            transform.position += step;
            transform.rotation = Quaternion.LookRotation(step.normalized);
        }

        // 处理碰撞接触：立即爆炸，或记录黏附对象与局部位置。
        void Contact(Collider collider, Vector3 center)
        {
            transform.position = center;
            if (!sticky) { Detonate(); return; }
            attached = true;
            attachedTo = collider.transform;
            hadAttachment = true;
            attachedLocalPoint = attachedTo.InverseTransformPoint(center);
            detonateAt = Time.time + Mathf.Max(0, detonationDelay);
            if (detonationDelay <= 0) Detonate();
        }
        // 只执行一次爆炸，创建爆炸 Hitbox 后销毁投射物。
        void Detonate()
        {
            if (detonated) return;
            detonated = true;
            if (explosionPrefab)
                Instantiate(explosionPrefab, transform.position, transform.rotation).Initialize(hit);
            Destroy(gameObject);
        }
        // 选中物体时在 Scene 窗口显示判定范围。
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
