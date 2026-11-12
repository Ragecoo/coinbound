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
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("01-menu-ambient");
            yield return new WaitForSecondsRealtime(0.2f);
            GameObject.Find("Start").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            var screen = FindAnyObjectByType<DungeonScreen>();
            if (screen.CurrentPage != "Story intro")
            {
                Debug.LogError("Preview did not enter the story intro.");
                Application.Quit(1); yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture("02-intro");
            screen.HandleBack();
            yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("02-heroes");
            GameObject.Find("Hero 1").GetComponent<Button>().onClick.Invoke();
            yield return Capture("02-heroes-archer");
            GameObject.Find("Create dungeon").GetComponent<Button>().onClick.Invoke();
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
            screen.ShowSettings();
            yield return Capture("03-new-settings");
            screen.CloseSettings();
            var numberPlayer = FindAnyObjectByType<DungeonPlayer>();
            DungeonDamageNumber.Create(numberPlayer, numberPlayer.transform.position + Vector3.left, 2, false, false);
            DungeonDamageNumber.Create(numberPlayer, numberPlayer.transform.position + Vector3.right, 3, false, true);
            DungeonDamageNumber.Create(numberPlayer, numberPlayer.transform.position + Vector3.down, 10, true, false);
            yield return new WaitForSecondsRealtime(.12f);
            yield return Capture("03-damage-numbers");

            if(Environment.GetCommandLineArgs().Contains("-coinbound-results-capture"))
            {
                foreach(var enemy in DungeonEnemy.Active.ToArray())enemy.enabled=false;
                numberPlayer.ReceiveRelic(DungeonRelics.All[0]);numberPlayer.ReceiveRelic(DungeonRelics.All[0]);numberPlayer.AwardScore(12345);
                float hp=numberPlayer.Health;
                foreach(int floor in new[]{1,2,1,0})
                {
                    if(!game.DebugTravelFloor(floor))throw new InvalidOperationException("Debug floor travel rejected");
                    yield return new WaitForSecondsRealtime(.6f);
                    numberPlayer=FindAnyObjectByType<DungeonPlayer>();
                    foreach(var enemy in DungeonEnemy.Active.ToArray())enemy.enabled=false;
                    if(game.FloorIndex!=floor||numberPlayer.Relics.Items.Count!=2||numberPlayer.Score!=12345||numberPlayer.Health!=hp)
                        throw new InvalidOperationException("Debug floor travel lost player state");
                }
                for(int i=0;i<30;i++)numberPlayer.ReceiveRelic(DungeonRelics.All[i]);
                var record=DungeonRunResult.Capture(numberPlayer,1234);record.name="Путник";
                numberPlayer.SetInputEnabled(false);Time.timeScale=0;
                screen.ShowVictory(record.score,record);yield return new WaitForSecondsRealtime(.6f);
                GameObject.Find("Run name").GetComponent<InputField>().text=record.name;
                yield return Capture("50-name-entry");
                var records=new System.Collections.Generic.List<DungeonRunResult>();
                for(int i=0;i<15;i++){var copy=JsonUtility.FromJson<DungeonRunResult>(JsonUtility.ToJson(record));copy.name="Путник "+(i+1);copy.score-=i*100;records.Add(copy);}
                screen.ShowLeaderboard(records);yield return new WaitForSecondsRealtime(.6f);
                FindAnyObjectByType<DungeonResultHover>().enter();yield return null;
                yield return Capture("51-leaderboard");
                Debug.Log("Results capture and debug floor round trip passed; no leaderboard files written.");Application.Quit();yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-coinbound-floors-capture"))
            {
                for(int floor=0;floor<3;floor++)
                {
                    var owner=FindAnyObjectByType<DungeonPlayer>();
                    foreach(var enemy in DungeonEnemy.Active.ToArray())enemy.enabled=false;
                    var environment=DungeonProps.Current;
                    var dense=environment.Items.GroupBy(p=>p.Room).Where(g=>g.Key>=0).OrderByDescending(g=>g.Count()).First();
                    var roomBounds=DungeonProps.RoomBounds(game.Map.Rooms[dense.Key]);
                    var topProp=dense.OrderByDescending(p=>p.transform.position.y).First();
                    owner.TeleportTo(new Vector2(Mathf.Clamp(topProp.transform.position.x,roomBounds.xMin+2,roomBounds.xMax-2),roomBounds.yMax-3));
                    yield return new WaitForSecondsRealtime(.4f);yield return Capture("40-floor-"+(floor+1));
                    var exit=FindAnyObjectByType<DungeonExit>();owner.TeleportTo((Vector2)exit.transform.position+Vector2.down*1.4f);
                    yield return new WaitForSecondsRealtime(.4f);yield return Capture("41-barrier-"+(floor+1));
                    foreach(var enemy in owner.transform.parent.GetComponentsInChildren<DungeonEnemy>())enemy.ReceiveHit(9999,Vector2.right,0,false);
                    exit.RefreshBarrier();yield return new WaitForSecondsRealtime(.3f);yield return Capture("42-open-"+(floor+1));
                    if(floor<2){if(!game.TryAscend(exit))throw new InvalidOperationException("Capture could not ascend from floor "+floor);yield return new WaitForSecondsRealtime(.6f);if(game.FloorIndex!=floor+1)throw new InvalidOperationException("Capture floor did not change");}
                    else {owner.SetInputEnabled(false);screen.ShowVictory(owner.Score);yield return new WaitForSecondsRealtime(.6f);yield return Capture("43-victory");}
                }
                Debug.Log("Three floor capture completed");Application.Quit();yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-coinbound-healing-capture"))
            {
                foreach(var enemy in DungeonEnemy.Active.ToArray())enemy.enabled=false;
                numberPlayer.TakeDamage(30,numberPlayer.transform.position+Vector3.left);
                yield return new WaitForSecondsRealtime(.5f);
                typeof(DungeonPlayer).GetMethod("AdvanceHealthRecovery",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(numberPlayer,new object[]{8f});
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Capture("30-health-recovery");
                Debug.Log("Healing capture completed: hp="+numberPlayer.Health+" particles="+FindAnyObjectByType<DungeonHudParticles>().ActiveCount);
                Application.Quit();yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-coinbound-environment-capture"))
            {
                foreach(var enemy in DungeonEnemy.Active.ToArray())enemy.enabled=false;
                var environment=DungeonProps.Current;
                var rooms=environment.Items.Where(p=>p.Room>=0).GroupBy(p=>p.Room).OrderByDescending(g=>g.Count()).Take(4).Select(g=>g.Key).ToArray();
                int shot=0;
                foreach(int room in rooms)
                {
                    numberPlayer.TeleportTo(DungeonProps.RoomBounds(game.Map.Rooms[room]).center);
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return Capture("20-environment-room-"+shot++);
                }
                var corridorProp=environment.Items.FirstOrDefault(p=>p.Room<0);
                if(corridorProp!=null)
                {
                    numberPlayer.TeleportTo(corridorProp.transform.position);
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return Capture("20-corridor-decoration");
                }
                var torchPositions=(System.Collections.Generic.List<Vector2>)typeof(DungeonProps).GetField("torches",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(environment);
                foreach(var torch in torchPositions)
                {
                    bool left=!game.Map.IsTileFloor(new ProceduralGeneration.TilePosition(Mathf.FloorToInt(torch.x-.25f),Mathf.FloorToInt(1-torch.y)));
                    bool right=!game.Map.IsTileFloor(new ProceduralGeneration.TilePosition(Mathf.FloorToInt(torch.x+.25f),Mathf.FloorToInt(1-torch.y)));
                    if(!left&&!right)continue;
                    numberPlayer.TeleportTo(torch+Vector2.right*(left?1.5f:-1.5f));
                    yield return new WaitForSecondsRealtime(.5f);yield return Capture("24-side-torch");break;
                }
                var animatedFountain=FindAnyObjectByType<DungeonFountain>();
                var fountainBounds=animatedFountain.GetComponent<Collider2D>().bounds;
                numberPlayer.TeleportTo(new Vector2(fountainBounds.center.x,fountainBounds.min.y-.5f));
                yield return new WaitForSecondsRealtime(.5f);yield return Capture("25-water-frame-a");
                yield return new WaitForSecondsRealtime(.25f);yield return Capture("26-water-frame-b");
                var breakable=environment.Items.First(p=>p.Definition.hp>0);
                numberPlayer.TeleportTo((Vector2)breakable.transform.position+Vector2.down*1.6f);
                yield return new WaitForSecondsRealtime(.5f);yield return Capture("21-prop-before");
                breakable.ReceiveHit(999,DungeonProps.NewAttackId());
                yield return new WaitForSecondsRealtime(.12f);yield return Capture("22-prop-burst");
                yield return new WaitForSecondsRealtime(.6f);yield return Capture("23-prop-after");
                yield return new WaitForSecondsRealtime(2);
                string metrics="props="+environment.Items.Count+" torches="+environment.TorchCount+" frames="+environment.MeasuredFrames+
                    " mean_update_ms="+(environment.TotalUpdateMilliseconds/System.Math.Max(1,environment.MeasuredFrames)).ToString("F4")+
                    " max_update_ms="+environment.MaximumUpdateMilliseconds.ToString("F4");
                File.WriteAllText(Path.Combine(output,"environment-performance.txt"),metrics);
                Debug.Log("Environment capture completed: "+metrics);Application.Quit();yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-coinbound-fountain-capture"))
            {
                var fountain=FindAnyObjectByType<DungeonFountain>();
                var basin=fountain.GetComponent<Collider2D>().bounds;
                numberPlayer.TeleportTo(new Vector2(basin.center.x,basin.min.y-.4f));
                yield return new WaitForSecondsRealtime(.3f);
                screen.ShowFountain(numberPlayer,fountain);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("17-fountain-ready");
                screen.BeginCoinToss();
                yield return new WaitForSecondsRealtime(.7f);
                yield return Capture("18-fountain-throw");
                Application.Quit();yield break;
            }
            if(Environment.GetCommandLineArgs().Contains("-coinbound-loot-capture"))
            {
                var owner=FindAnyObjectByType<DungeonPlayer>();
                Vector2 chestCaptureStart=owner.transform.position;
                var lootObject=new GameObject("Capture relic chest",typeof(SpriteRenderer));
                lootObject.transform.SetParent(owner.transform.parent);
                // Isolate the UI capture away from enemies and dungeon walls.
                owner.TeleportTo(new Vector2(7000,7000));
                lootObject.transform.position=new Vector2(7001,7000);
                var chest=lootObject.AddComponent<DungeonChest>();chest.Initialize("s");
                owner.SetAimDirection(Vector2.right);Physics2D.SyncTransforms();
                if(!chest.TryOpen(owner)){Debug.LogError("Loot capture could not open chest.");Application.Quit(1);yield break;}
                yield return new WaitForSecondsRealtime(.8f);
                // Synthetic landmarks for visual composition only, not a camera recognition test.
                var hand=new Vector3[21];hand[0]=new Vector3(.58f,.94f,0);
                hand[1]=new Vector3(.59f,.80f,0);hand[2]=new Vector3(.58f,.65f,0);hand[3]=new Vector3(.55f,.48f,0);hand[4]=new Vector3(.51f,.31f,0);
                for(int f=0;f<4;f++)
                {int m=5+f*4;float y=.59f+f*.085f;hand[m]=new Vector3(.49f,y,0);hand[m+1]=new Vector3(.35f,y,0);hand[m+2]=new Vector3(.32f,y+.035f,0);hand[m+3]=new Vector3(.43f,y+.045f,0);}
                var cameraInput=FindAnyObjectByType<DungeonCoinCamera>();
                typeof(DungeonCoinCamera).GetMethod("DrawSkeleton",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(cameraInput,new object[]{hand});
                yield return null;
                yield return Capture("09-relic-offer");
                screen.BeginCoinToss();yield return new WaitForSecondsRealtime(1f);
                yield return Capture("10-coin-toss");
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Capture("11-relic-result");
                screen.HandleBack();yield return new WaitForSecondsRealtime(.4f);
                yield return Capture("12-relic-acquired");
                if(owner.Relics.Items.Count!=1){Debug.LogError("Loot capture expected exactly one relic.");Application.Quit(1);yield break;}
                screen.ToggleDebugRelics();yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("13-debug-relic-menu");
                screen.GrantDebugRelic(0);screen.GrantDebugRelic(0);screen.GrantDebugRelic(1);screen.GrantDebugRelic(1);screen.GrantDebugRelic(2);
                screen.HandleBack();yield return new WaitForSecondsRealtime(.3f);
                yield return Capture("14-duplicate-relics");
                screen.ToggleDebugRelics();for(int i=0;i<30;i++)screen.GrantDebugRelic(i%32);screen.HandleBack();
                yield return new WaitForSecondsRealtime(.2f);
                var inventoryScroll=screen.GetComponentsInChildren<ScrollRect>().First(x=>x.name=="Relic viewport");
                Canvas.ForceUpdateCanvases();inventoryScroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                Vector2 hover=RectTransformUtility.WorldToScreenPoint(null,inventoryScroll.content.GetChild(inventoryScroll.content.childCount-1).position);
                if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
                if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.LeftCtrl));
                InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=hover});
                yield return new WaitForSecondsRealtime(.2f);
                yield return Capture("15-relic-scroll-tooltip");
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                owner.TeleportTo(chestCaptureStart);owner.SetAimDirection(Vector2.right);
                typeof(DungeonPlayer).GetMethod("SpawnDebugChest",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(owner,null);
                yield return new WaitForSecondsRealtime(1f);
                yield return Capture("16-indigo-chest");
                Debug.Log("Coinbound relic capture completed.");Application.Quit(0);yield break;
            }
            game.ShowMenu();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("04-pause");
            screen.ShowSettings();
            yield return Capture("05-settings");
            screen.CloseSettings();
            game.ReturnToTitle();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("06-resumable-menu");
            screen.RequestNewJourney();
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("06-new-game-confirmation");
            screen.HandleBack();
            game.Resume();
            yield return new WaitForSecondsRealtime(0.6f);
            screen.ShowDeath();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("07-death");
            game.ShowMenu();
            game.Resume();
            yield return new WaitForSecondsRealtime(0.6f);
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
            int width = Screen.width, height = Screen.height;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
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
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
