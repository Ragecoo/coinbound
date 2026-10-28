namespace ProceduralGeneration;

/// <summary>Координаты секции, от нуля. Каждая секция содержит 3x3 тайла.</summary>
public readonly record struct Cell(int X, int Y);
/// <summary>Координаты отдельного тайла, от нуля.</summary>
public readonly record struct TilePosition(int X, int Y);

public enum TileKind { Wall, Corridor, RoomFloor, Entrance, Fountain, Exit, Chest }
public enum RoomKind { Entrance, Regular }

/// <summary>Размеры внутреннего пола в секциях; снаружи — стена толщиной в одну секцию.</summary>
public sealed record DungeonRoom(int Left, int Top, int Width, int Height, RoomKind Kind)
{
    private readonly List<Cell> _openings = [];
    public int Right => Left + Width - 1;
    public int Bottom => Top + Height - 1;
    public Cell Center => new(Left + Width / 2, Top + Height / 2);
    public int TileLeft => Left * DungeonMap.SectionSize;
    public int TileTop => Top * DungeonMap.SectionSize;
    public int TileWidth => Width * DungeonMap.SectionSize;
    public int TileHeight => Height * DungeonMap.SectionSize;
    public IReadOnlyList<Cell> Openings => _openings;
    public bool Contains(Cell cell) => cell.X >= Left && cell.X <= Right && cell.Y >= Top && cell.Y <= Bottom;
    public bool OnPerimeter(Cell cell) => !Contains(cell) &&
        cell.X >= Left - 1 && cell.X <= Right + 1 && cell.Y >= Top - 1 && cell.Y <= Bottom + 1;

    internal void AddOpening(Cell cell) => _openings.Add(cell);

    public IEnumerable<Cell> Interior()
    {
        for (int y = Top; y <= Bottom; y++)
            for (int x = Left; x <= Right; x++)
                yield return new Cell(x, y);
    }

    public IEnumerable<Cell> Perimeter()
    {
        for (int y = Top - 1; y <= Bottom + 1; y++)
            for (int x = Left - 1; x <= Right + 1; x++)
                if (OnPerimeter(new Cell(x, y))) yield return new Cell(x, y);
    }
}

/// <summary>План секций и синхронная сетка тайлов. X вправо, Y вниз; фонтан проходим.</summary>
public sealed class DungeonMap
{
    public const int SectionSize = 3;
    private readonly TileKind[] _sections;
    private readonly TileKind[] _tiles;
    private readonly List<DungeonRoom> _rooms = [];
    private IReadOnlyList<Cell> _solution = [];

    public DungeonMap(int width, int height)
    {
        Width = width;
        Height = height;
        _sections = new TileKind[width * height];
        _tiles = new TileKind[TileWidth * TileHeight];
    }

    // Размеры плана в секциях; размеры готовой тайловой сетки — TileWidth/TileHeight.
    public int Width { get; }
    public int Height { get; }
    public int TileWidth => Width * SectionSize;
    public int TileHeight => Height * SectionSize;
    public int FloorCount { get; private set; }
    public int FloorTileCount => FloorCount * SectionSize * SectionSize;
    public Cell? Entrance { get; internal set; }
    public Cell? Exit { get; internal set; }
    public TilePosition? EntranceTile => Entrance is Cell cell ? SectionCenter(cell) : null;
    public TilePosition? ExitTile => Exit is Cell cell ? SectionCenter(cell) : null;
    public IReadOnlyList<Cell> Solution
    {
        get => _solution;
        internal set
        {
            _solution = value;
            TileSolution = ExpandRoute(value);
        }
    }
    public IReadOnlyList<TilePosition> TileSolution { get; private set; } = [];
    public IReadOnlyList<DungeonRoom> Rooms => _rooms;
    private readonly List<TilePosition> _chests = [];
    public IReadOnlyList<TilePosition> Chests => _chests.AsReadOnly();
    public int ChestMinimumDistance => Math.Max(9, Math.Min(TileWidth, TileHeight) / 4);

    internal void PlaceChest(TilePosition tile)
    {
        if (GetTile(tile) != TileKind.Corridor) throw new ArgumentException("Сундук требует свободный пол коридора.", nameof(tile));
        _tiles[tile.Y * TileWidth + tile.X] = TileKind.Chest;
        _chests.Add(tile);
    }

