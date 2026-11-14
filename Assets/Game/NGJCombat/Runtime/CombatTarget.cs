// 统一受击组件：处理装甲、噪值、控制和死亡，供玩家与敌人使用。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using UnityEngine;

namespace NGJ.Combat
{
    public class CombatTarget : MonoBehaviour
    {
        [Header("敌我阵营：同阵营不受伤；玩家建议 0，敌人建议 1")]
        // 阵营编号：相同阵营不会互相命中；玩家建议 0，敌人建议 1。
        [Tooltip("相同阵营不会互相命中；玩家建议 0，敌人建议 1。")]
        public int team = 1;
        // 噪值上限：启用噪值上限死亡时，达到此值触发死亡。
        [Tooltip("启用噪值上限死亡时，达到此值触发死亡。")]
        [Min(1)] public float maxNoise = 100;
        // 当前／初始噪值：编辑模式设置初始值；运行模式显示当前值。
        [Tooltip("编辑模式设置初始值；运行模式显示当前值。")]
        [SerializeField, Min(0)] protected float currentNoise;
        // 躯干装甲：普通部位使用的装甲等级。
        [Tooltip("普通部位使用的装甲等级。")]
        public ArmorGrade armor = ArmorGrade.Low;
        // 弱点装甲：仅暴露的弱点碰撞体使用此装甲。
        [Tooltip("仅暴露的弱点碰撞体使用此装甲。")]
        public ArmorGrade weakPointArmor = ArmorGrade.Low;
        // 处于空中：勾选后不受仅地面技能影响。
        [Tooltip("勾选后不受仅地面技能影响。")]
        public bool airborne;
        // 穿透不足伤害倍率：穿透低于装甲时的伤害倍率，测试规则。
        [Tooltip("穿透低于装甲时的伤害倍率，测试规则。")]
        [Range(0, 1)] public float underPenetrationMultiplier = .25f;
        // 装甲阻挡控制和易伤：穿透不足时阻挡眩晕与易伤。
        [Tooltip("穿透不足时阻挡眩晕与易伤。")]
        public bool armorBlocksEffects = true;
        // 达到噪值上限死亡：关闭后仅限制噪值，不因噪值触顶死亡。
        [Tooltip("关闭后仅限制噪值，不因噪值触顶死亡。")]
        public bool deathAtNoiseLimit = true;
        // 眩晕时暴露弱点：眩晕期间允许弱点判定。
        [Tooltip("眩晕期间允许弱点判定。")]
        public bool exposeWeakPointWhenStunned = true;
        // 主动暴露弱点：敌人 AI 在攻击窗口设置。
        [Tooltip("敌人 AI 在攻击窗口设置。")]
        public bool weakPointOpen;
        public float Noise => currentNoise;
        public bool IsAlive => !dead;
        public bool IsStunned => Time.time < stunnedUntil;
        public bool IsVulnerable => Time.time < vulnerableUntil;
        public bool WeakPointExposed => weakPointOpen || (exposeWeakPointWhenStunned && IsStunned);
        public event Action<float, float> NoiseChanged;
        public event Action<HitInfo> HitReceived;
        public event Action<float> Stunned;
        public event Action Died;
        protected bool dead;
        float stunnedUntil, vulnerableUntil, vulnerability = 1;

        // 初始化组件引用和资源范围。
        protected virtual void Awake()
        {
            maxNoise = Mathf.Max(1, maxNoise);
            currentNoise = Mathf.Clamp(currentNoise, 0, maxNoise);
            if (deathAtNoiseLimit && currentNoise >= maxNoise) dead = true;
        }

        // 接收一次攻击；返回是否接受本次命中。
        public virtual bool ReceiveHit(HitInfo hit, bool weakPoint = false)
        {
            if (!CombatUtility.CanHit(hit.source, this) || (hit.groundOnly && airborne)) return false;
            ArmorGrade effectiveArmor = weakPoint && WeakPointExposed ? weakPointArmor : armor;
            bool penetrated = hit.penetration >= effectiveArmor;
            float amount = Mathf.Max(0, hit.noise) * (penetrated ? 1 : underPenetrationMultiplier);
            if (IsVulnerable) amount *= vulnerability;
            AddNoise(amount);
            if (IsAlive && (penetrated || !armorBlocksEffects))
            {
                if (hit.stunDuration > 0)
                {
                    stunnedUntil = Mathf.Max(stunnedUntil, Time.time + hit.stunDuration);
                    // 飞行敌人的 AI 订阅此事件执行坠落；不要在受击组件直接修改导航。
                    Stunned?.Invoke(hit.stunDuration);
                }
                if (hit.vulnerabilityDuration > 0)
                {
                    vulnerableUntil = Mathf.Max(vulnerableUntil, Time.time + hit.vulnerabilityDuration);
                    vulnerability = Mathf.Max(1, hit.vulnerabilityMultiplier);
                }
            }
            HitReceived?.Invoke(hit);
            return true;
        }

        // 增加噪值；死亡目标和非正增量会被忽略。
        public void AddNoise(float amount)
        {
            if (!IsAlive || amount <= 0) return;
            SetNoise(currentNoise + amount);
        }

        // 统一修改噪值并发送资源、死亡事件。
        protected void SetNoise(float value)
        {
            currentNoise = Mathf.Clamp(value, 0, maxNoise);
            if (deathAtNoiseLimit && currentNoise >= maxNoise && !dead)
            {
                dead = true;
                NoiseChanged?.Invoke(currentNoise, maxNoise);
                Died?.Invoke();
                return;
            }
            NoiseChanged?.Invoke(currentNoise, maxNoise);
        }

        // 重置战斗状态，供重生或测试调用。
        public virtual void ResetCombat()
        {
            dead = false;
            stunnedUntil = vulnerableUntil = 0;
            weakPointOpen = false;
            vulnerability = 1;
            SetNoise(0);
        }
    }
}
