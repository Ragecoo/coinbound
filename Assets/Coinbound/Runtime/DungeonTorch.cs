using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>A compact radial shadow map shared by every terrain and actor sprite.</summary>
    [DefaultExecutionOrder(50)]
    public sealed class DungeonTorch : MonoBehaviour
    {
        private const int RayCount = 2048;
        private static readonly int ShadowMapId = Shader.PropertyToID("_TorchShadowMap");
        private static readonly int WallMapId = Shader.PropertyToID("_TorchWallMap");
        private static readonly int PositionId = Shader.PropertyToID("_TorchPosition");
        private static readonly int SettingsId = Shader.PropertyToID("_TorchSettings");
        private static readonly int ColorId = Shader.PropertyToID("_TorchColor");
        private static readonly int DarknessId = Shader.PropertyToID("_DarknessColor");
        private static readonly int LightTuningId = Shader.PropertyToID("_LightTuning");
        private static readonly int MapSizeId = Shader.PropertyToID("_DungeonSize");
        private static readonly int FlickerId = Shader.PropertyToID("_TorchFlicker");
        private DungeonMap map;
        private Transform source;
        private Material material;
        private Texture2D shadowMap;
        private Texture2D wallMap;
        private Vector2[] directions;
        private float[] wallDistances;
        private Vector2 lastPosition = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        private float sightDistance;
        public float Radius { get; private set; }
        public Texture2D ShadowMap => shadowMap;

        public void Initialize(DungeonMap floor, Transform player, Material target, float radius, float brightness, Color color,
            Color darkness, float unlitVisibility, float shadowSoftness, float wallLightMultiplier, float upperWallLightMultiplier,
            float wallLightDepth)
        {
            map = floor;
            source = player;
            material = target;
            Radius = radius;
            // Sight reaches any point in this map; only the torch's brightness has a radius.
            sightDistance = new Vector2(map.TileWidth, map.TileHeight).magnitude + 1;
            directions = new Vector2[RayCount];
            wallDistances = new float[RayCount];
            for (int i = 0; i < RayCount; i++)
            {
                float angle = ((i + 0.5f) / RayCount - 0.5f) * (2 * Mathf.PI);
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            shadowMap = new Texture2D(RayCount, 1, TextureFormat.RFloat, false, true)
            { name = "Player line of sight", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            wallMap = new Texture2D(map.TileWidth, map.TileHeight, TextureFormat.R8, false, true)
            { name = "Dungeon walls", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var walls = new byte[map.TileWidth * map.TileHeight];
            for (int y = 0; y < map.TileHeight; y++)
            for (int x = 0; x < map.TileWidth; x++)
                walls[y * map.TileWidth + x] = map.IsTileFloor(new TilePosition(x, y)) ? (byte)0 : (byte)255;
            wallMap.SetPixelData(walls, 0);
            wallMap.Apply(false, true);
            material.SetTexture(ShadowMapId, shadowMap);
            material.SetTexture(WallMapId, wallMap);
            material.SetVector(ColorId, ShaderColor(color));
            material.SetVector(DarknessId, ShaderColor(darkness));
            material.SetVector(LightTuningId, new Vector4(unlitVisibility, shadowSoftness, wallLightMultiplier, upperWallLightMultiplier));
            material.SetVector(MapSizeId, new Vector4(map.TileWidth, map.TileHeight, 1f / map.TileWidth, 1f / map.TileHeight));
            material.SetVector(SettingsId, new Vector4(Radius, brightness, 1f / RayCount, wallLightDepth));
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (source == null || material == null) return;
            Vector2 position = source.position;
            if ((position - lastPosition).sqrMagnitude > 0.000001f)
            {
                for (int i = 0; i < RayCount; i++)
                    wallDistances[i] = DungeonVisibility.Cast(map, position, directions[i], sightDistance);
                shadowMap.SetPixelData(wallDistances, 0);
                shadowMap.Apply(false, false);
                lastPosition = position;
            }
            material.SetVector(PositionId, new Vector4(position.x, position.y, 0, 1));
            // Restrained fluctuations, independent of shadow geometry, avoid flickering walls.
            float time = Time.time;
            float flicker = 1 + 0.015f * Mathf.Sin(time * 6.7f) + 0.01f * Mathf.Sin(time * 13.1f + 1.4f);
            material.SetFloat(FlickerId, flicker);
        }

        private static Vector4 ShaderColor(Color color)
        {
            // Use the same sRGB conversion as the camera clear color, including #101010.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
            return new Vector4(color.r, color.g, color.b, color.a);
        }

        private void OnDestroy()
        {
            if (shadowMap != null) Destroy(shadowMap);
            if (wallMap != null) Destroy(wallMap);
        }
    }
}
