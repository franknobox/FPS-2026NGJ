using System;
using UnityEngine;

namespace NGJ.Combat
{
    // 可独立运行的基础敌人：碰撞移动、追击、前摇、攻击、眩晕和飞行坠落。
    // 不处理动画、美术或复杂寻路；事件可交给表现系统。不要再挂第二个 CombatTarget。
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyCombatant : CombatTarget
    {
        [Tooltip("类型和数值来源；修改后在停止 Play 时重新运行，或使用应用配置菜单。")] public EnemyDefinition definition;
        [Tooltip("攻击目标；可手动指定玩家受击组件。")] public CombatTarget attackTarget;
        [Tooltip("没有目标时每秒尝试寻找 PlayerCombatStats。")] public bool findPlayerAutomatically = true;
        [Tooltip("射线起点；不填使用根物体上方一米。")] public Transform attackOrigin;
        [Tooltip("包含环境和角色的物理层；射线会跳过自身碰撞体。")] public LayerMask attackMask = ~0;
        [Min(.1f), Tooltip("下落重力，米/秒平方。")] public float fallGravity = 20;

        [Tooltip("地面移动交由场景 NavMeshAgent，攻击和控制仍由本组件处理。")] public bool externalGroundMovement;
        public bool IsAttacking { get; private set; }
        public bool IsWindingUp { get; private set; }
        public bool CanAct => IsAlive && !IsStunned;
        // Player-facing names always come from the design definition, never mesh filenames.
        public string DisplayName => definition ? definition.displayName : "敌人";
        public event Action AttackStarted;
        public event Action WindupStarted;
        public event Action AttackStopped;
        public event Action<Vector3, Vector3> ShotFired; // 每次检测后的射线起终点，可用于画线和音效。
        CharacterController motor;
        float nextAttackAt, windupEndsAt, beamEndsAt, nextTickAt, nextSearchAt, verticalSpeed;

        protected override void Awake()
        {
            motor = GetComponent<CharacterController>();
            ApplyDefinition();
            base.Awake();
            Stunned += OnStunned;
            Died += StopAttack;
        }

        // 在编辑器中也能通过组件右键菜单同步类型配置；不重置当前噪值。
        [ContextMenu("应用敌人配置")]
        public void ApplyDefinition()
        {
            if (!definition) return;
            maxNoise = Mathf.Max(1, definition.maxNoise);
            armor = definition.armor;
            weakPointArmor = definition.weakPointArmor;
            exposeWeakPointWhenStunned = definition.exposeDuringStun;
            airborne = definition.flying;
        }

        void Update()
        {
            if (!definition || !IsAlive) return;
            // 眩晕不能追击或攻击；飞行单位持续向下落，接触地面后才接受地面技能。
            bool falling = definition.flying && definition.fallsWhenStunned && IsStunned;
            airborne = definition.flying && (!falling || !motor.isGrounded);
            if (!CanAct) { Move(Vector3.zero, falling); return; }
            if ((!attackTarget || !attackTarget.IsAlive) && findPlayerAutomatically && Time.time >= nextSearchAt)
            {
                nextSearchAt = Time.time + 1;
                attackTarget = FindFirstObjectByType<PlayerCombatStats>();
            }
            if (!attackTarget || !CombatUtility.CanHit(this, attackTarget) ||
                Vector3.Distance(transform.position, attackTarget.transform.position) > definition.detectionRange)
            { StopAttack(); Move(Vector3.zero, false); return; }

            Vector3 delta = attackTarget.transform.position - transform.position;
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (horizontal.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(horizontal);
            bool inRange = delta.magnitude <= definition.attackRange;
            if ((IsAttacking || IsWindingUp) && !inRange) StopAttack();
            if (!IsAttacking && !IsWindingUp && inRange && Time.time >= nextAttackAt)
            { BeginWindup(); }
            if (IsWindingUp && Time.time >= windupEndsAt) BeginAttack();
            if (IsAttacking)
            {
                if (Time.time >= beamEndsAt) StopAttack();
                else if (definition.attackMode == EnemyAttackMode.ContinuousBeam && Time.time >= nextTickAt)
                { FireRay(); nextTickAt = Time.time + Mathf.Max(.02f, definition.beamTickInterval); }
            }
            Vector3 movement = !inRange && !IsAttacking && !IsWindingUp ? horizontal.normalized * definition.moveSpeed : Vector3.zero;
            if (definition.flying)
                movement.y = Mathf.Clamp(attackTarget.transform.position.y + definition.flightHeight - transform.position.y,
                    -definition.moveSpeed, definition.moveSpeed);
            Move(movement, false);
        }

        void Move(Vector3 velocity, bool falling)
        {
            if (externalGroundMovement && !definition.flying) return;
            if (!motor || !motor.enabled) return;
            if (!definition.flying || falling)
            {
                if (motor.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
                verticalSpeed -= Mathf.Max(.1f, fallGravity) * Time.deltaTime;
                velocity.y = verticalSpeed;
            }
            else verticalSpeed = 0;
            motor.Move(velocity * Time.deltaTime);
            if (falling) airborne = !motor.isGrounded;
        }

        void BeginWindup()
        {
            IsWindingUp = true;
            windupEndsAt = Time.time + Mathf.Max(0, definition.windup);
            weakPointOpen = definition.exposeDuringWindup;
            WindupStarted?.Invoke();
        }

        void BeginAttack()
        {
            IsWindingUp = false;
            IsAttacking = true;
            weakPointOpen = definition.exposeDuringAttack;
            beamEndsAt = Time.time + Mathf.Max(.02f, definition.attackMode == EnemyAttackMode.ContinuousBeam ? definition.beamDuration : definition.attackDuration);
            nextTickAt = Time.time + Mathf.Max(.02f, definition.beamTickInterval);
            AttackStarted?.Invoke();
            // 表现回调可能导致死亡或眩晕，所以结算前再次检查状态。
            if (!CanAct || !IsAttacking) { StopAttack(); return; }
            FireRay();
        }

        // 射线被第一个非自身碰撞体阻挡；敌人攻击复用 ReceiveHit，因此可被共鸣吸收。
        void FireRay()
        {
            if (!attackTarget || !attackTarget.IsAlive || !CanAct) return;
            Vector3 start = attackOrigin ? attackOrigin.position : transform.position + Vector3.up;
            var targetCollider = attackTarget.GetComponent<Collider>();
            Vector3 aim = targetCollider ? targetCollider.bounds.center : attackTarget.transform.position + Vector3.up;
            Vector3 direction = (aim - start).normalized;
            Vector3 end = start + direction * definition.attackRange;
            var hits = Physics.RaycastAll(start, direction, definition.attackRange, attackMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var collision in hits)
            {
                var victim = CombatUtility.Resolve(collision.collider, out bool weak);
                if (victim == this) continue;
                end = collision.point;
                if (CombatUtility.CanHit(this, victim))
                    victim.ReceiveHit(new HitInfo { source = this, noise = definition.attackNoise,
                        penetration = definition.attackPenetration, point = end, direction = direction }, weak);
                break;
            }
            ShotFired?.Invoke(start, end);
        }

        void OnStunned(float seconds) { StopAttack(); }
        void StopAttack()
        {
            bool wasActive = IsAttacking || IsWindingUp;
            IsAttacking = IsWindingUp = false;
            weakPointOpen = false;
            if (wasActive)
            {
                nextAttackAt = Time.time + (definition ? Mathf.Max(.01f, definition.attackInterval) : 1);
                AttackStopped?.Invoke();
            }
        }
        void OnDisable() { StopAttack(); }
        public override void ResetCombat()
        {
            StopAttack();
            ApplyDefinition();
            base.ResetCombat();
            verticalSpeed = 0;
            nextAttackAt = 0;
        }
    }
}
