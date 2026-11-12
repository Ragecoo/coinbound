COINBOUND — игровая сцена и интерфейс

Откройте Assets/Scenes/SampleScene.unity, нажмите Play и «Новая игра».
Перед генерацией выберите мечника или лучника и нажмите «Начать игру».
Доступны мечник из «тута/01» и лучник из «тута/02»; выбранный герой появляется в игре.
«Назад» / Esc возвращает в меню; если данж уже есть, его можно продолжить прежним героем.
«Новая игра» открывает выбор; при живом герое сначала появляется подтверждение.
Главный экран, выбор персонажа (две половины с авторскими портретами), загрузка,
пауза, смерть, настройки и подтверждения оформлены в тёмной палитре.
Главный экран: цельная сцена крипты, адаптированный логотип пользователя, текстовое меню.
Выбор: две половины экрана с упрощёнными пиксельными портретами и характеристиками.
Загрузка: анимированный игровой спрайт идёт по коридору по мере создания карты.
HUD: здоровье и мана слева сверху, очки формата 000000000 справа сверху.
Мана пока только шкала (100/100), без расходования — по указанию пользователя.
Враг даёт 100 очков один раз; счётчик плавно увеличивается. Новый забег сбрасывает очки.
Умения обозначены небольшими значками в ромбах. Лозунги и выдуманные описания удалены.
Переходы раскрываются из центра; анимация UI работает при остановленном игровом времени.
Настройки: громкость интерфейса, яркость подземелья, полный экран, VSync,
уменьшение анимации, подсказки и индикация урона. Сохраняются в PlayerPrefs.
Пауза останавливает Time.timeScale; Esc из настроек возвращает в паузу.
Возврат на главный экран сохраняет текущий забег до закрытия приложения.
После смерти можно повторить спуск тем же героем или выбрать другого.
Постоянного сохранения забега между запусками нет.
WASD / стрелки — движение; левый стик геймпада также поддерживается.
ЛКМ — удар мечом / стрела по направлению к курсору; можно удерживать для повторных атак.
Атаки направляются под любым углом. Удар мечом ближний, стрела летит до цели или стены.
Левый Shift — перекат по направлению движения, без движения — по последнему направлению.
Перекат длится 0.30 с, даёт неуязвимость на первые 0.24 с; откат 1 с от начала переката.
ПКМ — удерживать направленный блок мечника в сторону курсора. Первый заблокированный
удар запускает откат 2 с и оглушает ударившего врага на 0.6 с. Со спины блок не защищает.
Лучник не блокирует. На геймпаде: правый стик — прицел, правый триггер — атака,
нижняя лицевая кнопка — перекат; блок пока привязан к ПКМ.
Ctrl + колёсико — плавное приближение/отдаление. Вверх — ближе, вниз — дальше.
Начальный размер камеры 6.5 вместо 8.5, диапазон масштаба 3.5–12.
Esc / «Меню» — приостановить движение, бой, стрелы и врагов; продолжить или создать новый данж.
Во время загрузки «Отмена» / Esc возвращает на стартовый экран.
Камера ограничивает весь видимый прямоугольник границами карты, в том числе
при изменении пропорций окна. На очень широком/высоком экране масштаб уменьшается.

Generation/ — копия пяти файлов из TestingUtil/ProceduralGeneration.
Исходники TestingUtil не изменены. Изменения копии ограничены совместимостью
с C# 9 / .NET Standard 2.1: namespaces, collection expressions, value structs,
MaxBy, Order, IsFinite, форматирование строки цвета и IsExternalInit.
Выбор комнат, случайных чисел, проходов, тайлов, сундуков и врагов сохранён.
Размер по умолчанию: 39 x 19 секций = 117 x 57 тайлов.

