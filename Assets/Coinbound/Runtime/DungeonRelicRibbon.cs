using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    // A pixel-cut ink stroke behind the pickup title, without a dialog frame.
    public sealed class DungeonRelicRibbon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            const int columns=140;
            float step=r.width/columns;
            for(int x=0;x<columns;x++)
            {
                float edge=Mathf.Min(x,columns-1-x);
                float taper=Mathf.Clamp01(edge/14f);
                uint noise=(uint)(x+17)*2654435761u;noise^=noise>>13;
                float top=(30+(noise%5)*3)*taper;
                float bottom=(-28-((noise>>5)%4)*3)*taper;
                int n=vh.currentVertCount;Color32 ink=new Color32(12,5,12,244);
                vh.AddVert(new Vector3(r.xMin+x*step,bottom),ink,Vector2.zero);
                vh.AddVert(new Vector3(r.xMin+x*step,top),ink,Vector2.zero);
                vh.AddVert(new Vector3(r.xMin+(x+1)*step,top),ink,Vector2.zero);
                vh.AddVert(new Vector3(r.xMin+(x+1)*step,bottom),ink,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n+2,n+3,n);
            }
        }
    }
}
