// 技能配置资源：保存消耗、冷却、效果类型及关联预制体。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using UnityEngine;

namespace NGJ.Combat
{
    [CreateAssetMenu(menuName = "NGJ/Combat/技能配置")]
    public sealed class SkillDefinition : ScriptableObject
    {
        // 显示名称：供界面显示的名称。
        [Tooltip("供界面显示的名称。")]
        public string displayName;
        // 消耗资源类型：噪值或声腺。
        [Tooltip("噪值或声腺。")]
        public ResourceType resource = ResourceType.Gland;
        // 资源消耗量：不足时不能释放；声腺技能默认消耗 1。
        [Tooltip("不足时不能释放；声腺技能默认消耗 1。")]
        [Min(0)] public float cost = 1;
        // 冷却时间（秒）：成功释放后开始计时。
        [Tooltip("成功释放后开始计时。")]
        [Min(0)] public float cooldown = 2;
        // 技能效果类型：选择声波、投射物、增伤或共鸣。
        [Tooltip("选择声波、投射物、增伤或共鸣。")]
        public SkillEffect effect;
        // 命中区域预制体：声波或共鸣反击使用的 Hitbox。
        [Tooltip("声波或共鸣反击使用的 Hitbox。")]
        public CombatHitbox hitboxPrefab;
        // 投射物预制体：榴弹或黏附炸弹；技能选择投射物效果时必填。
        [Tooltip("榴弹或黏附炸弹；技能选择投射物效果时必填。")]
        public CombatProjectile projectilePrefab;
        // 投射物初速度（米／秒）：生成投射物时的前向速度。
        [Tooltip("生成投射物时的前向速度。")]
        [Min(.1f)] public float projectileSpeed = 15;
        public HitInfo hit;
        // 效果持续时间（秒）：用于增伤或共鸣吸收阶段。
        [Tooltip("用于增伤或共鸣吸收阶段。")]
        [Min(.01f)] public float duration = 2;
        // 增伤倍率：增伤技能的输出倍率，不提高开火自身噪值。
        [Tooltip("增伤技能的输出倍率，不提高开火自身噪值。")]
        [Min(1)] public float damageMultiplier = 1.5f;
        // 共鸣反击倍率：吸收的原始噪值乘以此倍率后加入反击伤害。
        [Tooltip("吸收的原始噪值乘以此倍率后加入反击伤害。")]
        [Min(0)] public float absorbedNoiseMultiplier = 1;
        [System.NonSerialized] public WeaponDefinition sourceWeapon;
        [System.NonSerialized] public int extraTriggers;
        [System.NonSerialized] public float rangeMultiplier=1;
    }
}
