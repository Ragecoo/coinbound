using System.Globalization;

namespace ProceduralGeneration;

/// <summary>Биты соседнего пола: N=1, E=2, S=4, W=8, NE=16, SE=32, SW=64, NW=128.</summary>
[Flags]
public enum FloorNeighbors { None = 0, N = 1, E = 2, S = 4, W = 8, NE = 16, SE = 32, SW = 64, NW = 128 }

public sealed record SpriteVariant(string Id, string Description, string DebugColor, int RotationDegrees = 0);

/// <summary>TerrainSprite — нижний слой, ObjectSprite — необязательный объект поверх пола.</summary>
public sealed record TileSpecification(
    int X, int Y, Cell Section, TilePosition LocalTile, TileKind Kind,
    bool Walkable, bool Visible, int FloorNeighborMask, string? TerrainSprite, string? ObjectSprite)
{
    public string? Facing { get; init; }
    // Реальное соседство хранится отдельно от формы стены, определяющей спрайт.
    public int? WallSpriteMask => Kind == TileKind.Wall ? TileSprites.WallSpriteMask(FloorNeighborMask) : null;

    // Именно этот вариант задаёт цвет на диагностической карте.
    public string SpriteId => ObjectSprite ?? TerrainSprite ?? "none";
}

/// <summary>Неизменяемый снимок спецификаций всех тайлов, включая скрытые стены.</summary>
public sealed class TileSpriteMap
{
    private readonly TileSpecification[] _tiles;

    private TileSpriteMap(int width, int height, TileSpecification[] tiles)
    {
        Width = width;
        Height = height;
        _tiles = tiles;
        Tiles = Array.AsReadOnly(tiles);
        Variants = tiles.SelectMany(tile => new[] { tile.TerrainSprite, tile.ObjectSprite, tile.SpriteId })
            .OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(TileSprites.Variant).ToArray();
        DisplayVariants = tiles.Where(tile => tile.Visible).Select(tile => tile.SpriteId)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(TileSprites.Variant).ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<TileSpecification> Tiles { get; }
    public IReadOnlyList<SpriteVariant> Variants { get; }
    public IReadOnlyList<SpriteVariant> DisplayVariants { get; }

    public TileSpecification Get(TilePosition tile)
    {
        if (tile.X < 0 || tile.X >= Width || tile.Y < 0 || tile.Y >= Height)
            throw new ArgumentOutOfRangeException(nameof(tile));
        return _tiles[tile.Y * Width + tile.X];
    }

    public static TileSpriteMap Create(DungeonMap map, int seed)
    {
        var tiles = new TileSpecification[map.TileWidth * map.TileHeight];
        for (int y = 0; y < map.TileHeight; y++)
            for (int x = 0; x < map.TileWidth; x++)
                tiles[y * map.TileWidth + x] = TileSprites.Describe(map, new TilePosition(x, y), seed);
        return new TileSpriteMap(map.TileWidth, map.TileHeight, tiles);
    }
}

public static class TileSprites
{
    private static readonly (int X, int Y, FloorNeighbors Bit)[] Directions =
    [
        (0, -1, FloorNeighbors.N), (1, 0, FloorNeighbors.E),
        (0, 1, FloorNeighbors.S), (-1, 0, FloorNeighbors.W),
        (1, -1, FloorNeighbors.NE), (1, 1, FloorNeighbors.SE),
        (-1, 1, FloorNeighbors.SW), (-1, -1, FloorNeighbors.NW)
    ];
    private static readonly IReadOnlyDictionary<string, SpriteVariant> Catalog = CreateCatalog();

    public static int FloorNeighborMask(DungeonMap map, TilePosition tile)
    {
        int mask = 0;
        foreach (var direction in Directions)
            if (map.IsTileFloor(new TilePosition(tile.X + direction.X, tile.Y + direction.Y)))
                mask |= (int)direction.Bit;
        return mask;
    }

    /// <summary>Диагональ влияет на стену только при отсутствии пола с обеих прилегающих сторон.</summary>
    public static int WallSpriteMask(int floorNeighborMask)
    {
        if (floorNeighborMask < 0 || floorNeighborMask > 255)
            throw new ArgumentOutOfRangeException(nameof(floorNeighborMask));

        var mask = (FloorNeighbors)floorNeighborMask;
        if ((mask & (FloorNeighbors.N | FloorNeighbors.E)) != 0) mask &= ~FloorNeighbors.NE;
        if ((mask & (FloorNeighbors.S | FloorNeighbors.E)) != 0) mask &= ~FloorNeighbors.SE;
        if ((mask & (FloorNeighbors.S | FloorNeighbors.W)) != 0) mask &= ~FloorNeighbors.SW;
        if ((mask & (FloorNeighbors.N | FloorNeighbors.W)) != 0) mask &= ~FloorNeighbors.NW;
        return (int)mask;
    }

