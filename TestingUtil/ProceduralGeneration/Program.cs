using System.Text;
using ProceduralGeneration;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
try
{
    CommandLineOptions options = CommandLineOptions.Parse(args);
    if (options.Help)
    {
        Console.WriteLine(CommandLineOptions.HelpText);
        return 0;
    }
    if (options.SelfTest) return SelfTests.Run();

    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += onCancel;
    try
    {
        for (int floor = 1; floor <= options.Floors; floor++)
        {
            int seed = unchecked(options.Settings.Seed + floor - 1);
            GenerationSettings settings = options.Settings with { Seed = seed };
            var generator = new DungeonGenerator(settings);
            using var log = GenerationLog.Create(settings, generator.Map, floor);
            using var renderer = new ConsoleRenderer(generator.Map, options, floor, seed);
            foreach (GenerationStep step in generator.Generate())
            {
                log.Record(step);
                if (!renderer.ShowStep(step, cancellation.Token))
                {
                    log.Finish(TileSpriteMap.Create(generator.Map, seed), "cancelled", generator.AddedLoops);
                    Console.WriteLine("Генерация остановлена.");
                    Console.WriteLine($"Лог незавершённой карты: {log.Path}");
                    return 130;
                }
            }
            DungeonMap map = generator.Map;
            TileSpriteMap sprites = TileSpriteMap.Create(map, seed);
            renderer.DrawSpriteMap(sprites);
            Console.WriteLine("Этаж построен. Планирование спавна врагов и предметов...");
            SpawnSettings spawnSettings = options.Spawns;
            SpawnPlan spawns = new SpawnPlanner(map, spawnSettings, seed).Generate();
            log.Finish(sprites, "complete", generator.AddedLoops, spawns);
            renderer.DrawSpawnMap(spawns);
            string mapPath = MapJsonExport.Save(log.Path, settings, map, sprites, floor, generator.AddedLoops, spawns);
            int regularRooms = map.Rooms.Count(room => room.Kind == RoomKind.Regular);
            Console.WriteLine($"Этаж {floor}: seed={seed}, обычных комнат={regularRooms}, стартовая=1, фонтанов=1, " +
                              $"доп. соединений={generator.AddedLoops}, путь={map.Solution.Count - 1} секций / {map.TileSolution.Count - 1} тайлов.");
            Console.WriteLine($"Лог карты: {log.Path}");
            Console.WriteLine($"JSON карты: {mapPath}");
            Console.WriteLine($"Сундуков: {spawns.Chests.Count}/{spawnSettings.Chests}, врагов: {spawns.Enemies.Count}.");
            foreach (string note in spawns.Notes) Console.WriteLine(note);
            if (floor < options.Floors && !Console.IsOutputRedirected &&
                cancellation.Token.WaitHandle.WaitOne(1200)) return 130;
        }
    }
    finally
    {
        Console.CancelKeyPress -= onCancel;
    }
    return 0;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine($"Ошибка: {exception.Message}");
    Console.Error.WriteLine("Справка: dotnet run -- --help");
    return 2;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Ошибка ввода-вывода: {exception.Message}");
    return 1;
}
