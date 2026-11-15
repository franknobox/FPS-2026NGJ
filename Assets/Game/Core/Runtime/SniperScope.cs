using UnityEngine;
using UnityEngine.UI;
namespace SlugWhitebox
{
    [ExecuteAlways,RequireComponent(typeof(CanvasRenderer))]
    public sealed class SniperScope : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=GetPixelAdjustedRect();float radius=Mathf.Min(rect.width,rect.height)*.39f;
            Color mask=new Color(.015f,.012f,.01f,.97f),ink=new Color(.025f,.018f,.012f,1);
            Quad(vh,new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,-radius),mask);
            Quad(vh,new Vector2(rect.xMin,radius),new Vector2(rect.xMax,rect.yMax),mask);
            Quad(vh,new Vector2(rect.xMin,-radius),new Vector2(-radius,radius),mask);
            Quad(vh,new Vector2(radius,-radius),new Vector2(rect.xMax,radius),mask);
            for(int i=0;i<96;i++)
            {
                float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var q=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                float x=radius/Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.y)),y=radius/Mathf.Max(Mathf.Abs(q.x),Mathf.Abs(q.y));
                Patch(vh,p*radius,q*radius,q*y,p*x,mask);
                Patch(vh,p*(radius-5),q*(radius-5),q*radius,p*radius,ink);
            }
            float reach=radius*.85f;
            Quad(vh,new Vector2(-reach,-1),new Vector2(-5,1),ink);Quad(vh,new Vector2(5,-1),new Vector2(reach,1),ink);
            Quad(vh,new Vector2(-1,-reach),new Vector2(1,-5),ink);Quad(vh,new Vector2(-1,5),new Vector2(1,reach),ink);
            for(int i=1;i<=3;i++)
            {
                float d=radius*.19f*i;
                Quad(vh,new Vector2(d-1,-5),new Vector2(d+1,5),ink);Quad(vh,new Vector2(-d-1,-5),new Vector2(-d+1,5),ink);
                Quad(vh,new Vector2(-5,-d-1),new Vector2(5,-d+1),ink);
            }
            Quad(vh,new Vector2(-1.5f,-1.5f),new Vector2(1.5f,1.5f),new Color(.7f,.08f,.025f));
        }
        static void Quad(VertexHelper vh,Vector2 min,Vector2 max,Color tint)=>Patch(vh,min,new Vector2(min.x,max.y),max,new Vector2(max.x,min.y),tint);
        static void Patch(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
        {
            int n=vh.currentVertCount;vh.AddVert(a,tint,Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,tint,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
