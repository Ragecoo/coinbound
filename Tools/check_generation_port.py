"""Compare the untouched .NET 10 utility with the Unity port. Writes only Logs/."""
import hashlib
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
source = root / "TestingUtil/ProceduralGeneration"
check = root / "Logs/GenerationParity"
original = check / "Original"
original.mkdir(parents=True, exist_ok=True)
before = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in source.rglob("*") if p.is_file()}
for name in ("DungeonGenerator.cs", "DungeonMap.cs", "TileSprites.cs", "SpawnPlanner.cs", "ChestPlanner.cs"):
    code = (source / name).read_text(encoding="utf-8-sig")
    (original / name).write_text(code.replace("namespace ProceduralGeneration;", "namespace Original;"), encoding="utf-8")
(check / "GenerationParity.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><Compile Include="../../Assets/Coinbound/Generation/*.cs" /></ItemGroup>
</Project>
''', encoding="utf-8")
(check / "Program.cs").write_text('''using System.Text.Json;
int[] seeds = Enumerable.Range(0, 64).Concat(new[] { int.MinValue, int.MaxValue, -1, 42 }).ToArray();
for (int n = 0; n < seeds.Length; n++)
{
    int seed = seeds[n];
    (int w, int h) = (n % 4) switch { 0 => (11, 11), 1 => (21, 15), 2 => (39, 19), _ => (61, 25) };
    int? rooms = n % 3 == 0 ? null : n % 9;
    double loops = n % 3 == 0 ? 0 : n % 3 == 1 ? 0.08 : 1;
    var original = new Original.DungeonGenerator(new(w, h, seed, rooms, loops));
    var port = new ProceduralGeneration.DungeonGenerator(new(w, h, seed, rooms, loops));
    Equal(original.Generate().ToArray(), port.Generate().ToArray(), "steps / progress / text");
    Equal(original.Map, port.Map, "map / rooms / route");
    Equal(Original.TileSpriteMap.Create(original.Map, seed),
          ProceduralGeneration.TileSpriteMap.Create(port.Map, seed), "tile / sprite selection");
    Equal(new Original.SpawnPlanner(original.Map, new(), seed).Generate(),
          new ProceduralGeneration.SpawnPlanner(port.Map, new(), seed).Generate(), "chests / enemies");
    void Equal(object expected, object actual, string area)
    {
        if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(actual))
            throw new Exception($"Parity failure: seed={seed}, {w}x{h}, {area}");
    }
}
Console.WriteLine($"PASS: {seeds.Length} exact comparisons of steps, progress, text, maps, rooms, routes, sprites, chests and enemies.");
''', encoding="utf-8")
subprocess.run(["dotnet", "run", "--project", str(check / "GenerationParity.csproj")], cwd=root, check=True)
after = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in source.rglob("*") if p.is_file()}
assert before == after, "The source utility was modified."
print("PASS: source utility file hashes are unchanged.")
