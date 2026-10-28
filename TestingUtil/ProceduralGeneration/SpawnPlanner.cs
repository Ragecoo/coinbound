namespace ProceduralGeneration;

public sealed record SpawnSettings
{
    public int Chests { get; init; } = 3;
    public int? ChestDistance { get; init; }
    public int SafeRadius { get; init; } = 15;
    public int RoomMin { get; init; } = 5;
    public int RoomMax { get; init; } = 6;
    public int CorridorMin { get; init; } = 2;
    public int CorridorMax { get; init; } = 4;
    public int RoomPacks { get; init; } = 1;
    public int LargeRoomPacks { get; init; } = 2;
    public int LargestRoomPacks { get; init; } = 3;
    public int LargeRoomArea { get; init; } = 15;
    public int LargestRoomArea { get; init; } = 25;
    public int CorridorPacks { get; init; } = 6;
    public int Singles { get; init; } = 3;
    public int PackRadius { get; init; } = 5;
    public int PackDistance { get; init; } = 9;
    public int Weight001 { get; init; } = 1;
    public int Weight002 { get; init; } = 1;
    public int Weight003 { get; init; } = 1;
    public int Weight004 { get; init; } = 1;

    public void Validate()
    {
        int[] counts = [Chests, SafeRadius, RoomMin, RoomMax, CorridorMin, CorridorMax, RoomPacks,
            LargeRoomPacks, LargestRoomPacks, CorridorPacks, Singles, PackDistance,
            Weight001, Weight002, Weight003, Weight004];
        if (counts.Any(n => n < 0 || n > 100000) || ChestDistance is < 1 or > 100000 ||
            RoomMin > RoomMax || CorridorMin > CorridorMax || PackRadius is < 1 or > 1000 ||
            LargeRoomArea < 1 || LargestRoomArea < LargeRoomArea)
            throw new ArgumentException("Некорректные настройки спавна: числа должны быть неотрицательными, min <= max, радиус >= 1, пороги комнат возрастают.");
    }
}

public sealed record ChestSpawn(TilePosition Position, string Facing)
{
    public string SpriteId => $"object.chest.{Facing}";
}
public sealed record EnemySpawn(TilePosition Position, string EnemyId, int GroupId, string Location, int? RoomId);
public sealed record SpawnPlan(int Seed, SpawnSettings Settings, IReadOnlyList<ChestSpawn> Chests,
    IReadOnlyList<EnemySpawn> Enemies, IReadOnlyList<string> Notes);

