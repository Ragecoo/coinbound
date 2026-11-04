using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Coinbound
{
    /// <summary>A tiny vector canvas, packed into horizontal pixel runs in one dynamic mesh.</summary>
    internal sealed class DungeonCombatMesh : MonoBehaviour
    {
        internal const int Resolution = 128;
        internal const float PixelSize = 1f / 32;
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int CombatMeshId = Shader.PropertyToID("_CombatMesh");
        private static readonly int GroundLightId = Shader.PropertyToID("_GroundLightPosition");
        private static readonly int EffectEmissionId = Shader.PropertyToID("_EffectEmission");
        private static readonly int EffectIgnoreOcclusionId = Shader.PropertyToID("_EffectIgnoreOcclusion");
        private readonly Color32[] canvas = new Color32[Resolution * Resolution];
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Color32> colors = new List<Color32>(4096);
        private readonly List<Vector2> uvs = new List<Vector2>(4096);
        private readonly List<int> triangles = new List<int>(6144);
        private MaterialPropertyBlock properties;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private Vector2 lowerLeft;

        internal int PixelCount { get; private set; }
        internal int VisiblePixelCount => meshRenderer != null && meshRenderer.enabled && gameObject.activeInHierarchy ? PixelCount : 0;
        internal Vector2 LowerLeft => lowerLeft;

        internal static DungeonCombatMesh Create(Transform parent, string name, Material material)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            var canvas = obj.AddComponent<DungeonCombatMesh>();
            canvas.properties = new MaterialPropertyBlock();
            canvas.meshRenderer = obj.GetComponent<MeshRenderer>();
            canvas.meshRenderer.sharedMaterial = material != null ? material : Resources.Load<Material>("DungeonSprite");
            canvas.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            canvas.meshRenderer.receiveShadows = false;
            canvas.mesh = new Mesh { name = name + " pixel runs" };
            canvas.mesh.MarkDynamic();
            obj.GetComponent<MeshFilter>().sharedMesh = canvas.mesh;
            return canvas;
        }

        internal void Begin(Vector2 center, Vector2 feet, bool ground = false, float emission = 0, bool ignoreOcclusion = false, bool unlit = false)
        {
            Array.Clear(canvas, 0, canvas.Length);
            PixelCount = 0;
            lowerLeft = new Vector2(Mathf.Floor(center.x * 32) - Resolution / 2,
                Mathf.Floor(center.y * 32) - Resolution / 2) * PixelSize;
            transform.position = lowerLeft;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            meshRenderer.sortingOrder = ground ? 2 : DungeonDepthSort.OrderForY(feet.y) + 3;
            properties.Clear();
            properties.SetFloat(CombatMeshId, 1);
            properties.SetTexture(MainTextureId, Texture2D.whiteTexture);
            properties.SetVector(GroundLightId, ground ? Vector4.zero : new Vector4(feet.x, feet.y, 0, 1));
            properties.SetFloat(EffectEmissionId, emission);
            properties.SetFloat("_EffectUnlit", unlit ? 1 : 0);
            properties.SetFloat(EffectIgnoreOcclusionId, ignoreOcclusion ? 1 : 0);
            meshRenderer.SetPropertyBlock(properties);
        }

        internal Vector2 WorldCenter(int x, int y) => lowerLeft + new Vector2(x + 0.5f, y + 0.5f) * PixelSize;

        internal void Paint(int x, int y, Color32 color)
        {
            if (color.a == 0 || x < 0 || y < 0 || x >= Resolution || y >= Resolution) return;
            int index = y * Resolution + x;
            if (canvas[index].a == 0) PixelCount++;
            canvas[index] = color;
        }

        internal void Pixel(Vector2 point, Color32 color, int width = 1, int height = 1)
        {
            int x = Mathf.FloorToInt((point.x - lowerLeft.x) * 32) - (width - 1) / 2;
            int y = Mathf.FloorToInt((point.y - lowerLeft.y) * 32) - (height - 1) / 2;
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++) Paint(x + column, y + row, color);
        }

        internal void Line(Vector2 from, Vector2 to, Color32 color, int width = 1)
        {
            Vector2 delta = to - from;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) * 32));
            for (int i = 0; i <= steps; i++) Pixel(Vector2.Lerp(from, to, (float)i / steps), color, width, width);
        }

        internal void Glint(Vector2 point, Color32 color, int radius)
        {
            Pixel(point, color, 2, 2);
            for (int i = 1; i <= radius; i++)
            {
                Pixel(point + Vector2.right * (i * PixelSize), color);
                Pixel(point - Vector2.right * (i * PixelSize), color);
                Pixel(point + Vector2.up * (i * PixelSize), color);
                Pixel(point - Vector2.up * (i * PixelSize), color);
            }
        }

        internal void SetVisible(bool visible) => meshRenderer.enabled = visible && PixelCount > 0;

        internal void End()
        {
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            triangles.Clear();
            for (int y = 0; y < Resolution; y++)
            {
                int x = 0;
                while (x < Resolution)
                {
                    Color32 color = canvas[y * Resolution + x];
                    if (color.a == 0) { x++; continue; }
                    int first = x++;
                    while (x < Resolution && Same(color, canvas[y * Resolution + x])) x++;
                    AddRun(first, x, y, color);
                }
            }
            mesh.Clear();
            if (vertices.Count == 0) { meshRenderer.enabled = false; return; }
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshRenderer.enabled = true;
        }

        private static bool Same(Color32 left, Color32 right) => left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;

        private void AddRun(int first, int last, int row, Color32 color)
        {
            int index = vertices.Count;
            float x0 = first * PixelSize, x1 = last * PixelSize, y0 = row * PixelSize, y1 = (row + 1) * PixelSize;
            vertices.Add(new Vector3(x0, y0, 0));
            vertices.Add(new Vector3(x1, y0, 0));
            vertices.Add(new Vector3(x1, y1, 0));
            vertices.Add(new Vector3(x0, y1, 0));
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = ((Color)color).linear;
            for (int i = 0; i < 4; i++) { colors.Add(color); uvs.Add(Vector2.one * 0.5f); }
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
