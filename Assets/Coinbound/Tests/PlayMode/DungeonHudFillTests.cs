using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Coinbound.Tests
{
    public sealed class DungeonHudFillTests
    {
        [UnityTest]
        public IEnumerator OnlyAnimatedLossEmitsResourcePixels()
        {
            var root = new GameObject("Resource loss fixture", typeof(RectTransform));
            var texture = new Texture2D(20, 2, TextureFormat.RGBA32, false);
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 1;
                var pixels = new Color32[40];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels32(pixels); texture.Apply();
                var fill = root.AddComponent<DungeonHudFill>();
                fill.rectTransform.sizeDelta = new Vector2(20, 2);
                fill.Initialize(texture);
                int lost = 0;
                int restored = 0;
                fill.PixelRestored += (point, tint) => restored++;
                var detached = new System.Collections.Generic.HashSet<Vector2>();
                fill.PixelDetached += (point, tint) => {
                    Assert.That(tint, Is.EqualTo((Color32)Color.white));
                    Assert.That(point.x, Is.InRange(0f, 9f));
                    Assert.That(point.y, Is.InRange(-1f, 0f));
                    Assert.That(detached.Add(point), Is.True, "A removed texel must detach only once.");
                };
                fill.PixelsLost += (point, count) => lost += count;
                fill.SetFraction(.5f);
                Assert.That(lost, Is.Zero, "Binding a run must not spray particles.");
                Assert.That(restored, Is.Zero);
                fill.AnimateTo(1);
                yield return new WaitForSeconds(.3f);
                Assert.That(lost, Is.Zero, "Restoring a resource must not look like damage.");
                Assert.That(restored, Is.GreaterThan(0));
                int previousRestored = restored;
                fill.AnimateTo(.5f);
                yield return new WaitForSeconds(.3f);
                Assert.That(lost, Is.EqualTo(10), "Each removed source column is emitted exactly once.");
                Assert.That(detached.Count, Is.EqualTo(20), "Both rows must detach at their original coordinates.");
                Assert.That(restored, Is.EqualTo(previousRestored));
                yield return new WaitForSeconds(.1f);
                Assert.That(lost, Is.EqualTo(10), "A stable resource must not emit again.");
            }
            finally
            {
                Time.timeScale = previousScale;
                Object.DestroyImmediate(root); Object.DestroyImmediate(texture);
            }
        }

        [TestCase(1f, 8)]
        [TestCase(.5f, 6)]
        [TestCase(0f, 0)]
        public void FillTranslatesAngledEdgeAndEmptiesCompletely(float fraction, int expectedRight)
        {
            var root = new GameObject("Fill fixture", typeof(RectTransform));
            var texture = new Texture2D(8, 2, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[16];
                for (int x = 0; x < 8; x++) pixels[x] = Color.white;
                for (int x = 0; x < 4; x++) pixels[8 + x] = Color.white;
                texture.SetPixels32(pixels); texture.Apply();
                var fill = root.AddComponent<DungeonHudFill>();
                fill.rectTransform.pivot = Vector2.zero;
                fill.rectTransform.sizeDelta = new Vector2(8, 2);
                fill.Initialize(texture); fill.SetFraction(fraction);
                using var mesh = new VertexHelper();
                typeof(DungeonHudFill).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(fill, new object[] { mesh });
                if (fraction == 0) Assert.That(mesh.currentVertCount, Is.Zero);
                else
                {
                    UIVertex vertex = default;
                    mesh.PopulateUIVertex(ref vertex, 2);
                    Assert.That(vertex.position.x, Is.EqualTo(expectedRight));
                    mesh.PopulateUIVertex(ref vertex, 6);
                    Assert.That(vertex.position.x, Is.EqualTo(expectedRight - 4));
                }
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(texture); }
        }
    }
}
