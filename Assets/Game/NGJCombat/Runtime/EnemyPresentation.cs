using UnityEngine;
namespace NGJ.Combat
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(EnemyCombatant))]
    public sealed class EnemyPresentation : MonoBehaviour
    {
        public Material soundMaterial;
        public Color color=Color.yellow;
        public AudioClip windupSound,shotSound;
        EnemyCombatant combat;
        LineRenderer beam,core;
        AudioSource audioSource;
        void Awake()
        {
            combat=GetComponent<EnemyCombatant>();
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=1;audioSource.minDistance=3;audioSource.maxDistance=32;audioSource.volume=.55f;
            beam=Line("声波瞄准与持续射线",2);core=Line("声器官状态",24);core.loop=true;
            combat.WindupStarted+=Windup;combat.ShotFired+=Shot;
        }
        LineRenderer Line(string name,int points)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.layer=2;
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=soundMaterial;line.positionCount=points;line.enabled=false;line.numCapVertices=3;
            return line;
        }
        void Windup(){if(windupSound)audioSource.PlayOneShot(windupSound,.55f);}
        void Shot(Vector3 start,Vector3 end)
        {
            if(shotSound)audioSource.PlayOneShot(shotSound,combat.definition.attackMode==EnemyAttackMode.ContinuousBeam?.35f:.65f);
        }
        void LateUpdate()
        {
            bool active=combat.enabled&&combat.IsAlive;
            var origin=combat.attackOrigin?combat.attackOrigin:transform;
            bool warning=active&&combat.IsWindingUp;
            bool shooting=active&&combat.IsAttacking&&combat.definition.attackMode==EnemyAttackMode.ContinuousBeam;
            beam.enabled=warning||shooting;
            if(beam.enabled)
            {
                Vector3 start=origin.position,end=shooting?combat.LastShotEnd:combat.AimPosition;
                if(warning&&Physics.Linecast(start,end,out var hit,CombatLayers.WorldMask,QueryTriggerInteraction.Ignore))end=hit.point;
                beam.SetPosition(0,start);beam.SetPosition(1,end);
                beam.startWidth=beam.endWidth=warning?.018f:.16f;
                beam.startColor=beam.endColor=warning?new Color(1,.45f,.06f,.8f):color;
            }
            core.enabled=active&&(warning||shooting||combat.WeakPointExposed||combat.IsStunned);
            if(core.enabled)
            {
                core.startColor=core.endColor=combat.IsStunned?Color.cyan:warning?new Color(1,.5f,.05f):color;
                core.startWidth=core.endWidth=.035f;
                float radius=.22f+Mathf.Sin(Time.time*14)*.035f;
                for(int i=0;i<24;i++){float a=i*Mathf.PI*2/24;core.SetPosition(i,origin.position+(origin.right*Mathf.Cos(a)+origin.up*Mathf.Sin(a))*radius);}
            }
            if(!active&&audioSource.isPlaying)audioSource.Stop();
        }
        void OnDestroy(){if(combat){combat.WindupStarted-=Windup;combat.ShotFired-=Shot;}}
    }
}