Runtime/DungeonGame.cs — запуск, исходные статусы генератора, прогресс и камера.
Генерация выполняется по шагам между кадрами. Первые 82% — генератор,
остальное — отрисовка и исходный отдельный SpawnPlanner; 100% означает готовую игру.
Настройки размера/seed/камеры доступны на объекте Coinbound в Inspector.
Чтобы повторить карту, отключите Random Seed и задайте Seed.
Игровые настройки спавна в DungeonGame.cs уменьшены: пачки в комнатах 4–5 врагов
вместо 5–6, в коридорах 2–3 вместо 2–4; коридорных пачек 5 вместо 6,
одиночных врагов 2 вместо 3. Это примерно на 20–25% меньше, точное число зависит
от карты и доступных мест. Исходный SpawnPlanner и TestingUtil не менялись.
Runtime/DungeonScreen*.cs — интерфейс Unity UI, макеты, настройки и переходы.
Runtime/DungeonSettings.cs — сохраняемые настройки; исходный DungeonEffects не изменяется.
Runtime/DungeonUiDecoration.cs — рамки, узоры, частицы и анимация монеты.
Runtime/DungeonView.cs — Tilemap, коллизии, объекты и создание персонажей.
Runtime/DungeonPlayer.cs — движение через Input System и Rigidbody2D, атаки, перекат и блок.
Runtime/DungeonHero.cs — имена, постоянные идентификаторы, папки рисунков и класс героя.
Выбор хранится в DungeonGame.SelectedHero и передаётся в DungeonPlayer.Hero при спавне.
Runtime/DungeonHeroAnimator.cs — покадровые idle, бег, атака, перекат и блок по четырём
направлениям. Кадры 32 x 64, 32 PPU, Point sampling; плащи анимированы в самих рисунках.
Рисунок привязан к стопам, квадратный BoxCollider2D 0.36 x 0.36 тайла находится только снизу.
Герои уменьшены на 20% внутри общего кэша кадров; каждый пиксель по-прежнему равен 1/32 тайла.
Исходные PNG не изменяются. Производные текстуры используют Point, без сглаживания и mipmaps.
VisualScale, PaletteSaturation, PaletteBrightness и PaletteTint в DungeonHeroAnimator.cs задают
размер и холодную приглушённую палитру игровых спрайтов. В интерфейсе используются
крупные авторские портреты из Resources/InterfaceArt, предоставленные пользователем.
HandHeight следует размеру героя, чтобы стрела, след меча и блок оставались у рук.

DungeonCombatFx.cs: белый след меча с нейтральной кромкой и затухающим хвостом,
тёплые металлические искры при ударе, блоке и попадании стрелы в стену.
DungeonCombatMesh.cs рисует эффекты на мировой сетке 32 px одним динамическим мешем;
картинка следа меча больше не обрезается стенами. Урон по-прежнему проверяет препятствия.
Development-сборка с -coinbound-fx-capture <каталог> записывает клипы выбранных эффектов;
Tools/Review-CombatCapture.ps1 собирает из них GIF и контактные листы.

Настройки выбранных эффектов: Assets/Coinbound/Resources/DungeonEffects.asset в Inspector.
- Sword trail: Slash Color — белый цвет рисунка; Slash Emission — его собственное свечение.
  Slash Light Color/Radius/Intensity/Duration — цвет, радиус в тайлах, сила и время подсветки.
  По умолчанию тёплый свет радиусом 0.9 тайла, сила 0.75, время 0.18 с.
- Sparks: Spark Color/Hot Color — янтарные искры и светлое ядро; Spark Emission — свечение.
  Spark Light Color/Radius/Intensity/Duration — свет на окружающих поверхностях:
  радиус 0.75 тайла, сила 1 (100%), время 0.16 с.
- Dynamic Lights/Effect Light Strength — общий переключатель и множитель локального света.
- Arrows: Arrow North Wall Inset = 0.85 тайла — глубина наконечника в верхней стене;
  Arrow Other Wall Inset = 0.25 — в остальных стенах; Arrow Stuck Lifetime = 1.1 с.
Положение картинки застрявшей стрелы учитывает сторону стены. Физическое попадание,
свет и коллизия остаются на стороне пола; после попадания стрела больше не наносит урон.
Параметры эффекта применяются при следующем его появлении. Изменения самого asset сохраняются.
Свет героя: объект Coinbound в SampleScene, раздел Player torch, поля Torch Intensity и Torch Color.
Torch Intensity = 1.12 (80% от прежних 1.4), Torch Color = (1, 0.95, 0.88), радиус прежний — 8.
Изменения света героя применяются при создании подземелья.
Декор, атмосфера и синтезированный звук из последнего набора не создаются в игре.
Кровь, пыль, сколы, отдача, hit-stop, дополнительные реакции/тени, вспышки интерфейса отключены.
Их старые исходники и исходные рисунки оставлены в проекте; генератор и TestingUtil не менялись.

