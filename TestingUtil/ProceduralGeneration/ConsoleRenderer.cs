using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProceduralGeneration;

internal static class MapColors
{
    public const string Reset = "\u001b[0m";
    public const string Surface = "\u001b[48;2;13;18;27m";
    public const string Cursor = "\u001b[38;2;255;215;70m";
    public const string Path = "\u001b[38;2;195;139;245m";

    public static string Foreground(TileKind kind) => kind switch
    {
        TileKind.Wall => "\u001b[38;2;112;132;154m",
        TileKind.Corridor => "\u001b[38;2;172;184;199m",
        TileKind.RoomFloor => "\u001b[38;2;224;182;117m",
        TileKind.Entrance => "\u001b[38;2;47;220;125m",
        TileKind.Fountain => "\u001b[38;2;64;198;255m",
        TileKind.Exit => "\u001b[38;2;255;98;113m",
        TileKind.Chest => "\u001b[38;2;242;161;45m",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static char Symbol(TileKind kind) => kind switch
    {
        TileKind.Wall => '#',
        TileKind.Entrance => 'S',
        TileKind.Fountain => 'F',
        TileKind.Exit => 'E',
        TileKind.Chest => 'C',
        _ => '.'
    };

    public static string Row(DungeonMap map, int y, Cell? cursor = null, HashSet<Cell>? route = null, int cellWidth = 2)
        => BuildRow(map.Width, x =>
        {
            var cell = new Cell(x, y);
            TileKind kind = map.GetSection(cell);
            if (kind == TileKind.Corridor && map.Chests.Any(tile => map.SectionAt(tile) == cell)) kind = TileKind.Chest;
            return (kind, cursor == cell, route?.Contains(cell) == true,
                kind != TileKind.Wall || HasSectionFloorNeighbor(map, x, y));
        }, cellWidth);

    public static string TileRow(DungeonMap map, int y, TilePosition? cursor = null,
        HashSet<TilePosition>? route = null, int cellWidth = 2)
        => BuildRow(map.TileWidth, x =>
        {
            var tile = new TilePosition(x, y);
            TileKind kind = map.GetTile(tile);
            return (kind, cursor == tile, route?.Contains(tile) == true,
                kind != TileKind.Wall || TileSprites.FloorNeighborMask(map, tile) != 0);
        }, cellWidth);

    public static string VariantForeground(SpriteVariant variant)
    {
        string rgb = variant.DebugColor;
        return $"\u001b[38;2;{Convert.ToInt32(rgb[1..3], 16)};{Convert.ToInt32(rgb[3..5], 16)};{Convert.ToInt32(rgb[5..7], 16)}m";
    }

    public static string VariantRow(TileSpriteMap sprites, int y, int cellWidth = 2)
    {
        var row = new StringBuilder(Surface);
        string? previousId = null;
        for (int x = 0; x < sprites.Width; x++)
        {
            TileSpecification tile = sprites.Get(new TilePosition(x, y));
            if (!tile.Visible)
            {
                row.Append(' ', cellWidth);
                continue;
            }
            if (tile.SpriteId != previousId)
                row.Append(VariantForeground(TileSprites.Variant(tile.SpriteId)));
            row.Append(Symbol(tile.Kind)).Append(' ', cellWidth - 1);
            previousId = tile.SpriteId;
        }
        return row.Append(Reset).ToString();
    }

    private static bool HasSectionFloorNeighbor(DungeonMap map, int x, int y)
    {
        // Диагонали сохраняют углы обводки вокруг пола, включая углы комнат.
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (map.IsFloor(x + dx, y + dy))
                    return true;
            }
        return false;
    }

