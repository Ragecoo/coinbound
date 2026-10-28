using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Coinbound.Tests
{
    public sealed class DungeonRuntimeTests
    {
        [UnityTest]
        public IEnumerator StartBuildExploreAndKeepWholeViewportInsideMap()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game = Object.FindAnyObjectByType<DungeonGame>();
            Assert.That(game, Is.Not.Null, "The build scene must contain the runtime entry point.");
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0.02}", game);
            Assert.That(GameObject.Find("Start screen"), Is.Not.Null);
            game.StartNewDungeon();
            float timeout = Time.realtimeSinceStartup + 30;
            int lastProgress = 0;
            var statuses = new System.Collections.Generic.HashSet<string>();
            while (!game.IsReady && Time.realtimeSinceStartup < timeout)
            {
                Text percentage = GameObject.Find("Percentage")?.GetComponent<Text>();
                if (percentage != null)
                {
                    int current = int.Parse(percentage.text.TrimEnd('%'));
                    Assert.That(current, Is.InRange(lastProgress, 100));
                    lastProgress = current;
                }
                Text status = GameObject.Find("Status")?.GetComponent<Text>();
                if (status != null) statuses.Add(status.text);
                yield return null;
            }
            Assert.That(game.IsReady, Is.True, "Generation did not finish.");
            Assert.That(statuses, Does.Contain("Прокладывание коридоров лабиринта..."));
            Assert.That(statuses, Does.Contain("Подземелье готово!"));
            Assert.That(game.CurrentSeed, Is.EqualTo(42));
            Assert.That(game.Map.TileWidth, Is.EqualTo(117));
            Assert.That(game.Map.TileHeight, Is.EqualTo(57));
            Assert.That(game.Spawns.Enemies.Count, Is.GreaterThan(0));
            Assert.That(game.Spawns.Chests.Count, Is.EqualTo(3));
            Assert.That(GameObject.Find("Game HUD"), Is.Not.Null);

            var actor = Object.FindAnyObjectByType<DungeonPlayer>();
            Vector3 playerStart = actor.transform.position;
            Assert.That(playerStart, Is.Not.EqualTo(DungeonView.WorldPosition(game.Map.EntranceTile.Value)));
            SpriteRenderer entrance = GameObject.Find("object.entrance").GetComponent<SpriteRenderer>();
            Assert.That(entrance.bounds.size.x, Is.GreaterThan(entrance.sprite.bounds.size.x));
            Assert.That(entrance.transform.position, Is.EqualTo(DungeonView.WorldPosition(game.Map.EntranceTile.Value) + Vector3.up));
            var entranceCollider = entrance.GetComponent<PolygonCollider2D>();
            var fountainCollider = GameObject.Find("object.fountain").GetComponent<PolygonCollider2D>();
            Assert.That(entranceCollider, Is.Not.Null);
            Assert.That(fountainCollider, Is.Not.Null);
            var playerCollider = actor.GetComponent<CircleCollider2D>();
            Assert.That(entranceCollider.Distance(playerCollider).isOverlapped, Is.False);
            Assert.That(fountainCollider.Distance(playerCollider).isOverlapped, Is.False);
            int renderedEnemies = Object.FindObjectsByType<SpriteRenderer>()
                .Count(renderer => renderer.name.StartsWith("Enemy "));
            Assert.That(renderedEnemies, Is.EqualTo(game.Spawns.Enemies.Count));

            // The player must stop at the entrance room's left wall, not cross solid tiles.
            actor.enabled = false;
            var body = actor.GetComponent<Rigidbody2D>();
            DungeonRoom entranceRoom = game.Map.Rooms.Single(room => room.Kind == RoomKind.Entrance);
            body.position = DungeonView.WorldPosition(new TilePosition(game.Map.EntranceTile.Value.X, entranceRoom.TileTop));
            for (int i = 0; i < 70; i++)
            {
                body.linearVelocity = Vector2.left * 7;
                yield return new WaitForFixedUpdate();
            }
            body.linearVelocity = Vector2.zero;
            Assert.That(body.position.x, Is.InRange(3.20f, 3.40f));

            // Test every edge and both extreme portrait and ultrawide viewports.
            body.simulated = false;
            Camera camera = Camera.main;
            foreach (float aspect in new[] { 0.1f, 0.5f, 16f / 9f, 5f, 20f })
            foreach (Vector2 target in new[] { new Vector2(-20, 20), new Vector2(140, -80), new Vector2(58, -28) })
            {
                camera.aspect = aspect;
                actor.transform.position = target;
                yield return null;
                yield return null;
                float halfHeight = camera.orthographicSize;
                float halfWidth = halfHeight * camera.aspect;
                Assert.That(camera.transform.position.x - halfWidth, Is.GreaterThanOrEqualTo(-0.001f));
                Assert.That(camera.transform.position.x + halfWidth, Is.LessThanOrEqualTo(game.Map.TileWidth + 0.001f));
                Assert.That(camera.transform.position.y - halfHeight, Is.GreaterThanOrEqualTo(1 - game.Map.TileHeight - 0.001f));
                Assert.That(camera.transform.position.y + halfHeight, Is.LessThanOrEqualTo(1.001f));
            }
            camera.ResetAspect();
            actor.transform.position = playerStart;
            body.simulated = true;
            actor.enabled = true;
            game.ShowMenu();
            Assert.That(GameObject.Find("Start screen"), Is.Not.Null);
            game.Resume();
            Assert.That(GameObject.Find("Game HUD"), Is.Not.Null);

            game.StartNewDungeon();
            timeout = Time.realtimeSinceStartup + 30;
            while (!game.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(game.IsReady, Is.True);
            Assert.That(Object.FindObjectsByType<DungeonPlayer>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DungeonView>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CancelLoadingReturnsToStartAndCanRetry()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game = Object.FindAnyObjectByType<DungeonGame>();
            game.StartNewDungeon();
            yield return null;
            game.CancelGeneration();
            yield return null;
            Assert.That(game.IsReady, Is.False);
            Assert.That(GameObject.Find("Start screen"), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<DungeonView>(), Is.Null);
            JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0}", game);
            game.StartNewDungeon();
            float timeout = Time.realtimeSinceStartup + 30;
            while (!game.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(game.IsReady, Is.True);
        }
    }
}
