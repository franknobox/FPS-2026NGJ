using UnityEngine;

namespace NGJ.Combat
{
    public enum EnemyKind { [InspectorName("颤音虫")] ChanYin, [InspectorName("觅音虫")] MiYin, [InspectorName("震音虫")] ZhenYin }
    public enum EnemyAttackMode { [InspectorName("间隙攻击")] Intermittent, [InspectorName("持续射线")] ContinuousBeam }
    public enum EnemyQuantity { [InspectorName("较多")] Many, [InspectorName("适中")] Normal, [InspectorName("较少")] Few }

    // 敌人类型资源：策划表的定性规则在生成器中设置，具体数值是可调整的测试值。
    [CreateAssetMenu(menuName = "NGJ/Combat/敌人配置")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Tooltip("对应策划表中的三类敌人。")] public EnemyKind kind;
        [Tooltip("显示在 UI 中的名称。")] public string displayName;
        [TextArea, Tooltip("策划表中的定位与特性说明。")] public string description;
        [Tooltip("仅为刷怪系统提供数量倾向，不会自动生成敌人。")] public EnemyQuantity quantity;
        [Min(1), Tooltip("噪值达到此值死亡；表中低中高的具体测试值。")] public float maxNoise = 100;
        [Tooltip("躯干使用的装甲。")] public ArmorGrade armor = ArmorGrade.Medium;
        [Tooltip("暴露时弱点使用的装甲。")] public ArmorGrade weakPointArmor = ArmorGrade.Low;
        [Tooltip("颤音虫启用飞行；眩晕可使其落地。")] public bool flying;
        [Min(0), Tooltip("移动速度，米/秒。")] public float moveSpeed = 3;
        [Min(0), Tooltip("相对目标脚底的悬浮高度，米。")] public float flightHeight = 3;
        [Tooltip("飞行敌人眩晕时受重力下落，结束后恢复飞行。")] public bool fallsWhenStunned;
        [Tooltip("蓄力预警期间暴露弱点，允许低穿透控制打断本次攻击。")] public bool exposeDuringWindup = true;
        [Tooltip("攻击期间继续暴露弱点，冷却期间关闭。")] public bool exposeDuringAttack = true;
        [Tooltip("眩晕期间暴露弱点。")] public bool exposeDuringStun = true;
        [Tooltip("间隙攻击一次结算；持续射线按间隔多次结算。")] public EnemyAttackMode attackMode;
        [Min(0), Tooltip("每次射击或每个射线伤害跳数产生的噪值。")] public float attackNoise = 10;
        [Tooltip("敌人攻击的穿透等级。")] public ArmorGrade attackPenetration = ArmorGrade.Medium;
        [Min(.1f), Tooltip("最大攻击距离，米。")] public float attackRange = 12;
        [Min(0), Tooltip("发现目标的距离，米。")] public float detectionRange = 30;
        [Min(0), Tooltip("攻击前摇，秒；可在前摇中眩晕打断。")] public float windup = .4f;
        [Min(.01f), Tooltip("攻击结束到下一次前摇的冷却，秒。")] public float attackInterval = 1.5f;
        [Min(.02f), Tooltip("间隙攻击的动作和弱点暴露窗口，秒；每轮仍只结算一次伤害。")] public float attackDuration = .3f;
        [Min(.01f), Tooltip("单轮持续射线长度，秒。")] public float beamDuration = 2;
        [Min(.02f), Tooltip("持续射线的伤害跳数间隔，秒。")] public float beamTickInterval = .3f;
    }
}