    public static TileSpecification Describe(DungeonMap map, TilePosition tile, int seed)
    {
        Cell section = map.SectionAt(tile); // Проверяет границы до чтения тайла.
        TileKind kind = map.GetTile(tile);
        int mask = FloorNeighborMask(map, tile);
        bool walkable = kind != TileKind.Wall;
        string? terrain = null;
        string? overlay = null;
        if (walkable)
        {
            bool room = kind is TileKind.RoomFloor or TileKind.Entrance or TileKind.Fountain;
            terrain = $"floor.{(room ? "room" : "corridor")}.{FloorVariation(seed, tile, room)}";
            overlay = kind switch
            {
                TileKind.Entrance => "object.entrance",
                TileKind.Fountain => "object.fountain",
                TileKind.Exit => "object.exit",
                TileKind.Chest => $"object.chest.{ChestFacing(map, tile)}",
                _ => null
            };
        }
        else if (mask != 0)
            terrain = $"wall.{WallSpriteMask(mask):X2}";

        return new TileSpecification(tile.X, tile.Y, section,
            new TilePosition(tile.X % DungeonMap.SectionSize, tile.Y % DungeonMap.SectionSize),
            kind, walkable, terrain is not null, mask, terrain, overlay)
        { Facing = kind == TileKind.Chest ? ChestFacing(map, tile) : null };
    }

    public static string ChestFacing(DungeonMap map, TilePosition tile)
    {
        Cell section = map.SectionAt(tile);
        Cell[] neighbors = map.Neighbors(section).ToArray();
        if (neighbors.Length == 1)
        {
            Cell passage = neighbors[0];
            return passage.X > section.X ? "e" : passage.X < section.X ? "w" : passage.Y > section.Y ? "s" : "n";
        }
        // Спинка у стены, лицевая сторона смотрит на свободный соседний тайл.
        foreach (var (dx, dy, facing) in new[] { (0, -1, "n"), (1, 0, "e"), (0, 1, "s"), (-1, 0, "w") })
            if (map.GetTile(new(tile.X - dx, tile.Y - dy)) == TileKind.Wall &&
                map.IsTileFloor(new(tile.X + dx, tile.Y + dy))) return facing;
        throw new InvalidOperationException("Сундук должен стоять у стены лицом к полу.");
    }

    public static SpriteVariant Variant(string id) => Catalog.TryGetValue(id, out SpriteVariant? variant)
        ? variant : throw new ArgumentException($"Неизвестный вариант спрайта: {id}", nameof(id));

    private static int FloorVariation(int seed, TilePosition tile, bool room)
    {
        // Не расходует Random генератора: результат стабилен при любой скорости отображения.
        uint hash = unchecked((uint)seed ^ (uint)tile.X * 0x9E3779B9u ^ (uint)tile.Y * 0x85EBCA6Bu ^ (room ? 0xC2B2AE35u : 0));
        hash ^= hash >> 16;
        hash = unchecked(hash * 0x7FEB352Du);
        hash ^= hash >> 15;
        return (int)(hash % 3);
    }

    private static IReadOnlyDictionary<string, SpriteVariant> CreateCatalog()
    {
        var variants = new Dictionary<string, SpriteVariant>(StringComparer.Ordinal)
        {
            ["none"] = new("none", "Скрытая непроходимая стена: спрайт не рисовать", "#0D121B")
        };
        for (int mask = 1; mask <= 255; mask++)
        {
            if (WallSpriteMask(mask) != mask) continue;
            string id = $"wall.{mask:X2}";
            string neighbors = string.Join(",", Directions.Where(direction => (mask & (int)direction.Bit) != 0)
                .Select(direction => direction.Bit.ToString()));
            variants[id] = new SpriteVariant(id, $"Стена; стороны и отдельные углы к полу: {neighbors}", Color(mask - 1));
        }
        for (int i = 0; i < 3; i++)
        {
            string corridor = $"floor.corridor.{i}", room = $"floor.room.{i}";
            variants[corridor] = new(corridor, $"Пол коридора, вариант {i}", Color(255 + i));
            variants[room] = new(room, $"Пол комнаты, вариант {i}", Color(258 + i));
        }
        variants["object.entrance"] = new("object.entrance", "Вход поверх пола комнаты", Color(261));
        variants["object.fountain"] = new("object.fountain", "Фонтан поверх пола комнаты", Color(262));
        variants["object.exit"] = new("object.exit", "Выход поверх пола коридора", Color(263));
        string[] facings = ["n", "e", "s", "w"];
        string[] descriptions = ["вверх", "вправо", "вниз", "влево"];
        for (int i = 0; i < facings.Length; i++)
        {
            string id = $"object.chest.{facings[i]}";
            variants[id] = new(id, $"Сундук 1x1, лицевая сторона {descriptions[i]}", Color(264 + i));
        }
        return variants;
    }

    private static string Color(int index)
    {
        // Цвет закреплён за ID, не зависит от порядка тайлов, seed или номера этажа.
        double hue = index * 137.50776405003785 % 360 / 60;
        const double chroma = 0.95 * 0.70, minimum = 0.95 - chroma;
        double secondary = chroma * (1 - Math.Abs(hue % 2 - 1));
        (double r, double g, double b) = (int)hue switch
        {
            0 => (chroma, secondary, 0d), 1 => (secondary, chroma, 0d),
            2 => (0d, chroma, secondary), 3 => (0d, secondary, chroma),
            4 => (secondary, 0d, chroma), _ => (chroma, 0d, secondary)
        };
        return string.Create(CultureInfo.InvariantCulture,
            $"#{(int)Math.Round((r + minimum) * 255):X2}{(int)Math.Round((g + minimum) * 255):X2}{(int)Math.Round((b + minimum) * 255):X2}");
    }
}