    private static string BuildRow(int width, Func<int, (TileKind Kind, bool Cursor, bool Path, bool Visible)> sample, int cellWidth)
    {
        var row = new StringBuilder(Surface);
        string? previous = null;
        for (int x = 0; x < width; x++)
        {
            var point = sample(x);
            if (!point.Visible)
            {
                row.Append(' ', cellWidth);
                continue;
            }
            TileKind tile = point.Kind;
            string color = Foreground(tile);
            char symbol = Symbol(tile);
            // Вход, выход и фонтаны всегда сохраняют свой цвет.
            if (tile is not (TileKind.Entrance or TileKind.Exit or TileKind.Fountain or TileKind.Chest))
            {
                if (point.Path) { color = Path; symbol = '*'; }
                if (point.Cursor) { color = Cursor; symbol = '@'; }
            }
            if (color != previous) row.Append(color);
            row.Append(symbol).Append(' ', cellWidth - 1);
            previous = color;
        }
        return row.Append(Reset).ToString();
    }

    public static string Swatch(string color, char symbol, string label) => Surface + color + symbol + Reset + " " + label;
}

internal sealed class ConsoleRenderer : IDisposable
{
    private readonly DungeonMap _map;
    private readonly CommandLineOptions _options;
    private readonly int _floor;
    private readonly int _seed;
    private readonly int _cellWidth;
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private bool _redraw;
    private bool _paused;
    private GenerationStage? _lastStage;
    private int _lastProgress = -1;
    private string? _lastStatus;
    private int _frameDelay;
    private GenerationStep? _lastStep;
    private int DisplayWidth => _options.Sections ? _map.Width : _map.TileWidth;
    private int DisplayHeight => _options.Sections ? _map.Height : _map.TileHeight;

    public ConsoleRenderer(DungeonMap map, CommandLineOptions options, int floor, int seed)
    {
        _map = map;
        _options = options;
        _floor = floor;
        _seed = seed;
        _cellWidth = !Console.IsOutputRedirected && Console.WindowWidth < DisplayWidth * 2 + 1 ? 1 : 2;
        EnableTerminalColors();
        _redraw = !options.Plain && !Console.IsOutputRedirected && !Console.IsInputRedirected && FitsWindow();
        if (_redraw)
        {
            Console.Clear();
            Console.CursorVisible = false;
        }
        else
        {
            Console.WriteLine("Процесс генерации: цветные промежуточные снимки карты.");
            if (!options.Plain && !Console.IsOutputRedirected)
            {
                Console.WriteLine($"Для анимации увеличьте окно до {Math.Max(DisplayWidth * _cellWidth, 53) + 1}x{DisplayHeight + 9}.");
                if (!options.Sections) Console.WriteLine("Компактный обзор секций: добавьте --sections.");
            }
        }
    }

    public bool ShowStep(GenerationStep step, CancellationToken cancellation)
    {
        bool drawn = Draw(step);
        return WaitForFrame(cancellation, drawn ? _frameDelay : 0);
    }

    public void DrawSpriteMap(TileSpriteMap sprites)
    {
        // Вторая карта допечатывается после обычной, а не перерисовывает её.
        if (_redraw) Console.CursorVisible = true;
        _redraw = false;
        int cellWidth = !Console.IsOutputRedirected && Console.WindowWidth < sprites.Width * 2 + 1 ? 1 : 2;
        Console.WriteLine();
        Console.WriteLine($"Варианты спрайтов — этаж {_floor}, seed {_seed}, {sprites.Width}x{sprites.Height} тайлов");
        Console.WriteLine("Каждый символ — тайл. Цвет обозначает SpriteId из лога; S/F/E — объект поверх пола.");
        for (int y = 0; y < sprites.Height; y++)
            Console.WriteLine(MapColors.VariantRow(sprites, y, cellWidth));
        Console.WriteLine("Легенда вариантов (N — вверх, E — вправо, S — вниз, W — влево):");
        Console.WriteLine("У стен диагонали, покрытые соседним полом по прямой, используют тот же спрайт и цвет.");
        foreach (SpriteVariant variant in sprites.DisplayVariants)
            Console.WriteLine(MapColors.Swatch(MapColors.VariantForeground(variant), '#', variant.Id) +
                              $" [{variant.DebugColor}] — {variant.Description}");
        Console.WriteLine("Пробел: none — скрытая непроходимая стена, спрайт не нужен.");
    }

