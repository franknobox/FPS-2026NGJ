// 武器执行器：外部输入调用 TryFire / TryWeaponSkill，不直接绑定按键。
// 配置可在 Unity Inspector 修改；米、秒为默认距离和时间单位。
using System;
using System.Collections.Generic;
using System.Collections;
using NGJ.Progression;
using UnityEngine;

namespace NGJ.Combat
{
    [RequireComponent(typeof(PlayerCombatStats), typeof(SkillRunner))]
    public sealed class CombatWeaponController : MonoBehaviour
    {
        // 所属玩家属性：连接玩家根物体上的 PlayerCombatStats。
        [Tooltip("连接玩家根物体上的 PlayerCombatStats。")]
        public PlayerCombatStats owner;
        // 技能执行组件：连接同一玩家的 SkillRunner。
        [Tooltip("连接同一玩家的 SkillRunner。")]
        public SkillRunner skills;
        // 普攻瞄准／发射点：正 Z 方向为开火方向。
        [Tooltip("正 Z 方向为开火方向。")]
        public Transform fireOrigin;
        [Tooltip("实际弹体出生点；瞄准仍由 fireOrigin 决定。")]
        public Transform muzzle;
        // 当前装备武器：可直接拖入武器配置测试。
        [Tooltip("可直接拖入武器配置测试。")]
        public WeaponDefinition equipped;
        // 普攻射线检测层：同时包含环境和目标层。
        [Tooltip("同时包含环境和目标层。")]
        public LayerMask hitMask = ~0;
        public event Action<WeaponDefinition> Fired;
        public event Action<WeaponDefinition> Equipped;
        readonly Dictionary<WeaponDefinition, float> nextShots = new Dictionary<WeaponDefinition, float>();
        readonly Dictionary<WeaponDefinition,int> nextShotStacks=new Dictionary<WeaponDefinition,int>();
        bool bursting;
        public bool RequiresPress=>equipped && (equipped.evolution&EvolutionEffect.Burst)!=0;
        // 重开关卡时清理各武器射击冷却。
        public void ResetWeapons() { StopAllCoroutines();bursting=false;nextShots.Clear();nextShotStacks.Clear(); }
        // 初始化组件引用和资源范围。
        void Awake()
        {
            if (!owner) owner = GetComponent<PlayerCombatStats>();
            if (!skills) skills = GetComponent<SkillRunner>();
        }
        // 切换当前武器，通知界面或装备表现。
        public void Equip(WeaponDefinition weapon)
        {
            equipped = weapon;
            Equipped?.Invoke(weapon);
        }
        // 尝试释放当前武器关联的技能。
        public bool TryWeaponSkill() => equipped && skills && skills.TryCast(equipped.skill);
        // 空放不增加噪值；一次普攻首次有效命中后结算一次自身噪值。
        public bool TryFire()
        {
            if (bursting || !isActiveAndEnabled || !equipped || !owner || !owner.CanAct ||
                (nextShots.TryGetValue(equipped, out float next) && Time.time < next)) return false;
            if ((equipped.type == WeaponType.BaoMingGrenade || equipped.type == WeaponType.RocketLauncher) &&
                (!equipped.projectilePrefab || !equipped.projectilePrefab.explosionPrefab)) return false;
            Transform origin = fireOrigin ? fireOrigin : transform;
            Vector3 spawnPosition = muzzle ? muzzle.position : origin.position;
            if (equipped.projectilePrefab && Physics.Linecast(origin.position, spawnPosition, CombatLayers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            if(RequiresPress)
            {
                var weapon=equipped;nextShots[weapon]=Time.time+Mathf.Max(.03f,weapon.fireInterval*1.5f);
                bursting=true;StartCoroutine(Burst(weapon));return true;
            }
            nextShots[equipped] = Time.time + Mathf.Max(.01f, equipped.fireInterval);
            FireOne(equipped);return true;
        }
        IEnumerator Burst(WeaponDefinition weapon)
        {
            for(int i=0;i<3;i++)
            {
                if(!owner || !owner.CanAct || equipped!=weapon)break;
                FireOne(weapon);if(i<2)yield return new WaitForSeconds(Mathf.Max(.01f,weapon.fireInterval*.5f));
            }
            bursting=false;
        }
        void FireOne(WeaponDefinition weapon)
        {
            Transform origin=fireOrigin?fireOrigin:transform;
            Vector3 spawnPosition=muzzle?muzzle.position:origin.position;
            float damage=weapon.targetNoise*owner.DamageMultiplierFor(weapon);
            if((weapon.evolution&EvolutionEffect.SharpNoise)!=0)damage+=owner.Noise*.25f;
            if(nextShotStacks.TryGetValue(weapon,out int stacks)){damage*=1+stacks*.1f;nextShotStacks[weapon]=0;}
            HitInfo hit=DecorateHit(new HitInfo{source=owner,noise=damage,penetration=weapon.penetration,knockback=weapon.knockback,pushedTargets=new HashSet<CombatTarget>()},weapon);
            CombatFx.Spawn(weapon.muzzleEffect, spawnPosition, muzzle ? muzzle.rotation : origin.rotation, muzzle);
            bool charged = false;
            float selfNoise = weapon.selfNoise;
            var shooter = owner;
            hit.hitConfirmed = () =>
            {
                if (charged) return;
                charged = true;
                if (shooter) shooter.RegisterShot(selfNoise);
            };
            owner.RegisterShot(0);
            if (weapon.projectilePrefab)
            {
                Vector3 aim = origin.position + origin.forward * weapon.range;
                if (Physics.Raycast(origin.position, origin.forward, out var contact, weapon.range, hitMask, QueryTriggerInteraction.Ignore)) aim = contact.point;
                Vector3 direction = (aim - spawnPosition).normalized;
                var projectile=Instantiate(weapon.projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
                if((weapon.evolution&EvolutionEffect.HeavyWarhead)!=0)projectile.gravity=12;
                projectile.Initialize(hit, direction * weapon.projectileSpeed);
            }
            else
                for (int pellet = 0; pellet < Mathf.Max(1, equipped.pelletCount); pellet++)
                {
                    Vector2 spread = equipped.type==WeaponType.HanYinShotgun&&pellet==0?Vector2.zero:UnityEngine.Random.insideUnitCircle * Mathf.Tan(equipped.spreadAngle * Mathf.Deg2Rad);
                    Vector3 direction = (origin.forward + origin.right * spread.x + origin.up * spread.y).normalized;
                    FireRay(origin.position, direction, hit);
                }
            Fired?.Invoke(weapon);
        }
        public HitInfo DecorateHit(HitInfo hit,WeaponDefinition weapon)
        {
            if(!weapon)return hit;
            var effects=weapon.evolution;var seen=new HashSet<CombatTarget>();
            if((effects&EvolutionEffect.StunAmplify)!=0)hit.stunnedMultiplier=1.5f;
            if((effects&EvolutionEffect.DirectWarhead)!=0)hit.directMultiplier=1.5f;
            float radius=weapon.wailRadius;
            hit.resolved=(target,amount,killed)=>
            {
                if(amount<=0 || !seen.Add(target))return;
                if((effects&EvolutionEffect.BioAmplify)!=0){nextShotStacks.TryGetValue(weapon,out int n);nextShotStacks[weapon]=n+1;}
                if(killed && (effects&EvolutionEffect.Wail)!=0)Wail(target,amount*.4f,radius,weapon.penetration);
            };
            return hit;
        }
        void Wail(CombatTarget victim,float noise,float radius,ArmorGrade penetration)
        {
            if(!victim || !owner)return;
            Vector3 center=victim.transform.position+Vector3.up*.5f;
            var seen=new HashSet<CombatTarget>();
            foreach(var collider in Physics.OverlapSphere(center,radius,CombatLayers.PlayerAttackMask,QueryTriggerInteraction.Collide))
            {
                var target=CombatUtility.Resolve(collider,out bool weak);
                if(!CombatUtility.CanHit(owner,target)||target==victim||!seen.Add(target))continue;
                Vector3 point=collider.ClosestPoint(center);
                if(!CombatUtility.Visible(center,point,CombatLayers.WorldMask))continue;
                target.ReceiveHit(new HitInfo{source=owner,noise=noise,penetration=penetration,point=point,direction=(point-center).normalized},weak);
            }
        }
        // 按距离处理射线命中；墙体阻挡，按配置穿透多个目标。
        void FireRay(Vector3 origin, Vector3 direction, HitInfo hit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, equipped.range, hitMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            var visited = new HashSet<CombatTarget>();
            int count = 0;
            Vector3 endpoint = origin + direction * equipped.range;
            foreach (var contact in hits)
            {
                CombatTarget target = CombatUtility.Resolve(contact.collider, out bool weakPoint);
                // 非受击目标的实体碰撞体视为墙体；普通触发器不挡子弹。
                if (!target)
                {
                    if (contact.collider.isTrigger) continue;
                    endpoint = contact.point;
                    CombatFx.Spawn(equipped.impactEffect, contact.point + contact.normal * .01f, Quaternion.LookRotation(contact.normal));
                    break;
                }
                if (!CombatUtility.CanHit(owner, target) || !visited.Add(target)) continue;
                hit.point = contact.point;
                hit.direction = direction;
                if (target.ReceiveHit(hit, weakPoint))
                {
                    hit.hitConfirmed?.Invoke();
                    CombatFx.Spawn(equipped.impactEffect, contact.point, Quaternion.LookRotation(contact.normal));
                    if (++count >= Mathf.Max(1, equipped.maxTargetsPerPellet)) { endpoint = contact.point; break; }
                }
            }
            CombatFx.Trail(equipped.bulletTrail, muzzle ? muzzle.position : origin, endpoint);
        }
    }
}
