// 技能执行器：先检查资源和冷却，再生成效果或进入共鸣阶段。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGJ.Combat
{
    [RequireComponent(typeof(PlayerCombatStats))]
    public sealed class SkillRunner : MonoBehaviour
    {
        // 所属玩家属性：连接玩家根物体上的 PlayerCombatStats。
        [Tooltip("连接玩家根物体上的 PlayerCombatStats。")]
        public PlayerCombatStats owner;
        // 技能瞄准／发射点：正 Z 方向为技能方向，建议连接跟随相机的瞄准点。
        [Tooltip("正 Z 方向为技能方向，建议连接跟随相机的瞄准点。")]
        public Transform castOrigin;
        public event Action<SkillDefinition> CastStarted;
        public event Action<SkillDefinition> CastFinished;
        readonly Dictionary<SkillDefinition, float> cooldowns = new Dictionary<SkillDefinition, float>();
        SkillDefinition resonance;
        float resonanceEndsAt;
        // 初始化组件引用和资源范围。
        void Awake() { if (!owner) owner = GetComponent<PlayerCombatStats>(); }
        // 返回此技能的剩余冷却秒数。
        public float CooldownRemaining(SkillDefinition skill)
        {
            return skill && cooldowns.TryGetValue(skill, out float until) ? Mathf.Max(0, until - Time.time) : 0;
        }
        // 检查资源、冷却及必需预制体，避免失败时扣费。
        public bool CanCast(SkillDefinition skill)
        {
            if (!isActiveAndEnabled || !owner || !skill || !owner.CanSpend(skill.resource, skill.cost) ||
                CooldownRemaining(skill) > 0 || resonance) return false;
            if (skill.effect == SkillEffect.Hitbox || skill.effect == SkillEffect.Resonance) return skill.hitboxPrefab;
            if (skill.effect == SkillEffect.Projectile) return skill.projectilePrefab && skill.projectilePrefab.explosionPrefab;
            return true;
        }
        // 成功时扣费并启动技能效果，失败返回 false。
        public bool TryCast(SkillDefinition skill)
        {
            if (!CanCast(skill)) return false;
            if (skill.effect == SkillEffect.Resonance && !owner.BeginAbsorption()) return false;
            if (!owner.TrySpend(skill.resource, skill.cost))
            {
                if (skill.effect == SkillEffect.Resonance) owner.EndAbsorption();
                return false;
            }
            cooldowns[skill] = Time.time + Mathf.Max(0, skill.cooldown);
            HitInfo hit = MakeHit(skill);
            switch (skill.effect)
            {
                case SkillEffect.Hitbox: SpawnHitbox(skill, hit); break;
                case SkillEffect.Projectile:
                    Instantiate(skill.projectilePrefab, Origin.position, Origin.rotation)
                        .Initialize(hit, Origin.forward * skill.projectileSpeed);
                    break;
                case SkillEffect.DamageBuff: owner.ApplyDamageBuff(skill.damageMultiplier, skill.duration); break;
                case SkillEffect.Resonance:
                    resonance = skill;
                    resonanceEndsAt = Time.time + Mathf.Max(.01f, skill.duration);
                    break;
            }
            CastStarted?.Invoke(skill);
            if (skill.effect != SkillEffect.Resonance) CastFinished?.Invoke(skill);
            return true;
        }
        Transform Origin => castOrigin ? castOrigin : transform;
        // 复制技能命中配置并填入攻击来源和当前增伤。
        HitInfo MakeHit(SkillDefinition skill)
        {
            HitInfo hit = skill.hit;
            hit.source = owner;
            hit.noise *= owner.DamageMultiplier;
            hit.knockback=Mathf.Max(3,hit.knockback);
            hit.pushedTargets=new HashSet<CombatTarget>();
            return hit;
        }
        // 在技能瞄准点创建并初始化命中区域。
        void SpawnHitbox(SkillDefinition skill, HitInfo hit)
        {
            Instantiate(skill.hitboxPrefab, Origin.position, Origin.rotation).Initialize(hit);
        }
        // 逐帧更新计时和状态。
        void Update()
        {
            if (!resonance) return;
            if (!owner || !owner.IsAlive || !owner.IsAbsorbing) { Cancel(); return; }
            if (Time.time < resonanceEndsAt) return;
            var skill = resonance;
            resonance = null;
            float absorbed = owner.EndAbsorption();
            HitInfo hit = MakeHit(skill);
            hit.noise += absorbed * skill.absorbedNoiseMultiplier;
            SpawnHitbox(skill, hit);
            CastFinished?.Invoke(skill);
        }
        // 取消共鸣不会释放反击，也不返还已消耗资源。冷却保留。
        // 取消进行中的共鸣；不返还资源，也不释放反击。
        public void Cancel()
        {
            if (!resonance) return;
            var skill = resonance;
            resonance = null;
            if (owner) owner.EndAbsorption();
            CastFinished?.Invoke(skill);
        }
        // 取消正在执行的技能并清空冷却，供重生或测试调用。
        public void ResetSkills() { Cancel(); cooldowns.Clear(); }
        // 组件禁用时结束技能状态，避免遗留吸收。
        void OnDisable() { Cancel(); }
    }
}
