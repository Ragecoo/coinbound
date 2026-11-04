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
            public float age, life;
            public bool mana;
            public int size;
            public Color tint;
        }
        private readonly Particle[] particles = new Particle[1024];
        private int cursor;
        private uint seed = 23117;
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
                p.velocity.y += (p.mana ? 8 : -90) * Time.deltaTime;
                p.position += p.velocity * Time.deltaTime;
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
