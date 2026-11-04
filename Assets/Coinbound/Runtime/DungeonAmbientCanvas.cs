using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Coinbound
{
    /// <summary>One bounded mesh for ambient world pixels, shaded by the dungeon's normal light and sight map.</summary>
    internal sealed class DungeonAmbientCanvas : MonoBehaviour
    {
        private readonly List<Vector3> vertices = new List<Vector3>(2048);
        private readonly List<Color32> colors = new List<Color32>(2048);
        private readonly List<Vector2> uvs = new List<Vector2>(2048);
        private readonly List<int> triangles = new List<int>(3072);
        private Mesh mesh;
        private MeshRenderer visual;
        private MaterialPropertyBlock properties;

        internal static DungeonAmbientCanvas Create(Transform parent, string label, Material material)
        {
            var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            var canvas = obj.AddComponent<DungeonAmbientCanvas>();
            canvas.visual = obj.GetComponent<MeshRenderer>();
            canvas.visual.sharedMaterial = material;
            canvas.visual.shadowCastingMode = ShadowCastingMode.Off;
            canvas.visual.receiveShadows = false;
            canvas.mesh = new Mesh { name = label };
            canvas.mesh.MarkDynamic();
            obj.GetComponent<MeshFilter>().sharedMesh = canvas.mesh;
            canvas.properties = new MaterialPropertyBlock();
            return canvas;
        }

        internal void Begin(int sortingOrder, Vector2? lightAnchor = null, float emission = 0)
        {
            vertices.Clear(); colors.Clear(); uvs.Clear(); triangles.Clear();
            visual.sortingOrder = sortingOrder;
            properties.Clear();
            properties.SetFloat("_CombatMesh", 1);
            properties.SetFloat("_EffectEmission", emission);
            properties.SetTexture("_MainTex", Texture2D.whiteTexture);
            properties.SetVector("_GroundLightPosition", lightAnchor.HasValue
                ? new Vector4(lightAnchor.Value.x, lightAnchor.Value.y, 0, 1) : Vector4.zero);
            visual.SetPropertyBlock(properties);
        }

        internal void Pixel(Vector2 world, Color32 color, int width = 1, int height = 1)
        {
            if (color.a == 0 || vertices.Count >= 12000) return;
            float x = Mathf.Floor(world.x * 32) / 32f;
            float y = Mathf.Floor(world.y * 32) / 32f;
            int first = vertices.Count;
            vertices.Add(new Vector3(x, y, 0));
            vertices.Add(new Vector3(x + width / 32f, y, 0));
            vertices.Add(new Vector3(x + width / 32f, y + height / 32f, 0));
            vertices.Add(new Vector3(x, y + height / 32f, 0));
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = ((Color)color).linear;
            for (int i = 0; i < 4; i++) { colors.Add(color); uvs.Add(Vector2.one * 0.5f); }
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        internal void Ellipse(Vector2 center, int radiusX, int radiusY, Color32 color, bool filled = false)
        {
            radiusX = Mathf.Max(1, radiusX); radiusY = Mathf.Max(1, radiusY);
            for (int y = -radiusY; y <= radiusY; y++)
            {
                int x = Mathf.RoundToInt(radiusX * Mathf.Sqrt(Mathf.Max(0, 1 - (float)y * y / (radiusY * radiusY))));
                if (filled) Pixel(center + new Vector2(-x, y) / 32, color, x * 2 + 1);
                else
                {
                    Pixel(center + new Vector2(-x, y) / 32, color);
                    if (x > 0) Pixel(center + new Vector2(x, y) / 32, color);
                    if (Mathf.Abs(y) == radiusY) Pixel(center + new Vector2(-Mathf.Max(1, radiusX / 3), y) / 32,
                        color, Mathf.Max(3, radiusX * 2 / 3));
                }
            }
        }

        internal void End()
        {
            mesh.Clear();
            if (vertices.Count == 0) { visual.enabled = false; return; }
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            visual.enabled = true;
        }

        internal void Hide() { if (visual != null) visual.enabled = false; }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