Runtime/DungeonDepthSort.cs сортирует персонажей и объекты по ногам/основанию;
высокий рисунок героя получает свет в точке ног и не обрезается картой стен на уровне головы.
Runtime/DungeonCursor.cs отображает игровой прицел и направление атаки.
Runtime/DungeonEnemy.cs — обнаружение, преследование, заметный замах, удар, стан и смерть.
Runtime/DungeonCombatNavigation.cs — общий маршрутный поиск по карте и основаниям объектов.
Runtime/DungeonArrow.cs — летящая стрела; DungeonCombatPhysics.cs проверяет цели и стены
по пути полёта, чтобы быстрый снаряд не пролетал через тонкие препятствия.
DungeonCombatFx.cs / DungeonCombatPixels.cs — пиксельный слэш, блок, искры и сигнал замаха.
Стрела и оружейные эффекты визуально подняты на 0.68 тайла к рукам; попадания остаются
на плоскости ног, поэтому высокая картинка не увеличивает коллизию персонажа.
DungeonPlaceholderArt.cs по-прежнему создаёт плейсхолдеры врагов и сундуков.

Настройка боя: постоянные исходные значения находятся в Runtime/DungeonPlayer.cs.
Поля speed (5), maximumHealth (6), rollCooldown (1) и blockCooldown (2) доступны
на созданном объекте Player во время Play; изменения в Play не сохраняются для нового запуска.
SwordDuration = 0.42 с; урон мечом 2, радиус 1.65 тайла; контакт на 0.16 с замаха.
BowDuration = 0.38 с; выпуск стрелы на 0.14 с. Скорость стрелы 16 тайлов/с,
урон 1 и время полёта 1.5 с задаются в DungeonArrow.cs.
DungeonEnemy.cs: здоровье 3, скорость 2.25 тайла/с, начало замаха с 1.05 тайла,
WindupDuration = 0.42 с, урон 1. Враг атакует только видимую цель и не бьёт через стены.
Полученный урон даёт герою 0.65 с защиты от следующих попаданий, чтобы группа не убивала
его за один кадр. Успешный блок не включает эту защиту: следующий удар во время КД опасен.
При потере фокуса окна и в меню таймеры боя, движение врагов и снаряды останавливаются.
После потери всего здоровья игра предлагает создать новое подземелье.
Runtime/DungeonTorch.cs и Shaders/TorchlitSprite.shader — приглушённый свет от героя
с лёгким тёплым оттенком, сохраняющим исходную индиго-палитру. Яркого ореола нет.
Мягкое затухание, лёгкое мерцание и рассеянные края теней.
Полутень сглаживается по 32 выборкам с гауссовыми весами вместо семи отдельных полос.
Свет и тени вычисляются на сетке 32 пикселя на тайл: диагональные границы
идут ступеньками по пикселям текстур, сохраняя мягкий переход между ними.
Множитель света для всех стен задаёт Wall Light Multiplier; стены, у которых
пол находится непосредственно снизу, получают дополнительный Upper Wall Light Multiplier.
Начальные значения в коде: 2.5 и 1.5; сохранённые настройки сцены имеют приоритет.
Цвет темноты — #101010.
DungeonVisibility.cs проверяет лучи по исходным стенам: свет не проходит сквозь
перегородки и закрытые углы. Возвращено прежнее затухание вдоль луча:
Wall Light Depth = 0.8 тайла, поэтому за краем стены видно существенно меньше,
чем при глубине 2.25. Сохраняется умеренно мягкая полутень на пиксельной сетке.
Видимость пола за стеной при этом не расширяется.
Пол, объекты и враги используют одну карту видимости. Видимость ограничена стенами,
но не расстоянием: вне радиуса света текстуры едва различимы с яркостью 0.035.
Радиус света остаётся 8 тайлов и не зависит от зума. Посещённые комнаты за стенами
снова скрыты. Цвет, интенсивность, радиус, мягкость теней, усиление стен и яркость
неосвещённых текстур доступны на Coinbound в Inspector.

