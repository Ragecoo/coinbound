using System.Collections.Generic;
using UnityEngine;
namespace Coinbound
{
    public sealed partial class DungeonProps
    {
        private readonly List<Vector2> torches=new List<Vector2>();
        private readonly List<int> visibleTorches=new List<int>(24);
        private readonly Queue<SpriteRenderer> debris=new Queue<SpriteRenderer>();
        private readonly List<HitAnimation> hitAnimations=new List<HitAnimation>(12);
        private readonly Particle[] particles=new Particle[192];
        private readonly DungeonAmbientCanvas[] flames=new DungeonAmbientCanvas[8];
        private DungeonAmbientCanvas fragments;
        private AudioSource breakAudio;
        private AudioClip woodClip,potteryClip;
        private float refresh,clock;private int burstFrame,bursts,particleIndex;
        private struct Particle {public Vector2 origin,velocity;public float start,life;public Color32 color;public int size;}
        private struct HitAnimation {public DungeonProp prop;public float until;}
        public int MeasuredFrames {get;private set;}
        public double TotalUpdateMilliseconds {get;private set;}
        public double MaximumUpdateMilliseconds {get;private set;}
        private readonly float[] soundUntil=new float[4];
        public int TorchCount=>torches.Count;
        public int ActiveParticleCount {get;private set;}
        private void PlanTorches(int roomId,Rect bounds)
        {
            for(float x=bounds.xMin+2.5f;x<bounds.xMax-2;x+=6)
            {
                Vector2 anchor=new Vector2(x,bounds.yMax-.125f);
                if(!StraightTorchWall(anchor,Vector2.up))continue;
                bool covered=false;
                foreach(var opening in map.Rooms[roomId].Openings)
                    if(Vector2.Distance(anchor,new Vector2(opening.X*3+1.5f,1-(opening.Y*3+1.5f)))<2.5f)covered=true;
                foreach(var item in Items)if(item.Definition.tall&&item.VisualBounds.Contains(anchor+Vector2.up*.5f))covered=true;
                if(!covered)torches.Add(anchor);
            }
        }
        private void PlanPassageAndSideTorches()
        {
            for(int y=2;y<map.TileHeight-2;y++)for(int x=2;x<map.TileWidth-2;x++)
            {
                var tile=new ProceduralGeneration.TilePosition(x,y);
                if(!map.IsTileFloor(tile))continue;
                Vector2 center=new Vector2(x+.5f,.5f-y),normal;
                bool corridor=map.GetTile(tile)==ProceduralGeneration.TileKind.Corridor;
                if(!Floor(center+Vector2.left))normal=Vector2.left;
                else if(!Floor(center+Vector2.right))normal=Vector2.right;
                else if(corridor&&!Floor(center+Vector2.up))normal=Vector2.up;
                else continue;
                if(((x*17+y*31)%11)!=0)continue;
                Vector2 anchor=center+normal*.375f;
                if(!StraightTorchWall(anchor,normal))continue;
                bool clear=true;
                foreach(var torch in torches)if(Vector2.Distance(torch,anchor)<6){clear=false;break;}
                foreach(var prop in Items)if(prop.Definition.tall&&prop.VisualBounds.Contains(anchor+Vector2.up*.5f)){clear=false;break;}
                foreach(var room in map.Rooms)foreach(var door in room.Openings)
                    if(Vector2.Distance(anchor,new Vector2(door.X*3+1.5f,1-(door.Y*3+1.5f)))<2)clear=false;
                if(clear)torches.Add(anchor);
            }
        }
        public bool StraightTorchWall(Vector2 anchor,Vector2 normal)
        {
            Vector2 tangent=new Vector2(-normal.y,normal.x);
            // Require continuous support and open floor for two tiles on either side.
            for(float offset=-2;offset<=2;offset+=.25f)
            {
                Vector2 point=anchor+tangent*offset;
                if(!Floor(point)||Floor(point+normal*.5f)||!Floor(point-normal*.75f))return false;
            }
            return true;
        }
        private void BuildEffects()
        {
            for(int i=0;i<flames.Length;i++)flames[i]=DungeonAmbientCanvas.Create(transform,"Visible flame "+i,material);
            fragments=DungeonAmbientCanvas.Create(transform,"Shared prop fragments",material);
            breakAudio=gameObject.AddComponent<AudioSource>();breakAudio.playOnAwake=false;
            woodClip=MakeSound(1,.23f);potteryClip=MakeSound(2,.3f);
        }
        private static AudioClip MakeSound(int kind,float duration)
        {
            const int rate=22050;var data=new float[Mathf.CeilToInt(rate*duration)];uint state=771;float filtered=0;
            for(int i=0;i<data.Length;i++)
            {
                state=state*1664525+1013904223;float noise=(state>>8)/(float)0xFFFFFF*2-1;filtered=Mathf.Lerp(filtered,noise,.14f);
                float t=i/(float)rate,envelope=kind==0?.5f+.5f*Mathf.Sin(2*Mathf.PI*t/duration):Mathf.Exp(-t*18)*Mathf.Min(t*1000,1);
                float value=kind==2?Mathf.Sin(t*1913*2*Mathf.PI)*.5f+filtered:filtered+(noise>.98f?noise*.6f:0);
                data[i]=value*envelope*(kind==0?.12f:.3f);
            }
            var clip=AudioClip.Create("Environment "+kind,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        internal void RegisterDebris(SpriteRenderer sprite)
        {debris.Enqueue(sprite);while(debris.Count>64){var old=debris.Dequeue();if(old!=null)Destroy(old.gameObject);}}
        internal void HitFeedback(DungeonProp prop)
        {hitAnimations.Add(new HitAnimation{prop=prop,until=clock+.07f});}
        internal void BreakFeedback(DungeonProp prop)
        {
            if(cameraView==null||!DungeonEffectLights.AmbientActive(prop.lightAnchor,cameraView))return;
            if(burstFrame!=Time.frameCount){burstFrame=Time.frameCount;bursts=0;}if(bursts++>=4)return;
            bool ceramic=prop.Definition.material=="pottery"||prop.Definition.material=="ceramic";
            for(int voice=0;voice<soundUntil.Length;voice++)
                if(clock>=soundUntil[voice]){soundUntil[voice]=clock+.3f;breakAudio.PlayOneShot(ceramic?potteryClip:woodClip,.45f/(1+Vector2.Distance(player.transform.position,prop.lightAnchor)*.2f));break;}
            int count=ceramic?8:10;
            for(int i=0;i<count;i++)
            {
                float angle=i*2.399963f;float speed=.7f+(i%5)*.25f;
                Color32 tint=ceramic?new Color32(91,89,95,255):new Color32(97,88,90,255);
                if(prop.Definition.extra.Length>0&&i>=count-3)tint=prop.Definition.extra=="wax_crumbs"?new Color32(138,133,122,255):prop.Definition.extra=="cloth_fibres"?new Color32(78,48,65,255):new Color32(83,87,106,255);
                particles[particleIndex++%particles.Length]=new Particle{origin=prop.lightAnchor,velocity=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*speed,start=clock,life=.3f+(i%4)*.07f,color=tint,size=1+i%3};
            }
        }
        private void Update()
        {
            if(player==null)return;long stamp=System.Diagnostics.Stopwatch.GetTimestamp();if(CanAct)clock+=Time.deltaTime;
            if(dirty){Physics2D.SyncTransforms();RebuildShadowGeometry();DungeonCombatNavigation.Invalidate(map);dirty=false;}
            for(int i=hitAnimations.Count-1;i>=0;i--)
            {
                var hit=hitAnimations[i];if(hit.prop==null){hitAnimations.RemoveAt(i);continue;}
                bool done=clock>=hit.until;
                hit.prop.visual.transform.localPosition=done?Vector3.zero:Vector3.right*(Mathf.Sin((hit.until-clock)*90)>0?1:-1)/32;
                hit.prop.visual.color=done?Color.white:new Color(1,.9f,.8f);
                if(done)hitAnimations.RemoveAt(i);
            }
            refresh-=Time.unscaledDeltaTime;if(refresh<=0){refresh=.04f;RefreshVisible();}
            DrawFragments();
            double ms=(System.Diagnostics.Stopwatch.GetTimestamp()-stamp)*1000d/System.Diagnostics.Stopwatch.Frequency;
            MeasuredFrames++;TotalUpdateMilliseconds+=ms;MaximumUpdateMilliseconds=System.Math.Max(MaximumUpdateMilliseconds,ms);
        }
        private void RefreshVisible()
        {
            if(cameraView==null)cameraView=Camera.main;if(cameraView==null)return;
            int shown=0;foreach(var item in Items)
            {
                Vector3 viewport=cameraView.WorldToViewportPoint(item.transform.position);
                bool visible=viewport.x>-.1f&&viewport.x<1.1f&&viewport.y>-.15f&&viewport.y<1.15f&&shown<96;
                item.SetVisible(visible);if(visible)shown++;
            }
            int count=0;float nearest=100;
            // Six nearby torch emitters and two candle emitters share the effect-light system.
            visibleTorches.Clear();
            for(int i=0;i<torches.Count;i++)if(DungeonEffectLights.AmbientActive(torches[i],cameraView))visibleTorches.Add(i);
            visibleTorches.Sort((a,b)=>((Vector2)cameraView.transform.position-torches[a]).sqrMagnitude.CompareTo(((Vector2)cameraView.transform.position-torches[b]).sqrMagnitude));
            for(int slot=0;slot<visibleTorches.Count&&count<6;slot++)
            {
                int i=visibleTorches[slot];Vector2 point=torches[i];
                nearest=Mathf.Min(nearest,Vector2.Distance(point,player.transform.position));
                int side=!Floor(point+Vector2.left*.25f)?-1:!Floor(point+Vector2.right*.25f)?1:0;
                DrawFlame(flames[count++],point,point+new Vector2(side*.12f,.55f),true,i,side);
            }
            int candles=0;
            foreach(var prop in Items)
            {
                if(candles>=2||prop.Destroyed||prop.Definition.flames.Length==0||!DungeonEffectLights.AmbientActive(prop.lightAnchor,cameraView))continue;
                Vector2 head=(Vector2)prop.transform.position+prop.Definition.flames[0]/32;
                nearest=Mathf.Min(nearest,Vector2.Distance(prop.lightAnchor,player.transform.position));
                DrawFlame(flames[count++],prop.lightAnchor,head,false,prop.Id,0,prop);candles++;
            }
            while(count<flames.Length)flames[count++].Hide();
        }
        private void DrawFlame(DungeonAmbientCanvas canvas,Vector2 anchor,Vector2 head,bool torch,int phase,int side=0,DungeonProp candleProp=null)
        {
            canvas.Begin(DungeonDepthSort.OrderForY(anchor.y)+1,anchor,.7f);
            if(torch)
            {
                canvas.Pixel(head+new Vector2(-2,-11)/32,new Color32(37,31,33,255),5,12);
                canvas.Pixel(head+new Vector2(-1,-9)/32,new Color32(101,67,39,255),2,9);
                canvas.Pixel(head+new Vector2(-4,-5)/32,new Color32(65,64,75,255),8,3);
            }
            if(candleProp==null)DrawFireParticles(canvas,head,torch,phase);
            else for(int wick=0;wick<candleProp.Definition.flames.Length;wick++)
                DrawFireParticles(canvas,(Vector2)candleProp.transform.position+candleProp.Definition.flames[wick]/32,false,phase+wick*17);
            if(torch&&side!=0)canvas.Pixel(head+new Vector2(-side*5,-6)/32,new Color32(65,64,75,255),6,2);
            canvas.End();
            float flicker=.98f+.022f*Mathf.Sin(clock*5+phase)+.008f*Mathf.Sin(clock*11+phase);
            Vector2 lightPoint=torch&&side!=0?anchor+Vector2.up*.45f:anchor;
            if(torch)lightPoint-=(side==0?Vector2.up:Vector2.right*side)*.2f;
            DungeonEffectLights.AmbientGlow(canvas,lightPoint,torch?3.5f:2.3f,(torch?1.1f:.65f)*flicker,new Color(1,.65f,.3f));
        }
        private void DrawFireParticles(DungeonAmbientCanvas canvas,Vector2 head,bool torch,int phase)
        {
            // Analytic particle ages continue off screen: no static flame image or cold start.
            int amount=torch?20:9;
            for(int i=0;i<amount;i++)
            {
                float life=.35f+(i%5)*.065f;
                float time=clock+phase*.137f+i*.077f;
                float age=Mathf.Repeat(time,life),t=age/life;
                float seed=Mathf.Floor(time/life)*1.719f+i*2.399f+phase;
                float drift=Mathf.Sin(seed)*(.025f+.055f*t);
                Vector2 point=head+new Vector2(drift,age*(torch?.5f:.28f));
                Color color=Color.Lerp(new Color(1,.84f,.35f),new Color(.9f,.22f,.035f),t);
                color.a=Mathf.Clamp01((1-t)*1.5f);
                int size=t<.4f?2:1;
                canvas.Pixel(point,color,size,size);
            }
        }
        private void DrawFragments()
        {
            fragments.Begin(DungeonDepthSort.OrderForY(player.transform.position.y)+1,null,.05f);int active=0;
            for(int i=0;i<particles.Length;i++)
            {
                var p=particles[i];float age=clock-p.start;if(p.life<=0||age<0||age>=p.life)continue;active++;
                float t=age/p.life;float height=Mathf.Sin(Mathf.Min(t*1.4f,1)*Mathf.PI)*.28f;
                if(t>.72f)height=Mathf.Sin((t-.72f)/.28f*Mathf.PI)*.06f;
                Vector2 point=p.origin+p.velocity*age+Vector2.up*height;var color=p.color;color.a=(byte)(255*(1-t));
                fragments.Pixel(point,color,p.size,Mathf.Max(1,p.size-1));
            }
            ActiveParticleCount=active;fragments.End();
        }
        private void OnDestroy(){foreach(var clip in new[]{woodClip,potteryClip})if(clip!=null)Destroy(clip);}
    }
}
