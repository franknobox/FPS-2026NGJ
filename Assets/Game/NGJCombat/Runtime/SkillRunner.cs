// 技能执行器：先检查资源和冷却，再生成效果或进入共鸣阶段。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using System.Collections.Generic;
using System.Collections;
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
        GameObject activeVisual;
        float visualEndsAt;
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
            CombatFx.Spawn(skill.castEffect, Origin.position + Origin.forward * .65f, Origin.rotation, Origin);
            if (skill.activeEffect)
            {
                ClearVisual();
                activeVisual = Instantiate(skill.activeEffect, Origin.position, Origin.rotation, Origin);
                foreach(var particles in activeVisual.GetComponentsInChildren<ParticleSystem>()) particles.Play(false);
                visualEndsAt = Time.time + Mathf.Max(.01f,skill.duration);
            }
            Execute(skill,hit,Origin.position,Origin.rotation);
            if(skill.extraTriggers>0)StartCoroutine(Echo(skill,hit,Origin.position,Origin.rotation));
            CastStarted?.Invoke(skill);
            if (skill.effect != SkillEffect.Resonance) CastFinished?.Invoke(skill);
            return true;
        }
        IEnumerator Echo(SkillDefinition skill,HitInfo hit,Vector3 position,Quaternion rotation)
        {
            for(int i=0;i<skill.extraTriggers;i++){yield return new WaitForSeconds(.12f);if(!owner||!owner.IsAlive)yield break;Execute(skill,hit,position,rotation);}
        }
        void Execute(SkillDefinition skill,HitInfo hit,Vector3 position,Quaternion rotation)
        {
            switch (skill.effect)
            {
                case SkillEffect.Hitbox: SpawnHitbox(skill, hit,position,rotation); break;
                case SkillEffect.Projectile:
                    Instantiate(skill.projectilePrefab,position,rotation).Initialize(hit,rotation*Vector3.forward*skill.projectileSpeed);
                    break;
                case SkillEffect.DamageBuff: owner.ApplyDamageBuff(skill.damageMultiplier,skill.duration,skill.sourceWeapon,
                    !skill.sourceWeapon || (skill.sourceWeapon.evolution&NGJ.Progression.EvolutionEffect.SharedBuff)!=0);break;
                case SkillEffect.Resonance:
                    resonance = skill;
                    resonanceEndsAt = Time.time + Mathf.Max(.01f, skill.duration);
                    break;
            }
        }
        Transform Origin => castOrigin ? castOrigin : transform;
        // 复制技能命中配置并填入攻击来源和当前增伤。
        HitInfo MakeHit(SkillDefinition skill)
        {
            HitInfo hit = skill.hit;
            hit.source = owner;
            hit.noise *= owner.DamageMultiplierFor(skill.sourceWeapon);
            hit.knockback=Mathf.Max(3,hit.knockback);
            hit.pushedTargets=new HashSet<CombatTarget>();
            if(skill.sourceWeapon && TryGetComponent<CombatWeaponController>(out var weapons))hit=weapons.DecorateHit(hit,skill.sourceWeapon);
            return hit;
        }
        // 在技能瞄准点创建并初始化命中区域。
        void SpawnHitbox(SkillDefinition skill, HitInfo hit)
        {
            SpawnHitbox(skill,hit,Origin.position,Origin.rotation);
        }
        void SpawnHitbox(SkillDefinition skill,HitInfo hit,Vector3 position,Quaternion rotation)
        {
            var box=Instantiate(skill.hitboxPrefab,position,rotation);box.radius*=skill.rangeMultiplier;box.halfExtents*=skill.rangeMultiplier;box.Initialize(hit);
        }
        // 逐帧更新计时和状态。
        void Update()
        {
            if (activeVisual && (Time.time >= visualEndsAt || !owner || !owner.IsAlive)) ClearVisual();
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
            StopAllCoroutines();
            ClearVisual();
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
        void ClearVisual() { if (activeVisual) Destroy(activeVisual); activeVisual = null; }
    }
}
