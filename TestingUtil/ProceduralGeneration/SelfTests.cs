using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ProceduralGeneration;

internal static class SelfTests
{
    public static int Run()
    {
        var timer = Stopwatch.StartNew();
        int cases = 0;
        string context = "начало";
        try
        {
            (int Width, int Height)[] sizes = [(11, 11), (21, 15), (39, 19), (61, 25), (81, 35)];
            int?[] rooms = [null, 0, 1, 3, 8];
            var observedSizes = new HashSet<(int, int)>();
            for (int seed = 0; seed < 256; seed++)
            {
                var size = sizes[seed % sizes.Length];
                int? count = rooms[seed / sizes.Length % rooms.Length];
                var settings = new GenerationSettings(size.Width, size.Height, seed, count);
                context = $"seed={seed}, {size.Width}x{size.Height}, rooms={count}";
                DungeonMap map = Generate(settings);
                ValidateMap(map);
                Require(Signature(map) == Signature(Generate(settings)), "Повторяемость по seed");
                Require(map.Rooms.Count(room => room.Kind == RoomKind.Regular) <= (count ?? 5), "Лимит только обычных комнат");
                foreach (DungeonRoom room in map.Rooms.Where(room => room.Kind != RoomKind.Entrance))
                    observedSizes.Add((room.Width, room.Height));
                cases++;
            }
            Require(observedSizes.SetEquals([(5, 5), (4, 3), (5, 3)]), "Все три размера обычных комнат встречаются");
            Console.WriteLine("OK: 256 конфигураций — без диагональных стыков; размеры, стены, фонтаны, тупики и повторяемость.");

            foreach (var size in sizes)
            {
                context = $"обходные пути {size}";
                var settings = new GenerationSettings(size.Width, size.Height, 42, 0, 0);
                DungeonMap baseline = Generate(settings);
                ValidateMap(baseline);
                Require(baseline.Rooms.Count == 1 && baseline.Rooms[0].Kind == RoomKind.Entrance,
                    "При --rooms 0 остаётся только стартовая комната с фонтаном");
                var withLoops = new DungeonGenerator(settings with { LoopChance = 1 });
                foreach (GenerationStep _ in withLoops.Generate()) { }
                ValidateMap(withLoops.Map);
                Require(withLoops.AddedLoops > 0 && withLoops.Map.FloorCount > baseline.FloorCount,
                    "Дополнительные проходы действительно открываются");
                cases += 2;
            }
            Console.WriteLine("OK: --rooms 0 сохраняет только стартовую комнату с фонтаном; обходные пути не разрушают стены.");

            for (int seed = 0; seed < 128; seed++)
            {
                context = $"тупики при максимуме обходных путей и комнат, seed={seed}";
                ValidateMap(Generate(new GenerationSettings(seed % 2 == 0 ? 11 : 21, 15, seed, 8, 1)));
                cases++;
            }
            Console.WriteLine("OK: выход остаётся конечной клеткой тупика при --loops 1 и --rooms 8.");

            for (int seed = 0; seed < 8; seed++)
            {
                context = $"промежуточные шаги seed={seed}";
                var generator = new DungeonGenerator(new GenerationSettings(21, 15, seed, 3));
                int lastStep = 0, lastFloorCount = 0;
                var stages = new HashSet<GenerationStage>();
                foreach (GenerationStep step in generator.Generate())
                {
                    stages.Add(step.Stage);
                    Require(step.Number == ++lastStep, "Нумерация шагов");
                    Require(generator.Map.FloorCount >= lastFloorCount, "Пол не исчезает между шагами");
                    lastFloorCount = generator.Map.FloorCount;
                    ValidateWalls(generator.Map);
                    ValidateTiles(generator.Map);
                    if (step.Stage is GenerationStage.Loops or GenerationStage.Exit or GenerationStage.Complete)
                    {
                        Require(Distances(generator.Map, generator.Map.Entrance!.Value).Count == lastFloorCount,
                            "После соединения каждый шаг сохраняет связность");
                        ValidateCorridorJunctions(generator.Map);
                    }
                }
                Require(stages.Count == Enum.GetValues<GenerationStage>().Length, "Все стадии процесса видимы");
                ExpectException<InvalidOperationException>(() => generator.Generate().ToList());
                cases++;
            }
            Console.WriteLine("OK: внешняя стена и оболочки комнат сохранены на каждом шаге анимации.");

            for (int seed = 0; seed < 48; seed++)
            {
                context = $"комнаты по умолчанию seed={seed}";
                DungeonMap map = Generate(new GenerationSettings(Seed: seed));
                ValidateMap(map);
                cases++;
            }
            DungeonMap counted = Generate(new GenerationSettings(61, 25, 42, 4));
            ValidateMap(counted);
            Require(counted.Rooms.Count == 5 && counted.Rooms.Count(room => room.Kind == RoomKind.Regular) == 4,
                "--rooms 4 даёт четыре обычные комнаты плюс стартовую при наличии места");
            cases++;
            foreach (int seed in new[] { int.MinValue, int.MaxValue, -1, 0 })
            {
                context = $"максимальная карта, seed={seed}";
                ValidateMap(Generate(new GenerationSettings(151, 81, seed, 8, 1)));
                cases++;
            }
            foreach (var size in new[] { (Width: 11, Height: 81), (Width: 151, Height: 11) })
            {
                context = $"вытянутая карта {size}";
                ValidateMap(Generate(new GenerationSettings(size.Width, size.Height, 42, 8, 1)));
                cases++;
            }
            Console.WriteLine("OK: ограничение фонтанов, максимальные/вытянутые карты и граничные seed.");

            context = "цветное отображение";
            var paletteMap = new DungeonMap(11, 11);
            TileKind[] tiles = Enum.GetValues<TileKind>();
            Require(tiles.Select(MapColors.Foreground).Distinct().Count() == tiles.Length, "Типы клеток различимы по цвету");
            for (int i = 1; i < tiles.Length; i++)
                paletteMap.Carve(new Cell(i, 1), tiles[i]);
            string coloredRow = MapColors.Row(paletteMap, 1);
            string stripped = Regex.Replace(coloredRow, @"\x1B\[[0-9;]*m", "");
            Require(stripped.Length == paletteMap.Width * 2 && stripped == "# . . S F E C # " + new string(' ', 6),
                "Стены, пол, вход, фонтан и выход отображаются символами");
            foreach (TileKind tile in tiles)
                Require(coloredRow.Contains(MapColors.Foreground(tile)), "Цвет символов каждого типа выводится");
            Cell fountain = new((int)TileKind.Fountain, 1);
            Require(paletteMap.GetSection(fountain) == TileKind.Fountain, "Тестовая клетка фонтана");
            string highlighted = MapColors.Row(paletteMap, 1, fountain, [fountain]);
            Require(highlighted.Contains(MapColors.Foreground(TileKind.Fountain)) &&
                    Regex.Replace(highlighted, @"\x1B\[[0-9;]*m", "") == stripped,
                "Подсветка процесса и пути не скрывает символ фонтана");
            Require(Regex.Replace(MapColors.Row(paletteMap, 1, cellWidth: 1), @"\x1B\[[0-9;]*m", "") == "#..SFEC#   ",
                "Компактное отображение сохраняет все символы");
            Console.WriteLine("OK: цветные символы, компактный режим и сохранение обозначений объектов.");

            context = "отображение отдельных тайлов";
            string tileRow = Regex.Replace(MapColors.TileRow(paletteMap, 4, cellWidth: 1), @"\x1B\[[0-9;]*m", "");
            Require(tileRow.Length == 33, "11 секций отображаются как 33 отдельных тайла");
            Require(tileRow.Substring(9, 3) == ".S." && tileRow.Substring(12, 3) == ".F." &&
                    tileRow.Substring(15, 3) == ".E.", "Объекты занимают по одному тайлу в центре секции");
            string upperTiles = Regex.Replace(MapColors.TileRow(paletteMap, 3, cellWidth: 1), @"\x1B\[[0-9;]*m", "");
            Require(upperTiles.Substring(9, 9) == ".........", "Вокруг объектов остаётся пол");
            Require(paletteMap.SectionAt(new TilePosition(7, 10)) == new Cell(2, 3) &&
                    paletteMap.SectionCenter(new Cell(2, 3)) == new TilePosition(7, 10), "Перевод координат секций и тайлов");
            foreach (TilePosition outside in new[] { new TilePosition(-1, 4), new TilePosition(4, -1),
                         new TilePosition(33, 4), new TilePosition(4, 33) })
            {
                Require(paletteMap.GetTile(outside) == TileKind.Wall, "За границей тайловой карты — стена");
                ExpectException<ArgumentOutOfRangeException>(() => paletteMap.SectionAt(outside));
            }
            ExpectException<ArgumentOutOfRangeException>(() => paletteMap.SectionOrigin(new Cell(-1, 0)));
            Require(!CommandLineOptions.Parse([]).Sections && CommandLineOptions.Parse(["--sections"]).Sections,
                "По умолчанию показаны тайлы, --sections включает обзор секций");
            Console.WriteLine("OK: секции 3x3 тайла, одиночные объекты, координаты и два масштаба отображения.");

            context = "спрайты и логи";
            SpriteTests.Run();

            context = "неверные параметры";
            GenerationSettings[] invalid =
            [
                new(Width: 10), new(Width: 153), new(Height: 12), new(Height: 83),
                new(HallCount: -1), new(HallCount: 9), new(LoopChance: -0.1),
                new(LoopChance: 1.1), new(LoopChance: double.NaN), new(LoopChance: double.PositiveInfinity)
            ];
            foreach (GenerationSettings settings in invalid)
                ExpectException<ArgumentException>(() => new DungeonGenerator(settings));
            string[][] invalidArguments =
            [
                ["--seed"], ["--seed", "abc"], ["--width", "12"], ["--delay", "-1"],
                ["--delay", "1001"], ["--floors", "0"], ["--floors", "101"], ["--unknown"],
                ["--loops", "NaN"], ["--loops", "0,2"], ["--rooms", "9"], ["--halls", "-1"]
            ];
            foreach (string[] arguments in invalidArguments)
                ExpectException<ArgumentException>(() => CommandLineOptions.Parse(arguments));
            Require(CommandLineOptions.Parse(["--seed", "42", "--rooms", "3"]).Settings ==
                    CommandLineOptions.Parse(["--seed", "42", "--halls", "3"]).Settings, "Совместимость --halls");
            Console.WriteLine("OK: проверка аргументов и совместимость --halls / --rooms.");
            Console.WriteLine($"Все проверки пройдены: {cases} конфигураций (+ повторы), {timer.Elapsed.TotalSeconds:F2} с.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ПРОВЕРКА НЕ ПРОЙДЕНА ({context}): {exception}");
            return 1;
        }
    }

