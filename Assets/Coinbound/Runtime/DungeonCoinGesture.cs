using UnityEngine;
namespace Coinbound
{
    // Forgiving fist-to-thumbs-up recognition restored at the player request.
    public sealed class DungeonCoinGesture
    {
        public int Stage {get;private set;}
        public float HoldProgress=>Mathf.Clamp01(held/.3f);
        private float held,armedAge,thumbHeld,missing,baseThumb;
        public void Reset(){Stage=0;held=armedAge=thumbHeld=missing=0;}
        public bool Observe(Vector3[] points,Vector3[] world,float dt)
        {
            if(dt<=0)return false;
            dt=Mathf.Min(dt,.2f);
            if(points==null||world==null||points.Length!=21||world.Length!=21)
            {missing+=dt;if(missing>.8f)Reset();return false;}
            missing=0;
            float scale=Vector3.Distance(world[0],world[9]);
            float imageScale=Vector2.Distance(points[0],points[9]);
            if(scale<.01f||imageScale<.025f)return false;
            int curled=0;
            for(int finger=0;finger<4;finger++)
            {
                int mcp=5+finger*4,pip=mcp+1,tip=mcp+3;
                float angle=Vector3.Angle(world[mcp]-world[pip],world[tip]-world[pip]);
                if(angle<155||Vector3.Distance(world[tip],world[0])<Vector3.Distance(world[pip],world[0])*1.05f)curled++;
            }
            float thumbAngle=Vector3.Angle(world[2]-world[3],world[4]-world[3]);
            float thumbHeight=(points[0].y-points[4].y)/imageScale;
            bool extended=thumbAngle>115 || Vector3.Distance(world[4],world[2])>scale*.65f;
            bool thumbsUp=curled>=3 && extended && points[4].y<points[5].y-imageScale*.12f;
            if(Stage==2 && curled>=3 && extended && thumbHeight-baseThumb>.22f)thumbsUp=true;
            if(Stage<2)
            {
                if(curled<3||thumbsUp){Reset();return false;}
                Stage=1;held+=dt;
                if(held>=.3f){Stage=2;armedAge=0;baseThumb=thumbHeight;}
            }
            else
            {
                armedAge+=dt;
                thumbHeld=thumbsUp?thumbHeld+dt:0;
                if(thumbHeld>=.12f){Reset();return true;}
                if(armedAge>5||curled<2)Reset();
            }
            return false;
        }
    }
}
