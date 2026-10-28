using System.Globalization;

namespace ProceduralGeneration;

internal sealed class CommandLineOptions
{
    public GenerationSettings Settings { get; private set; } = new(Seed: Random.Shared.Next());
    public SpawnSettings Spawns { get; private set; } = new();
    public int Floors { get; private set; } = 1;
    public int Delay { get; private set; } = 20;
    public bool Plain { get; private set; }
    public bool ShowPath { get; private set; }
    public bool Sections { get; private set; }
    public bool SelfTest { get; private set; }
    public bool Help { get; private set; }

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            string Value()
            {
                if (++i >= args.Length) throw new ArgumentException($"После {argument} нужно значение.");
                return args[i];
            }
            int Integer()
            {
                string value = Value();
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    throw new ArgumentException($"Для {argument} требуется целое число, получено: {value}");
                return number;
            }
            switch (argument)
            {
                case "--width": options.Settings = options.Settings with { Width = Integer() }; break;
                case "--height": options.Settings = options.Settings with { Height = Integer() }; break;
                case "--seed": options.Settings = options.Settings with { Seed = Integer() }; break;
                case "--chests": options.Spawns = options.Spawns with { Chests = Integer() }; break;
                case "--safe-radius": options.Spawns = options.Spawns with { SafeRadius = Integer() }; break;
                case "--corridor-packs": options.Spawns = options.Spawns with { CorridorPacks = Integer() }; break;
                case "--singles": options.Spawns = options.Spawns with { Singles = Integer() }; break;
                case "--spawn-config":
                    try
                    {
                        options.Spawns = System.Text.Json.JsonSerializer.Deserialize<SpawnSettings>(File.ReadAllText(Value()),
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true,
                                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
                            }) ?? throw new ArgumentException("Пустые настройки спавна.");
                    }
                    catch (System.Text.Json.JsonException error) { throw new ArgumentException($"Ошибка JSON настроек спавна: {error.Message}"); }
                    break;
                case "--rooms": case "--halls": options.Settings = options.Settings with { HallCount = Integer() }; break;
                case "--loops":
                    if (!double.TryParse(Value(), NumberStyles.Float, CultureInfo.InvariantCulture, out double chance))
                        throw new ArgumentException("Для --loops нужно число от 0 до 1, например 0.08 (через точку).");
                    options.Settings = options.Settings with { LoopChance = chance };
                    break;
                case "--floors": options.Floors = Integer(); break;
                case "--delay": options.Delay = Integer(); break;
                case "--plain": options.Plain = true; break;
                case "--show-path": options.ShowPath = true; break;
                case "--sections": options.Sections = true; break;
                case "--self-test": options.SelfTest = true; break;
                case "--help": case "-h": options.Help = true; break;
                default: throw new ArgumentException($"Неизвестный параметр: {argument}");
            }
        }
        options.Settings.Validate();
        options.Spawns.Validate();
        if (options.Floors is < 1 or > 100)
            throw new ArgumentException("Количество этажей должно быть от 1 до 100.");
        if (options.Delay is < 0 or > 1000)
            throw new ArgumentException("Задержка должна быть от 0 до 1000 миллисекунд.");
        return options;
    }

    public const string HelpText = """
        Лабиринт из цветных символов: комнаты, открытые проходы, стартовый фонтан и выход в тупике.

        dotnet run -- [параметры]

        --seed N        Повторяемый seed (по умолчанию случайный, показан над картой)
        --width N       Нечётная ширина в секциях: 11..151, по умолчанию 39
        --height N      Нечётная высота в секциях: 11..81, по умолчанию 19
        --floors N      Сколько этажей создать: 1..100, по умолчанию 1
        --rooms N       Максимум обычных комнат: 0..8; по умолчанию 3..5
        --chests N      Количество сундуков, по умолчанию 3; 0 отключает (при нехватке мест — меньше)
        --spawn-config FILE  JSON всех настроек спавна (пример: spawn-settings.json)
        --safe-radius N Безопасная зона входа в тайлах, по умолчанию 15
        --corridor-packs N  Пачки коридоров, по умолчанию 6
        --singles N     Одиночные враги в коридорах, по умолчанию 3
        --halls N       То же, что --rooms (совместимость с прежним запуском)
        --loops P       Вероятность открыть подходящую стену: 0..1, по умолчанию 0.08
        --delay N       Задержка между кадрами в мс: 0..1000, по умолчанию 20
        --plain         Печатать цветные снимки без перерисовки экрана
        --show-path     Выделить путь по центрам секций фиолетовым на готовой карте
        --sections      Компактный обзор: один символ на секцию вместо тайла
        --self-test     Проверить алгоритм на множестве seed и размеров
        --help, -h      Эта справка

        Во время генерации: пробел — пауза, Q / Esc — выход, Ctrl+C — отмена.
        Над картой — прогрессбар 0–100%, под ним — название текущего этапа.
        Короткие подписи видны минимум 100 мс; --delay 0 отключает все эти задержки.
        Одна секция = 3x3 тайла. Карта 39x19 секций = 117x57 тайлов.
        По умолчанию символ = тайл. S, F и E занимают по одному тайлу в центре секции.
        Размеры комнат указаны в секциях без стен, ширина x высота:
        старт 3x4: вход (2,2), фонтан (2,3), координаты внутри комнаты от 1;
        стартовая комната в тайлах — 9x12, стены и коридоры толщиной 3 тайла.
        обычные 5x5 / 4x3 / 5x3; единственный фонтан находится в стартовой комнате.
        Стартовая комната добавляется сверх --rooms и есть даже при --rooms 0.
        # стена, . пол, S вход, F фонтан, E выход, C сундук 1x1 тайл, @ шаг, * путь.
        Стены вдали от пола показаны пробелами; обводка сохраняет стороны и углы.
        Каждый этаж сохраняет текстовый JSONL-лог в logs/ рядом с проектом.
        В логе — параметры, шаги, комнаты и спецификация каждого тайла со спрайтами.
        В конце выводится вторая карта: цвета вариантов спрайтов с легендой.
        Готовая карта сохраняется отдельным .json рядом с логом в папке logs.
        Затем отдельная карта спавна: 1/2/3/4 = враги 001/002/003/004, C = сундук.
        В JSON объекты находятся в spawns, геометрия этажа — в tiles.
        Настройки применяются слева направо: сначала --spawn-config, затем переопределения.
        Вторая карта всегда тайловая, даже если для анимации выбран --sections.
        При перенаправлении вывода или маленьком окне печатаются снимки процесса.
        Следующий этаж использует seed предыдущего + 1 (с переходом через границу int).
        """;
}
