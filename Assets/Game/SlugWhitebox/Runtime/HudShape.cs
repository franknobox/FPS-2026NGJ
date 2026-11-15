using UnityEngine;
using UnityEngine.UI;
namespace SlugWhitebox
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudShape : MaskableGraphic
    {
        public int sides=6;
        public float border=4;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            Polygon(vh,r.center,r.size*.5f,ClientFlow.ClientUI.Ink);
            Polygon(vh,r.center,Vector2.Max(Vector2.zero,r.size*.5f-Vector2.one*border),color);
        }
        void Polygon(VertexHelper vh,Vector2 center,Vector2 radius,Color tint)
        {
            int start=vh.currentVertCount;vh.AddVert(center,tint,Vector2.zero);
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;vh.AddVert(center+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),radius),tint,Vector2.zero);}
            for(int i=0;i<sides;i++)vh.AddTriangle(start,start+1+(i+1)%sides,start+1+i);
        }
    }
}
