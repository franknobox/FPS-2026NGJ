// 武器配置资源：在 Inspector 修改即可调参，所有引用该资源的武器共用配置。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using UnityEngine;

namespace NGJ.Combat
{
    [CreateAssetMenu(menuName = "NGJ/Combat/武器配置")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        // 武器类型：决定武器分类；榴弹需要投射物预制体。
        [Tooltip("决定武器分类；榴弹需要投射物预制体。")]
        public WeaponType type;
        // 显示名称：供界面显示的名称。
        [Tooltip("供界面显示的名称。")]
        public string displayName;
        // 开火间隔（秒）：两次普攻之间的最小间隔，越小射速越快。
        [Tooltip("两次普攻之间的最小间隔，越小射速越快。")]
        [Min(.01f)] public float fireInterval = .2f;
        // 开火自身噪值：每次普攻增加给玩家的噪值。
        [Tooltip("每次普攻增加给玩家的噪值。")]
        [Min(0)] public float selfNoise = 2;
        // 单弹丸伤害／噪值：每颗弹丸给敌人增加的基础噪值，霰弹会按弹丸累加。
        [Tooltip("每颗弹丸给敌人增加的基础噪值，霰弹会按弹丸累加。")]
        [Min(0)] public float targetNoise = 10;
        // 穿透等级：与目标装甲等级比较，决定减伤和控制效果。
        [Tooltip("与目标装甲等级比较，决定减伤和控制效果。")]
        public ArmorGrade penetration;
        // 射线射程（米）：用于射线普攻；投射物使用速度和存活时间决定飞行距离。
        [Tooltip("用于射线普攻；投射物使用速度和存活时间决定飞行距离。")]
        [Min(.1f)] public float range = 30;
        // 弹丸数量：一次普攻发射的射线数量。
        [Tooltip("一次普攻发射的射线数量。")]
        [Min(1)] public int pelletCount = 1;
        // 散布半角（度）：相对瞄准方向的最大散布半角。
        [Tooltip("相对瞄准方向的最大散布半角。")]
        [Range(0, 90)] public float spreadAngle;
        // 单弹丸命中上限：每颗弹丸最多穿透多少个不同目标。
        [Tooltip("每颗弹丸最多穿透多少个不同目标。")]
        [Min(1)] public int maxTargetsPerPellet = 1;
        // 投射物预制体：榴弹或黏附炸弹；技能选择投射物效果时必填。
        [Tooltip("榴弹或黏附炸弹；技能选择投射物效果时必填。")]
        public CombatProjectile projectilePrefab;
        // 投射物初速度（米／秒）：生成投射物时的前向速度。
        [Tooltip("生成投射物时的前向速度。")]
        [Min(.1f)] public float projectileSpeed = 15;
        // 武器技能：此武器释放的技能配置。
        [Tooltip("此武器释放的技能配置。")]
        public SkillDefinition skill;
    }
}
