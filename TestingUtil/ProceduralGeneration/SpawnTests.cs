using System.Text.Json;

namespace ProceduralGeneration;

internal static class SpawnTests
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        Check(CommandLineOptions.Parse([]).Spawns.Chests == 3 &&
            CommandLineOptions.Parse(["--chests", "7"]).Spawns.Chests == 7, "CLI спавна сундуков");
        for (int seed = 0; seed < 24; seed++)
        {
            var generator = new DungeonGenerator(new GenerationSettings(Seed: seed, HallCount: 4));
            foreach (var step in generator.Generate()) { }
            DungeonMap map = generator.Map;
            var before = TileSpriteMap.Create(map, seed).Tiles.ToArray();
            var settings = new SpawnSettings();
            SpawnPlan plan = new SpawnPlanner(map, settings, seed).Generate();
            Check(before.SequenceEqual(TileSpriteMap.Create(map, seed).Tiles) && map.Chests.Count == 0,
                "План спавна не изменяет ни одного тайла геометрии");
            Check(JsonSerializer.Serialize(plan) == JsonSerializer.Serialize(new SpawnPlanner(map, settings, seed).Generate()),
                "Спавн повторяем по seed");
            Check(plan.Chests.Count == 3 && plan.Enemies.Count > 0, "По умолчанию размещаются сундуки и враги");
            var occupied = plan.Chests.Select(c => c.Position).ToHashSet();
            foreach (var chest in plan.Chests)
            {
                var d = chest.Facing switch { "n" => new TilePosition(0, -1), "e" => new(1, 0), "s" => new(0, 1), "w" => new(-1, 0), _ => throw new InvalidOperationException() };
                TilePosition center = map.SectionCenter(map.SectionAt(chest.Position));
                Check(chest.Position == new TilePosition(center.X - d.X, center.Y - d.Y) &&
                    map.GetTile(new(chest.Position.X - d.X, chest.Position.Y - d.Y)) == TileKind.Wall,
                    "Сундук по центру стены с правильным направлением");
                foreach (var other in plan.Chests.Where(c => c != chest))
                    Check(Squared(chest.Position, other.Position) >= map.ChestMinimumDistance * map.ChestMinimumDistance,
                        "Сундуки сохраняют расстояние");
                Cell section = map.SectionAt(chest.Position);
                var neighbors = map.Neighbors(section).ToArray();
                if (neighbors.Length == 1) Check(neighbors[0] == new Cell(section.X + d.X, section.Y + d.Y), "Сундук в торце тупика");
            }
            var start = map.Rooms.Single(r => r.Kind == RoomKind.Entrance);
            foreach (var enemy in plan.Enemies)
            {
                Check(occupied.Add(enemy.Position), "Враги и сундуки не перекрываются");
                Check(enemy.EnemyId is "001" or "002" or "003" or "004", "Четыре ID врагов");
                Check(map.GetTile(enemy.Position) is TileKind.Corridor or TileKind.RoomFloor &&
                    !start.Contains(map.SectionAt(enemy.Position)) &&
                    Squared(enemy.Position, map.EntranceTile!.Value) > settings.SafeRadius * settings.SafeRadius,
                    "Враги только на полу вне входа и безопасной зоны");
            }
            foreach (var group in plan.Enemies.GroupBy(e => e.GroupId))
            {
                string location = group.First().Location;
                Check(location switch { "room" => group.Count() is >= 5 and <= 6,
                    "corridor" => group.Count() is >= 2 and <= 4, "single" => group.Count() == 1, _ => false }, "Размер пачки");
                var remaining = group.Select(e => e.Position).ToHashSet();
                var queue = new Queue<TilePosition>(); queue.Enqueue(remaining.First()); remaining.Remove(queue.Peek());
                while (queue.TryDequeue(out var p))
                    foreach (var next in new[] { new TilePosition(p.X - 1, p.Y), new(p.X + 1, p.Y), new(p.X, p.Y - 1), new(p.X, p.Y + 1) })
                        if (remaining.Remove(next)) queue.Enqueue(next);
                Check(remaining.Count == 0, "Пачка связная, без врагов за стеной");
            }
            var disabled = new SpawnPlanner(map, settings with { Chests = 0, Weight001 = 0, Weight002 = 0, Weight003 = 0, Weight004 = 0 }, seed).Generate();
            Check(disabled.Chests.Count == 0 && disabled.Enemies.Count == 0, "Отключение всех объектов");
            var only004 = new SpawnPlanner(map, settings with { Weight001 = 0, Weight002 = 0, Weight003 = 0 }, seed).Generate();
            Check(only004.Enemies.All(e => e.EnemyId == "004"), "Веса типов врагов");
            if (seed == 0)
            {
                var custom = new SpawnPlanner(map, settings with
                {
                    Chests = 0, RoomMin = 2, RoomMax = 2, RoomPacks = 2, LargeRoomPacks = 2,
                    LargestRoomPacks = 2, CorridorPacks = 0, Singles = 0, PackDistance = 0
                }, seed).Generate();
                Check(custom.Enemies.Count > 0 && custom.Enemies.All(e => e.Location == "room") &&
                    custom.Enemies.GroupBy(e => e.GroupId).All(g => g.Count() == 2), "Настраиваемые размеры пачек и отключение коридоров");
                var safeAll = new SpawnPlanner(map, settings with { SafeRadius = 1000 }, seed).Generate();
                Check(safeAll.Enemies.Count == 0, "Большая безопасная зона исключает всех врагов без зависания");
            }
            using var stream = new MemoryStream();
            MapJsonExport.Write(stream, new GenerationSettings(Seed: seed), map, TileSpriteMap.Create(map, seed), 1, generator.AddedLoops, plan);
            stream.Position = 0;
            using var json = JsonDocument.Parse(stream);
            Check(json.RootElement.GetProperty("spawns").GetProperty("enemies").GetArrayLength() == plan.Enemies.Count &&
                json.RootElement.GetProperty("spawns").GetProperty("chests").GetArrayLength() == 3, "Экспорт отдельного слоя спавна");
        }
        foreach (var invalid in new[] { new SpawnSettings { RoomMin = 9, RoomMax = 2 }, new SpawnSettings { Weight001 = -1 },
            new SpawnSettings { PackRadius = 0 }, new SpawnSettings { ChestDistance = 0 } })
        {
            bool rejected = false;
            try { invalid.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Неверные настройки спавна отклоняются");
        }
        Console.WriteLine("OK: отдельный спавн, безопасный вход, связные пачки, ID и веса врагов, сундуки и JSON.");
    }

    private static long Squared(TilePosition a, TilePosition b) => (long)(a.X - b.X) * (a.X - b.X) + (long)(a.Y - b.Y) * (a.Y - b.Y);
}
