using System.Collections;
using NUnit.Framework;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Coinbound.Tests
{
    public sealed class DungeonLightingTests
    {
        [Test]
        public void RaysStopAtWallsAndDoNotSeeTheRoomBehindThem()
        {
            var generator = new DungeonGenerator(new GenerationSettings(Seed: 42));
            foreach (var step in generator.Generate()) { }
            DungeonMap map = generator.Map;
            Vector2 origin = DungeonView.WorldPosition(map.EntranceTile.Value);
            Assert.That(DungeonVisibility.Cast(map, origin, Vector2.left, 20), Is.EqualTo(4.5f).Within(0.001));
            Assert.That(DungeonVisibility.Cast(map, origin, Vector2.up, 20), Is.EqualTo(4.5f).Within(0.001));
            Assert.That(DungeonVisibility.Cast(map, origin, Vector2.right, 20), Is.EqualTo(4.5f).Within(0.001));
            Assert.That(DungeonVisibility.Cast(map, origin, Vector2.right, 2), Is.EqualTo(2));
            Assert.That(DungeonVisibility.Cast(map, origin, new Vector2(-1, 1), 20),
                Is.EqualTo(Mathf.Sqrt(4.5f * 4.5f * 2)).Within(0.001));
            Assert.That(DungeonVisibility.Cast(map, new Vector2(-1, -1), Vector2.right, 20), Is.Zero);

            // Check actual occlusion at many sub-tile positions, independently of the renderer.
            foreach (Vector2 shift in new[] { Vector2.zero, new Vector2(0.19f, -0.27f), new Vector2(-0.4f, 0.35f) })
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64 + 0.0123f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 start = origin + shift;
                float hit = DungeonVisibility.Cast(map, start, direction, 8);
                for (float distance = 0; distance < hit - 0.002f; distance += 0.025f)
                    Assert.That(IsFloor(map, start + direction * distance), Is.True, "Sight crossed a solid cell.");
                if (hit < 7.998f)
                    Assert.That(IsFloor(map, start + direction * (hit + 0.002f)), Is.False, "Ray did not stop on a wall.");
            }
        }

        [UnityTest]
        public IEnumerator CtrlWheelChangesZoomWithinLimitsWithoutChangingLightRadius()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game = Object.FindAnyObjectByType<DungeonGame>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0}", game);
            game.StartNewDungeon();
            float timeout = Time.realtimeSinceStartup + 30;
            while (!game.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(game.IsReady, Is.True);
            var torch = Object.FindAnyObjectByType<DungeonTorch>();
            Assert.That(torch, Is.Not.Null);
            Assert.That(torch.ShadowMap.width, Is.EqualTo(2048));
            Assert.That(torch.Radius, Is.EqualTo(8));
            Camera camera = Camera.main;
            Assert.That(camera.orthographicSize, Is.EqualTo(6.5f).Within(0.001));
            // Batchmode has no focused Game View. Route these synthetic devices to the game.
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keys = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(camera.orthographicSize, Is.EqualTo(6.5f).Within(0.001), "Wheel alone must not zoom.");
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.LeftCtrl));
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
                yield return new WaitForSecondsRealtime(0.35f);
                float zoomedOut = camera.orthographicSize;
                Assert.That(zoomedOut, Is.GreaterThan(6.5f));
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 240) });
                yield return new WaitForSecondsRealtime(0.35f);
                Assert.That(camera.orthographicSize, Is.LessThan(zoomedOut));
                for (int i = 0; i < 12; i++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 480) });
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(camera.orthographicSize, Is.EqualTo(3.5f).Within(0.03));
                for (int i = 0; i < 12; i++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -480) });
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.That(camera.orthographicSize, Is.EqualTo(12).Within(0.03));
                Assert.That(torch.Radius, Is.EqualTo(8), "Zooming must not increase the light radius.");
                float width = camera.orthographicSize * camera.aspect;
                Assert.That(camera.transform.position.x - width, Is.GreaterThanOrEqualTo(-0.001f));
                Assert.That(camera.transform.position.x + width, Is.LessThanOrEqualTo(game.Map.TileWidth + 0.001f));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keys);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            }
        }

        private static bool IsFloor(DungeonMap map, Vector2 world) =>
            map.IsTileFloor(new TilePosition(Mathf.FloorToInt(world.x), Mathf.FloorToInt(1 - world.y)));
    }
}
