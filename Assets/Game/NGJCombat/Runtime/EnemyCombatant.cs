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
        public bool HasLineOfSight { get; private set; }
        public Vector3 LastShotEnd { get; private set; }
        public EnemyAttackCoordinator attackCoordinator;
        public bool IsKnockedBack => Time.time<knockbackUntil;
        Vector3 knockbackVelocity;
        float knockbackUntil;
        public void DelayFirstAttack(float delay){nextAttackAt=Time.time+delay;}
        public Vector3 KnockbackStep(float dt)
        {
            if(!IsKnockedBack)return Vector3.zero;
            var delta=knockbackVelocity*dt;knockbackVelocity=Vector3.MoveTowards(knockbackVelocity,Vector3.zero,28*dt);return delta;
        }
        public override bool ReceiveHit(HitInfo hit,bool weakPoint=false)
        {
            bool received=base.ReceiveHit(hit,weakPoint);
            if(received&&IsAlive&&hit.noise>0&&hit.knockback>0&&(hit.pushedTargets==null||hit.pushedTargets.Add(this)))
            {
                var direction=Vector3.ProjectOnPlane(hit.direction,Vector3.up);
                if(direction.sqrMagnitude<.001f&&hit.source)direction=Vector3.ProjectOnPlane(transform.position-hit.source.transform.position,Vector3.up);
                float mass=definition&&definition.kind==EnemyKind.ZhenYin?.35f:1;
                knockbackVelocity=direction.normalized*hit.knockback*mass;knockbackUntil=Time.time+.22f;
            }
            return received;
        }
        public Vector3 AimPosition => attackTarget && attackTarget.TryGetComponent<Collider>(out var col) ? col.bounds.center : attackTarget ? attackTarget.transform.position + Vector3.up : transform.position;
        [Tooltip("正式关卡的活动范围；白盒及通用预制体保持关闭。")] public bool hasMovementBounds;
        public Bounds movementBounds;
        // Player-facing names always come from the design definition, never mesh filenames.
        public string DisplayName => definition ? definition.displayName : "敌人";
        public event Action AttackStarted;
        public event Action WindupStarted;
        public event Action AttackStopped;
        public event Action<Vector3, Vector3> ShotFired; // 每次检测后的射线起终点，可用于画线和音效。
        CharacterController motor;
        float nextAttackAt, windupEndsAt, beamEndsAt, nextTickAt, nextSearchAt, verticalSpeed;
        static readonly float[] AvoidanceAngles = { 60f, -60f, 100f, -100f };
        float nextSightAt, avoidUntil, climbUntil;
        Vector3 avoidDirection;

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
            if(Time.time >= nextSightAt)
            {
                nextSightAt=Time.time+.12f;
                HasLineOfSight=CombatUtility.Visible(attackOrigin ? attackOrigin.position : transform.position+Vector3.up,AimPosition,CombatLayers.WorldMask);
            }
            bool inRange = delta.magnitude <= definition.attackRange && HasLineOfSight;
            if ((IsAttacking || IsWindingUp) && !inRange) StopAttack();
            if (!IsAttacking && !IsWindingUp && inRange && Time.time >= nextAttackAt)
            { if(!attackCoordinator||attackCoordinator.TryAcquire(this))BeginWindup(); }
            if (IsWindingUp && Time.time >= windupEndsAt) BeginAttack();
            if (IsAttacking)
            {
                if (Time.time >= beamEndsAt) StopAttack();
                else if (definition.attackMode == EnemyAttackMode.ContinuousBeam && Time.time >= nextTickAt)
                { FireRay(); nextTickAt = Time.time + Mathf.Max(.02f, definition.beamTickInterval); }
            }
            Vector3 movement = !inRange && !IsAttacking && !IsWindingUp ? horizontal.normalized * definition.moveSpeed : Vector3.zero;
            if (definition.flying)
            {
                if(Time.time < avoidUntil && movement.sqrMagnitude > .01f)movement=avoidDirection*definition.moveSpeed;
                if (hasMovementBounds && movement.sqrMagnitude > .01f && Physics.SphereCast(transform.position + Vector3.up * motor.height * .5f,
                    motor.radius * .8f, movement.normalized, out _, 2, CombatLayers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 avoided = Vector3.zero;
                    foreach (float angle in AvoidanceAngles)
                    {
                        var direction = Quaternion.Euler(0, angle, 0) * horizontal.normalized;
                        if (!Physics.SphereCast(transform.position + Vector3.up * motor.height * .5f, motor.radius * .8f, direction,
                            out _, 2, CombatLayers.WorldMask, QueryTriggerInteraction.Ignore)) { avoided = direction * definition.moveSpeed; avoidDirection=direction;avoidUntil=Time.time+.65f;break; }
                    }
                    movement = avoided;
                    if(avoided.sqrMagnitude<.01f)climbUntil=Time.time+1.5f;
                }
                movement.y = Mathf.Clamp(attackTarget.transform.position.y + definition.flightHeight + (Time.time<climbUntil?4:0) - transform.position.y,
                    -definition.moveSpeed, definition.moveSpeed);
            }
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
            Vector3 next = transform.position + velocity * Time.deltaTime;
            next+=KnockbackStep(Time.deltaTime);
            if (hasMovementBounds)
            {
                next.x = Mathf.Clamp(next.x, movementBounds.min.x + motor.radius, movementBounds.max.x - motor.radius);
                next.z = Mathf.Clamp(next.z, movementBounds.min.z + motor.radius, movementBounds.max.z - motor.radius);
            }
            motor.Move(next - transform.position);
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
            Vector3 aim = AimPosition;
            Vector3 direction = (aim - start).normalized;
            if(definition.attackMode==EnemyAttackMode.Intermittent && definition.projectilePrefab)
            {
                var bolt=Instantiate(definition.projectilePrefab,start,Quaternion.LookRotation(direction));
                bolt.Initialize(new HitInfo{source=this,noise=definition.attackNoise,penetration=definition.attackPenetration},direction*definition.projectileSpeed);
                ShotFired?.Invoke(start,start+direction*.3f);return;
            }
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
            LastShotEnd=end;
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
                nextAttackAt = Time.time + (definition ? Mathf.Max(.01f, definition.attackInterval) : 1)+UnityEngine.Random.Range(.2f,.9f);
                if(attackCoordinator)attackCoordinator.Release(this);
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
            nextSightAt=avoidUntil=climbUntil=0;HasLineOfSight=false;
            knockbackUntil=0;knockbackVelocity=Vector3.zero;
        }
    }
}
