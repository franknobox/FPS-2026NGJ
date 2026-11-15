using NGJ.Combat;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
namespace SlugWhitebox
{
    // 场景适配器：地面寻路交给 NavMesh，攻击和控制统一使用新战斗模块。
    public class SlugEnemy : MonoBehaviour
    {
        public SlugGame game;
        public EnemyCombatant combat;
        public int kind;
        public bool dead;
        public NavMeshAgent agent;
        public Transform body;
        public Renderer shell;
        public float hp => combat ? combat.maxNoise-combat.Noise : 0;
        public float maxHp => combat ? combat.maxNoise : 0;
        public float VulnerableLeft => combat && combat.IsVulnerable ? 1 : 0;
        public float SlowLeft => 0;
        public float StaggerLeft => combat && combat.IsStunned ? 1 : 0;
        public Vector3 AimPoint => aimPoint ? aimPoint.position : combat && combat.attackOrigin ? combat.attackOrigin.position : transform.position+Vector3.up;
        Material skin,weakSkin;
        float pathAt;
        Color baseColor;
        bool initialized;
        Transform aimPoint;
        public string DisplayName => combat ? combat.DisplayName : "敌人";
        IEnumerator Start()
        {
            if(initialized)yield break;
            var owner=FindFirstObjectByType<SlugGame>();
            if(!owner){enabled=false;yield break;}
            while(!owner.HasStarted)yield return null;
            if(initialized)yield break;
            var actor=GetComponent<EnemyCombatant>();
            Initialize(owner,(int)actor.definition.kind);
            if(!owner.enemies.Contains(this))owner.enemies.Add(this);
        }
        public void Initialize(SlugGame owner,int type)
        {
            if(initialized)return;
            initialized=true;
            game=owner;kind=type;combat=GetComponent<EnemyCombatant>();
            var visual=GetComponent<EnemyVisualBinding>();
            if(visual)body=visual.CreateVisual(combat);
            var weak=System.Array.Find(GetComponentsInChildren<CombatHurtbox>(true),h=>h.weakPoint);
            aimPoint=weak?weak.transform:null;
            CombatLayers.SetActorLayer(gameObject,CombatLayers.Enemy);
            combat.attackTarget=game.stats;combat.findPlayerAutomatically=false;combat.attackMask=CombatLayers.AttackMask;
            if(!combat.definition.flying)
            {
                combat.externalGroundMovement=true;
                var motor=GetComponent<CharacterController>();motor.enabled=false;
                var capsule=gameObject.AddComponent<CapsuleCollider>();capsule.center=motor.center;capsule.height=motor.height;capsule.radius=motor.radius;
                agent=gameObject.AddComponent<NavMeshAgent>();agent.radius=motor.radius;agent.height=motor.height;
                agent.speed=combat.definition.moveSpeed;agent.acceleration=18;agent.angularSpeed=360;
                agent.stoppingDistance=combat.definition.attackRange*.8f;agent.updateRotation=false;
            }
            if(!body)
            {
            body=new GameObject("虫体白盒外观").transform;body.SetParent(transform,false);
            baseColor=kind==0?new Color(.15f,.55f,.65f):kind==1?new Color(.75f,.45f,.18f):new Color(.42f,.22f,.55f);
            skin=new Material(game.surfaceMaterial);skin.color=baseColor;
            shell=game.Visual(kind==0?PrimitiveType.Sphere:kind==1?PrimitiveType.Capsule:PrimitiveType.Cube,body,Vector3.up,
                kind==0?new Vector3(.85f,.65f,.85f):kind==1?new Vector3(.85f,.85f,.85f):new Vector3(1.25f,1.8f,1.1f),skin).GetComponent<Renderer>();
            if(kind==0)for(int side=-1;side<=1;side+=2)game.Visual(PrimitiveType.Cube,body,new Vector3(side*.65f,1,0),new Vector3(.8f,.08f,.5f),skin);
            weakSkin=new Material(game.surfaceMaterial);weakSkin.color=Color.gray;
            game.Visual(PrimitiveType.Sphere,body,new Vector3(0,1.2f,.56f),Vector3.one*.4f,weakSkin);
            }
            combat.ShotFired += (a,b) => game.Trail(a,b,kind==2?Color.magenta:Color.yellow,kind==2?.2f:.09f);
            combat.HitReceived += hit => game.HitFeedback(combat.WeakPointExposed);
            combat.Died += () => {
                dead=true;if(agent)agent.enabled=false;
                foreach(var col in GetComponentsInChildren<Collider>())col.enabled=false;
                game.EnemyKilled(this);Destroy(gameObject,visual && visual.HasVisual ? visual.deathDespawnDelay : 0);
            };
        }
        void Update()
        {
            if(dead || !combat || !game)return;
            combat.enabled=game.Running;
            if(skin)skin.color=combat.IsStunned?Color.cyan:combat.IsVulnerable?Color.white:baseColor;
            if(weakSkin)weakSkin.color=combat.WeakPointExposed?Color.yellow:Color.gray;
            if(!agent || !agent.isOnNavMesh)return;
            agent.isStopped=!game.Running || combat.IsStunned || combat.IsAttacking || combat.IsWindingUp;
            if(!agent.isStopped && Time.time>pathAt)
            {
                pathAt=Time.time+.25f;
                var destination=game.PlayerPosition;
                if(combat.hasMovementBounds)
                {
                    destination.x=Mathf.Clamp(destination.x,combat.movementBounds.min.x+agent.radius,combat.movementBounds.max.x-agent.radius);
                    destination.z=Mathf.Clamp(destination.z,combat.movementBounds.min.z+agent.radius,combat.movementBounds.max.z-agent.radius);
                }
                agent.SetDestination(destination);
            }
        }
        // 外部交互也走真实受击接口，不直接改血量。
        public void Hit(float damage) => combat.ReceiveHit(new HitInfo{source=game.stats,noise=damage,penetration=ArmorGrade.High});
        public void ApplySkill(int skill) => combat.ReceiveHit(new HitInfo{source=game.stats,stunDuration=2,penetration=ArmorGrade.High});
        void OnDestroy(){if(skin)Destroy(skin);if(weakSkin)Destroy(weakSkin);}
    }
}
