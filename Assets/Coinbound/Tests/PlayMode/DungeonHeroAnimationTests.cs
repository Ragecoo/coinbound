using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Coinbound.Tests
{
    public sealed class DungeonHeroAnimationTests
    {
        [Test]
        public void HeroArtHasCompleteNativeFramesWithStableSizeAndUncutWeapons()
        {
            foreach (DungeonHero hero in DungeonHero.All)
            {
                var facingHeights = new List<int>();
                foreach (string direction in new[] { "down", "left", "right", "up" })
                {
                    string path = "HeroArt/" + hero.ArtFolder + "/" + direction;
                    Texture2D sheet = Resources.Load<Texture2D>(path);
                    Assert.That(sheet, Is.Not.Null, path);
                    Assert.That(new Vector2Int(sheet.width, sheet.height), Is.EqualTo(new Vector2Int(256, 320)), path);
                    Assert.That(sheet.isReadable, Is.True, path);
                    Assert.That(sheet.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(sheet.mipmapCount, Is.EqualTo(1), path);
                    Color32[] pixels = sheet.GetPixels32();
                    var idleHeights = new List<int>();
                    int states = hero.IsArcher ? 4 : 5;
                    for (int row = 0; row < states; row++)
                    {
                        var distinctFrames = new HashSet<uint>();
                        for (int frame = 0; frame < 8; frame++)
                        {
                            string label = path + " row " + row + " frame " + frame;
                            int opaque = 0, footPixels = 0, boundaryPixels = 0, low = 64, high = -1;
                            bool crisp = true;
                            uint hash = 2166136261;
                            for (int y = 0; y < 64; y++)
                            for (int x = 0; x < 32; x++)
                            {
                                Color32 color = pixels[((4 - row) * 64 + y) * sheet.width + frame * 32 + x];
                                crisp &= color.a == 0 || color.a == 255;
                                if (color.a > 0)
                                {
                                    opaque++;
                                    low = Mathf.Min(low, y); high = Mathf.Max(high, y);
                                    if (x >= 4 && x < 28 && y >= 6 && y <= 10) footPixels++;
                                    if (y < 4 || y > 62) boundaryPixels++;
                                }
                                hash = unchecked((hash ^ (uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24)) * 16777619);
                            }
                            Assert.That(crisp, Is.True, label + " has a blurred alpha edge.");
                            Assert.That(boundaryPixels, Is.Zero, label + " touches the top/bottom crop boundary.");
                            Assert.That(opaque, Is.InRange(150, 1600), label + " is missing or contains a filled background.");
                            Assert.That(footPixels, Is.GreaterThan(0), label + " has lost its common ground contact.");
                            distinctFrames.Add(hash);
                            if (row == 0) idleHeights.Add(high - low + 1);
                        }
                        Assert.That(distinctFrames.Count, Is.GreaterThanOrEqualTo(row == 0 || row == 4 ? 2 : 4),
                            path + " row " + row + " must contain authored motion, not repeated stills.");
                    }
                    idleHeights.Sort();
                    facingHeights.Add(idleHeights[idleHeights.Count / 2]);
                }
                facingHeights.Sort();
                Assert.That(facingHeights[facingHeights.Count - 1] - facingHeights[0], Is.LessThanOrEqualTo(3),
                    hero.DisplayName + " visibly changes body size when turning.");
            }
        }

        [Test]
        public void RuntimePortraitShrinksAroundFeetWithoutChangingWorldPixelSize()
        {
            foreach (DungeonHero hero in DungeonHero.All)
            {
                Texture2D original = Resources.Load<Texture2D>("HeroArt/" + hero.ArtFolder + "/down");
                Sprite portrait = DungeonHeroAnimator.GetPortrait(hero);
                Assert.That(portrait, Is.SameAs(DungeonHeroAnimator.GetPortrait(hero)), "UI and world must borrow the same prepared art.");
                Assert.That(portrait.texture, Is.Not.SameAs(original), "Preparing the display art must leave the original asset untouched.");
                Assert.That(portrait.pixelsPerUnit, Is.EqualTo(32));
                Assert.That(portrait.pivot, Is.EqualTo(new Vector2(16, 8)));
                Assert.That(portrait.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(portrait.texture.mipmapCount, Is.EqualTo(1));
                RectInt before = OpaqueBounds(original, portrait.rect);
                RectInt after = OpaqueBounds(portrait.texture, portrait.rect);
                Assert.That(after.width, Is.EqualTo(before.width * 0.8f).Within(1));
                Assert.That(after.height, Is.EqualTo(before.height * 0.8f).Within(1));
                Assert.That(after.yMin, Is.EqualTo(before.yMin).Within(1), "The feet must stay on the same ground pixel.");
                bool crisp = true, cleanPadding = true;
                foreach (Color32 pixel in portrait.texture.GetPixels32())
                {
                    crisp &= pixel.a == 0 || pixel.a == 255;
                    if (pixel.a == 0) cleanPadding &= (pixel.r | pixel.g | pixel.b) == 0;
                }
                Assert.That(crisp, Is.True, "Native shrink must not create blurred alpha.");
                Assert.That(cleanPadding, Is.True, "Transparent padding must not add colored fringes.");
                Color32[] sourcePixels = original.GetPixels32();
                Color32[] displayPixels = portrait.texture.GetPixels32();
                var sourcePalette = new HashSet<uint>();
                var displayPalette = new HashSet<uint>();
                int inkPixels = 0;
                bool unchangedSilhouette = true;
                for (int y = 0; y < original.height; y++)
                for (int x = 0; x < original.width; x++)
                {
                    int index = y * original.width + x;
                    Color32 source = sourcePixels[index], display = displayPixels[index];
                    if (source.a != 0) sourcePalette.Add(ColorKey(source));
                    if (display.a != 0)
                    {
                        displayPalette.Add(ColorKey(display));
                        if (ColorKey(display) == ColorKey(DungeonHeroAnimator.SilhouetteInk)) inkPixels++;
                    }
                    // Styling may recolor a native pixel, but must never grow/shrink its
                    // coverage, erase a narrow weapon, or change any frame's foot contact.
                    int sx = Mathf.FloorToInt((x % 32 + 0.5f - 16) / 0.8f + 16);
                    int sy = Mathf.FloorToInt((y % 64 + 0.5f - 8) / 0.8f + 8);
                    byte expectedAlpha = sx < 0 || sx >= 32 || sy < 0 || sy >= 64 ? (byte)0
                        : sourcePixels[(y / 64 * 64 + sy) * original.width + x / 32 * 32 + sx].a;
                    unchangedSilhouette &= display.a == expectedAlpha;
                }
                Assert.That(unchangedSilhouette, Is.True, "Palette cleanup must preserve every frame's exact native silhouette.");
                Assert.That(displayPalette.Count, Is.LessThan(sourcePalette.Count), "Near-duplicate colors should form a cleaner shared palette.");
                Assert.That(inkPixels, Is.GreaterThan(0), "The thin internal indigo contour is missing.");
            }
        }

        [UnityTest]
        public IEnumerator ProductionPlayerAnimatesMovementActionsAndPauseWithoutMovingItsFootAnchor()
        {
            var fixture = new GameObject("Hero animation fixture");
            var eventSystems = new List<EventSystem>();
            foreach (EventSystem system in Object.FindObjectsByType<EventSystem>())
                if (system.enabled) { eventSystems.Add(system); system.enabled = false; }
            InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode editor = InputSystem.settings.editorInputBehaviorInPlayMode;
            float timeScale = Time.timeScale;
            Keyboard keyboard = null;
            Mouse mouse = null;
            Gamepad pad = null;
            try
            {
                Time.timeScale = 1;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                pad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.up });
                InputSystem.Update();

                var view = fixture.AddComponent<DungeonView>();
                DungeonPlayer player = view.CreatePlayer(new TilePosition(2048, -2048), DungeonHero.First);
                SpriteRenderer visual = player.transform.Find("Hero visual").GetComponent<SpriteRenderer>();
                var feet = player.GetComponent<BoxCollider2D>();
                Assert.That(feet, Is.Not.Null, "The player's feet need the requested square collision shape.");
                Assert.That(feet.size.x, Is.EqualTo(feet.size.y));
                Assert.That(feet.size.x, Is.InRange(0.3f, 0.4f), "The tall art must not turn the whole body into floor collision.");
                Assert.That(feet.offset, Is.EqualTo(Vector2.zero));
                Vector2 footSize = feet.size;
                player.SetInputEnabled(true);
                Assert.That(player.CanAct, Is.True, "Run this test with Game View focused or in batchmode.");
                yield return null;
                yield return null;
                AssertPose(visual, "up", 0);
                AssertFootAnchor(player, visual, feet, footSize);

                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right, rightStick = Vector2.up });
                yield return new WaitForFixedUpdate();
                yield return null;
                yield return null;
                AssertPose(visual, "right", 1);
                Sprite runStart = visual.sprite;
                yield return new WaitForSeconds(0.16f);
                Assert.That(visual.sprite, Is.Not.SameAs(runStart), "Moving must advance real run frames.");
                AssertFootAnchor(player, visual, feet, footSize);

                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.right });
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.That(player.TryAttack(Vector2.left), Is.True);
                yield return null;
                yield return null;
                AssertPose(visual, "left", 2);
                Sprite attackStart = visual.sprite;
                yield return new WaitForSeconds(0.1f);
                AssertPose(visual, "left", 2); // Live aim points right; committed attack must remain left.
                Assert.That(visual.sprite, Is.Not.SameAs(attackStart));

                player.SetInputEnabled(false);
                Sprite paused = visual.sprite;
                float progress = player.AttackProgress;
                yield return new WaitForSeconds(0.18f);
                Assert.That(visual.sprite, Is.SameAs(paused), "Menu pause must freeze the displayed action pose.");
                Assert.That(player.AttackProgress, Is.EqualTo(progress));
                AssertFootAnchor(player, visual, feet, footSize);

                player.SetInputEnabled(true);
                Assert.That(player.TryRoll(Vector2.up), Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = Vector2.left });
                yield return null;
                yield return null;
                AssertPose(visual, "up", 3);
                Sprite rollStart = visual.sprite;
                yield return new WaitForSeconds(0.09f);
                AssertPose(visual, "up", 3);
                Assert.That(visual.sprite, Is.Not.SameAs(rollStart));
                AssertFootAnchor(player, visual, feet, footSize);
                yield return new WaitForSeconds(0.3f);
                AssertPose(visual, "left", 0);

                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                yield return null;
                Assert.That(player.IsBlocking, Is.True);
                AssertPose(visual, "left", 4);
                AssertFootAnchor(player, visual, feet, footSize);

            }
            finally
            {
                Object.Destroy(fixture);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editor;
                Time.timeScale = timeScale;
                foreach (EventSystem system in eventSystems) if (system != null) system.enabled = true;
            }
            yield return null;
        }

        private static void AssertPose(SpriteRenderer visual, string direction, int row)
        {
            Assert.That(visual.sprite.name, Does.StartWith("01/" + direction + "/"));
            Assert.That(visual.sprite.rect.y, Is.EqualTo((4 - row) * 64));
        }

        private static RectInt OpaqueBounds(Texture2D texture, Rect rect)
        {
            Color32[] pixels = texture.GetPixels32();
            int left = (int)rect.xMax, right = -1, bottom = (int)rect.yMax, top = -1;
            for (int y = (int)rect.yMin; y < rect.yMax; y++)
            for (int x = (int)rect.xMin; x < rect.xMax; x++)
            {
                if (pixels[y * texture.width + x].a == 0) continue;
                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
            Assert.That(right, Is.GreaterThanOrEqualTo(left), "The prepared pose must not be empty.");
            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }

        private static uint ColorKey(Color32 color) => (uint)(color.r << 16 | color.g << 8 | color.b);

        private static void AssertFootAnchor(DungeonPlayer player, SpriteRenderer visual, BoxCollider2D feet, Vector2 originalSize)
        {
            Assert.That(visual.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(visual.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(visual.sprite.pivot, Is.EqualTo(new Vector2(16, 8)));
            Assert.That(visual.sprite.pixelsPerUnit, Is.EqualTo(32));
            Assert.That(feet.size, Is.EqualTo(originalSize));
            var properties = new MaterialPropertyBlock();
            visual.GetPropertyBlock(properties);
            Vector4 light = properties.GetVector("_GroundLightPosition");
            // A test coroutine can resume between physics/Update and the renderer's LateUpdate.
            float frameTravel = Mathf.Max(0.02f, player.Velocity.magnitude * Time.deltaTime * 2 + 0.02f);
            Assert.That(light.w, Is.EqualTo(1));
            Assert.That(Vector2.Distance(new Vector2(light.x, light.y), player.transform.position), Is.LessThan(frameTravel),
                "Changing animation frames must preserve the feet-based light position.");
        }
    }
}