    private static DungeonMap Generate(GenerationSettings settings)
    {
        var generator = new DungeonGenerator(settings);
        GenerationStep? last = null;
        foreach (GenerationStep step in generator.Generate())
        {
            Require(step.Progress is >= 0 and <= 100, "Прогресс в пределах 0–100%");
            Require(step.Progress >= (last?.Progress ?? 0), "Прогресс не идёт назад");
            Require(!string.IsNullOrWhiteSpace(step.Status), "У каждого шага есть подпись этапа");
            if (last is null) Require(step.Progress == 0, "Новый этаж начинается с 0%");
            Require((step.Progress == 100) == (step.Stage == GenerationStage.Complete),
                "100% появляется только после полного завершения");
            last = step;
        }
        Require(last?.Stage == GenerationStage.Complete, "Генерация завершается");
        return generator.Map;
    }

    private static void ValidateWalls(DungeonMap map)
    {
        for (int x = 0; x < map.Width; x++)
            Require(!map.IsFloor(x, 0) && !map.IsFloor(x, map.Height - 1), "Верхняя/нижняя граница");
        for (int y = 0; y < map.Height; y++)
            Require(!map.IsFloor(0, y) && !map.IsFloor(map.Width - 1, y), "Левая/правая граница");
        foreach (DungeonRoom room in map.Rooms)
            foreach (Cell cell in room.Perimeter())
                if (!room.Openings.Contains(cell))
                    Require(map.GetSection(cell) == TileKind.Wall, "Периметр комнаты сплошной, кроме открытых проходов");
    }

