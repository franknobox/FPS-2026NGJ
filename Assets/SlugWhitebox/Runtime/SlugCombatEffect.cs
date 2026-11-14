using NGJ.Combat;
using UnityEngine;
namespace SlugWhitebox
{
    // 仅添加可见反馈，不参与碰撞和伤害。
    public class SlugCombatEffect : MonoBehaviour
    {
        LineRenderer ring;float born;float radius;
        void Start()
        {
            born=Time.time;
            var game=FindFirstObjectByType<SlugGame>();if(!game)return;
            var projectile=GetComponent<CombatProjectile>();
            if(projectile){game.Visual(PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.3f,game.cyanMaterial);return;}
            var hitbox=GetComponent<CombatHitbox>();if(!hitbox)return;
            radius=hitbox.radius;ring=gameObject.AddComponent<LineRenderer>();ring.sharedMaterial=game.lineMaterial;
            ring.positionCount=48;ring.loop=true;ring.startWidth=ring.endWidth=.06f;ring.startColor=ring.endColor=Color.cyan;
        }
        void Update()
        {
            if(!ring)return;
            float r=Mathf.Lerp(.2f,radius,Mathf.Clamp01((Time.time-born)*8));
            for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;ring.SetPosition(i,transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*r);}
        }
    }
}