    public bool IsInterior(Cell cell) => cell.X > 0 && cell.X < Width - 1 && cell.Y > 0 && cell.Y < Height - 1;
    public TileKind GetSection(Cell cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height
        ? _sections[cell.Y * Width + cell.X] : TileKind.Wall;
    public TileKind GetTile(TilePosition tile) => ContainsTile(tile)
        ? _tiles[tile.Y * TileWidth + tile.X] : TileKind.Wall;
    public bool IsFloor(int x, int y) => IsFloor(new Cell(x, y));
    public bool IsFloor(Cell cell) => GetSection(cell) != TileKind.Wall;
    public bool IsTileFloor(TilePosition tile) => GetTile(tile) != TileKind.Wall;

    public TilePosition SectionOrigin(Cell section)
    {
        if (section.X < 0 || section.X >= Width || section.Y < 0 || section.Y >= Height)
            throw new ArgumentOutOfRangeException(nameof(section));
        return new TilePosition(section.X * SectionSize, section.Y * SectionSize);
    }

    public TilePosition SectionCenter(Cell section)
    {
        TilePosition origin = SectionOrigin(section);
        return new TilePosition(origin.X + SectionSize / 2, origin.Y + SectionSize / 2);
    }

    public Cell SectionAt(TilePosition tile)
    {
        if (!ContainsTile(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
        return new Cell(tile.X / SectionSize, tile.Y / SectionSize);
    }

    private bool ContainsTile(TilePosition tile) => tile.X >= 0 && tile.X < TileWidth && tile.Y >= 0 && tile.Y < TileHeight;

    internal bool Carve(Cell cell, TileKind kind = TileKind.Corridor)
    {
        if (!IsInterior(cell)) throw new ArgumentOutOfRangeException(nameof(cell), "Внешнюю стену нельзя разрушать.");
        if (kind == TileKind.Wall) throw new ArgumentException("Carve создаёт только проходимые клетки.", nameof(kind));
        int index = cell.Y * Width + cell.X;
        if (_sections[index] == kind) return false;
        if (_sections[index] == TileKind.Wall) FloorCount++;
        _sections[index] = kind;

        // Объект занимает только центральный тайл, остальные восемь остаются полом.
        TileKind floor = kind switch
        {
            TileKind.Entrance or TileKind.Fountain => TileKind.RoomFloor,
            TileKind.Exit or TileKind.Chest => TileKind.Corridor,
            _ => kind
        };
        TilePosition origin = SectionOrigin(cell);
        for (int y = 0; y < SectionSize; y++)
            for (int x = 0; x < SectionSize; x++)
                _tiles[(origin.Y + y) * TileWidth + origin.X + x] = floor;
        TilePosition center = SectionCenter(cell);
        _tiles[center.Y * TileWidth + center.X] = kind;
        return true;
    }

    private List<TilePosition> ExpandRoute(IReadOnlyList<Cell> route)
    {
        var tiles = new List<TilePosition>();
        if (route.Count == 0) return tiles;
        TilePosition current = SectionCenter(route[0]);
        tiles.Add(current);
        for (int i = 1; i < route.Count; i++)
        {
            TilePosition target = SectionCenter(route[i]);
            int dx = Math.Sign(target.X - current.X), dy = Math.Sign(target.Y - current.Y);
            for (int step = 0; step < SectionSize; step++)
            {
                current = new TilePosition(current.X + dx, current.Y + dy);
                tiles.Add(current);
            }
        }
        return tiles;
    }

    internal void AddRoom(DungeonRoom room) => _rooms.Add(room);

    public static IEnumerable<Cell> Adjacent(Cell cell)
    {
        yield return new Cell(cell.X, cell.Y - 1);
        yield return new Cell(cell.X + 1, cell.Y);
        yield return new Cell(cell.X, cell.Y + 1);
        yield return new Cell(cell.X - 1, cell.Y);
    }

    public IEnumerable<Cell> Neighbors(Cell cell) => Adjacent(cell).Where(IsFloor);

    public IEnumerable<Cell> Floors()
    {
        for (int y = 1; y < Height - 1; y++)
            for (int x = 1; x < Width - 1; x++)
                if (IsFloor(x, y)) yield return new Cell(x, y);
    }

    public int CountDeadEnds() => Floors().Count(cell => Neighbors(cell).Count() == 1);
}

