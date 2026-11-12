using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    public sealed class DungeonLootFx : MaskableGraphic
    {
        private readonly Vector2[] trail=new Vector2[12];
        private int trailCount;
        private float sampled,burstAt=-100;
        private Vector2 center,card;
        private bool positive,frame;
        public void ResetEffect(){trailCount=0;burstAt=-100;SetVerticesDirty();}
        public void Follow(Vector2 point)
        {
            if(DungeonSettings.ReducedMotion)return;
            if(Time.unscaledTime-sampled<.025f)return;sampled=Time.unscaledTime;
            for(int i=11;i>0;i--)trail[i]=trail[i-1];trail[0]=point;trailCount=Mathf.Min(12,trailCount+1);SetVerticesDirty();
        }
        public void Reveal(Vector2 point,Vector2 cardCenter,bool good,bool frame=true)
        {this.frame=frame;center=point;card=cardCenter;positive=good;burstAt=Time.unscaledTime;trailCount=0;SetVerticesDirty();}
        private void Update(){if(Time.unscaledTime-burstAt<1.5f)SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for(int i=1;i<trailCount;i++)
            {
                float a=(1-i/12f)*.42f;
                DungeonUiPixels.Rect(mesh,this,trail[i].x-2,trail[i].y-2,4,4,new Color(1,.75f,.3f,a));
            }
            float age=Time.unscaledTime-burstAt;if(age<0||age>1.5f)return;
            float fade=Mathf.Clamp01(1-age/1.5f);
            Color tint=positive?new Color(1,.83f,.4f,fade):new Color(.78f,.42f,.85f,fade);
            // Short pixel corner accents frame the winning card without flooding the screen.
            if(frame)for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
            {
                float px=card.x+x*172,py=card.y+y*190;
                DungeonUiPixels.Rect(mesh,this,px-(x>0?20:0),py,20,2,tint);
                DungeonUiPixels.Rect(mesh,this,px,py-(y>0?20:0),2,20,tint);
            }
            if(DungeonSettings.ReducedMotion)return;
            for(int i=0;i<32;i++)
            {
                float angle=i*2.399963f,speed=28+(i*19%67);
                Vector2 p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(36+speed*age);
                p.y-=age*age*34;
                float size=i%5==0?4:2;
                var color=tint;color.a*=Mathf.Clamp01((1.25f-age)*1.2f);
                DungeonUiPixels.Rect(mesh,this,p.x,p.y,size,size,color);
                if(i%7==0){DungeonUiPixels.Rect(mesh,this,p.x-4,p.y,10,2,color);DungeonUiPixels.Rect(mesh,this,p.x,p.y-4,2,10,color);}
            }
        }
    }
}
