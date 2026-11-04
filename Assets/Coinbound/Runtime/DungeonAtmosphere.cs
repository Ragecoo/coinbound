using System;
using System.Collections.Generic;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.Rendering;

namespace Coinbound
{
    /// <summary>Seeded visual dressing and a small, pooled layer of life; never edits map tiles or collision.</summary>
    public sealed class DungeonAtmosphere : MonoBehaviour
    {
        private const int PoolSize = 48;
        private const int DustCount = 30;
        private const float NearbyRadius = 14;
        private static DungeonAtmosphere current;
        private static readonly int[][] Themes = {
            new[] { 3, 4, 1, 13, 0 },       // ruined store room
            new[] { 5, 9, 2, 19, 0 },       // crypt
            new[] { 11, 10, 6, 14, 18 },    // abandoned sanctuary
            new[] { 17, 14, 8, 1, 7 },      // damp chamber
            new[] { 12, 3, 4, 18, 13 }      // forgotten camp
        };
        private readonly List<Decoration> decorations = new List<Decoration>(320);
        private readonly List<int> nearby = new List<int>(128);
        private readonly List<WetArea> wetAreas = new List<WetArea>();
        private readonly List<RatRoute> ratRoutes = new List<RatRoute>();
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private readonly Slot[] slots = new Slot[PoolSize];
        private readonly Dust[] dust = new Dust[DustCount];
        private readonly Sprite[] art = new Sprite[20];
        private readonly Sprite[] floors = new Sprite[6];
        private readonly DungeonAmbientCanvas[] flames = new DungeonAmbientCanvas[2];
        private MaterialPropertyBlock ratProperties;
        private DungeonMap map;
        private DungeonPlayer player;
        private Material material;
        private DungeonAmbientCanvas ground;
        private DungeonAmbientCanvas air;
        private DungeonAmbientCanvas water;
        private SpriteRenderer rat;
        private SpriteRenderer fountain;
        private uint random;
        private float clock;
        private float refresh;
        private float drawTime;
        private float flameTime;
        private float ratWait = 5;
        private float ratProgress = -1;
        private Vector2 ratFrom;
        private Vector2 ratTo;

        private sealed class Decoration
        {
            public Vector2 position;
            public Sprite sprite;
            public int artIndex;
            public float phase;
            public float density;
            public bool floor;
        }
        private sealed class Slot
        {
            public GameObject obj;
            public SpriteRenderer sprite;
            public MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public Decoration decoration;
            public Cloth cloth;
        }
        private struct WetArea { public Vector2 center; public float radius; public float phase; }
        private struct RatRoute { public Vector2 first; public Vector2 last; }
        private struct Dust { public Vector2 position; public float life; public float duration; public float phase; }

        public static DungeonAtmosphere Create(DungeonMap map, SpawnPlan spawns, DungeonPlayer player, Material material, int seed)
        {
            var obj = new GameObject("Dungeon atmosphere");
            obj.transform.SetParent(player.transform.parent, false);
            var atmosphere = obj.AddComponent<DungeonAtmosphere>();
            atmosphere.Initialize(map, spawns, player, material, seed);
            return atmosphere;
        }

