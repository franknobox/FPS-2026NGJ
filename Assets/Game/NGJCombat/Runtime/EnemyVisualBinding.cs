using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NGJ.Combat
{
    // Lives on the Game logic prefab. ART prefabs remain presentation-only assets.
    [AddComponentMenu("NGJ/美术/敌人外观接入")]
    public sealed class EnemyVisualBinding : MonoBehaviour
    {
        [Tooltip("拖入 ART 的美术预制体；留空时使用白盒外观。")]
        public GameObject visualPrefab;
        public Vector3 localOffset;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        [Tooltip("美术实例内的相对路径，或唯一节点名。现有虫体为 SoundSocket。")]
        public string attackSocket = "SoundSocket";
        public string weakPointSocket = "SoundSocket";
        public Vector3 weakPointOffset;
        [Min(.01f)] public float weakPointRadius = .22f;
        [Min(.1f)] public float collisionHeight = 2;
        [Min(.01f)] public float collisionRadius = .5f;
        [Min(0)] public float deathDespawnDelay = .8f;
        public bool HasVisual => instance;
        [SerializeField, HideInInspector] public Transform preparedVisual;
        Transform instance;
        EnemyCombatant combat;
        Animator animator;
        Transform weakSocket;
        CombatHurtbox weakHurtbox;
        Vector3 previousPosition;
        readonly Dictionary<string, AnimatorControllerParameterType> parameters = new Dictionary<string, AnimatorControllerParameterType>();

        void Start()
        {
            if (!instance && TryGetComponent<EnemyCombatant>(out var owner)) CreateVisual(owner);
        }

        public Transform CreateVisual(EnemyCombatant owner)
        {
            if (!visualPrefab && !preparedVisual) return null;
            if (instance) return instance;
            combat = owner;
            instance = preparedVisual ? preparedVisual : Instantiate(visualPrefab, transform).transform;
            preparedVisual = instance;
            instance.name = "Visual";
            instance.localPosition = localOffset;
            instance.localRotation = Quaternion.Euler(localEulerAngles);
            instance.localScale = localScale;
            // Gameplay collisions belong to the logic root and its weak point.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var motor = owner.GetComponent<CharacterController>();
            motor.radius = Mathf.Max(.01f, collisionRadius);
            motor.height = Mathf.Max(collisionHeight, motor.radius * 2);
            motor.center = Vector3.up * motor.height * .5f;
            var attack = FindSocket(attackSocket);
            if (attack) combat.attackOrigin = attack;
            weakSocket = FindSocket(weakPointSocket);
            weakHurtbox = owner.GetComponentsInChildren<CombatHurtbox>(true).FirstOrDefault(h => h.weakPoint && h.target == owner);
            if (weakSocket && weakHurtbox)
            {
                // Follow the animated socket without inheriting imported bone scales.
                FollowWeakPoint();
                if (weakHurtbox.TryGetComponent<SphereCollider>(out var sphere)) sphere.radius = weakPointRadius;
            }
            animator = instance.GetComponentInChildren<Animator>(true);
            if (animator && animator.runtimeAnimatorController)
                foreach (var parameter in animator.parameters) parameters[parameter.name] = parameter.type;
            combat.WindupStarted += OnWindup;
            combat.AttackStarted += OnAttack;
            combat.Stunned += OnStun;
            combat.Died += OnDeath;
            CombatLayers.SetActorLayer(owner.gameObject, CombatLayers.Enemy);
            previousPosition = transform.position;
            return instance;
        }

        Transform FindSocket(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var exact = instance.Find(path);
            if (exact) return exact;
            var matches = instance.GetComponentsInChildren<Transform>(true).Where(t => t.name == path).ToArray();
            if (matches.Length == 1) return matches[0];
            Debug.LogWarning("美术挂点不存在或名称重复，请填写唯一相对路径：" + path, this);
            return null;
        }

        void LateUpdate()
        {
            FollowWeakPoint();
            float speed = Time.deltaTime > 0 ? (transform.position - previousPosition).magnitude / Time.deltaTime : 0;
            previousPosition = transform.position;
            if (!combat || !animator || !animator.runtimeAnimatorController) return;
            if (parameters.TryGetValue("Speed", out var speedType) && speedType == AnimatorControllerParameterType.Float)
                animator.SetFloat("Speed", combat.IsAlive ? speed : 0);
            Bool("WindingUp", combat.IsWindingUp);
            Bool("Attacking", combat.IsAttacking);
            Bool("Stunned", combat.IsStunned);
            Bool("WeakPointOpen", combat.WeakPointExposed);
            Bool("Airborne", combat.airborne);
            Bool("Dead", !combat.IsAlive);
        }

        void FollowWeakPoint()
        {
            if (!weakSocket || !weakHurtbox) return;
            weakHurtbox.transform.SetPositionAndRotation(weakSocket.position + instance.rotation * weakPointOffset, weakSocket.rotation);
        }

        void Bool(string name, bool value)
        {
            if (parameters.TryGetValue(name, out var type) && type == AnimatorControllerParameterType.Bool) animator.SetBool(name, value);
        }
        void Trigger(string name)
        {
            if (animator && parameters.TryGetValue(name, out var type) && type == AnimatorControllerParameterType.Trigger) animator.SetTrigger(name);
        }
        void OnWindup() { Trigger("Windup"); }
        void OnAttack() { Trigger("Attack"); }
        void OnStun(float duration) { Trigger("Stun"); }
        void OnDeath() { Trigger("Die"); }
        void OnDestroy()
        {
            if (!combat) return;
            combat.WindupStarted -= OnWindup;
            combat.AttackStarted -= OnAttack;
            combat.Stunned -= OnStun;
            combat.Died -= OnDeath;
        }
    }
}
