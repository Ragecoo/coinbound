using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ProceduralGeneration;

internal static class SpriteTests
{
    // Для каждой из 16 комбинаций N/E/S/W: углы, не покрытые прямыми сторонами.
    private static readonly int[] UncoveredCorners =
        [0xF0, 0x60, 0xC0, 0x40, 0x90, 0x00, 0x80, 0x00, 0x30, 0x20, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00];

    public static void Run()
    {
        foreach (var (dx, dy, facing) in new[] { (0, -1, "n"), (1, 0, "e"), (0, 1, "s"), (-1, 0, "w") })
        {
            var fixtureMap = new DungeonMap(11, 11);
            fixtureMap.Carve(new Cell(4, 4));
            fixtureMap.Carve(new Cell(4 + dx, 4 + dy));
            var position = new TilePosition(13 - dx, 13 - dy);
            fixtureMap.PlaceChest(position);
            var spec = TileSprites.Describe(fixtureMap, position, 1);
            Check(spec.Facing == facing && spec.ObjectSprite == $"object.chest.{facing}", "Четыре ориентации торцевого сундука");
            Check(TileSprites.Variant(spec.SpriteId).Id == spec.SpriteId, "Каталог содержит направленный спрайт");
        }
        SpawnTests.Run();
        var wallShapes = new HashSet<int>();
        for (int mask = 0; mask <= 255; mask++)
        {
            int shape = TileSprites.WallSpriteMask(mask);
            Check(shape == ExpectedWallMask(mask), $"Форма стены для соседей {mask:X2}");
            wallShapes.Add(shape);
            if (shape != 0)
                Check(TileSprites.Variant($"wall.{shape:X2}").Id == $"wall.{shape:X2}",
                    "Для каждой формы существует спрайт");
        }
        Check(wallShapes.Count == 47, "256 сочетаний соседей дают 46 видимых форм стен и скрытую стену");

        var fixture = new DungeonMap(11, 11);
        fixture.Carve(new Cell(2, 2), TileKind.RoomFloor);
        (TilePosition Position, string Sprite)[] edges =
        [
            (new(7, 5), "wall.04"), (new(7, 9), "wall.01"),
            (new(5, 7), "wall.02"), (new(9, 7), "wall.08"),
            (new(6, 5), "wall.04"), (new(8, 5), "wall.04"),
            (new(5, 5), "wall.20"), (new(9, 5), "wall.40"),
            (new(5, 9), "wall.10"), (new(9, 9), "wall.80")
        ];
        foreach (var edge in edges)
            Check(TileSprites.Describe(fixture, edge.Position, 42).TerrainSprite == edge.Sprite,
                $"Направление стороны/угла стены в {edge.Position}");
        TileSpecification straight = TileSprites.Describe(fixture, new TilePosition(7, 5), 42);
        Check(straight.FloorNeighborMask == 0x64 && straight.WallSpriteMask == 0x04,
            "Полная маска S+SE+SW сохраняется отдельно от формы S");
        TileSpecification hidden = TileSprites.Describe(fixture, new TilePosition(0, 0), 42);
        Check(!hidden.Visible && !hidden.Walkable && hidden.SpriteId == "none" && hidden.TerrainSprite is null && hidden.WallSpriteMask == 0,
            "Скрытая стена не получает спрайт и остаётся непроходимой");
        fixture.Carve(new Cell(2, 2), TileKind.Fountain);
        TileSpecification fountain = TileSprites.Describe(fixture, new TilePosition(7, 7), 42);
        Check(fountain.ObjectSprite == "object.fountain" && fountain.TerrainSprite!.StartsWith("floor.room.", StringComparison.Ordinal),
            "Фонтан имеет отдельный слой поверх пола комнаты");
        fixture.Carve(new Cell(1, 1));
        fixture.Carve(new Cell(2, 1));
        fixture.Carve(new Cell(1, 2));
        Check(TileSprites.Describe(fixture, new TilePosition(9, 6), 42).TerrainSprite == "wall.08",
            "Объект не меняет стену вокруг пола");
        var innerCorner = new DungeonMap(11, 11);
        innerCorner.Carve(new Cell(1, 1));
        innerCorner.Carve(new Cell(2, 1));
        innerCorner.Carve(new Cell(1, 2));
        Check(TileSprites.Describe(innerCorner, new TilePosition(6, 6), 42).TerrainSprite == "wall.09",
            "Внутренний угол N+W не получает лишний вариант из-за NE, SW и NW");

        var catalog = wallShapes.Where(mask => mask != 0).Select(mask => TileSprites.Variant($"wall.{mask:X2}"))
            .Concat(Enumerable.Range(0, 3).Select(i => TileSprites.Variant($"floor.corridor.{i}")))
            .Concat(Enumerable.Range(0, 3).Select(i => TileSprites.Variant($"floor.room.{i}")))
            .Concat(new[] { "none", "object.entrance", "object.fountain", "object.exit" }.Select(TileSprites.Variant)).ToArray();
        Check(catalog.Select(variant => variant.DebugColor).Distinct().Count() == catalog.Length,
            "Каждый ID имеет свой стабильный цвет");

        var settings = new GenerationSettings(21, 15, 42, 3);
        var generator = new DungeonGenerator(settings);
        using var text = new StringWriter();
        TileSpriteMap sprites;
        int stepCount = 0;
        using (var log = new GenerationLog(text, settings, generator.Map, 1, "memory"))
        {
            foreach (GenerationStep step in generator.Generate()) { log.Record(step); stepCount++; }
            sprites = TileSpriteMap.Create(generator.Map, settings.Seed);
            log.Finish(sprites, "complete", generator.AddedLoops);
        }
        DungeonMap map = generator.Map;
        Check(sprites.Tiles.Count == map.TileWidth * map.TileHeight, "Спецификация есть для каждого тайла");
        foreach (TileSpecification tile in sprites.Tiles)
        {
            if (tile.Kind == TileKind.Wall)
            {
                int shape = ExpectedWallMask(tile.FloorNeighborMask);
                Check(tile.WallSpriteMask == shape && tile.SpriteId == (shape == 0 ? "none" : $"wall.{shape:X2}"),
                    "Карта назначает стене общий спрайт по форме, сохраняя реальное соседство");
            }
            else Check(tile.WallSpriteMask is null, "У пола и объектов нет маски спрайта стены");
        }
        Check(sprites.Tiles.SequenceEqual(TileSpriteMap.Create(map, 42).Tiles), "Спецификации повторяются по seed");
        Check(sprites.Tiles.Zip(TileSpriteMap.Create(map, 43).Tiles).Any(pair => pair.First.TerrainSprite != pair.Second.TerrainSprite),
            "Seed влияет на варианты пола");
        foreach (string material in new[] { "floor.room.", "floor.corridor." })
            Check(sprites.Tiles.Select(tile => tile.TerrainSprite).OfType<string>()
                .Where(id => id.StartsWith(material, StringComparison.Ordinal)).Distinct().Count() == 3,
                "У каждого материала встречаются три варианта пола");
        for (int y = 0; y < map.TileHeight; y++)
            Check(Strip(MapColors.TileRow(map, y, cellWidth: 1)) == Strip(MapColors.VariantRow(sprites, y, cellWidth: 1)),
                "Диагностическая карта меняет только цвета, сохраняя все символы и пробелы");

        int tileCount = 0, loggedSteps = 0, roomCount = 0, spriteCount = 0;
        var positions = new HashSet<(int, int)>();
        string[] lines = text.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Check(!text.ToString().Contains('\u001b') && text.ToString().Contains("Маска соседнего пола", StringComparison.Ordinal),
            "Лог читается как обычный текст: русские описания без ANSI");
        foreach (string line in lines)
        {
            using JsonDocument json = JsonDocument.Parse(line);
            JsonElement root = json.RootElement;
            switch (root.GetProperty("record").GetString())
            {
                case "step": loggedSteps++; break;
                case "room": roomCount++; break;
                case "sprite": spriteCount++; break;
                case "tile":
                    JsonElement tile = root.GetProperty("data");
                    int x = tile.GetProperty("x").GetInt32(), y = tile.GetProperty("y").GetInt32();
                    Check(positions.Add((x, y)), "Координаты тайлов в логе не повторяются");
                    TileSpecification spec = sprites.Get(new TilePosition(x, y));
                    Check(tile.GetProperty("spriteId").GetString() == spec.SpriteId &&
                          tile.GetProperty("visible").GetBoolean() == spec.Visible &&
                          tile.GetProperty("walkable").GetBoolean() == spec.Walkable &&
                          tile.GetProperty("floorNeighborMask").GetInt32() == spec.FloorNeighborMask &&
                          (spec.WallSpriteMask is int shape
                              ? tile.GetProperty("wallSpriteMask").GetInt32() == shape
                              : tile.GetProperty("wallSpriteMask").ValueKind == JsonValueKind.Null) &&
                          tile.GetProperty("terrainSprite").GetString() == spec.TerrainSprite &&
                          tile.GetProperty("objectSprite").GetString() == spec.ObjectSprite,
                        "Запись тайла совпадает с данными, используемыми цветной картой");
                    tileCount++;
                    break;
            }
        }
        Check(tileCount == sprites.Tiles.Count && loggedSteps == stepCount && roomCount == map.Rooms.Count && spriteCount == sprites.Variants.Count,
            "Лог содержит все шаги, комнаты, варианты и тайлы");
        using (JsonDocument first = JsonDocument.Parse(lines[0]))
            Check(first.RootElement.GetProperty("record").GetString() == "header" &&
                  first.RootElement.GetProperty("formatVersion").GetInt32() == 2 &&
                  first.RootElement.GetProperty("wallSprites").GetString()!.Contains("WallSpriteMask", StringComparison.Ordinal),
                "Лог версии 2 описывает параметры генерации и правило выбора спрайтов стен");
        using (JsonDocument last = JsonDocument.Parse(lines[^1]))
            Check(last.RootElement.GetProperty("status").GetString() == "complete" &&
                  last.RootElement.GetProperty("tilePath").GetArrayLength() == map.TileSolution.Count,
                "Лог завершается статусом и полным путём");

        using (var export = new MemoryStream())
        {
            MapJsonExport.Write(export, settings, map, sprites, 2, generator.AddedLoops);
            export.Position = 0;
            using JsonDocument document = JsonDocument.Parse(export);
            JsonElement root = document.RootElement;
            Check(root.GetProperty("format").GetString() == "procedural-dungeon-map" &&
                  root.GetProperty("formatVersion").GetInt32() == 2 &&
                  root.GetProperty("status").GetString() == "complete" &&
                  root.GetProperty("floor").GetInt32() == 2 && root.GetProperty("seed").GetInt32() == settings.Seed,
                "JSON карты является одним документом с версией, номером этажа и seed");
            var readOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            TileSpecification[] restored = root.GetProperty("tiles").Deserialize<TileSpecification[]>(readOptions)!;
            Check(restored.SequenceEqual(sprites.Tiles),
                "Из JSON восстанавливаются все тайлы и атрибуты, включая скрытые стены и слои объектов");
            Check(root.GetProperty("dimensions").GetProperty("tileWidth").GetInt32() == map.TileWidth &&
                  root.GetProperty("dimensions").GetProperty("tileHeight").GetInt32() == map.TileHeight &&
                  root.GetProperty("rooms").GetArrayLength() == map.Rooms.Count &&
                  root.GetProperty("spriteVariants").GetArrayLength() == sprites.Variants.Count &&
                  root.GetProperty("entrance").Deserialize<TilePosition>(readOptions) == map.EntranceTile &&
                  root.GetProperty("exit").Deserialize<TilePosition>(readOptions) == map.ExitTile &&
                  root.GetProperty("tilePath").Deserialize<TilePosition[]>(readOptions)!.SequenceEqual(map.TileSolution),
                "JSON содержит размеры, комнаты, каталог спрайтов, вход, выход и маршрут готовой карты");
        }

        using var interruptedText = new StringWriter();
        var interrupted = new DungeonGenerator(settings);
        using (var log = new GenerationLog(interruptedText, settings, interrupted.Map, 1, "memory"))
            foreach (GenerationStep step in interrupted.Generate().Take(5)) log.Record(step);
        string[] interruptedLines = interruptedText.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        using (JsonDocument last = JsonDocument.Parse(interruptedLines[^1]))
            Check(last.RootElement.GetProperty("status").GetString() == "interrupted" &&
                  last.RootElement.GetProperty("tileCount").GetInt32() == map.TileWidth * map.TileHeight,
                "Прерванная генерация сохраняет честный статус и снимок всей текущей карты");
        Console.WriteLine("OK: все 256 масок соседей, 46 форм стен, слои объектов, цвета, JSONL-логи, экспорт полной карты в JSON и карта спрайтов.");
    }

    private static int ExpectedWallMask(int mask) => (mask & 0x0F) | (mask & UncoveredCorners[mask & 0x0F]);

    private static string Strip(string text) => Regex.Replace(text, @"\x1B\[[0-9;]*m", "");
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