        public void Initialize(DungeonMap dungeonMap, SpawnPlan spawns, DungeonPlayer owner, Material dungeonMaterial, int seed)
        {
            map = dungeonMap; player = owner; material = dungeonMaterial;
            ratProperties = new MaterialPropertyBlock();
            random = unchecked((uint)seed ^ 0xC481B72Du);
            if (random == 0) random = 1;
            current = this;
            Texture2D sheet = Resources.Load<Texture2D>("AmbientArt/decor");
            if (sheet != null && sheet.width == 240 && sheet.height == 192)
                for (int i = 0; i < art.Length; i++)
                {
                    art[i] = Sprite.Create(sheet, new Rect(i % 5 * 48, (3 - i / 5) * 48, 48, 48),
                        new Vector2(0.5f, 8f / 48), 32, 0, SpriteMeshType.FullRect);
                    art[i].name = "Ambient prop " + i;
                    ownedSprites.Add(art[i]);
                }
            else Debug.LogWarning("AmbientArt/decor must be a 240 x 192 native pixel atlas; procedural atmosphere remains available.");
            for (int i = 0; i < floors.Length; i++) floors[i] = Resources.Load<Sprite>("DungeonArt/floor.extra." + i);
            foreach (SpriteRenderer visual in owner.transform.parent.GetComponentsInChildren<SpriteRenderer>())
                if (visual.name == "object.fountain") { fountain = visual; break; }
            PlanDressing(spawns);
            for (int i = 0; i < slots.Length; i++)
            {
                var obj = new GameObject("Dressing " + i, typeof(SpriteRenderer));
                obj.transform.SetParent(transform, false);
                var visual = obj.GetComponent<SpriteRenderer>();
                visual.sharedMaterial = material;
                visual.enabled = false;
                slots[i] = new Slot { obj = obj, sprite = visual };
            }
            ground = DungeonAmbientCanvas.Create(transform, "Damp floor and falling grit", material);
            air = DungeonAmbientCanvas.Create(transform, "Motes and drops", material);
            water = DungeonAmbientCanvas.Create(transform, "Fountain water", material);
            for (int i = 0; i < flames.Length; i++) flames[i] = DungeonAmbientCanvas.Create(transform, "Quiet candle flame " + i, material);
            var ratObject = new GameObject("Occasional rat", typeof(SpriteRenderer));
            ratObject.transform.SetParent(transform, false);
            rat = ratObject.GetComponent<SpriteRenderer>();
            rat.sharedMaterial = material; rat.sprite = art[15]; rat.enabled = false;
            for (int i = 0; i < dust.Length; i++) ResetDust(i, true);
            RefreshPool();
        }

        public static bool IsWet(Vector2 worldPosition)
        {
            if (current == null || current.map == null || !current.IsFloor(worldPosition)) return false;
            foreach (WetArea wet in current.wetAreas)
                if ((worldPosition - wet.center).sqrMagnitude <= wet.radius * wet.radius) return true;
            return false;
        }

        private float Next()
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return (random & 0xFFFFFF) / 16777216f;
        }
        private int Next(int count) => Mathf.Min(count - 1, (int)(Next() * count));
        private static Vector2 Snap(Vector2 point) => new Vector2(Mathf.Round(point.x * 32), Mathf.Round(point.y * 32)) / 32;
        private bool IsFloor(Vector2 point) => map.IsTileFloor(new TilePosition(Mathf.FloorToInt(point.x), Mathf.FloorToInt(1 - point.y)));

