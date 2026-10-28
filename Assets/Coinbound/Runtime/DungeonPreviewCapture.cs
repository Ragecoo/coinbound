#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Coinbound
{
    // Opt-in visual smoke check for development builds. Normal launches never add this component.
    // Coinbound.exe -coinbound-capture <output-directory> renders real UI / gameplay frames, then exits.
    internal sealed class DungeonPreviewCapture : MonoBehaviour
    {
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "-coinbound-capture");
            if (flag < 0 || flag + 1 >= args.Length) return;
            var capture = new GameObject("Preview capture").AddComponent<DungeonPreviewCapture>();
            capture.output = Path.GetFullPath(args[flag + 1]);
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Directory.CreateDirectory(output);
            var game = FindAnyObjectByType<DungeonGame>();
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42}", game);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("01-menu");
            yield return new WaitForSecondsRealtime(0.2f);
            GameObject.Find("Start").GetComponent<Button>().onClick.Invoke();
            bool capturedLoading = false;
            float timeout = Time.realtimeSinceStartup + 30;
            while (!game.IsReady && Time.realtimeSinceStartup < timeout)
            {
                if (!capturedLoading && GameObject.Find("Status")?.GetComponent<Text>().text == "Прокладывание коридоров лабиринта...")
                {
                    yield return null;
                    yield return Capture("02-loading");
                    capturedLoading = true;
                }
                yield return null;
            }
            if (!game.IsReady) { Debug.LogError("Preview generation timed out."); Application.Quit(1); yield break; }
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("03-entrance");
            yield return new WaitForSecondsRealtime(0.2f);
            var player = FindAnyObjectByType<DungeonPlayer>();
            var body = player.GetComponent<Rigidbody2D>();
            body.simulated = false;
            if (game.Spawns.Enemies.Count > 0)
            {
                player.transform.position = DungeonView.WorldPosition(game.Spawns.Enemies[0].Position);
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Capture("04-enemies");
            }
            if (game.Spawns.Chests.Count > 0)
            {
                player.transform.position = DungeonView.WorldPosition(game.Spawns.Chests[0].Position) + Vector3.right;
                yield return new WaitForSecondsRealtime(0.2f);
                yield return Capture("05-chest");
            }
            player.transform.position = DungeonView.WorldPosition(game.Map.ExitTile.Value);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Capture("06-exit");
            var corner = game.Map.Floors().First(cell =>
            {
                Cell[] neighbors = game.Map.Neighbors(cell).ToArray();
                return game.Map.GetSection(cell) == TileKind.Corridor && neighbors.Length == 2 &&
                       neighbors[0].X != neighbors[1].X && neighbors[0].Y != neighbors[1].Y;
            });
            player.transform.position = DungeonView.WorldPosition(game.Map.SectionCenter(corner));
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Capture("07-corner-shadows");
            // The capture window is hidden; only this opt-in check should accept background input.
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            for (int i = 0; i < 6; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -120) });
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("08-zoom-out");
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackground;
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("Coinbound visual capture completed.");
            Application.Quit(0);
        }

        private IEnumerator Capture(string name)
        {
            // Explicit rendering also works when the preview's OS window is hidden.
            Camera camera = Camera.main;
            Canvas canvas = FindAnyObjectByType<DungeonScreen>().GetComponent<Canvas>();
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            int previousSorting = canvas.sortingOrder;
            bool previousOverride = canvas.overrideSorting;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                // Switching out of overlay mode makes the canvas participate in world sorting.
                canvas.overrideSorting = true;
                canvas.sortingOrder = short.MaxValue;
                // Let canvas transforms and clipping settle after changing render modes.
                yield return null;
                foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>()) graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                canvas.overrideSorting = previousOverride;
                canvas.sortingOrder = previousSorting;
                RenderTexture.active = previousActive;
                target.Release();
                Destroy(target);
                Destroy(pixels);
            }
        }
    }
}
#endif
