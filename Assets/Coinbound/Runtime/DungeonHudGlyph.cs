using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonHudGlyph : MaskableGraphic
    {
        public enum Symbol { Diamond, Gauge, Gem, Sword, Bow, Shield, Roll }
        public Symbol symbol;
        protected override void OnPopulateMesh(VertexHelper m)
        {
            m.Clear(); Rect r = rectTransform.rect;
            float w = r.width / 2, h = r.height / 2;
            Color faint = color; faint.a *= .5f;
            if (symbol == Symbol.Diamond || symbol == Symbol.Gem)
            {
                float a = Mathf.Min(w, h) - 2;
                Path(m, new[] { new Vector2(0,a), new Vector2(a,0), new Vector2(0,-a), new Vector2(-a,0), new Vector2(0,a) }, 1, faint);
                if (symbol == Symbol.Gem)
                {
                    int radius = Mathf.Max(1, Mathf.RoundToInt(a * .57f / DungeonUiPixels.Size));
                    for (int row = -radius; row <= radius; row++)
                    {
                        int halfWidth = radius - Mathf.Abs(row);
                        DungeonUiPixels.Rect(m, this, -halfWidth * DungeonUiPixels.Size,
                            row * DungeonUiPixels.Size, (halfWidth * 2 + 1) * DungeonUiPixels.Size,
                            DungeonUiPixels.Size, color);
                    }
                }
                else { Line(m,new Vector2(-2,a),new Vector2(2,a),4,color); Line(m,new Vector2(-2,-a),new Vector2(2,-a),4,color); }
            }
            else if (symbol == Symbol.Gauge)
            {
                Line(m,new Vector2(-w+4,h),new Vector2(w-5,h),1,faint);
                Line(m,new Vector2(-w+4,-h),new Vector2(w-5,-h),1,faint);
                Path(m,new[]{new Vector2(w-5,h),new Vector2(w+3,0),new Vector2(w-5,-h)},1,color);
            }
            else if (symbol == Symbol.Sword)
            {
                Line(m,new Vector2(-10,-13),new Vector2(12,15),4,color);
                Line(m,new Vector2(-13,-3),new Vector2(0,-14),3,color);
            }
            else if (symbol == Symbol.Bow)
            {
                Path(m,new[]{new Vector2(-7,-17),new Vector2(9,0),new Vector2(-7,17),new Vector2(-7,-17)},2,color);
                Line(m,new Vector2(-13,0),new Vector2(17,0),2,color);
                Path(m,new[]{new Vector2(11,5),new Vector2(17,0),new Vector2(11,-5)},2,color);
            }
            else if (symbol == Symbol.Shield)
            {
                Path(m,new[]{new Vector2(-12,12),new Vector2(12,12),new Vector2(10,-5),new Vector2(0,-15),new Vector2(-10,-5),new Vector2(-12,12)},2,color);
                Line(m,new Vector2(0,9),new Vector2(0,-8),2,color);
                Line(m,new Vector2(-6,2),new Vector2(6,2),2,color);
            }
            else
            {
                for(int i=0;i<10;i++)
                {
                    float a=i*Mathf.PI/7,b=(i+1)*Mathf.PI/7;
                    Line(m,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*13,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*13,2,color);
                }
                Path(m,new[]{new Vector2(-16,-3),new Vector2(-11,-13),new Vector2(-2,-10)},3,color);
            }
        }
        private void Path(VertexHelper m,Vector2[] p,float width,Color c)
        { for(int i=0;i<p.Length-1;i++) Line(m,p[i],p[i+1],width,c); }
        private void Line(VertexHelper m,Vector2 a,Vector2 b,float width,Color c)
        {
            DungeonUiPixels.Line(m, this, a, b, width, c);
        }
    }
}