/// <summary>Планирует объекты на готовом этаже, не изменяя тайлы и геометрию.</summary>
public sealed partial class SpawnPlanner
{
    private readonly DungeonMap Map;
    private readonly SpawnSettings _settings;
    private readonly Random _random;
    private readonly int _seed;
    private bool _used;
    private static readonly TilePosition[] Directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];
    public SpawnPlanner(DungeonMap map, SpawnSettings settings, int floorSeed)
    {
        settings.Validate();
        if (map.EntranceTile is null || map.ExitTile is null) throw new ArgumentException("Для спавна нужен готовый этаж.");
        Map = map; _settings = settings; _seed = floorSeed;
        _random = new Random(_seed);
    }

    public SpawnPlan Generate()
    {
        if (_used) throw new InvalidOperationException("Создайте новый планировщик для повторного спавна.");
        _used = true;
        var chests = PlaceChests();
        var occupied = chests.Select(c => c.Position).ToHashSet();
        var enemies = new List<EnemySpawn>();
        var notes = new List<string>();
        if (chests.Count < _settings.Chests) notes.Add($"Сундуков {chests.Count}/{_settings.Chests}: недостаточно мест с нужным расстоянием.");
        int[] weights = [_settings.Weight001, _settings.Weight002, _settings.Weight003, _settings.Weight004];
        int weightSum = weights.Sum();
        if (weightSum == 0) return new(_seed, _settings, chests, enemies, notes);
        TilePosition entrance = Map.EntranceTile!.Value;
        DungeonRoom start = Map.Rooms.Single(r => r.Kind == RoomKind.Entrance);
        var safe = Distances(entrance, _settings.SafeRadius, Map.IsTileFloor).Keys.ToHashSet();
        bool Available(TilePosition tile) => !occupied.Contains(tile) &&
            Map.GetTile(tile) is TileKind.Corridor or TileKind.RoomFloor &&
            !start.Contains(Map.SectionAt(tile)) && !safe.Contains(tile) &&
            Squared(tile, entrance) > (long)_settings.SafeRadius * _settings.SafeRadius &&
            Map.SectionAt(tile) != Map.Exit;
        var anchors = new List<TilePosition>();
        int group = 0;
        void Packs(List<TilePosition> region, int count, int min, int max, string location, int? roomId)
        {
            if (count == 0 || max == 0) return;
            var regionSet = region.ToHashSet();
            int placed = 0;
            for (int pack = 0; pack < count; pack++)
            {
                int size = _random.Next(min, max + 1);
                if (size == 0) continue;
                var candidates = region.Where(Available)
                    .Where(t => anchors.All(a => Squared(t, a) >= (long)_settings.PackDistance * _settings.PackDistance))
                    .OrderBy(_ => _random.Next()).ToArray();
                List<TilePosition>? members = null;
                TilePosition anchor = default;
                foreach (var candidate in candidates)
                {
                    // Только локально связанная пачка; через стены враги не группируются.
                    var local = Distances(candidate, _settings.PackRadius, t => regionSet.Contains(t) && Available(t));
                    if (local.Count < size) continue;
                    members = local.OrderBy(p => p.Value).ThenBy(_ => _random.Next()).Take(size).Select(p => p.Key).ToList();
                    anchor = candidate; break;
                }
                if (members is null) break;
                anchors.Add(anchor); group++; placed++;
                foreach (var tile in members)
                {
                    int roll = _random.Next(weightSum), type = 0;
                    while (roll >= weights[type]) roll -= weights[type++];
                    enemies.Add(new(tile, (type + 1).ToString("D3"), group, location, roomId));
                    occupied.Add(tile);
                }
            }
            if (placed < count) notes.Add($"{location}, комната {roomId?.ToString() ?? "—"}: пачек {placed}/{count}; ограничения места и дистанции.");
        }
        for (int id = 0; id < Map.Rooms.Count; id++)
        {
            DungeonRoom room = Map.Rooms[id];
            if (room.Kind == RoomKind.Entrance) continue;
            int area = room.Width * room.Height;
            int count = area >= _settings.LargestRoomArea ? _settings.LargestRoomPacks :
                area >= _settings.LargeRoomArea ? _settings.LargeRoomPacks : _settings.RoomPacks;
            var region = new List<TilePosition>();
            for (int y = room.TileTop; y < room.TileTop + room.TileHeight; y++)
                for (int x = room.TileLeft; x < room.TileLeft + room.TileWidth; x++) region.Add(new(x, y));
            Packs(region, count, _settings.RoomMin, _settings.RoomMax, "room", id);
        }
        var corridors = new List<TilePosition>();
        for (int y = 0; y < Map.TileHeight; y++)
            for (int x = 0; x < Map.TileWidth; x++)
                if (Map.GetTile(new(x, y)) == TileKind.Corridor) corridors.Add(new(x, y));
        Packs(corridors, _settings.CorridorPacks, _settings.CorridorMin, _settings.CorridorMax, "corridor", null);
        Packs(corridors, _settings.Singles, 1, 1, "single", null);
        return new(_seed, _settings, chests, enemies, notes);
    }

    private static long Squared(TilePosition a, TilePosition b) => (long)(a.X - b.X) * (a.X - b.X) + (long)(a.Y - b.Y) * (a.Y - b.Y);
    private static Dictionary<TilePosition, int> Distances(TilePosition origin, int radius, Func<TilePosition, bool> allowed)
    {
        var result = new Dictionary<TilePosition, int> { [origin] = 0 };
        var queue = new Queue<TilePosition>(); queue.Enqueue(origin);
        while (queue.TryDequeue(out var current))
        {
            int distance = result[current] + 1;
            if (distance > radius) continue;
            foreach (var d in Directions)
            {
                var next = new TilePosition(current.X + d.X, current.Y + d.Y);
                if (!result.ContainsKey(next) && allowed(next)) { result[next] = distance; queue.Enqueue(next); }
            }
        }
        return result;
    }
}