        private bool ClearFloor(Vector2 from, Vector2 to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 4));
            for (int i = 0; i <= steps; i++) if (!IsFloor(Vector2.Lerp(from, to, (float)i / steps))) return false;
            return true;
        }

        private bool SafeDecoration(TilePosition tile, SpawnPlan spawns)
        {
            if (map.GetTile(tile) != TileKind.RoomFloor) return false;
            Vector2 position = DungeonView.WorldPosition(tile);
            if (map.EntranceTile.HasValue && Vector2.Distance(position, DungeonView.WorldPosition(map.EntranceTile.Value)) < 4) return false;
            if (map.ExitTile.HasValue && Vector2.Distance(position, DungeonView.WorldPosition(map.ExitTile.Value)) < 2) return false;
            if (fountain != null && Vector2.Distance(position, fountain.transform.position) < 2.7f * fountain.transform.lossyScale.x) return false;
            if (spawns != null)
            {
                foreach (ChestSpawn chest in spawns.Chests)
                    if (Vector2.Distance(position, DungeonView.WorldPosition(chest.Position)) < 1.7f) return false;
                foreach (EnemySpawn enemy in spawns.Enemies)
                    if (Vector2.Distance(position, DungeonView.WorldPosition(enemy.Position)) < 0.8f) return false;
            }
            return DungeonCombatPhysics.FreeFeet(position, 0.38f);
        }

        private void PlanDressing(SpawnPlan spawns)
        {
            var used = new HashSet<TilePosition>();
            foreach (DungeonRoom room in map.Rooms)
            {
                int theme = room.Kind == RoomKind.Entrance ? 2 : Next(Themes.Length);
                int clusters = Mathf.Clamp((room.TileWidth + room.TileHeight) / 10, 2, 5);
                for (int cluster = 0; cluster < clusters; cluster++)
                {
                    int edge = Next(4);
                    bool horizontal = edge < 2;
                    int x = room.TileLeft + 1 + Next(Mathf.Max(1, room.TileWidth - 2));
                    int y = room.TileTop + 1 + Next(Mathf.Max(1, room.TileHeight - 2));
                    if (edge == 0) y = room.TileTop;
                    if (edge == 1) y = room.TileTop + room.TileHeight - 1;
                    if (edge == 2) x = room.TileLeft;
                    if (edge == 3) x = room.TileLeft + room.TileWidth - 1;
                    var tile = new TilePosition(x, y);
                    var outside = new TilePosition(x + (edge == 2 ? -1 : edge == 3 ? 1 : 0), y + (edge == 0 ? -1 : edge == 1 ? 1 : 0));
                    if (map.IsTileFloor(outside) || !SafeDecoration(tile, spawns)) continue;
                    float density = cluster == 0 ? 0 : Next() * 1.65f;
                    for (int member = -1; member <= 1; member++)
                    {
                        var site = new TilePosition(x + (horizontal ? member : 0), y + (horizontal ? 0 : member));
                        var behindSite = new TilePosition(site.X + (edge == 2 ? -1 : edge == 3 ? 1 : 0),
                            site.Y + (edge == 0 ? -1 : edge == 1 ? 1 : 0));
                        if (map.IsTileFloor(behindSite) || !SafeDecoration(site, spawns) || !used.Add(site)) continue;
                        int index = Themes[theme][Next(Themes[theme].Length)];
                        // Tall hanging pieces use the north face; other edges get low clutter.
                        if (edge != 0 && (index == 4 || index == 5 || index == 6 || index == 8 || index == 9 || index == 13))
                            index = member == 0 ? 14 : 0;
                        Vector2 position = DungeonView.WorldPosition(site);
                        if (edge == 0) position.y += 0.27f;
                        if (edge == 1) position.y -= 0.15f;
                        if (edge == 2) position.x -= 0.2f;
                        if (edge == 3) position.x += 0.2f;
                        AddDecoration(position, art[index], index, false, density);
                    }
                    if (edge == 0 && Next() < 0.35f)
                        AddDecoration((Vector2)DungeonView.WorldPosition(tile) + new Vector2(0.65f, 0.45f), art[7], 7, false, density);
                    int wear = theme == 3 ? 4 : theme == 1 ? 2 : 5;
                    for (int offset = 0; offset < 3; offset++)
                    {
                        var worn = new TilePosition(x + (horizontal ? offset - 1 : edge == 2 ? 1 : -1),
                            y + (!horizontal ? offset - 1 : edge == 0 ? 1 : -1));
                        if (SafeDecoration(worn, spawns)) AddDecoration(DungeonView.WorldPosition(worn), floors[wear], -1, true, density);
                    }
                    if (theme == 3)
                    {
                        Vector2 wet = DungeonView.WorldPosition(tile);
                        wet += edge == 0 ? Vector2.down * 0.5f : edge == 1 ? Vector2.up * 0.5f : edge == 2 ? Vector2.right * 0.5f : Vector2.left * 0.5f;
                        wetAreas.Add(new WetArea { center = wet, radius = 0.85f, phase = Next() * 20 });
                        if (Next() < 0.3f) AddDecoration(wet, art[16], 16, true, density);
                    }
                }
                Vector2 first = DungeonView.WorldPosition(new TilePosition(room.TileLeft + 1, room.TileTop + room.TileHeight - 2));
                Vector2 last = first + Vector2.right * Mathf.Min(5, room.TileWidth - 3);
                if (ClearFloor(first, last) && DungeonCombatPhysics.ClearLine(first, last, 0.15f))
                    ratRoutes.Add(new RatRoute { first = first, last = last });
            }
            if (fountain != null)
            {
                float size = fountain.transform.lossyScale.x;
                wetAreas.Add(new WetArea { center = fountain.transform.position, radius = 1.7f * size, phase = 0 });
                // The fountain's accessible outer paving carries the same damp stone variant.
                for (int i = 0; i < 8; i++)
                {
                    Vector2 offset = new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4)) * (1.5f * size);
                    Vector2 point = (Vector2)fountain.transform.position + offset;
                    var tile = new TilePosition(Mathf.FloorToInt(point.x), Mathf.FloorToInt(1 - point.y));
                    if (map.IsTileFloor(tile) && DungeonCombatPhysics.FreeFeet(DungeonView.WorldPosition(tile), 0.15f))
                        AddDecoration(DungeonView.WorldPosition(tile), floors[4], -1, true, 0);
                }
            }
        }

        private void AddDecoration(Vector2 point, Sprite sprite, int index, bool floor, float density)
        {
            if (sprite == null || decorations.Count >= 512) return;
            decorations.Add(new Decoration { position = Snap(point), sprite = sprite, artIndex = index,
                floor = floor, density = density, phase = Next() * 20 });
        }

        private void RefreshPool()
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            Vector2 center = player.transform.position;
            nearby.Clear();
            for (int i = 0; i < decorations.Count; i++)
            {
                Decoration decoration = decorations[i];
                if (settings.ambientDensity <= 0 || decoration.density >= settings.ambientDensity ||
                    (decoration.floor ? !settings.floorWear : decoration.artIndex == 7 ? !settings.cobwebs : !settings.wallProps)) continue;
                if ((decoration.position - center).sqrMagnitude < NearbyRadius * NearbyRadius) nearby.Add(i);
            }
            nearby.Sort((left, right) => (decorations[left].position - center).sqrMagnitude.CompareTo((decorations[right].position - center).sqrMagnitude));
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                slot.decoration = i < nearby.Count ? decorations[nearby[i]] : null;
                if (slot.decoration == null) { slot.sprite.enabled = false; slot.cloth?.Hide(); continue; }
                Decoration item = slot.decoration;
                slot.obj.transform.position = item.position;
                slot.sprite.sprite = item.sprite;
                slot.sprite.sortingOrder = item.floor ? 2 : DungeonDepthSort.OrderForY(item.position.y);
                slot.sprite.color = item.floor ? new Color(0.82f, 0.85f, 1, item.artIndex < 0 ? 0.32f : 0.85f) : Color.white;
                slot.properties.Clear();
                if (!item.floor) slot.properties.SetVector("_GroundLightPosition", new Vector4(item.position.x, item.position.y, 0, 1));
                slot.sprite.SetPropertyBlock(slot.properties);
                bool cloth = settings.clothMotion && (item.artIndex == 6 || item.artIndex == 18);
                slot.sprite.enabled = !cloth;
                if (cloth)
                {
                    if (slot.cloth == null) slot.cloth = new Cloth(slot.obj, material);
                    slot.cloth.Draw(item.sprite, item.position, clock + item.phase, slot.sprite.sortingOrder);
                }
                else slot.cloth?.Hide();
            }
        }

        private void Update()
        {
            if (player == null || map == null || !player.CanAct || player.IsHitStopped) return;
            float dt = Time.deltaTime;
            clock += dt; refresh -= dt; drawTime -= dt;
            flameTime -= dt;
            if (refresh <= 0) { refresh = 0.22f; RefreshPool(); }
            if (flameTime <= 0) { flameTime = 0.13f; RefreshCandleLights(); }
            UpdateRat(dt);
            for (int i = 0; i < dust.Length; i++)
            {
                Dust mote = dust[i]; mote.life -= dt;
                mote.position += new Vector2(Mathf.Sin(clock * 0.7f + mote.phase) * 0.045f, 0.055f) * dt;
                dust[i] = mote;
                if (mote.life <= 0 || Vector2.Distance(mote.position, player.transform.position) > 7) ResetDust(i, false);
            }
            if (drawTime > 0) return;
            drawTime = 1f / 15;
            DrawAmbient();
            foreach (Slot slot in slots)
                if (slot.decoration != null && slot.cloth != null && DungeonFxSettings.Current.clothMotion &&
                    (slot.decoration.artIndex == 6 || slot.decoration.artIndex == 18))
                    slot.cloth.Draw(slot.decoration.sprite, slot.decoration.position, clock + slot.decoration.phase, slot.sprite.sortingOrder);
        }

        private void ResetDust(int index, bool initial)
        {
            float angle = Next() * Mathf.PI * 2;
            float radius = Mathf.Sqrt(Next()) * 6;
            float duration = 4 + Next() * 5;
            dust[index] = new Dust { position = (Vector2)player.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                duration = duration, life = initial ? Next() * duration : duration, phase = Next() * 20 };
        }

        private void DrawAmbient()
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            Vector2 center = player.transform.position;
            float density = Mathf.Clamp(settings.ambientDensity, 0, 2);
            float lightRadius = Mathf.Min(6, material.GetVector("_TorchSettings").x * 0.8f);
            ground.Begin(3); air.Begin(26000);
            if (density > 0 && settings.ambientDust)
                for (int i = 0; i < Mathf.Min(dust.Length, Mathf.CeilToInt(20 * density)); i++)
                {
                    Dust mote = dust[i];
                    if (Vector2.Distance(center, mote.position) > lightRadius || !ClearFloor(center, mote.position)) continue;
                    float fade = Mathf.Sin(Mathf.Clamp01(mote.life / mote.duration) * Mathf.PI);
                    air.Pixel(mote.position, new Color32(170, 174, 198, (byte)(55 * fade)));
                }
            int visibleWet = 0;
            foreach (WetArea wet in wetAreas)
            {
                if (density <= 0 || visibleWet >= 6 || Vector2.Distance(center, wet.center) > 9 || !ClearFloor(center, wet.center)) continue;
                visibleWet++;
                bool nearFountain = fountain != null && Vector2.Distance(wet.center, fountain.transform.position) < 0.1f;
                if (settings.mist && !nearFountain)
                    for (int strip = 0; strip < 5; strip++)
                    {
                        float phase = clock * 0.21f + wet.phase + strip * 1.7f;
                        Vector2 point = wet.center + new Vector2(Mathf.Sin(phase) * 0.35f - 0.4f, (strip - 2) * 0.07f);
                        ground.Pixel(point, new Color32(123, 126, 161, (byte)(8 + strip % 2 * 3)), 20 + strip * 3, 2);
                    }
                if (settings.drips && !nearFountain)
                {
                    float age = (clock + wet.phase) % 5.7f;
                    Vector2 point = wet.center + new Vector2(Mathf.Sin(wet.phase) * 0.25f, 0);
                    if (age < 0.38f)
                        air.Pixel(point + Vector2.up * (1.1f * (1 - age / 0.38f)), new Color32(136, 161, 193, 120), 1, 2);
                    else if (age < 1.1f)
                    {
                        float progress = (age - 0.38f) / 0.72f;
                        ground.Ellipse(point, 2 + (int)(progress * 9), 1 + (int)(progress * 3),
                            new Color32(121, 151, 181, (byte)(100 * (1 - progress))));
                    }
                }
            }
            if (density > 0 && settings.fallingDebris && nearby.Count > 0)
            {
                Decoration source = decorations[nearby[(int)(clock / 13) % Mathf.Min(nearby.Count, 12)]];
                float age = (clock + 6) % 13;
                if (age < 0.55f && !source.floor && ClearFloor(center, source.position))
                    for (int i = 0; i < 3; i++)
                        air.Pixel(source.position + new Vector2((i - 1) * 0.11f, 1.45f - age * age * 4.7f + i * 0.06f),
                            new Color32(116, 113, 134, (byte)(150 * (1 - age / 0.55f))), 1 + i % 2);
            }
            ground.End(); air.End();
            DrawFountain(settings, density);
            DrawCandles(settings, density);
        }

        private void DrawCandles(DungeonFxSettings settings, float density)
        {
            int count = 0;
            if (settings.wallProps && density > 0)
                foreach (Slot slot in slots)
                {
                    Decoration candle = slot.decoration;
                    if (candle == null || candle.artIndex != 10 || count >= flames.Length ||
                        Vector2.Distance(player.transform.position, candle.position) > 8 || !ClearFloor(player.transform.position, candle.position)) continue;
                    DungeonAmbientCanvas flame = flames[count++];
                    flame.Begin(slot.sprite.sortingOrder + 1, candle.position, 0.52f);
                    for (int wick = 0; wick < 3; wick++)
                    {
                        Vector2 point = candle.position + new Vector2(wick == 0 ? 0 : wick == 1 ? -0.28f : 0.31f, wick == 0 ? 1.08f : 0.69f);
                        int height = 3 + ((int)((clock + candle.phase + wick) * 5) % 3 == 0 ? 1 : 0);
                        flame.Pixel(point - Vector2.right / 32, new Color32(189, 158, 121, 160), 3, height);
                        flame.Pixel(point, new Color32(224, 215, 192, 220), 1, 2);
                    }
                    flame.End();
                }
            while (count < flames.Length) flames[count++].Hide();
        }

        private void RefreshCandleLights()
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            if (!settings.wallProps || settings.ambientDensity <= 0 || !settings.dynamicLights) return;
            int count = 0;
            foreach (Slot slot in slots)
            {
                Decoration candle = slot.decoration;
                if (candle == null || candle.artIndex != 10 || Vector2.Distance(player.transform.position, candle.position) > 8 ||
                    !ClearFloor(player.transform.position, candle.position)) continue;
                DungeonEffectLights.Flash(candle.position, 1.5f, 0.19f + Mathf.Sin(clock * 2.1f + candle.phase) * 0.015f,
                    new Color32(255, 208, 150, 255), 0.3f, true);
                if (++count == flames.Length) break;
            }
        }

        private void DrawFountain(DungeonFxSettings settings, float density)
        {
            if (fountain == null) { water.Hide(); return; }
            Vector2 center = fountain.transform.position;
            water.Begin(fountain.sortingOrder + 1, center);
            if (density > 0 && settings.fountainRipples && Vector2.Distance(player.transform.position, center) < 11)
            {
                float scale = fountain.transform.lossyScale.x;
                Vector2 bowl = center + Vector2.down * 0.17f * scale;
                for (int ring = 0; ring < 2; ring++)
                {
                    float progress = Mathf.Repeat(clock * 0.55f + ring * 0.5f, 1);
                    water.Ellipse(bowl, Mathf.RoundToInt((5 + progress * 17) * scale), Mathf.RoundToInt((2 + progress * 7) * scale),
                        new Color32(113, 153, 190, (byte)(85 * (1 - progress))));
                }
                for (int i = 0; i < 4; i++)
                {
                    float age = Mathf.Repeat(clock * 1.1f + i * 0.25f, 1);
                    Vector2 point = center + new Vector2((i % 2 == 0 ? -1 : 1) * 0.12f * age, 0.47f - age * 0.62f) * scale;
                    water.Pixel(point, new Color32(140, 179, 209, (byte)(115 * Mathf.Sin(age * Mathf.PI))), 1, 2);
                }
            }
            water.End();
        }

        private void UpdateRat(float dt)
        {
            DungeonFxSettings settings = DungeonFxSettings.Current;
            if (!settings.rats || settings.ambientDensity <= 0 || rat.sprite == null) { rat.enabled = false; return; }
            if (ratProgress >= 0)
            {
                ratProgress += dt / 1.65f;
                if (ratProgress >= 1) { ratProgress = -1; rat.enabled = false; ratWait = 20 + Next() * 18; return; }
                Vector2 point = Vector2.Lerp(ratFrom, ratTo, ratProgress);
                rat.transform.position = Snap(point) + Vector2.up * ((int)(clock * 15) % 2 / 32f);
                rat.flipX = ratTo.x < ratFrom.x;
                rat.sortingOrder = DungeonDepthSort.OrderForY(point.y);
                ratProperties.SetVector("_GroundLightPosition", new Vector4(point.x, point.y, 0, 1)); rat.SetPropertyBlock(ratProperties);
                return;
            }
            ratWait -= dt;
            if (ratWait > 0) return;
            ratWait = 7;
            foreach (RatRoute route in ratRoutes)
            {
                if (Vector2.Distance(player.transform.position, (route.first + route.last) * 0.5f) > 7) continue;
                bool reverse = Next() < 0.5f;
                ratFrom = reverse ? route.last : route.first; ratTo = reverse ? route.first : route.last;
                ratProgress = 0; rat.enabled = true; rat.transform.position = ratFrom;
                break;
            }
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
            foreach (Slot slot in slots) slot?.cloth?.Dispose();
            foreach (Sprite sprite in ownedSprites) if (sprite != null) Destroy(sprite);
        }

        /// <summary>Horizontal two-pixel strips move by whole native pixels; cloth edges never rotate or blur.</summary>
        private sealed class Cloth : IDisposable
        {
            private readonly Mesh mesh;
            private readonly MeshRenderer visual;
            private readonly Vector3[] vertices = new Vector3[96];
            private readonly Vector2[] uvs = new Vector2[96];
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            internal Cloth(GameObject obj, Material material)
            {
                var strips = new GameObject("Cloth pixels", typeof(MeshFilter), typeof(MeshRenderer));
                strips.transform.SetParent(obj.transform, false);
                visual = strips.GetComponent<MeshRenderer>(); visual.sharedMaterial = material;
                visual.shadowCastingMode = ShadowCastingMode.Off; visual.receiveShadows = false;
                mesh = new Mesh { name = "Native pixel cloth strips" }; mesh.MarkDynamic();
                strips.GetComponent<MeshFilter>().sharedMesh = mesh;
                var triangles = new int[144]; var colors = new Color32[96];
                for (int row = 0; row < 24; row++)
                {
                    int v = row * 4, t = row * 6;
                    triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                    triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                }
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
                mesh.vertices = vertices; mesh.colors32 = colors; mesh.triangles = triangles;
            }
            internal void Draw(Sprite sprite, Vector2 position, float time, int order)
            {
                Rect source = sprite.rect; Texture texture = sprite.texture;
                for (int row = 0; row < 24; row++)
                {
                    float displacement = Mathf.Round(Mathf.Sin(time * 1.3f + row * 0.17f) * (1 - row / 23f) * 1.2f) / 32;
                    float bottom = (row * 2 - 8) / 32f, top = bottom + 2f / 32;
                    int v = row * 4;
                    vertices[v] = new Vector3(-0.75f + displacement, bottom, 0); vertices[v + 1] = new Vector3(0.75f + displacement, bottom, 0);
                    vertices[v + 2] = new Vector3(0.75f + displacement, top, 0); vertices[v + 3] = new Vector3(-0.75f + displacement, top, 0);
                    float u0 = source.xMin / texture.width, u1 = source.xMax / texture.width;
                    float v0 = (source.yMin + row * 2) / texture.height, v1 = (source.yMin + row * 2 + 2) / texture.height;
                    uvs[v] = new Vector2(u0, v0); uvs[v + 1] = new Vector2(u1, v0);
                    uvs[v + 2] = new Vector2(u1, v1); uvs[v + 3] = new Vector2(u0, v1);
                }
                mesh.vertices = vertices; mesh.uv = uvs; mesh.RecalculateBounds();
                properties.SetFloat("_CombatMesh", 1); properties.SetTexture("_MainTex", texture);
                properties.SetVector("_GroundLightPosition", new Vector4(position.x, position.y, 0, 1));
                visual.SetPropertyBlock(properties); visual.sortingOrder = order; visual.enabled = true;
            }
            internal void Hide() => visual.enabled = false;
            public void Dispose() { if (mesh != null) UnityEngine.Object.Destroy(mesh); }
        }
    }
}
