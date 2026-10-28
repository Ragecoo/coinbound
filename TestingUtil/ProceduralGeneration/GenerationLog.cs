using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProceduralGeneration;

/// <summary>UTF-8 JSON Lines: описание генерации и каждого тайла, без ANSI и рисунка карты.</summary>
internal sealed class GenerationLog : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly TextWriter _writer;
    private readonly DungeonMap _map;
    private readonly int _seed;
    private bool _finished;
    private int _steps;

    internal GenerationLog(TextWriter writer, GenerationSettings settings, DungeonMap map, int floor, string path)
    {
        _writer = writer;
        _map = map;
        _seed = settings.Seed;
        Path = path;
        Write(new
        {
            record = "header", formatVersion = 2, startedUtc = DateTimeOffset.UtcNow, floor, settings,
            sections = new { width = map.Width, height = map.Height, size = DungeonMap.SectionSize },
            tiles = new { width = map.TileWidth, height = map.TileHeight },
            coordinates = "От нуля: X вправо, Y вниз; section — секция, localTile — тайл внутри неё (0..2).",
            neighbors = "Маска соседнего пола: N=1,E=2,S=4,W=8,NE=16,SE=32,SW=64,NW=128. У стены маска указывает сторону пола.",
            wallSprites = "FloorNeighborMask — все соседи; WallSpriteMask — форма стены: диагональ учитывается только без пола с обеих прилегающих сторон. Ключ wall.XX содержит WallSpriteMask в hex; 0 означает none. Для пола WallSpriteMask=null.",
            layers = "TerrainSprite — поверхность, ObjectSprite — объект поверх неё. SpriteId задаёт цвет диагностики. Все спрайты без поворота.",
            hiddenWalls = "Visible=false: спрайта нет, но тайл непроходим. Ключи спрайтов логические, без привязки к изображениям."
        });
    }

    public string Path { get; }

    public static GenerationLog Create(GenerationSettings settings, DungeonMap map, int floor)
    {
        string directory = DefaultDirectory();
        Directory.CreateDirectory(directory);
        string filename = $"dungeon-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-floor-{floor}-seed-{settings.Seed}-{Guid.NewGuid():N}.jsonl";
        string path = System.IO.Path.Combine(directory, filename);
        var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        try { return new GenerationLog(writer, settings, map, floor, path); }
        catch { writer.Dispose(); throw; }
    }

    private static string DefaultDirectory()
    {
        // dotnet run --project должен писать в папку проекта, независимо от текущего каталога.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "ProceduralGeneration.csproj")))
                return System.IO.Path.Combine(directory.FullName, "logs");
        return System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public void Record(GenerationStep step)
    {
        if (_finished) throw new InvalidOperationException("Лог уже завершён.");
        Write(new { record = "step", data = step });
        _steps++;
    }

    public void Finish(TileSpriteMap sprites, string status, int? addedLoops = null, SpawnPlan? spawns = null)
    {
        if (_finished) throw new InvalidOperationException("Лог уже завершён.");
        _finished = true;
        for (int i = 0; i < _map.Rooms.Count; i++)
            Write(new { record = "room", id = i, data = _map.Rooms[i] });
        foreach (SpriteVariant variant in sprites.Variants)
            Write(new { record = "sprite", data = variant });
        foreach (TileSpecification tile in sprites.Tiles)
            Write(new { record = "tile", data = tile });
        if (spawns is not null) Write(new { record = "spawns", data = spawns });
        Write(new
        {
            record = "end", status, finishedUtc = DateTimeOffset.UtcNow, steps = _steps,
            tileCount = sprites.Tiles.Count, floorSections = _map.FloorCount, floorTiles = _map.FloorTileCount,
            entrance = _map.EntranceTile, exit = _map.ExitTile, addedLoops,
            chestCount = spawns?.Chests.Count ?? 0, enemyCount = spawns?.Enemies.Count ?? 0,
            chestMinimumDistance = spawns?.Settings.ChestDistance ?? _map.ChestMinimumDistance,
            sectionPath = _map.Solution, tilePath = _map.TileSolution
        });
        _writer.Flush();
    }

    private void Write<T>(T record) => _writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions));

    public void Dispose()
    {
        try
        {
            if (!_finished) Finish(TileSpriteMap.Create(_map, _seed), "interrupted");
        }
        finally { _writer.Dispose(); }
    }
}
