// Ported from TestingUtil/ProceduralGeneration. Only C# / .NET compatibility changes.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace ProceduralGeneration
{

public sealed record GenerationSettings(
    int Width = 39, int Height = 19, int Seed = 1, int? HallCount = null, double LoopChance = 0.08)
{
    public void Validate()
    {
        if (Width < 11 || Width > 151 || Width % 2 == 0)
            throw new ArgumentException("Ширина в секциях должна быть нечётным числом от 11 до 151.");
        if (Height < 11 || Height > 81 || Height % 2 == 0)
            throw new ArgumentException("Высота в секциях должна быть нечётным числом от 11 до 81.");
        if (HallCount is < 0 or > 8)
            throw new ArgumentException("Количество обычных комнат должно быть от 0 до 8.");
        if ((double.IsNaN(LoopChance) || double.IsInfinity(LoopChance)) || LoopChance < 0 || LoopChance > 1)
            throw new ArgumentException("Вероятность дополнительных проходов должна быть от 0 до 1.");
    }
}

public enum GenerationStage { Fill, Rooms, Maze, Connections, Loops, Exit, Complete }
public sealed record GenerationStep(GenerationStage Stage, Cell? Cursor, int Number, string Detail)
{
    // Общий прогресс по этапам, а не оценка оставшегося времени.
    public int Progress { get; init; }
    public string Status { get; init; } = "";
}

/// <summary>
/// MoveNext() выполняет очередной шаг и обновляет Map. Комнаты и участки лабиринта
/// сначала строятся отдельно, затем соединяются через разрешённые проёмы.
/// Один экземпляр создаёт один этаж; отображение не влияет на генерацию.
/// </summary>
public sealed class DungeonGenerator
{
    private static readonly Cell[] Directions = { new(0, -2), new(2, 0), new(0, 2), new(-2, 0) };
    private readonly GenerationSettings _settings;
    private readonly Random _random;
    private readonly bool[] _reserved;
    private readonly bool[] _protectedWall;
    private int _step;
    private int _progress;
    private string _status = "";
    private bool _started;

    public DungeonGenerator(GenerationSettings settings)
    {
        settings.Validate();
        _settings = settings;
        _random = new Random(settings.Seed);
        Map = new DungeonMap(settings.Width, settings.Height);
        _reserved = new bool[Map.Width * Map.Height];
        _protectedWall = new bool[_reserved.Length];
    }

    public DungeonMap Map { get; }
    public int AddedLoops { get; private set; }
    private Cell ExitBranchEnd => new(Map.Width - 2, Map.Height - 2);

    public IEnumerable<GenerationStep> Generate()
    {
        if (_started) throw new InvalidOperationException("Этот генератор уже запущен. Создайте новый экземпляр.");
        _started = true;
        yield return Step(GenerationStage.Fill, null, "Внешняя граница остаётся сплошной стеной",
            0, "Возведение стен подземелья...");
        PlanRooms();
        yield return Step(GenerationStage.Rooms, null, $"Комнат: {Map.Rooms.Count}, включая стартовую",
            2, "Планирование комнат...");
        int roomWork = Map.Rooms.Sum(room => room.Width * room.Height + room.Openings.Count);
        int built = 0;
        foreach (DungeonRoom room in Map.Rooms)
        {
            string status = room.Kind switch
            {
                RoomKind.Entrance => "Создание входной комнаты...",
                _ => "Создание прямоугольных залов..."
            };
            foreach (Cell cell in room.Interior())
            {
                Map.Carve(cell, TileKind.RoomFloor);
                yield return Step(GenerationStage.Rooms, cell, $"Комната {room.Width}x{room.Height} секций: строим пол и стены",
                    2 + 16 * ++built / roomWork, status);
            }

            foreach (Cell opening in room.Openings)
            {
                Map.Carve(opening);
                yield return Step(GenerationStage.Rooms, opening, "Оставляем открытый проход; остальные стены защищены",
                    2 + 16 * ++built / roomWork);
            }
        }
        DungeonRoom entranceRoom = Map.Rooms.Single(room => room.Kind == RoomKind.Entrance);
        // Локальные координаты секций от 1: вход (2,2), единственный фонтан (2,3).
        Map.Entrance = new Cell(entranceRoom.Left + 1, entranceRoom.Top + 1);
        Map.Carve(Map.Entrance.Value, TileKind.Entrance);
        Cell fountain = new(entranceRoom.Left + 1, entranceRoom.Top + 2);
        Map.Carve(fountain, TileKind.Fountain);
        yield return Step(GenerationStage.Rooms, fountain, "Устанавливаем вход и фонтан в стартовой комнате",
            20, "Размещение входа и фонтана...");

        // DFS обходит зарезервированные комнаты вместе с их стенами.
        // Отдельные участки вокруг препятствий объединяет следующая стадия.
        int mazeNodes = 0;
        for (int y = 1; y < Map.Height - 1; y += 2)
            for (int x = 1; x < Map.Width - 1; x += 2)
                if (!_reserved[Index(new Cell(x, y))] && !Map.IsFloor(x, y)) mazeNodes++;
        // Каждый узел посещается один раз и один раз снимается со стека DFS.
        int mazeWork = Math.Max(1, mazeNodes * 2);
        int processed = 0;
        yield return Step(GenerationStage.Maze, null, "Прокладываем лабиринт вокруг комнат",
            20, "Прокладывание коридоров лабиринта...");
        var stack = new Stack<Cell>();
        for (int y = 1; y < Map.Height - 1; y += 2)
        {
            for (int x = 1; x < Map.Width - 1; x += 2)
            {
                var start = new Cell(x, y);
                if (_reserved[Index(start)] || Map.IsFloor(start)) continue;
                Map.Carve(start);
                stack.Push(start);
                yield return Step(GenerationStage.Maze, start, "Начало участка лабиринта", 20 + 55 * ++processed / mazeWork);
                while (stack.Count > 0)
                {
                    Cell current = stack.Peek();
                    Cell[] candidates = Directions.Select(direction =>
                        new Cell(current.X + direction.X, current.Y + direction.Y))
                        .Where(next => Map.IsInterior(next) && !_reserved[Index(next)] && !Map.IsFloor(next) &&
                            !_reserved[Index(new Cell((current.X + next.X) / 2, (current.Y + next.Y) / 2))])
                        .ToArray();
                    if (candidates.Length == 0)
                    {
                        stack.Pop();
                        yield return Step(GenerationStage.Maze, stack.Count > 0 ? stack.Peek() : null,
                            "Возвращаемся к развилке", 20 + 55 * ++processed / mazeWork);
                        continue;
                    }
                    Cell next = candidates[_random.Next(candidates.Length)];
                    Cell passage = new((current.X + next.X) / 2, (current.Y + next.Y) / 2);
                    Map.Carve(passage);
                    yield return Step(GenerationStage.Maze, passage, "Прокладываем коридор");
                    Map.Carve(next);
                    stack.Push(next);
                    yield return Step(GenerationStage.Maze, next, "Прокладываем коридор", 20 + 55 * ++processed / mazeWork);
                }
            }
        }

        yield return Step(GenerationStage.Connections, null, "Соединяем все участки через открытые проходы",
            75, "Соединение комнат с лабиринтом...");
        // Короткая ветка у границы гарантирует хотя бы один тупик даже при --loops 1.
        // Её начало — посещённая DFS клетка (Width - 4, Height - 2).
        Cell branchPassage = new(ExitBranchEnd.X - 1, ExitBranchEnd.Y);
        Map.Carve(branchPassage);
        yield return Step(GenerationStage.Connections, branchPassage, "Прокладываем тупиковую ветку");
        Map.Carve(ExitBranchEnd);
        yield return Step(GenerationStage.Connections, ExitBranchEnd, "Завершаем тупиковую ветку");
        int openings = Map.Rooms.Sum(room => room.Openings.Count);
        int connected = 0;
        foreach (DungeonRoom room in Map.Rooms)
        {
            foreach (Cell opening in room.Openings)
            {
                foreach (Cell cell in FindOpeningConnection(Outside(room, opening)))
                {
                    if (Map.IsFloor(cell)) continue;
                    Map.Carve(cell);
                    yield return Step(GenerationStage.Connections, cell, "Соединяем комнату с коридором");
                }
                yield return Step(GenerationStage.Connections, opening, "Проход комнаты соединён с лабиринтом",
                    75 + 5 * ++connected / openings);
            }
        }
        while (true)
        {
            SearchResult reachable = Search(Map.Entrance!.Value);
            int reached = reachable.Distances.Count(distance => distance >= 0);
            yield return Step(GenerationStage.Connections, null, $"Доступно секций: {reached}/{Map.FloorCount}",
                80 + 5 * reached / Map.FloorCount, "Соединение участков подземелья...");
            if (reached == Map.FloorCount) break;
            foreach (Cell cell in FindConnection(reachable.Distances))
            {
                if (Map.IsFloor(cell)) continue;
                Map.Carve(cell);
                yield return Step(GenerationStage.Connections, cell, "Соединяем комнату или участок лабиринта");
            }
        }
        yield return Step(GenerationStage.Connections, null, "Проверяем диагональные касания проходов",
            85, "Исправление стыков коридоров...");
        foreach (Cell cell in RepairDiagonalContacts())
            yield return Step(GenerationStage.Connections, cell, "Убираем диагональное касание проходов");
        yield return Step(GenerationStage.Loops, null, "Добавляем обходные пути, сохраняя стены комнат",
            90, "Создание обходных путей...");
        var walls = new List<Cell>();
        for (int y = 1; y < Map.Height - 1; y++)
            for (int x = 1; x < Map.Width - 1; x++)
                if (CanConnect(new Cell(x, y))) walls.Add(new Cell(x, y));
        Shuffle(walls);
        for (int i = 0; i < walls.Count; i++)
        {
            Cell wall = walls[i];
            bool carve = _random.NextDouble() < _settings.LoopChance && CanConnect(wall);
            if (carve)
            {
                Map.Carve(wall);
                AddedLoops++;
            }
            yield return Step(GenerationStage.Loops, carve ? wall : null, $"Дополнительных проходов: {AddedLoops}",
                90 + 7 * (i + 1) / walls.Count);
        }

        yield return Step(GenerationStage.Exit, null, "Ищем наиболее удалённую конечную секцию тупика",
            98, "Поиск выхода в дальнем тупике...");
        SearchResult search = Search(Map.Entrance!.Value);
        Cell exit = Map.Floors().Where(cell => Map.GetSection(cell) == TileKind.Corridor && Map.Neighbors(cell).Count() == 1)
            .MaxByStable(cell => search.Distances[Index(cell)]);
        Map.Exit = exit;
        Map.Carve(exit, TileKind.Exit);
        var route = new List<Cell>();
        for (int index = Index(exit); index >= 0; index = search.Parents[index])
            route.Add(new Cell(index % Map.Width, index / Map.Width));
        route.Reverse();
        Map.Solution = route;
        yield return Step(GenerationStage.Complete, null,
            $"Готово: комнат {Map.Rooms.Count}, фонтанов {Map.Floors().Count(c => Map.GetSection(c) == TileKind.Fountain)}, путь {route.Count - 1} секций",
            100, "Подземелье готово!");
    }

    private void PlanRooms()
    {
        // Защищаем боковую стену и пол гарантированной ветки от комнат и обходных путей.
        for (int x = ExitBranchEnd.X - 1; x <= ExitBranchEnd.X; x++)
        {
            _reserved[Index(new Cell(x, ExitBranchEnd.Y))] = true;
            Cell sideWall = new(x, ExitBranchEnd.Y - 1);
            _reserved[Index(sideWall)] = true;
            _protectedWall[Index(sideWall)] = true;
        }
        Map.AddRoom(new DungeonRoom(1, 1, 3, 4, RoomKind.Entrance));
        // Стартовая комната обязательна и не расходует лимит обычных комнат.
        int requested = _settings.HallCount ?? _random.Next(3, 6);
        for (int i = 0; i < requested; i++)
        {
            DungeonRoom? room = FindRoom();
            if (room is not null) Map.AddRoom(room);
        }
        foreach (DungeonRoom room in Map.Rooms)
        {
            foreach (Cell cell in room.Interior()) _reserved[Index(cell)] = true;
            foreach (Cell cell in room.Perimeter())
            {
                _reserved[Index(cell)] = true;
                _protectedWall[Index(cell)] = true;
            }
        }
        foreach (DungeonRoom room in Map.Rooms)
        {
            var candidates = room.Perimeter().Where(opening =>
                (opening.X >= room.Left && opening.X <= room.Right || opening.Y >= room.Top && opening.Y <= room.Bottom) &&
                Map.IsInterior(opening) && Map.IsInterior(Outside(room, opening)) &&
                !_reserved[Index(Outside(room, opening))]).ToList();
            Shuffle(candidates);
            if (candidates.Count == 0) throw new InvalidOperationException("Не удалось выбрать проход в комнату.");
            room.AddOpening(candidates[0]);
            if (room.Kind == RoomKind.Regular)
            {
                Cell firstOutside = Outside(room, candidates[0]);
                Cell direction = new(firstOutside.X - candidates[0].X, firstOutside.Y - candidates[0].Y);
                foreach (Cell candidate in candidates.Skip(1))
                {
                    Cell otherOutside = Outside(room, candidate);
                    if (otherOutside.X - candidate.X == direction.X && otherOutside.Y - candidate.Y == direction.Y) continue;
                    room.AddOpening(candidate);
                    break;
                }
            }
            foreach (Cell opening in room.Openings) _protectedWall[Index(opening)] = false;
        }
    }

    private DungeonRoom? FindRoom()
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            (int width, int height) = _random.Next(3) switch
            {
                0 => (5, 5),
                1 => (4, 3),
                _ => (5, 3)
            };
            var room = new DungeonRoom(_random.Next(1, Map.Width - width),
                _random.Next(1, Map.Height - height), width, height, RoomKind.Regular);
            if (CanPlaceRoom(room)) return room;
        }
        return null;
    }

    private bool CanPlaceRoom(DungeonRoom room)
    {
        // Ветка и её начальная клетка должны оставаться вне оболочек комнат.
        if (room.Right + 1 >= ExitBranchEnd.X - 2 && room.Left - 1 <= ExitBranchEnd.X &&
            room.Bottom + 1 >= ExitBranchEnd.Y - 2 && room.Top - 1 <= ExitBranchEnd.Y) return false;
        // Между оболочками соседних комнат остаётся минимум клетка для коридора.
        return Map.Rooms.All(other => room.Right + 3 < other.Left || other.Right + 3 < room.Left ||
                                     room.Bottom + 3 < other.Top || other.Bottom + 3 < room.Top);
    }

    private static Cell Outside(DungeonRoom room, Cell opening) => opening switch
    {
        _ when opening.X < room.Left => new Cell(opening.X - 1, opening.Y),
        _ when opening.X > room.Right => new Cell(opening.X + 1, opening.Y),
        _ when opening.Y < room.Top => new Cell(opening.X, opening.Y - 1),
        _ => new Cell(opening.X, opening.Y + 1)
    };

    private List<Cell> FindConnection(int[] reachable)
    {
        var queue = new Queue<Cell>();
        int[] parents = Enumerable.Repeat(-2, reachable.Length).ToArray();
        foreach (Cell cell in Map.Floors())
        {
            if (reachable[Index(cell)] < 0) continue;
            parents[Index(cell)] = -1;
            queue.Enqueue(cell);
        }
        while (queue.TryDequeue(out Cell current))
        {
            foreach (Cell next in DungeonMap.Adjacent(current))
            {
                if (!Map.IsInterior(next) || _protectedWall[Index(next)] || parents[Index(next)] != -2) continue;
                parents[Index(next)] = Index(current);
                if (Map.IsFloor(next))
                {
                    var path = new List<Cell>();
                    for (int index = Index(next); index >= 0; index = parents[index])
                        path.Add(new Cell(index % Map.Width, index / Map.Width));
                    path.Reverse();
                    return path;
                }
                queue.Enqueue(next);
            }
        }
        throw new InvalidOperationException("Не удалось связать этаж, сохранив стены комнат.");
    }

    private List<Cell> FindOpeningConnection(Cell start)
    {
        // Каждый открытый проход ведёт к настоящему участку лабиринта.
        var queue = new Queue<Cell>();
        int[] parents = Enumerable.Repeat(-2, _reserved.Length).ToArray();
        parents[Index(start)] = -1;
        queue.Enqueue(start);
        while (queue.TryDequeue(out Cell current))
        {
            if (Map.IsFloor(current))
            {
                var path = new List<Cell>();
                for (int index = Index(current); index >= 0; index = parents[index])
                    path.Add(new Cell(index % Map.Width, index / Map.Width));
                path.Reverse();
                return path;
            }
            foreach (Cell next in DungeonMap.Adjacent(current))
            {
                if (!Map.IsInterior(next) || _reserved[Index(next)] || parents[Index(next)] != -2) continue;
                parents[Index(next)] = Index(current);
                queue.Enqueue(next);
            }
        }
        throw new InvalidOperationException("Не удалось соединить открытый проход с лабиринтом.");
    }

    private bool CanConnect(Cell cell)
    {
        if (_reserved[Index(cell)] || Map.IsFloor(cell)) return false;
        bool left = Map.IsFloor(cell.X - 1, cell.Y), right = Map.IsFloor(cell.X + 1, cell.Y);
        bool up = Map.IsFloor(cell.X, cell.Y - 1), down = Map.IsFloor(cell.X, cell.Y + 1);
        return left && right && !up && !down || up && down && !left && !right;
    }

    private IEnumerable<Cell> RepairDiagonalContacts()
    {
        // В блоке 2x2 нельзя оставлять два диагональных пола и две диагональные стены.
        // Превращаем такой стык в обычный поворот, не затрагивая стены комнат и ветки выхода.
        var pending = new Queue<Cell>();
        var queued = new bool[_reserved.Length];
        void Enqueue(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Map.Width - 1 || y >= Map.Height - 1) return;
            var cell = new Cell(x, y);
            if (queued[Index(cell)]) return;
            queued[Index(cell)] = true;
            pending.Enqueue(cell);
        }
        for (int y = 0; y < Map.Height - 1; y++)
            for (int x = 0; x < Map.Width - 1; x++)
                Enqueue(x, y);

        while (pending.TryDequeue(out Cell corner))
        {
            queued[Index(corner)] = false;
            Cell[] square = { corner, new(corner.X + 1, corner.Y),
                new(corner.X, corner.Y + 1), new(corner.X + 1, corner.Y + 1) };
            bool a = Map.IsFloor(square[0]), b = Map.IsFloor(square[1]);
            bool c = Map.IsFloor(square[2]), d = Map.IsFloor(square[3]);
            if (a != d || b != c || a == b) continue;

            Cell[] bridges = square.Where(cell => Map.IsInterior(cell) && !Map.IsFloor(cell) &&
                !_protectedWall[Index(cell)]).OrderBy(cell => Map.Neighbors(cell).Count()).ToArray();
            if (bridges.Length == 0)
                throw new InvalidOperationException("Диагональный стык зажат между защищёнными стенами.");
            Cell bridge = bridges[0];
            Map.Carve(bridge);
            // Исправление может изменить три соседних блока: проверяем их до устойчивого результата.
            for (int y = bridge.Y - 1; y <= bridge.Y; y++)
                for (int x = bridge.X - 1; x <= bridge.X; x++)
                    Enqueue(x, y);
            yield return bridge;
        }
    }

    private SearchResult Search(Cell start)
    {
        int[] distances = Enumerable.Repeat(-1, Map.Width * Map.Height).ToArray();
        int[] parents = Enumerable.Repeat(-1, distances.Length).ToArray();
        var queue = new Queue<Cell>();
        queue.Enqueue(start);
        distances[Index(start)] = 0;
        while (queue.TryDequeue(out Cell current))
        {
            foreach (Cell neighbor in Map.Neighbors(current))
            {
                if (distances[Index(neighbor)] >= 0) continue;
                distances[Index(neighbor)] = distances[Index(current)] + 1;
                parents[Index(neighbor)] = Index(current);
                queue.Enqueue(neighbor);
            }
        }
        return new SearchResult(distances, parents);
    }

    private void Shuffle<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private int Index(Cell cell) => cell.Y * Map.Width + cell.X;
    private GenerationStep Step(GenerationStage stage, Cell? cursor, string detail, int? progress = null, string? status = null)
    {
        if (progress.HasValue) _progress = progress.Value;
        if (status is not null) _status = status;
        return new GenerationStep(stage, cursor, ++_step, detail) { Progress = _progress, Status = _status };
    }
    private sealed record SearchResult(int[] Distances, int[] Parents);
}


}
