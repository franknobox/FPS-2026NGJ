using UnityEngine;

namespace NGJ.Combat
{
    // 只管理表现，不参与伤害。所有参数都可在预制体 Inspector 调整。
    public sealed class CombatVfxMotion : MonoBehaviour
    {
        [Min(.05f), Tooltip("视觉存活秒数，与伤害判定持续时间独立。")]
        public float duration = .65f;
        [Tooltip("圆环从小到大扩散；关闭后用于枪焰、命中火花。")]
        public bool expandRing;
        [Min(.01f)] public float radius = 5;
        [Range(0,180), Tooltip("180 为整圈；其他值为前方扇形半角。")]
        public float halfAngle = 180;
        public bool groundRing;
        [Tooltip("水平波环相对出生点高度；施法点在眼部时下移，爆炸贴地时设为 0.05。")]
        public float verticalOffset = -1.25f;
        public Color tint = Color.cyan;
        public Material ringMaterial;
        [Tooltip("移动声波的跟随目标，由 Hitbox 初始化；不改变判定位置。")]
        public Transform follow;
        [Tooltip("武器技能使用前向冲击束，与声腺圆环区分。")]
        public bool directionalBurst;
        [Min(.01f), Tooltip("武器冲击束宽度；声腺环仍使用细线。") ] public float weaponWidth=.18f;
        [Range(1,3), Tooltip("声腺技能的连续波纹层数。")]
        public int echoCount = 1;
        LineRenderer[] rings;
        Vector3[] directions, positions;
        const int Segments = 48;
        float born;
        bool wasFollowing;
        void Start()
        {
            born = Time.time;
            wasFollowing = follow;
            if (!expandRing && !directionalBurst) return;
            int count = directionalBurst ? 5 : Mathf.Clamp(echoCount,1,3);
            rings = new LineRenderer[count];
            directions = new Vector3[Segments+1]; positions = new Vector3[Segments+1];
            for(int i=0;i<=Segments;i++)
            {
                float a = Mathf.Lerp(-halfAngle,halfAngle,i/(float)Segments)*Mathf.Deg2Rad;
                directions[i]=groundRing?new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)):new Vector3(Mathf.Sin(a),Mathf.Cos(a),0);
            }
            for(int j=0;j<count;j++)
            {
                var child = new GameObject(directionalBurst?"武器冲击束":"声腺波纹");
                child.transform.SetParent(transform,false);
                var line=child.AddComponent<LineRenderer>(); rings[j]=line;
                line.sharedMaterial=ringMaterial; line.useWorldSpace=false;
                line.positionCount=directionalBurst?3:Segments+1;
                line.loop=!directionalBurst && halfAngle>=180;
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows=false;
            }
        }
        void LateUpdate()
        {
            if (follow) { transform.position = follow.position; transform.rotation = follow.rotation; }
            // 声波撞墙后立刻结束，避免特效穿墙；原地脉冲仍可自然淡出。
            else if (wasFollowing) { Destroy(gameObject); return; }
            // 地面冲击环保持水平，不继承榴弹落地时的俯仰角。
            if (groundRing) transform.rotation = Quaternion.Euler(0,transform.eulerAngles.y,0);
            float t = Mathf.Clamp01((Time.time - born) / Mathf.Max(.05f,duration));
            if (rings != null)
            {
                for(int j=0;j<rings.Length;j++)
                {
                    var ring=rings[j];
                    float phase=Mathf.Clamp01(t-j*.12f);
                    float r=radius*Mathf.Lerp(.06f,1,1-Mathf.Pow(1-phase,3));
                    ring.startWidth=ring.endWidth=Mathf.Lerp(directionalBurst?weaponWidth:.065f,.012f,phase);
                    Color c=tint; c.a*= (1-t)*(1-j*.15f);
                    if(t<j*.12f)c.a=0;
                    ring.startColor=ring.endColor=c;
                    if(directionalBurst)
                    {
                        float angle=Mathf.Lerp(-Mathf.Min(halfAngle,40),Mathf.Min(halfAngle,40),j/4f)*Mathf.Deg2Rad;
                        Vector3 direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                        // 折线形成向前展开的暖色箭羽，从第一人称也能看见横向轮廓。
                        Vector3 side=Vector3.right*(j-2)*.18f;
                        ring.SetPosition(0,direction*Mathf.Max(.4f,r-2.5f)+side);
                        ring.SetPosition(1,direction*Mathf.Max(.8f,r-1.1f)+side+Vector3.up*((j%2==0?1:-1)*.35f));
                        ring.SetPosition(2,direction*Mathf.Max(1.2f,r)+side);
                    }
                    else
                    {
                        Vector3 offset=groundRing?Vector3.up*verticalOffset:Vector3.forward*.3f;
                        for(int i=0;i<=Segments;i++) positions[i]=directions[i]*r+offset;
                        // 批量上传顶点，避免每个顶点一次托管/原生调用。
                        ring.SetPositions(positions);
                    }
                }
            }
            if (t >= 1) Destroy(gameObject);
        }
    }
}