Настройка освещения: откройте SampleScene, вне Play выберите в Hierarchy объект
Coinbound, затем компонент Dungeon Game в Inspector. После изменения сохраните
сцену (Ctrl+S). Значения применяются при создании подземелья.
Player torch: Torch Radius — радиус; Torch Intensity — яркость; Torch Color — цвет;
Shadow Softness — мягкость теней; Wall Light Multiplier — усиление всех стен;
Upper Wall Light Multiplier — дополнительная подсветка стен с полом непосредственно снизу.
Wall Light Depth — глубина затухания за краем стены вдоль луча: меньше — видно меньше стены.
Darkness / distant visibility: Darkness Color — цвет темноты;
Unlit Visibility — слабая видимость текстур за пределами радиуса света.
У каждого параметра есть русская подсказка при наведении.

Resources/DungeonArt — 64 PNG на основе «вот тут/indigo-crypt-32-v14».
Вход и фонтан заменены перерисованными спрайтами; оригиналы сохранены в «вот тут».
Point filtering, без mipmaps и сжатия; итоговая плотность — 32 пикселя на тайл.
TerrainSprite определяет пол/стены; background.void заполняет скрытую стену.
floor.extra.0–5 — шесть новых вариантов пола: разветвлённая трещина, сколотый стык,
потёртые камни, небольшая каменная заплатка, пятно сырости и полосы износа.
Каждый PNG имеет размер 32 x 32, 32 PPU и Point filtering. Двухпиксельная кромка
в точности взята из floor.room.0, поэтому швы соединяются с существующими тайлами.
Внутренние швы между четырьмя плитами тоже взяты из floor.room.0: полосы x=14..17
и y=14..17 сохраняют исходную толщину, цвет и неровный край. Детали вне швов не меняются.
Цвета приведены к палитре исходного пола; повороты и зеркальные отражения не применяются.
DungeonView подмешивает новые варианты примерно в 45% клеток пола комнат и коридоров.
Выбор зависит только от seed и координат, не расходует Random генератора и не меняет
геометрию, коллизии или спавн. Generation/ и TestingUtil остаются без изменений.
Источник рисунков — встроенный image_gen. Промпты и исходные результаты:
ArtSource/Floors; tiling-preview.png показывает старые и новые тайлы вместе.
Tools/Import-FloorVariants.ps1 готовит игровые PNG, сохраняет кромки и швы и собирает мозаику.
Y генератора направлен вниз; в Unity используется (x + 0.5, 0.5 - y).
Вход/фонтан/выход ставятся относительно маркеров генератора.
Вход увеличен в 1.5 раза (4.5 x 4.5 тайла), фонтан — в 1.25 (3.75 x 3.75).
Вход перерисован в PNG 144 x 144, фонтан — 120 x 120. В импорте заданы 48 и 40 PPU:
с масштабами 1.5 и 1.25 это сохраняет размер объектов и даёт 32 пикселя на тайл.
Цвета текстур читаются только через Point sampling, без размытия или интерполяции.
Билинейное сглаживание в шейдере используется исключительно для альфа-маски теней.
Для этих двух спрайтов используется Full Rect, чтобы сетку не обрезала диагональная
геометрия Tight Mesh. Прозрачные поля соответствуют прежним габаритам рисунков.
Перерисовка: встроенный image_gen; исходные результаты и точные промпты находятся
в ArtSource/Landmarks. Tools/Import-LandmarkRedraws.ps1 приводит рисунки к игровым
размерам без смешивания цветов и сохраняет прозрачный фон.
Вход дополнительно поднят на один тайл: зазор между его нижним каменным краем
и верхним краем фонтана получается примерно 1.08 тайла.
У входа и фонтана сплошные PolygonCollider2D по каменному контуру без прозрачных
полей; у фонтана — восьмиугольник с непроходимой серединой.
Персонаж появляется на ближайшем свободном месте рядом со входом,
с запасом под радиус коллизии; внутрь входа или фонтана он не помещается.
В Inspector, раздел Landmarks: Entrance Scale и Fountain Scale меняют размеры
спрайтов вместе с коллизиями; Entrance Lift поднимает вход относительно маркера.
Вход и фонтан отбрасывают короткую полупрозрачную тень от света персонажа.
Она слегка расширяется от основания, направлена от источника света и имеет мягкий
пиксельный край. Проекция рядом с персонажем увеличена, чтобы тень выступала за спрайт.
Отбрасывается только на видимый пол. Плотность учитывает линейное цветовое пространство:
0.35 даёт заметное затемнение, а не слабое уменьшение линейной энергии света.
Затухание факела не применяется повторно к тени: она ослабляет долю прямого света
и плавно исчезает, когда остаётся только слабая фоновая видимость.
Объекты не добавляются в карту стен: фонтан не перекрывает обзор пространства за ним.
Landmark Shadow Length — максимальная длина тени за краем объекта (0.65 тайла);
Landmark Shadow Opacity — плотность (0.35); Landmark Shadow Softness — мягкость (0.08).
DungeonLandmarkShadow.cs задаёт проекцию, DungeonSpriteGrid.cs — общую пиксельную сетку.
Камень входа, фонтана и выхода приведён к индиго-палитре стен и получает такое же
Wall Light Multiplier. Вода фонтана сохраняет синий цвет. Оттенок задаётся отдельно
для этих спрайтов через MaterialPropertyBlock.
Выход сохраняет размер 3 x 3 тайла.
object.exit сопоставлен с object.exit.open; закрытый вариант тоже импортирован.
Выход пока только отображается: правила открытия и переходы между этажами не заданы.