    private static void ValidateMap(DungeonMap map)
    {
        ValidateWalls(map);
        ValidateTiles(map);
        ValidateCorridorJunctions(map);
        Require(map.Entrance is not null && map.Exit is not null, "Вход и выход заданы");
        Cell entrance = map.Entrance!.Value, exit = map.Exit!.Value;
        Require(map.GetSection(entrance) == TileKind.Entrance && map.GetSection(exit) == TileKind.Exit && entrance != exit,
            "Вход и выход имеют отдельные типы клеток");
        DungeonRoom start = map.Rooms.Single(room => room.Kind == RoomKind.Entrance);
        Require(start.Width == 3 && start.Height == 4, "Старт: 3 по горизонтали, 4 по вертикали");
        Require(entrance == new Cell(start.Left + 1, start.Top + 1), "Вход в локальной клетке (2,2)");
        Cell startFountain = new(start.Left + 1, start.Top + 2);
        Require(map.GetSection(startFountain) == TileKind.Fountain, "Стартовый фонтан в локальной клетке (2,3)");
        Require(map.Floors().Count(cell => map.GetSection(cell) == TileKind.Fountain) == 1,
            "На этаже ровно один фонтан — в стартовой комнате");
        Require(map.Rooms.All(room => room.Kind is RoomKind.Entrance or RoomKind.Regular),
            "На этаже есть только стартовая комната и обычные залы");

        foreach (DungeonRoom room in map.Rooms)
        {
            if (room.Kind == RoomKind.Regular)
                Require((room.Width, room.Height) is (5, 5) or (4, 3) or (5, 3), "Обычная комната 5x5, 4x3 или 5x3");
            foreach (Cell cell in room.Interior())
                Require(map.GetSection(cell) is TileKind.RoomFloor or TileKind.Entrance or TileKind.Fountain,
                    "Прямоугольник комнаты заполнен полом и объектами");
            Require(room.Openings.Count == (room.Kind == RoomKind.Regular ? 2 : 1), "Количество открытых проходов комнаты");
            foreach (Cell opening in room.Openings)
            {
                Require(room.OnPerimeter(opening) && map.GetSection(opening) == TileKind.Corridor, "Проход в стене комнаты — обычный пол");
                Cell[] inward = DungeonMap.Adjacent(opening).Where(room.Contains).ToArray();
                Require(inward.Length == 1, "Проход не в углу");
                Cell outside = new(2 * opening.X - inward[0].X, 2 * opening.Y - inward[0].Y);
                Require(map.IsFloor(outside) && !map.Rooms.Any(other => other.Contains(outside)),
                    "Открытый проход ведёт в коридор");
            }
            foreach (DungeonRoom other in map.Rooms.Where(other => !ReferenceEquals(other, room)))
                Require(!room.Interior().Concat(room.Perimeter()).Any(cell => other.Contains(cell) || other.OnPerimeter(cell)),
                    "Комнаты и стены не пересекаются");
        }

        Dictionary<Cell, int> distances = Distances(map, entrance);
        Require(distances.Count == map.FloorCount && map.Floors().Count() == map.FloorCount, "Весь этаж доступен из входа");
        Require(!map.Rooms.Any(room => room.Contains(exit)), "Выход расположен в лабиринте");
        int NeighborCount(Cell cell) => new[] { new Cell(cell.X - 1, cell.Y), new Cell(cell.X + 1, cell.Y),
            new Cell(cell.X, cell.Y - 1), new Cell(cell.X, cell.Y + 1) }.Count(map.IsFloor);
        Require(NeighborCount(exit) == 1, "Выход — последняя клетка тупика, с одним проходимым соседом");
        Require(map.Floors().Where(cell => map.GetSection(cell) == TileKind.Corridor && NeighborCount(cell) == 1)
            .All(cell => distances[cell] <= distances[exit]), "Выбран наиболее удалённый тупик");
        Require(map.Solution.Count - 1 == distances[exit], "Маршрут кратчайший");
        Require(map.Solution[0] == entrance && map.Solution[^1] == exit, "Концы маршрута");
        for (int i = 1; i < map.Solution.Count; i++)
        {
            Cell previous = map.Solution[i - 1], current = map.Solution[i];
            Require(map.IsFloor(current) && Math.Abs(previous.X - current.X) + Math.Abs(previous.Y - current.Y) == 1,
                "Маршрут проходит по соседним клеткам пола");
        }
        Require(map.TileSolution.Count == (map.Solution.Count - 1) * 3 + 1,
            "Маршрут по центрам секций развёрнут в непрерывный тайловый путь");
        Require(map.TileSolution[0] == map.EntranceTile && map.TileSolution[^1] == map.ExitTile,
            "Тайловый путь соединяет вход и выход");
        for (int i = 1; i < map.TileSolution.Count; i++)
        {
            TilePosition previous = map.TileSolution[i - 1], current = map.TileSolution[i];
            Require(map.IsTileFloor(current) && Math.Abs(previous.X - current.X) + Math.Abs(previous.Y - current.Y) == 1,
                "Тайловый путь проходит только по соседним проходимым тайлам");
        }
    }

