using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProceduralGeneration;

/// <summary>Один JSON-документ с полной готовой картой для загрузки в игру.</summary>
internal static class MapJsonExport
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Save(string logPath, GenerationSettings settings, DungeonMap map,
        TileSpriteMap sprites, int floor, int addedLoops, SpawnPlan? spawns = null)
    {
        string path = Path.ChangeExtension(logPath, ".json");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        Write(stream, settings, map, sprites, floor, addedLoops, spawns);
        return path;
    }

    internal static void Write(Stream stream, GenerationSettings settings, DungeonMap map,
        TileSpriteMap sprites, int floor, int addedLoops, SpawnPlan? spawns = null)
    {
        JsonSerializer.Serialize(stream, new
        {
            format = "procedural-dungeon-map", formatVersion = 2,
            status = "complete", createdUtc = DateTimeOffset.UtcNow, floor, seed = settings.Seed, settings,
            dimensions = new
            {
                sectionWidth = map.Width, sectionHeight = map.Height, sectionSize = DungeonMap.SectionSize,
                tileWidth = map.TileWidth, tileHeight = map.TileHeight
            },
            coordinates = "От нуля: X вправо, Y вниз. Tiles идут по строкам: индекс = y * tileWidth + x. Section — секция, LocalTile — тайл внутри секции (0..2).",
            neighborBits = new { N = 1, E = 2, S = 4, W = 8, NE = 16, SE = 32, SW = 64, NW = 128 },
            wallSpriteRule = "FloorNeighborMask хранит всех соседей. WallSpriteMask исключает диагональ, если хотя бы с одной прилегающей прямой стороны есть пол. Ключ wall.XX содержит упрощённую маску в hex.",
            layers = "TerrainSprite — поверхность, ObjectSprite — объект поверх неё. SpriteId — ключ цвета диагностики. Visible=false и none означают скрытую непроходимую стену. Ключи спрайтов логические, изображения подключаются отдельно.",
            entrance = map.EntranceTile, exit = map.ExitTile,
            spawns,
            chestMinimumDistance = spawns?.Settings.ChestDistance ?? map.ChestMinimumDistance,
            statistics = new { tileCount = sprites.Tiles.Count, floorSections = map.FloorCount, floorTiles = map.FloorTileCount, addedLoops },
            rooms = map.Rooms.Select((room, id) => new { id, data = room }),
            spriteVariants = sprites.Variants,
            tiles = sprites.Tiles,
            sectionPath = map.Solution, tilePath = map.TileSolution
        }, Options);
    }
}
