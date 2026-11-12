using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>One shared flow field per dungeon: enemies route around walls and the solid landmark bases.</summary>
    internal sealed class DungeonCombatNavigation
    {
        private static readonly ConditionalWeakTable<DungeonMap, DungeonCombatNavigation> Fields =
            new ConditionalWeakTable<DungeonMap, DungeonCombatNavigation>();
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly DungeonMap map;
        private bool[] walkable;
        private readonly int[] distance;
        private readonly Queue<Vector2Int> open = new Queue<Vector2Int>();
        private bool built;
        private int revision,seenRevision=-1;
        internal static void Invalidate(DungeonMap map){if(map!=null)For(map).revision++;}
        private Vector2Int lastGoal = new Vector2Int(int.MinValue, int.MinValue);

        private DungeonCombatNavigation(DungeonMap map)
        {
            this.map = map;
            walkable = new bool[map.TileWidth * map.TileHeight];
            distance = new int[walkable.Length];
        }

        internal static DungeonCombatNavigation For(DungeonMap map) => map == null ? null : Fields.GetValue(map, value => new DungeonCombatNavigation(value));
        internal static DungeonCombatNavigation Independent(DungeonMap map) => map == null ? null : new DungeonCombatNavigation(map);

        internal void Path(Vector2 from, Vector2 target, List<Vector2> result, float maximumLength = float.PositiveInfinity)
        {
            result.Clear();
            // Force the same walkability and flow field used by enemy navigation to build.
            if (DungeonCombatPhysics.ClearLine(from, target, .31f)) { result.Add(from); result.Add(target); return; }
            Direction(from, target);
            Vector2Int current = Cell(from);
            if (!Walkable(current) || distance[Index(current)] == int.MaxValue) return;
            result.Add(from);
            float length = 0;
            for (int guard = 0; guard < distance.Length; guard++)
            {
                int here = distance[Index(current)];
                Vector2 point = Center(current);
                length += Vector2.Distance(result[result.Count - 1], point);
                result.Add(point);
                if (length >= maximumLength) return;
                if (here == 0) break;
                bool found = false;
                foreach (Vector2Int delta in Directions)
                {
                    Vector2Int next = current + delta;
                    if (!Walkable(next) || distance[Index(next)] >= here) continue;
                    current = next; found = true; break;
                }
                if (!found) break;
            }
            if (result.Count > 0 && DungeonCombatPhysics.ClearLine(result[result.Count-1], target, .1f)) result.Add(target);
        }

        private static Vector2Int Cell(Vector2 world) => new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(1 - world.y));
        private readonly Queue<Vector2Int> localOpen = new Queue<Vector2Int>();
        private readonly Dictionary<Vector2Int,Vector2Int> localParents = new Dictionary<Vector2Int,Vector2Int>();
        internal bool LocalPath(Vector2 from,Vector2 to,List<Vector2> result)
        {
            result.Clear();
            if(DungeonCombatPhysics.ClearLine(from,to,.31f)){result.Add(from);result.Add(to);return true;}
            EnsureWalkability();Vector2Int start=Cell(from),goal=Cell(to);
            if(!Walkable(start)||!Walkable(goal))return false;
            localOpen.Clear();localParents.Clear();localOpen.Enqueue(start);localParents.Add(start,start);
            while(localOpen.Count>0&&localParents.Count<768)
            {
                Vector2Int cell=localOpen.Dequeue();
                if(cell==goal)
                {
                    result.Add(to);
                    while(cell!=start){result.Add(Center(cell));cell=localParents[cell];}
                    result.Add(from);result.Reverse();return true;
                }
                foreach(Vector2Int step in Directions)
                {Vector2Int next=cell+step;if(!Walkable(next)||localParents.ContainsKey(next))continue;localParents.Add(next,cell);localOpen.Enqueue(next);}
            }
            return false;
        }
        private static Vector2 Center(Vector2Int cell) => new Vector2(cell.x + 0.5f, 0.5f - cell.y);
        private int Index(Vector2Int cell) => cell.y * map.TileWidth + cell.x;
        private bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < map.TileWidth && cell.y < map.TileHeight;
        private bool Walkable(Vector2Int cell) => Contains(cell) && walkable[Index(cell)];

        private void EnsureWalkability()
        {
            DungeonCombatNavigation shared = For(map);
            if (built && seenRevision==shared.revision) return;
            seenRevision=shared.revision;lastGoal=new Vector2Int(int.MinValue,int.MinValue);
            if (shared != this)
            {
                shared.EnsureWalkability(); walkable = shared.walkable; built = true; return;
            }
            Physics2D.SyncTransforms();
            for (int y = 0; y < map.TileHeight; y++)
            for (int x = 0; x < map.TileWidth; x++)
            {
                var cell = new Vector2Int(x,y);
                walkable[Index(cell)] = map.IsTileFloor(new TilePosition(x,y)) && DungeonCombatPhysics.FreeFeet(Center(cell),.32f);
            }
            built = true;
        }

        internal Vector2 Direction(Vector2 from, Vector2 target)
        {
            if (DungeonCombatPhysics.ClearLine(from, target, 0.31f)) return (target - from).normalized;
            EnsureWalkability();
            Vector2Int goal = Cell(target);
            if (!Walkable(goal))
            {
                float nearest = float.PositiveInfinity;
                Vector2Int replacement = goal;
                for (int y = -2; y <= 2; y++)
                    for (int x = -2; x <= 2; x++)
                    {
                        Vector2Int cell = goal + new Vector2Int(x, y);
                        if (!Walkable(cell)) continue;
                        float gap = (Center(cell) - target).sqrMagnitude;
                        if (gap < nearest) { nearest = gap; replacement = cell; }
                    }
                goal = replacement;
            }
            if (!Walkable(goal)) return Vector2.zero;
            if (goal != lastGoal) Rebuild(goal);
            Vector2Int here = Cell(from);
            if (!Contains(here)) return Vector2.zero;
            int best = Walkable(here) ? distance[Index(here)] : int.MaxValue;
            Vector2 waypoint = Center(here);
            bool found = best == 0;
            foreach (Vector2Int step in Directions)
            {
                Vector2Int neighbor = here + step;
                if (!Walkable(neighbor)) continue;
                int value = distance[Index(neighbor)];
                if (value >= best) continue;
                Vector2 next = Center(neighbor);
                best = value;
                // Recenter first when a diagonal approach would clip the corner of a wall.
                waypoint = DungeonCombatPhysics.ClearLine(from, next, 0.31f) ? next : Center(here);
                found = true;
            }
            return found ? (waypoint - from).normalized : Vector2.zero;
        }

        private void Rebuild(Vector2Int goal)
        {
            lastGoal = goal;
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;
            open.Clear();
            distance[Index(goal)] = 0;
            open.Enqueue(goal);
            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();
                int nextDistance = distance[Index(current)] + 1;
                foreach (Vector2Int step in Directions)
                {
                    Vector2Int next = current + step;
                    if (!Walkable(next) || distance[Index(next)] <= nextDistance) continue;
                    distance[Index(next)] = nextDistance;
                    open.Enqueue(next);
                }
            }
        }
    }
}