    private static void ValidateTiles(DungeonMap map)
    {
        Require(map.TileWidth == map.Width * 3 && map.TileHeight == map.Height * 3,
            "Размер тайловой карты в три раза больше по каждой оси");
        int floors = 0, entrances = 0, fountains = 0, exits = 0;
        for (int y = 0; y < map.TileHeight; y++)
            for (int x = 0; x < map.TileWidth; x++)
            {
                TileKind section = map.GetSection(new Cell(x / 3, y / 3));
                bool center = x % 3 == 1 && y % 3 == 1;
                TileKind expected = section switch
                {
                    TileKind.Entrance or TileKind.Fountain when !center => TileKind.RoomFloor,
                    TileKind.Exit or TileKind.Chest when !center => TileKind.Corridor,
                    _ => section
                };
                TileKind actual = map.GetTile(new TilePosition(x, y));
                if (map.Chests.Contains(new TilePosition(x, y))) expected = TileKind.Chest;
                Require(actual == expected, "Все девять тайлов соответствуют секции и центральному объекту");
                if (actual != TileKind.Wall) floors++;
                if (actual == TileKind.Entrance) entrances++;
                if (actual == TileKind.Fountain) fountains++;
                if (actual == TileKind.Exit) exits++;
                if (x < 3 || x >= map.TileWidth - 3 || y < 3 || y >= map.TileHeight - 3)
                    Require(actual == TileKind.Wall, "Внешняя стена толщиной три тайла");
            }
        Require(floors == map.FloorCount * 9 && floors == map.FloorTileCount, "Каждая проходимая секция содержит девять тайлов пола");
        Require(entrances <= 1 && fountains <= 1 && exits <= 1, "Вход, фонтан и выход не дублируются на девять тайлов");
        foreach (DungeonRoom room in map.Rooms)
            Require(room.TileWidth == room.Width * 3 && room.TileHeight == room.Height * 3 &&
                    room.TileLeft == room.Left * 3 && room.TileTop == room.Top * 3, "Границы комнат в тайлах соответствуют секциям");
    }

