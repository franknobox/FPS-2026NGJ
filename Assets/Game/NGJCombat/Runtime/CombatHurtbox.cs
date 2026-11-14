// 子碰撞体代理：可把躯干和弱点转发给同一个受击组件。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using UnityEngine;

namespace NGJ.Combat
{
    public sealed class CombatHurtbox : MonoBehaviour
    {
        // 所属受击目标：子碰撞体把命中转交给此目标。
        [Tooltip("子碰撞体把命中转交给此目标。")]
        public CombatTarget target;
        // 是否为弱点：只有目标弱点已暴露时才使用弱点装甲。
        [Tooltip("只有目标弱点已暴露时才使用弱点装甲。")]
        public bool weakPoint;
        // 在编辑器首次添加组件时设置默认引用或阵营。
        void Reset() { target = GetComponentInParent<CombatTarget>(); }
    }
}