    public void DrawSpawnMap(SpawnPlan plan)
    {
        var enemies = plan.Enemies.ToDictionary(e => e.Position);
        var chests = plan.Chests.ToDictionary(c => c.Position);
        string[] colors = ["\u001b[38;2;255;90;90m", "\u001b[38;2;100;220;255m", "\u001b[38;2;210;120;255m", "\u001b[38;2;120;255;120m"];
        Console.WriteLine($"Карта спавна — этаж {_floor}, seed {plan.Seed}. Один символ = один тайл.");
        Console.WriteLine("1 = 001, 2 = 002, 3 = 003, 4 = 004; C = сундук, S = вход, F = фонтан, E = выход.");
        for (int y = 0; y < _map.TileHeight; y++)
        {
            var row = new StringBuilder(MapColors.Surface);
            for (int x = 0; x < _map.TileWidth; x++)
            {
                var tile = new TilePosition(x, y);
                if (enemies.TryGetValue(tile, out var enemy))
                {
                    int type = int.Parse(enemy.EnemyId) - 1;
                    row.Append(colors[type]).Append(type + 1);
                }
                else if (chests.ContainsKey(tile)) row.Append(MapColors.Foreground(TileKind.Chest)).Append('C');
                else
                {
                    TileKind kind = _map.GetTile(tile);
                    row.Append(MapColors.Foreground(kind)).Append(kind == TileKind.Wall && TileSprites.FloorNeighborMask(_map, tile) == 0 ? ' ' : MapColors.Symbol(kind));
                }
            }
            Console.WriteLine(row.Append(MapColors.Reset));
        }
    }

    private bool Draw(GenerationStep step, bool force = false)
    {
        _lastStep = step;
        if (_redraw && !FitsWindow())
        {
            _redraw = false;
            Console.CursorVisible = true;
            Console.WriteLine();
            Console.WriteLine("Окно уменьшено: продолжаем показывать снимки процесса.");
        }
        bool changedStage = step.Stage != _lastStage;
        bool changedStatus = step.Status != _lastStatus;
        // Число кадров зависит от процентов, а не от тысяч внутренних шагов.
        // При --delay 0 ограничиваем только частоту рисования, без ожидания.
        if (!force && !changedStage && !changedStatus && step.Stage != GenerationStage.Complete &&
            (step.Progress - _lastProgress < (_redraw ? 1 : 10) ||
             _redraw && _options.Delay == 0 && _frameTimer.ElapsedMilliseconds < 25))
            return false;

        HashSet<Cell>? route = _options.ShowPath && step.Stage == GenerationStage.Complete
            ? _map.Solution.ToHashSet() : null;
        HashSet<TilePosition>? tileRoute = !_options.Sections && route is not null
            ? _map.TileSolution.ToHashSet() : null;
        TilePosition? tileCursor = step.Cursor is Cell cursor ? _map.SectionCenter(cursor) : null;
        if (_redraw) Console.SetCursorPosition(0, 0);
        WriteText($"Этаж {_floor}/{_options.Floors} | Seed: {_seed} | {_map.Width}x{_map.Height} секций = {_map.TileWidth}x{_map.TileHeight} тайлов");
        WriteText(_options.Sections ? "Обзор: символ = секция из 3x3 тайлов." : "Подробно: символ = тайл; секция = 3x3 тайла.");
        WriteProgress(step.Progress);
        WriteText(step.Status);
        for (int y = 0; y < DisplayHeight; y++)
            WriteColorLine(_options.Sections
                ? MapColors.Row(_map, y, step.Cursor, route, _cellWidth)
                : MapColors.TileRow(_map, y, tileCursor, tileRoute, _cellWidth), DisplayWidth * _cellWidth);

        WriteLegend([
            (MapColors.Foreground(TileKind.Wall), '#', "стена"),
            (MapColors.Foreground(TileKind.Corridor), '.', "коридор"),
            (MapColors.Foreground(TileKind.RoomFloor), '.', "комната")
        ]);
        WriteLegend([
            (MapColors.Foreground(TileKind.Entrance), 'S', "вход"),
            (MapColors.Foreground(TileKind.Fountain), 'F', "фонтан"),
            (MapColors.Foreground(TileKind.Exit), 'E', "выход"),
            (MapColors.Foreground(TileKind.Chest), 'C', "сундук"),
            (MapColors.Cursor, '@', "шаг"),
            (MapColors.Path, '*', "путь")
        ]);
        WriteText(_paused ? "ПАУЗА. Пробел — продолжить; Q / Esc — выход."
            : "Пробел — пауза; Q / Esc — выход; Ctrl+C — отмена.");
        if (!_redraw) Console.WriteLine();
        _lastStage = step.Stage;
        _lastProgress = step.Progress;
        _lastStatus = step.Status;
        // Короткие этапы чуть дольше держат подпись, чтобы её было видно.
        _frameDelay = _options.Delay == 0 || step.Stage == GenerationStage.Complete
            ? 0 : Math.Max(_options.Delay, changedStatus ? 100 : 0);
        _frameTimer.Restart();
        return true;
    }

