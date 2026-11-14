using UnityEngine;
using UnityEngine.AI;

namespace SlugWhitebox
{
    public class SlugEnemy : MonoBehaviour
    {
        public SlugGame game;
        public int kind;
        public float hp, maxHp, vulnerableUntil, slowedUntil, staggeredUntil;
        public bool dead;
        public NavMeshAgent agent;
        public Transform body;
        public Renderer shell;
        public float VulnerableLeft => Mathf.Max(0, vulnerableUntil - Time.time);
        public float SlowLeft => Mathf.Max(0, slowedUntil - Time.time);
        public float StaggerLeft => Mathf.Max(0, staggeredUntil - Time.time);
        public Vector3 AimPoint => transform.position + Vector3.up * (kind == 1 ? 1 : .75f);
        float attackAt, pathAt;
        Color baseColor;
        Material skin;

        public void Initialize(SlugGame owner, int type)
        {
            game = owner; kind = type;
            hp = maxHp = game.tuning.enemyHealth[kind];
            gameObject.layer = 8;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = kind == 1 ? .7f : .45f;
            agent.height = 1.8f;
            agent.acceleration = 18; agent.angularSpeed = 540;
            agent.stoppingDistance = kind == 2 ? 8 : 1.65f;
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = Vector3.up * .8f; col.height = 1.6f; col.radius = agent.radius;
            body = new GameObject("敌人几何占位").transform; body.SetParent(transform, false);
            float shade=kind==0?.32f:kind==1?.22f:.43f;
            baseColor = new Color(shade,shade,shade);
            skin = new Material(game.surfaceMaterial); skin.color = baseColor;
            var torso = game.Visual(kind==0?PrimitiveType.Capsule:kind==1?PrimitiveType.Cube:PrimitiveType.Sphere,
                body, new Vector3(0,kind==1?1:.75f,0),kind==0?new Vector3(.85f,.75f,.85f):kind==1?new Vector3(1.4f,2,1.4f):Vector3.one*1.3f, skin);
            shell = torso.GetComponent<Renderer>();
        }

        void Update()
        {
            if (dead || !agent.isOnNavMesh) return;
            if(!game.Running) {agent.isStopped=true;return;}
            bool stagger = StaggerLeft > 0;
            agent.isStopped = stagger;
            agent.speed = game.tuning.enemySpeed[kind] * (SlowLeft > 0 ? .32f : 1);
            if (Time.time > pathAt) { pathAt = Time.time + .2f; agent.SetDestination(game.PlayerPosition); }
            skin.color = VulnerableLeft > 0 ? Color.white : SlowLeft > 0 ? new Color(.65f,.65f,.65f) : baseColor;
            body.localPosition = Vector3.up * Mathf.Sin(Time.time * 10 + GetInstanceID()) * .04f;
            float dist = Vector3.Distance(transform.position, game.PlayerPosition);
            if (!stagger && Time.time > attackAt && dist < (kind == 2 ? 13 : 2.05f) && game.ClearLine(AimPoint, game.PlayerPosition + Vector3.up))
            {
                attackAt = Time.time + (kind == 2 ? 2.2f : 1.25f);
                if (kind == 2) game.SpawnSpit(AimPoint + transform.forward, game.PlayerPosition + Vector3.up);
                else { game.Hurt(game.tuning.enemyDamage * (kind == 1 ? 1.5f : 1)); body.localPosition += Vector3.forward * .25f; }
            }
        }

        public void ApplySkill(int skill)
        {
            if (dead) return;
            float now = Time.time;
            if (skill == 0 || skill == 2) vulnerableUntil = now + game.tuning.vulnerabilityDuration;
            if (skill == 1) slowedUntil = now + game.tuning.slowDuration;
            if (skill == 1 || skill == 2) staggeredUntil = now + (skill == 1 ? .8f : game.tuning.staggerDuration);
        }

        public void Hit(float damage)
        {
            if (dead) return;
            hp -= damage * (VulnerableLeft > 0 ? game.tuning.vulnerabilityMultiplier : 1);
            game.HitFeedback(VulnerableLeft > 0);
            if (hp > 0) return;
            dead = true;
            agent.enabled = false; GetComponent<Collider>().enabled = false;
            game.EnemyKilled(this);
            Destroy(gameObject);
        }
        void OnDestroy() { if (skin) Destroy(skin); }
    }
}