    private static void ValidateCorridorJunctions(DungeonMap map)
    {
        for (int y = 0; y < map.Height - 1; y++)
            for (int x = 0; x < map.Width - 1; x++)
            {
                bool topLeft = map.IsFloor(x, y), topRight = map.IsFloor(x + 1, y);
                bool bottomLeft = map.IsFloor(x, y + 1), bottomRight = map.IsFloor(x + 1, y + 1);
                Require(!(topLeft == bottomRight && topRight == bottomLeft && topLeft != topRight),
                    $"Проходы не соприкасаются только по диагонали: блок 2x2 в ({x},{y})");
            }
    }

    // Независимый поиск: не использует обход генератора или Map.Neighbors.
    private static Dictionary<Cell, int> Distances(DungeonMap map, Cell start)
    {
        var distances = new Dictionary<Cell, int> { [start] = 0 };
        var queue = new Queue<Cell>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out Cell cell))
        {
            Cell[] neighbors = [new(cell.X - 1, cell.Y), new(cell.X + 1, cell.Y),
                                new(cell.X, cell.Y - 1), new(cell.X, cell.Y + 1)];
            foreach (Cell neighbor in neighbors)
                if (map.IsFloor(neighbor) && distances.TryAdd(neighbor, distances[cell] + 1))
                    queue.Enqueue(neighbor);
        }
        return distances;
    }

    private static string Signature(DungeonMap map) =>
        string.Join(',', Enumerable.Range(0, map.Width * map.Height)
            .Select(index => (int)map.GetSection(new Cell(index % map.Width, index / map.Width)))) +
        "|" + string.Join(';', map.Rooms.Select(room =>
            $"{room.Left},{room.Top},{room.Width},{room.Height},{room.Kind}:{string.Join(',', room.Openings)}")) +
        $"|{map.Entrance}|{map.Exit}|" + string.Join(';', map.Solution);

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }

    private static void ExpectException<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Ожидалось исключение {typeof(T).Name}.");
    }
}