    private void WriteProgress(int progress)
    {
        const int barWidth = 36;
        int filled = barWidth * progress / 100;
        string percent = $" {progress,3}%";
        string bar = "[" + MapColors.Foreground(TileKind.Entrance) + new string('=', filled) +
                     MapColors.Foreground(TileKind.Wall) + new string('-', barWidth - filled) +
                     MapColors.Reset + "]" + percent;
        WriteColorLine(bar, barWidth + 2 + percent.Length);
    }

    private void WriteText(string text)
    {
        if (_redraw)
        {
            int width = Console.WindowWidth - 1;
            text = (text.Length > width ? text[..width] : text).PadRight(width);
        }
        Console.WriteLine(text);
    }

    private void WriteColorLine(string text, int visibleWidth)
    {
        Console.Write(text);
        if (_redraw) Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - 1 - visibleWidth)));
        Console.WriteLine();
    }

    private void WriteLegend((string Color, char Symbol, string Label)[] items)
    {
        string line = string.Join("  ", items.Select(item => MapColors.Swatch(item.Color, item.Symbol, item.Label)));
        int visibleWidth = items.Sum(item => 2 + item.Label.Length) + (items.Length - 1) * 2;
        WriteColorLine(line, visibleWidth);
    }

    private bool WaitForFrame(CancellationToken cancellation, int delay)
    {
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
            return !cancellation.IsCancellationRequested;

        var timer = Stopwatch.StartNew();
        do
        {
            if (cancellation.IsCancellationRequested) return false;
            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.Q or ConsoleKey.Escape || key.KeyChar is 'q' or 'Q') return false;
                if (key.Key == ConsoleKey.Spacebar || key.KeyChar == ' ')
                {
                    _paused = !_paused;
                    if (_lastStep is not null) Draw(_lastStep, force: true);
                }
            }
            if (!_paused && timer.ElapsedMilliseconds >= delay) break;
            Thread.Sleep(_paused ? 25 : Math.Min(5, Math.Max(1, delay)));
        } while (true);
        return true;
    }

    private bool FitsWindow() => Console.WindowWidth >= Math.Max(DisplayWidth * _cellWidth, 53) + 1 &&
                                 Console.WindowHeight >= DisplayHeight + 9;

    private static void EnableTerminalColors()
    {
        if (!OperatingSystem.IsWindows() || Console.IsOutputRedirected) return;
        IntPtr handle = GetStdHandle(-11);
        if (GetConsoleMode(handle, out uint mode)) SetConsoleMode(handle, mode | 0x0004);
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int identifier);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    public void Dispose()
    {
        Console.Write(MapColors.Reset);
        if (_redraw) Console.CursorVisible = true;
    }
}