Цветные круги — временные рисунки врагов 001–004; они преследуют героя и атакуют вблизи.
Золотые сундуки ориентированы по Facing генератора и остаются проходимыми.
Содержимое сундуков и взаимодействия с фонтаном/выходом пока не реализованы.

Window > General > Test Runner > PlayMode: Coinbound.Tests.
Проверяются загрузка, статусы/прогресс, отображение объектов, спавн, коллизии,
границы камеры при разных пропорциях окна, отмена и повторная генерация,
блокировка лучей стенами и Ctrl + колёсико с ограничениями масштаба.
DungeonCombatTests: направление меча/стрелы, стены, однократный блок и стан, откаты,
неуязвимость и коллизия переката, остановка боя/врагов/стрел при открытом меню.
python Tools/check_generation_port.py сравнивает оригинал и копию на 68 конфигурациях
(нужны Python и .NET 10): все шаги, тексты, прогресс, карта, тайлы, спрайты и спавн.
Сборка Windows:
1. Открыть корень проекта в Unity 6000.6.6f1 через Unity Hub.
2. Выйти из Play Mode и дождаться компиляции скриптов без красных ошибок.
3. Нажать Coinbound > Build Windows preview в верхнем меню Unity.
   Команда сама включает Assets/Scenes/SampleScene.unity и собирает Windows x64.
4. Запустить Builds/Coinbound/Coinbound.exe. Передавать всю папку Builds/Coinbound,
   включая Coinbound_Data, UnityPlayer.dll и остальные файлы, а не один exe.
Это development-сборка для проверки. Для релизной сборки открыть File > Build Profiles,
выбрать Windows, проверить SampleScene в Scene List, выключить Development Build
и нажать Build, указав отдельную папку результата.
Меню создаётся при запуске: в редакторе оно видно в Game после нажатия Play.

В development-сборке флаг -coinbound-capture <папка> сохраняет набор кадров
интерфейса/данжа с seed 42 и закрывает игру; обычный запуск не включает эту проверку.


