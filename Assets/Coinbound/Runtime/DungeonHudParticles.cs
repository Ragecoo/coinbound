using UnityEngine;
using UnityEngine.UI;

namespace Coinbound
{
    /// <summary>Bounded, allocation-free HUD particles in native HUD pixels.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DungeonHudParticles : MaskableGraphic
    {
        private struct Particle
        {
            public Vector2 position, velocity;
            public Vector2 origin, target;
            public bool gathering, healing;
            public float age, life;
            public bool mana;
            public int size;
            public Color tint;
        }
        private readonly Particle[] particles = new Particle[1024];
        private int cursor;
        private uint seed = 23117;
        private float nextGatherTime, nextHealTime;
        public int ActiveCount { get; private set; }
        private float Next() { seed = unchecked(seed * 1664525u + 1013904223u); return (seed >> 8) / 16777216f; }

        public void Detach(Vector2 position, Color32 tint, bool mana)
        {
            if (DungeonSettings.ReducedMotion) return;
            particles[cursor] = new Particle {
                position = position,
                velocity = new Vector2((Next() - .5f) * (mana ? 22 : 7), mana ? 5 + Next() * 16 : -2 - Next() * 5),
                life = .35f + Next() * .35f, mana = mana, size = 1, tint = tint
            };
            cursor = (cursor + 1) % particles.Length;
            SetVerticesDirty();
        }

        public void Clear()
        {
            System.Array.Clear(particles, 0, particles.Length);
            ActiveCount = 0; cursor = 0; SetVerticesDirty();
            nextGatherTime = nextHealTime = 0;
        }

        public void Gather(Vector2 target, Color32 tint)
        {
            if (DungeonSettings.ReducedMotion || Time.time < nextGatherTime) return;
            nextGatherTime = Time.time + .2f;
            Vector2 origin = target + new Vector2(2 + Next() * 3, -3 - Next() * 3);
            Color color = tint; color.a *= .65f;
            particles[cursor] = new Particle {
                position = origin, origin = origin, target = target, gathering = true,
                life = .3f, mana = true, size = 1, tint = color
            };
            cursor = (cursor + 1) % particles.Length;
            SetVerticesDirty();
        }

        public void GatherHealth(Vector2 target)
        {
            if(DungeonSettings.ReducedMotion||Time.time<nextHealTime)return;
            nextHealTime=Time.time+.18f;
            for(int i=0;i<4;i++)
            {
                Vector2 origin=target+new Vector2((Next()-.5f)*14,3+Next()*7);
                particles[cursor]=new Particle{position=origin,origin=origin,target=target,gathering=true,healing=true,
                    life=.65f+Next()*.25f,size=i==0?2:1,tint=i==0?new Color(.92f,.94f,.62f,.8f):new Color(.42f,.88f,.62f,.7f)};
                cursor=(cursor+1)%particles.Length;
            }
            SetVerticesDirty();
        }

        private void Update()
        {
            if (DungeonSettings.ReducedMotion) { if (ActiveCount > 0) Clear(); return; }
            bool changed = false;
            ActiveCount = 0;
            for (int i = 0; i < particles.Length; i++)
            {
                ref Particle p = ref particles[i];
                if (p.life <= 0 || p.age >= p.life) continue;
                changed = true; p.age += Time.deltaTime;
                if (p.gathering)
                {
                    float t=Mathf.Clamp01(p.age/p.life);
                    p.position=Vector2.Lerp(p.origin,p.target,p.healing?t*t:t);
                    if(p.healing)p.position+=Vector2.right*Mathf.Sin(t*Mathf.PI)*(p.origin.x<p.target.x?-2:2);
                }
                else
                {
                    p.velocity.y += (p.mana ? 8 : -90) * Time.deltaTime;
                    p.position += p.velocity * Time.deltaTime;
                }
                if (p.age < p.life) ActiveCount++;
            }
            if (changed) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            foreach (Particle p in particles)
            {
                if (p.life <= 0 || p.age >= p.life) continue;
                float t = p.age / p.life;
                float fade = Mathf.Ceil(Mathf.Min(1, (1 - t) * 3) * 4) / 4;
                Color tint = p.tint; tint.a *= fade;
                int size = t > .65f ? 1 : p.size;
                float x = Mathf.Round(p.position.x), y = Mathf.Round(p.position.y);
                if(p.healing)
                {
                    tint.a*=Mathf.Clamp01(t*6);
                    Pixel(mesh,x,y,1,1,tint);
                    if(p.size==2&&t>.15f&&t<.7f)
                    {Pixel(mesh,x-1,y,3,1,tint);Pixel(mesh,x,y-1,1,3,tint);}
                    continue;
                }
                Pixel(mesh, x, y, size, p.mana ? size : size + (t > .15f && t < .65f ? 1 : 0), tint);
                if (p.mana && p.size == 2 && t < .55f)
                {
                    int spread = 1 + Mathf.FloorToInt(t * 6);
                    Pixel(mesh, x - spread, y, 1, 1, new Color(.65f, .88f, 1, fade));
                    Pixel(mesh, x + spread, y + 1, 1, 1, tint);
                }
            }
        }

        private static void Pixel(VertexHelper mesh, float x, float y, int w, int h, Color tint)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x, y + h), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y + h), tint, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y), tint, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2); mesh.AddTriangle(n + 2, n + 3, n);
        }
    }
}
