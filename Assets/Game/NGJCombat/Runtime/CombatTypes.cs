// 战斗模块共用的分类、命中数据和物理查询辅助方法。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using UnityEngine;

namespace NGJ.Combat
{
    // 只改变 Inspector 中的显示名称，枚举顺序保持不变，兼容已生成配置。
    public enum WeaponType
    {
        [InspectorName("尖啸体／狙击")] JianXiaoSniper,
        [InspectorName("嘶鸣体／步枪")] SiMingRifle,
        [InspectorName("撼音体／霰弹")] HanYinShotgun,
        [InspectorName("爆鸣体／榴弹")] BaoMingGrenade,
        [InspectorName("火箭筒")] RocketLauncher
    }
    public enum ArmorGrade { [InspectorName("低")] Low, [InspectorName("中")] Medium, [InspectorName("高")] High }
    public enum ResourceType { [InspectorName("噪值")] Noise, [InspectorName("声腺")] Gland }
    public enum SkillEffect
    {
        [InspectorName("命中区域／声波")] Hitbox,
        [InspectorName("投射物")] Projectile,
        [InspectorName("临时增伤")] DamageBuff,
        [InspectorName("共鸣吸收与反击")] Resonance
    }
    public enum HitboxShape { [InspectorName("球形")] Sphere, [InspectorName("盒形")] Box }

    [Serializable]
    public struct HitInfo
    {
        // 同一次普攻的弹丸和爆炸共享回调，由发射方保证只结算一次自身噪值。
        [NonSerialized] public Action hitConfirmed;
        [NonSerialized] public System.Collections.Generic.HashSet<CombatTarget> pushedTargets;
        public float knockback;
        // 攻击来源：运行时自动设置，用于敌我过滤。
        [Tooltip("运行时自动设置，用于敌我过滤。")]
        public CombatTarget source;
        // 基础伤害／噪值：本次命中给目标增加的噪值。
        [Tooltip("本次命中给目标增加的噪值。")]
        [Min(0)] public float noise;
        // 穿透等级：与目标装甲等级比较，决定减伤和控制效果。
        [Tooltip("与目标装甲等级比较，决定减伤和控制效果。")]
        public ArmorGrade penetration;
        // 眩晕时间（秒）：设为 0 不眩晕；实际停止行动由 AI 响应。
        [Tooltip("设为 0 不眩晕；实际停止行动由 AI 响应。")]
        [Min(0)] public float stunDuration;
        // 易伤时间（秒）：设为 0 不施加易伤。
        [Tooltip("设为 0 不施加易伤。")]
        [Min(0)] public float vulnerabilityDuration;
        // 易伤倍率：影响后续命中伤害，不叠乘。
        [Tooltip("影响后续命中伤害，不叠乘。")]
        [Min(1)] public float vulnerabilityMultiplier;
        // 仅命中地面目标：过滤 airborne 为真的目标。
        [Tooltip("过滤 airborne 为真的目标。")]
        public bool groundOnly;
        // 命中位置：运行时由命中检测填写。
        [Tooltip("运行时由命中检测填写。")]
        public Vector3 point;
        // 命中方向：运行时由命中检测填写。
        [Tooltip("运行时由命中检测填写。")]
        public Vector3 direction;
    }

    // 可挂在子物体碰撞体上，支持弱点和多个碰撞体共用一个受击目标。
    public static class CombatUtility
    {
        // 从碰撞体查找受击目标，并返回弱点标记。
        public static CombatTarget Resolve(Collider collider, out bool weakPoint)
        {
            var hurtbox = collider.GetComponentInParent<CombatHurtbox>();
            weakPoint = hurtbox && hurtbox.weakPoint;
            return hurtbox && hurtbox.target ? hurtbox.target : collider.GetComponentInParent<CombatTarget>();
        }

        // 统一判断存活状态、自己和同阵营过滤。
        public static bool CanHit(CombatTarget source, CombatTarget target)
        {
            return target && target.IsAlive && (!source || (source != target && source.team != target.team));
        }

        // 用环境层射线检查两点之间是否被墙体遮挡。
        public static bool Visible(Vector3 origin, Vector3 point, LayerMask obstructionMask)
        {
            return !Physics.Linecast(origin, point, obstructionMask, QueryTriggerInteraction.Ignore);
        }
    }
}
