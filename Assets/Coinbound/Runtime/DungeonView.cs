using System;
using System.Collections;
using System.Collections.Generic;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Coinbound
{
    /// <summary>Turns the generator's sprite IDs into Unity art without changing its map.</summary>
    public sealed class DungeonView : MonoBehaviour
    {
        public const float PlayerFootSize = 0.36f;
        private static readonly int StoneTintId = Shader.PropertyToID("_StoneTint");
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly Dictionary<string, Tile> terrainTiles = new Dictionary<string, Tile>(StringComparer.Ordinal);
        private readonly List<UnityEngine.Object> ownedAssets = new List<UnityEngine.Object>();
        private readonly List<SpriteRenderer> solidLandmarks = new List<SpriteRenderer>();
        private readonly List<DungeonEnemy> enemies = new List<DungeonEnemy>();
        private Material material;

        public int AliveEnemyCount
        {
            get
            {
                int count = 0;
                foreach (DungeonEnemy enemy in enemies)
                    if (enemy != null && !enemy.IsDead) count++;
                return count;
            }
        }

        public static Vector3 WorldPosition(TilePosition tile) => new Vector3(tile.X + 0.5f, 0.5f - tile.Y, 0);

        public IEnumerator<object> Build(DungeonMap map, TileSpriteMap specification, int floorSeed, float entranceScale, float fountainScale,
            float entranceLift, Action<float> progress)
        {
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("DungeonArt")) sprites.Add(sprite.name, sprite);
            Material template = Resources.Load<Material>("DungeonSprite");
            if (template == null) throw new InvalidOperationException("Не найден материал DungeonSprite.");
            // Each floor owns its light textures and material; no global state survives regeneration.
            material = new Material(template) { name = "Dungeon torch material" };
            ownedAssets.Add(material);
            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(transform, false);
            var layer = new GameObject("Terrain", typeof(Tilemap), typeof(TilemapRenderer));
            layer.transform.SetParent(grid.transform, false);
            var tilemap = layer.GetComponent<Tilemap>();
            layer.GetComponent<TilemapRenderer>().sharedMaterial = material;

            int count = 0;
            foreach (TileSpecification tile in specification.Tiles)
            {
                // Hidden solid cells use the pack's seamless void, not an invented wall mask.
                string id = FloorSpriteId(tile, floorSeed);
                if (!terrainTiles.TryGetValue(id, out Tile asset))
                {
                    asset = ScriptableObject.CreateInstance<Tile>();
                    asset.name = id;
                    asset.sprite = FindSprite(id);
                    asset.colliderType = tile.Walkable ? Tile.ColliderType.None : Tile.ColliderType.Grid;
                    terrainTiles.Add(id, asset);
                    ownedAssets.Add(asset);
                }
                tilemap.SetTile(new Vector3Int(tile.X, -tile.Y, 0), asset);
                if (tile.ObjectSprite != null)
                {
                    // A marker is the CENTER of the object's 3 x 3 tile base size before scaling.
                    // object.exit is the algorithm ID; the art provides open/closed variants.
                    string objectId = tile.ObjectSprite == "object.exit" ? "object.exit.open" : tile.ObjectSprite;
                    var landmark = CreateSprite(objectId, FindSprite(objectId), WorldPosition(new TilePosition(tile.X, tile.Y)), 10);
                    MatchWallStone(landmark, objectId);
                    if (objectId == "object.entrance") landmark.transform.position += Vector3.up * entranceLift;
                    if (objectId == "object.entrance" || objectId == "object.fountain")
                    {
                        MakeSolidLandmark(landmark, objectId == "object.entrance" ? entranceScale : fountainScale,
                            objectId == "object.fountain");
                        var renderer = landmark.GetComponent<SpriteRenderer>();
                        var properties = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(properties);
                        DungeonSpriteGrid.Apply(renderer, properties);
                        solidLandmarks.Add(renderer);
                    }
                    SpriteRenderer landmarkVisual = landmark.GetComponent<SpriteRenderer>();
                    // Use the stone base, excluding the transparent margin around the large prop art.
                    float baseOffset = objectId == "object.entrance" || objectId == "object.fountain"
                        ? -1.0625f * landmark.transform.lossyScale.y
                        : landmarkVisual.bounds.min.y - landmark.transform.position.y;
                    landmark.AddComponent<DungeonDepthSort>().Initialize(landmarkVisual, landmark.transform, baseOffset);
                }
                count++;
                if (count % 256 != 0) continue;
                progress((float)count / specification.Tiles.Count);
                yield return null;
            }
            tilemap.CompressBounds();
            layer.AddComponent<TilemapCollider2D>();
            progress(1);
            yield return null;
        }

        private static string FloorSpriteId(TileSpecification tile, int seed)
        {
            string original = tile.TerrainSprite ?? "background.void";
            if (!tile.Walkable || !original.StartsWith("floor.", StringComparison.Ordinal)) return original;
            // Visual variety is independent of generator randomness, geometry and spawn planning.
            uint hash = unchecked((uint)seed ^ (uint)tile.X * 0x9E3779B9u ^ (uint)tile.Y * 0x85EBCA6Bu ^ 0xA341316Cu);
            hash ^= hash >> 16;
            hash = unchecked(hash * 0x7FEB352Du);
            hash ^= hash >> 15;
            hash = unchecked(hash * 0x846CA68Bu);
            hash ^= hash >> 16;
            // Keep the quiet original stones on 55% of the floor; distribute six accent variants over the rest.
            // No rotation or flipping: all four borders retain their exact matching mortar connections.
            return hash % 100 < 55 ? original : "floor.extra." + ((hash >> 8) % 6);
        }

        public IEnumerator<object> PlaceSpawns(SpawnPlan plan)
        {
            foreach (ChestSpawn spawn in plan.Chests)
            {
                var item = CreateSprite(spawn.SpriteId, null, WorldPosition(spawn.Position), 20);
                item.AddComponent<DungeonChest>().Initialize(spawn.Facing);
                var visual = item.GetComponent<SpriteRenderer>();
                item.AddComponent<DungeonDepthSort>().Initialize(visual, item.transform, 0, true);
                solidLandmarks.Add(visual);
            }
            Color32[] colors = {
                new Color32(239, 106, 106, 255), new Color32(236, 148, 81, 255),
                new Color32(202, 111, 220, 255), new Color32(166, 130, 236, 255)
            };
            var enemySprites = new Sprite[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                enemySprites[i] = CreatePlaceholder($"Enemy {i + 1:000} placeholder", colors[i], false);
            int count = 0;
            foreach (EnemySpawn spawn in plan.Enemies)
            {
                int type = int.Parse(spawn.EnemyId) - 1;
                var enemy = CreateSprite($"Enemy {spawn.EnemyId} / group {spawn.GroupId}", enemySprites[type], WorldPosition(spawn.Position), 30);
                enemy.AddComponent<DungeonDepthSort>().Initialize(enemy.GetComponent<SpriteRenderer>(), enemy.transform, 0, true);
                enemies.Add(enemy.AddComponent<DungeonEnemy>());
                if (++count % 24 == 0) yield return null;
            }
        }

        public DungeonPlayer CreatePlayer(TilePosition position, DungeonHero hero = null)
        {
            hero = hero ?? DungeonHero.First;
            var actor = new GameObject("Player");
            actor.transform.SetParent(transform, false);
            actor.transform.position = WorldPosition(position);
            actor.tag = "Player";
            var body = actor.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = actor.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * PlayerFootSize;
            var physicsMaterial = new PhysicsMaterial2D("Player slide") { friction = 0, bounciness = 0 };
            collider.sharedMaterial = physicsMaterial;
            ownedAssets.Add(physicsMaterial);
            var player = actor.AddComponent<DungeonPlayer>();
            player.Initialize(hero);
            var visualObject = new GameObject("Hero visual", typeof(SpriteRenderer));
            visualObject.transform.SetParent(actor.transform, false);
            var visual = visualObject.GetComponent<SpriteRenderer>();
            visual.sharedMaterial = material;
            visual.sprite = DungeonHeroAnimator.GetPortrait(hero);
            actor.AddComponent<DungeonHeroAnimator>().Initialize(player, visual);
            actor.AddComponent<DungeonDepthSort>().Initialize(visual, actor.transform, 0, true);
            return player;
        }

        public void ActivateEnemies(DungeonPlayer target, DungeonMap map)
        {
            foreach (DungeonEnemy enemy in enemies)
                if (enemy != null) enemy.Initialize(target, map);
        }

        public void AddEffectLights(DungeonMap map, DungeonPlayer player)
        {
            DungeonEffectLights.Create(map, player, material);
        }

        private static void MatchWallStone(GameObject landmark, string objectId)
        {
            // Match the pack's representative stone midtones to wall.01 / wall.04.
            // The shader keeps saturated fountain water out of this stone-only correction.
            Color source;
            switch (objectId)
            {
                case "object.entrance": source = new Color32(66, 74, 99, 255); break;
                case "object.fountain": source = new Color32(79, 84, 113, 255); break;
                case "object.exit.open":
                case "object.exit.closed": source = new Color32(53, 60, 83, 255); break;
                default: return;
            }
            Color wall = new Color32(37, 41, 64, 255);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                source = source.linear;
                wall = wall.linear;
            }
            var renderer = landmark.GetComponent<SpriteRenderer>();
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetVector(StoneTintId, new Vector4(wall.r / source.r, wall.g / source.g, wall.b / source.b, 1));
            renderer.SetPropertyBlock(properties);
        }

        private static void MakeSolidLandmark(GameObject landmark, float scale, bool fountain)
        {
            landmark.transform.localScale = new Vector3(scale, scale, 1);
            // A single filled octagon follows the fountain rim; the water is also solid.
            // Both outlines exclude transparent margins; redraws retain the same 3 x 3 tile base size.
            Vector2[] outline = fountain ? new[]
            {
                new Vector2(-0.625f, 1.0625f), new Vector2(0.625f, 1.0625f),
                new Vector2(1.25f, 0.4375f), new Vector2(1.25f, -0.4375f),
                new Vector2(0.625f, -1.0625f), new Vector2(-0.625f, -1.0625f),
                new Vector2(-1.25f, -0.4375f), new Vector2(-1.25f, 0.4375f)
            } : new[]
            {
                new Vector2(-1.125f, 1.0625f), new Vector2(1.125f, 1.0625f),
                new Vector2(1.25f, 0.75f), new Vector2(1.25f, -0.625f),
                new Vector2(1.0625f, -1.0625f), new Vector2(-1.0625f, -1.0625f),
                new Vector2(-1.25f, -0.625f), new Vector2(-1.25f, 0.75f)
            };
            // Adding a polygon beside a sprite asks Unity to trace its unreadable texture first.
            // The collision outline is authored here, so suppress that unused automatic trace.
            SpriteRenderer renderer = landmark.GetComponent<SpriteRenderer>();
            Sprite sprite = renderer.sprite;
            try
            {
                renderer.sprite = null;
                var collider = landmark.AddComponent<PolygonCollider2D>();
                collider.autoTiling = false;
                collider.pathCount = 1;
                collider.SetPath(0, outline);
            }
            finally { renderer.sprite = sprite; }
        }

        public TilePosition FindPlayerSpawn(DungeonMap map)
        {
            // The entrance marker is now inside a solid object. Search outward for room to stand beside it.
            Physics2D.SyncTransforms();
            var queue = new Queue<TilePosition>();
            var visited = new HashSet<TilePosition>();
            TilePosition entrance = map.EntranceTile.Value;
            queue.Enqueue(entrance);
            visited.Add(entrance);
            var directions = new[] { new Vector2Int(0, 1), Vector2Int.right, Vector2Int.left, new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                TilePosition tile = queue.Dequeue();
                if (Physics2D.OverlapBox(WorldPosition(tile), Vector2.one * (PlayerFootSize + 0.04f), 0) == null) return tile;
                foreach (Vector2Int direction in directions)
                {
                    var next = new TilePosition(tile.X + direction.x, tile.Y + direction.y);
                    if (map.IsTileFloor(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            throw new InvalidOperationException("Не найдено свободное место для персонажа возле входа.");
        }

        public void AddPlayerLight(DungeonMap map, Transform player, float radius, float intensity, Color color,
            Color darkness, float unlitVisibility, float shadowSoftness, float wallLightMultiplier, float upperWallLightMultiplier,
            float wallLightDepth, float landmarkShadowLength, float landmarkShadowOpacity, float landmarkShadowSoftness)
        {
            gameObject.AddComponent<DungeonTorch>().Initialize(map, player, material, radius, intensity, color,
                darkness, unlitVisibility, shadowSoftness, wallLightMultiplier, upperWallLightMultiplier, wallLightDepth);
            foreach (SpriteRenderer landmark in solidLandmarks)
            {
                var shadow = new GameObject(landmark.name + " shadow").AddComponent<DungeonLandmarkShadow>();
                shadow.transform.SetParent(transform, false);
                // Let nearby shadows extend visibly past the sprite; the configured length still caps them.
                float heightRatio = landmark.GetComponent<DungeonChest>() != null ? .16f : landmark.name == "object.fountain" ? 0.2f : 0.24f;
                shadow.Initialize(landmark, player, heightRatio, landmarkShadowLength, landmarkShadowOpacity, landmarkShadowSoftness);
                // Projected shadows stay on the ground even when their caster sorts in front of an actor.
                shadow.GetComponent<SpriteRenderer>().sortingOrder = 1;
            }
        }

        private Sprite FindSprite(string id)
        {
            if (sprites.TryGetValue(id, out Sprite sprite)) return sprite;
            throw new InvalidOperationException($"Не найден спрайт генератора: {id}");
        }

        private GameObject CreateSprite(string label, Sprite sprite, Vector3 position, int order)
        {
            var item = new GameObject(label, typeof(SpriteRenderer));
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            var renderer = item.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            return item;
        }

        private Sprite CreatePlaceholder(string label, Color color, bool chest)
        {
            Sprite sprite = DungeonPlaceholderArt.Create(label, color, chest);
            ownedAssets.Add(sprite);
            ownedAssets.Add(sprite.texture);
            return sprite;
        }

        private void OnDestroy()
        {
            foreach (UnityEngine.Object asset in ownedAssets) if (asset != null) Destroy(asset);
        }
    }
}