Actor attributes
----------------
DungeonStats.cs defines immutable CombatStats, PlayerStats and EnemyStats. Hero definitions
own player attributes; enemies accept an EnemyStats definition at initialization. Current
HP/MP and score belong to the actor, not the shared definition. No upgrades or runtime
attribute mutation are implemented yet.
Player defaults: HP 100, MP 100, critical chance 5%, luck 5 percentage points.
Movement: swordsman 3 units/s, archer 3.5 units/s. Roll cooldown: 1.5 seconds.
Swordsman: damage 2, attack rate 1/0.42 attacks/s; archer: damage 1, rate 1/0.38.
A critical roll is sampled once per sword swing or launched arrow and multiplies damage
by exactly 1.5. Enemy HP is fractional to preserve archer critical damage.
Enemy defaults: HP 3, defense 0, damage 10 (temporary testing value), movement 2.25 units/s, score drop 100.
Defense is flat reduction after critical damage: max(0, incoming - defense).
Luck adds percentage points to a favorable coin outcome, capped at 100%; the default
fair coin would have a 55% good-drop chance. PlayerStats.RollGoodDrop exposes the rule;
coin tossing/drop interaction does not exist in the current game and is not added here.
The five compact HUD rows (damage, movement, attack rate, critical chance, luck) sit below
the vitals on the left, with icons and values only. HP/MP remain in their resource bars.
Pause is available through Esc; the HUD has no pause button. Tools/Import-StatIcons.ps1
copies hand-sliced PNGs unchanged from ура/icons and ура/gui. GUI panels/buttons use 14px
nine-slice borders; the divider preserves its 61px center ornament. Hover, pressed and disabled
buttons use their supplied sprites. Point filtering, no mipmaps and integer source-pixel scaling
keep the original art crisp. Stat icons retain their source aspect ratio.

Authored HUD: InterfaceArt/Hud is copied unchanged from the supplied hud folder.
DungeonScreen.Hud composes the base, angled resource fills, slot states and cooldown masks
using the source README coordinates. The 165x57 group scales in whole physical pixels.
HP/MP display actual player resources, with no changes to the combat balance.
Swordsman has sword/block/dash; archer has bow/empty/dash. Sound is a non-interactive placeholder.
Cooldown masks drain vertically; HP/MP right edges move by whole source pixels.
The mana track remains visible at zero. The stats list sits below the authored group.

HUD tuning: score text is 13 units. Stat numbers sit 2 units above row centers and
reserve 70 units of width for signed values. Critical chance and luck display as fractions
(e.g. 0.05); luck's stored percentage-point convention remains unchanged.
Resource fills interpolate at four full bars per second, quantized to whole source pixels.
Binding a new player snaps bars to initial values. Loading/death menu particles are removed.

UI font: Cozette Regular 1.30.0 (CozetteVector.ttf), thin monospace with Cyrillic.
All normal labels use 13-unit text; headings use 26/39/52. Raster rendering and point
filtering remain enabled. Source and MIT license are included beside the font.
Resource HUD now shows only HP/MP bars, without numeric labels or an MP prefix.

Pixel particles: footsteps now emit small dust puffs, and rolls leave a short dust trail.
Player damage adds directional red fragments in the existing 32px world combat canvas.
HUD resource loss emits from the animated fill edge: falling HP droplets and rising,
breaking mana fragments. A 128-particle buffer limits HUD cost; private random sequences
leave combat RNG untouched. Binding/reset and healing do not emit loss particles.
Reduced Motion suppresses HUD resource particles. Existing rollDust/footDust settings
remain available and are enabled in DungeonEffects.asset.

Chests: Indigo 32x48 sprites at 32 PPU, shared base-center pivot (16,40) from top-left.
Source: ура/chest/README.md; Resources/ChestArtIndigo. Tile placement uses its center in every animation frame.
All four directions use their authored frames without mirroring. Point filtering, no mipmaps.
Opening: 00 -> 01 -> 02 -> 03 at 120 ms per frame, then hold 04. Closing frames 05..07
are available in the source but are not played by this one-shot loot chest.
E opens the nearest available chest within 1.4 tiles in the player's forward cone.
While moving, facing follows movement; at rest it follows aim. Walls block interaction.
Rolling, attacking, guarding, hit stop and pause prevent opening.
The static base collider never changes with lid animation. Depth sorting uses the ground anchor.
RewardRequested opens the coin interface; re-opening an unclaimed offer never rerolls it.

Fountain checkpoints (09.11.2026): E beside the basin opens a coin offering.
A recognized hand gesture or Space throws the coin INTO the water; landing always saves.
Continue restores the most recent fountain checkpoint after restarting the application.
Leaving the game or beginning another run never writes a checkpoint automatically.
The file is fountain-save.json in Application.persistentDataPath, atomically replaced with a backup.
Map seed/settings, hero, HP/MP, score, relic duplicates, cooldowns, surviving enemies and all chest states are restored.
Transient combat animations/projectiles are not resumed from mid-flight.
Keyboard spells: 1 pulse, 2 trace, 3 ward, 4 decoy, 5 recall; numpad supported.
