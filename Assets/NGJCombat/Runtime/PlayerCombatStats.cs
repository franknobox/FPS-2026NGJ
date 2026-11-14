// 玩家战斗资源与状态：在统一受击逻辑上增加声腺、增伤与共鸣吸收。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using UnityEngine;

namespace NGJ.Combat
{
    public sealed class PlayerCombatStats : CombatTarget
    {
        [Header("以下为测试默认值，非策划定稿")]
        // 声腺上限：玩家可持有的声腺数量上限。
        [Tooltip("玩家可持有的声腺数量上限。")]
        [Min(0)] public float maxGlands = 2;
        // 当前／初始声腺：编辑模式设置初始值；运行模式显示当前值。
        [Tooltip("编辑模式设置初始值；运行模式显示当前值。")]
        [SerializeField, Min(0)] float glands = 2;
        // 每秒声腺回复：设为 0 关闭自然回复。
        [Tooltip("设为 0 关闭自然回复。")]
        [Min(0)] public float glandRegenPerSecond;
        // 每秒自然降噪：设为 0 关闭自然降噪。
        [Tooltip("设为 0 关闭自然降噪。")]
        [Min(0)] public float noiseDecayPerSecond;
        // 降噪等待时间（秒）：最后一次开火或受击后，等待此时间开始降噪。
        [Tooltip("最后一次开火或受击后，等待此时间开始降噪。")]
        [Min(0)] public float noiseDecayDelay = 1;
        public float Glands => glands;
        public bool CanAct => isActiveAndEnabled && IsAlive && !IsStunned;
        public bool IsAbsorbing => absorbing;
        public float AbsorbedNoise => absorbedNoise;
        public float DamageMultiplier => Time.time < buffUntil ? damageMultiplier : 1;
        public event Action<float, float> GlandsChanged;
        public event Action<float> AttackAbsorbed;
        float lastActivity, buffUntil, damageMultiplier = 1, absorbedNoise;
        bool absorbing;

        // 在编辑器首次添加组件时设置默认引用或阵营。
        void Reset() { team = 0; }
        // 初始化组件引用和资源范围。
        protected override void Awake()
        {
            base.Awake();
            glands = Mathf.Clamp(glands, 0, maxGlands);
        }
        // 逐帧更新计时和状态。
        void Update()
        {
            if (!IsAlive) return;
            if (noiseDecayPerSecond > 0 && Time.time >= lastActivity + noiseDecayDelay)
                SetNoise(Noise - noiseDecayPerSecond * Time.deltaTime);
            if (glandRegenPerSecond > 0) AddGland(glandRegenPerSecond * Time.deltaTime);
        }

        // 记录开火时间，并累计本次开火的自身噪值。
        public void RegisterShot(float selfNoise)
        {
            lastActivity = Time.time;
            AddNoise(selfNoise);
        }
        // 检查角色状态与资源是否满足消耗，不实际扣除。
        public bool CanSpend(ResourceType resource, float amount)
        {
            return CanAct && amount >= 0 && (resource == ResourceType.Noise ? Noise : glands) >= amount;
        }
        // 校验通过后扣除资源；失败保持资源不变。
        public bool TrySpend(ResourceType resource, float amount)
        {
            if (!CanSpend(resource, amount)) return false;
            if (resource == ResourceType.Noise) SetNoise(Noise - amount);
            else { glands -= amount; GlandsChanged?.Invoke(glands, maxGlands); }
            return true;
        }
        // 尝试消耗指定噪值。
        public bool TryConsumeNoise(float amount) => TrySpend(ResourceType.Noise, amount);
        // 尝试消耗指定声腺。
        public bool TryConsumeGland(float amount) => TrySpend(ResourceType.Gland, amount);
        // 补充声腺并限制在上限以内。
        public void AddGland(float amount)
        {
            if (!IsAlive || amount <= 0) return;
            float next = Mathf.Clamp(glands + amount, 0, maxGlands);
            if (next == glands) return;
            glands = next;
            GlandsChanged?.Invoke(glands, maxGlands);
        }
        // 设置临时输出倍率与到期时间。
        public void ApplyDamageBuff(float multiplier, float duration)
        {
            damageMultiplier = Mathf.Max(1, multiplier);
            buffUntil = Time.time + Mathf.Max(0, duration);
        }
        // 开始共鸣吸收，清空上一轮累计量。
        public bool BeginAbsorption()
        {
            if (!CanAct || absorbing) return false;
            absorbing = true;
            absorbedNoise = 0;
            return true;
        }
        // 结束吸收并取出累计噪值，同时清理吸收状态。
        public float EndAbsorption()
        {
            float result = absorbedNoise;
            absorbing = false;
            absorbedNoise = 0;
            return result;
        }
        // 接收一次攻击；返回是否接受本次命中。
        public override bool ReceiveHit(HitInfo hit, bool weakPoint = false)
        {
            if (!CombatUtility.CanHit(hit.source, this) || (hit.groundOnly && airborne)) return false;
            lastActivity = Time.time;
            if (absorbing)
            {
                absorbedNoise += Mathf.Max(0, hit.noise);
                AttackAbsorbed?.Invoke(hit.noise);
                return true;
            }
            return base.ReceiveHit(hit, weakPoint);
        }
        // 重置战斗状态，供重生或测试调用。
        public override void ResetCombat()
        {
            EndAbsorption();
            buffUntil = 0;
            lastActivity = Time.time;
            base.ResetCombat();
            glands = maxGlands;
            GlandsChanged?.Invoke(glands, maxGlands);
        }
    }
}
