using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Small, wall-occluded transient lights, shared by the existing dungeon material.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class DungeonEffectLights : MonoBehaviour
    {
        private const int Capacity = 24, VisibleCount = 8, Rays = 256;
        private static DungeonEffectLights active;
        private static readonly int CountId = Shader.PropertyToID("_EffectLightCount");
        private static readonly int PositionsId = Shader.PropertyToID("_EffectLightPositions");
        private static readonly int ColorsId = Shader.PropertyToID("_EffectLightColors");
        private static readonly int ShadowsId = Shader.PropertyToID("_EffectLightShadows");
        private readonly FlashLight[] lights = new FlashLight[Capacity];
        private readonly Vector4[] positions = new Vector4[VisibleCount];
        private readonly Vector4[] colors = new Vector4[VisibleCount];
        private readonly float[] shadows = new float[VisibleCount * Rays];
        private readonly int[] chosen = new int[VisibleCount];
        private DungeonPlayer player;
        private DungeonMap map;
        private Material material;
        private Texture2D shadowTexture;
        private float clock;

        private sealed class FlashLight
        {
            internal Vector2 position;
            internal Color color;
            internal float radius, intensity, start, duration;
            internal bool sustained,playerSight;
            internal Object source;
            internal readonly float[] depths = new float[Rays];
        }

        public static DungeonEffectLights Create(DungeonMap map, DungeonPlayer player, Material material)
        {
            var instance = new GameObject("Local effect lights").AddComponent<DungeonEffectLights>();
            instance.transform.SetParent(player.transform.parent, false);
            instance.map = map;
            instance.player = player;
            instance.material = material;
            instance.shadowTexture = new Texture2D(Rays, VisibleCount, TextureFormat.RFloat, false, true)
            { name = "Effect wall shadows", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            material.SetTexture(ShadowsId, instance.shadowTexture);
            active = instance;
            return instance;
        }

        public static void Flash(Vector2 world, float radius, float intensity, Color color, float duration, bool sustained = false)
        {
            if (active == null || !DungeonFxSettings.Current.dynamicLights || duration <= 0) return;
            active.Emit(world, Mathf.Clamp(radius, 0.1f, 4), Mathf.Clamp(intensity, 0, 2), color, duration, sustained);
        }

        // One refreshed light per effect, including moving projectiles: never a trail of stacked lights.
        public static void Glow(Object source, Vector2 world, float radius, float intensity, Color color)
        {
            if (active == null || source == null || !DungeonFxSettings.Current.dynamicLights) return;
            active.Emit(world, Mathf.Clamp(radius,.1f,4), Mathf.Clamp(intensity,0,2), color, .12f, true, source);
        }

        public static bool AmbientVisible(DungeonMap map,Vector2 player,Vector2 source,Camera camera)
        {
            if(map==null||camera==null)return false;
            Vector3 viewport=camera.WorldToViewportPoint(source);
            if(viewport.z<0||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1)return false;
            Vector2 delta=source-player;float distance=delta.magnitude;
            return distance<=14 && DungeonVisibility.Cast(map,player,delta,distance+.01f)>=distance-.015f;
        }
        public static bool AmbientActive(Vector2 source,Camera camera)
        {
            if(camera==null)return false;
            Vector3 view=camera.WorldToViewportPoint(source);
            float height=camera.orthographicSize*2;
            return view.z>=0&&view.x>=-3/(height*camera.aspect)&&view.x<=1+3/(height*camera.aspect)&&view.y>=-3/height&&view.y<=1+3/height;
        }
        public static void AmbientGlow(Object source,Vector2 world,float radius,float intensity,Color color)
        {
            if(active==null||source==null||!DungeonFxSettings.Current.dynamicLights)return;
            active.Emit(world,radius,intensity,color,.12f,true,source,true);
        }
        private void Emit(Vector2 world, float radius, float intensity, Color color, float duration, bool sustained, Object source = null,bool playerSight=false)
        {
            if (!map.IsTileFloor(new TilePosition(Mathf.FloorToInt(world.x), Mathf.FloorToInt(1 - world.y)))) return;
            int slot = -1;
            float oldest = float.PositiveInfinity;
            for (int i = 0; i < Capacity; i++)
            {
                FlashLight item = lights[i];
                // Refresh stationary flames rather than stacking a fresh point light every frame.
                if (item != null && (source != null ? item.source == source : item.source == null && (item.position - world).sqrMagnitude < 0.0025f && item.color == color))
                { slot = i; break; }
                if (item == null || item.start + item.duration < clock) { slot = i; oldest = float.NegativeInfinity; }
                else if (oldest > item.start + item.duration) { slot = i; oldest = item.start + item.duration; }
            }
            FlashLight light = lights[slot] ?? (lights[slot] = new FlashLight());
            bool changed=light.position!=world || light.radius!=radius;
            light.playerSight=playerSight;
            light.position = world;
            light.radius = radius;
            light.intensity = intensity;
            light.color = color;
            light.start = clock;
            light.duration = duration;
            light.sustained = sustained;
            light.source = source;
            if(changed)for (int i = 0; i < Rays; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2 / Rays;
                light.depths[i] = DungeonVisibility.Cast(map, world, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radius);
            }
        }

        private void LateUpdate()
        {
            if (material == null || player == null) return;
            if (player.CanAct) clock += Time.deltaTime;
            if (!DungeonFxSettings.Current.dynamicLights) { material.SetInt(CountId, 0); return; }
            int count = 0;
            for (int n = 0; n < VisibleCount; n++)
            {
                int best = -1;
                float score = 0;
                for (int i = 0; i < Capacity; i++)
                {
                    FlashLight light = lights[i];
                    if (light == null || clock >= light.start + light.duration) continue;
                    if(light.playerSight&&!AmbientActive(light.position,Camera.main))continue;
                    bool used = false;
                    for (int k = 0; k < count; k++) if (chosen[k] == i) used = true;
                    if (used) continue;
                    float fade = Fade(light);
                    float candidate = light.intensity * fade / (1 + ((Vector2)player.transform.position - light.position).sqrMagnitude * 0.08f);
                    if (candidate > score) { score = candidate; best = i; }
                }
                if (best < 0) break;
                chosen[count] = best;
                FlashLight selected = lights[best];
                float remaining = Fade(selected);
                Color tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? selected.color.linear : selected.color;
                positions[count] = new Vector4(selected.position.x, selected.position.y, selected.radius,
                    selected.intensity * remaining * DungeonFxSettings.Current.effectLightStrength);
                colors[count] = new Vector4(tint.r, tint.g, tint.b, selected.playerSight?(AmbientVisible(map,player.transform.position,selected.position,Camera.main)?2f:1f):0f);
                for (int ray = 0; ray < Rays; ray++) shadows[count * Rays + ray] = selected.depths[ray];
                count++;
            }
            material.SetInt(CountId, count);
            if (count == 0) return;
            material.SetVectorArray(PositionsId, positions);
            material.SetVectorArray(ColorsId, colors);
            shadowTexture.SetPixelData(shadows, 0);
            shadowTexture.Apply(false, false);
        }

        private float Fade(FlashLight light) => light.sustained
            ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((light.start + light.duration - clock) / 0.08f))
            : 1 - Mathf.Clamp01((clock - light.start) / light.duration);

        private void OnEnable(){if(map!=null)active=this;}
        private void OnDisable(){if(active==this)active=null;}
        private void OnDestroy()
        {
            if (active == this) active = null;
            if (material != null) material.SetInt(CountId, 0);
            if (shadowTexture != null) Destroy(shadowTexture);
        }
    }
}
