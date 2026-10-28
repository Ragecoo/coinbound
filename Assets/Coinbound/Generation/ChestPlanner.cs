// Ported from TestingUtil/ProceduralGeneration. Only C# / .NET compatibility changes.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
namespace ProceduralGeneration
{

public sealed partial class SpawnPlanner
{
    private List<ChestSpawn> PlaceChests()
    {
        var chests = new List<ChestSpawn>();
        var candidates = new List<(TilePosition Tile, bool DeadEnd)>();
        var route = Map.TileSolution.ToHashSet();
        foreach (Cell section in Map.Floors())
        {
            if (Map.GetSection(section) != TileKind.Corridor) continue;
            Cell[] neighbors = Map.Neighbors(section).ToArray();
            bool deadEnd = neighbors.Length == 1;
            TilePosition origin = Map.SectionOrigin(section);
            for (int dy = 0; dy < DungeonMap.SectionSize; dy++)
                for (int dx = 0; dx < DungeonMap.SectionSize; dx++)
                {
                    var tile = new TilePosition(origin.X + dx, origin.Y + dy);
                    // Центры сторон секции: исключаем углы и смещение к боковой стене.
                    if (!((dx == 1 && dy != 1) || (dy == 1 && dx != 1))) continue;
                    if (deadEnd)
                    {
                        int facingX = neighbors[0].X - section.X, facingY = neighbors[0].Y - section.Y;
                        // Только торец напротив единственного прохода, не боковые стены.
                        if ((facingX == 1 && dx != 0) || (facingX == -1 && dx != DungeonMap.SectionSize - 1) ||
                            (facingY == 1 && dy != 0) || (facingY == -1 && dy != DungeonMap.SectionSize - 1)) continue;
                    }
                    if (route.Contains(tile)) continue;
                    if (Map.GetTile(new(tile.X - 1, tile.Y)) == TileKind.Wall ||
                        Map.GetTile(new(tile.X + 1, tile.Y)) == TileKind.Wall ||
                        Map.GetTile(new(tile.X, tile.Y - 1)) == TileKind.Wall ||
                        Map.GetTile(new(tile.X, tile.Y + 1)) == TileKind.Wall)
                        candidates.Add((tile, deadEnd));
                }
        }
        // Случайный первый сундук, следующие — подальше от уже размещённых.
        var remaining = candidates.OrderBy(_ => _random.Next()).ToList();
        int minimumDistance = _settings.ChestDistance ?? Map.ChestMinimumDistance;
        while (chests.Count < _settings.Chests && remaining.Count > 0)
        {
            int DistanceSquared(TilePosition a, TilePosition b) =>
                (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
            var ranked = remaining.Select(candidate => new
            {
                Candidate = candidate,
                Distance = chests.Count == 0 ? 0 : chests.Min(chest => DistanceSquared(chest.Position, candidate.Tile))
            }).ToArray();
            // Бонус тупику сохраняет предпочтение, но удалённость важнее скопления сундуков.
            var selected = ranked.MaxByStable(item => chests.Count == 0
                ? (item.Candidate.DeadEnd ? 1d : 0d)
                : item.Distance * (item.Candidate.DeadEnd ? 1.5 : 1.0))!.Candidate;
            chests.Add(new ChestSpawn(selected.Tile, TileSprites.ChestFacing(Map, selected.Tile)));
            remaining.RemoveAll(candidate => DistanceSquared(candidate.Tile, selected.Tile) < (long)minimumDistance * minimumDistance);

        }
        return chests;
    }
}

}
