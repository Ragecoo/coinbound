using ProceduralGeneration;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Exact grid traversal in the original map. Neither physics nor camera zoom changes sight.</summary>
    public static class DungeonVisibility
    {
        public static float Cast(DungeonMap map, Vector2 origin, Vector2 direction, float maximumDistance)
        {
            float magnitude = direction.magnitude;
            if (magnitude < 0.000001f || maximumDistance <= 0) return 0;
            direction /= magnitude;
            // Generator coordinates are Y-down. World cell (x, y) occupies [x,x+1] x [-y,1-y].
            Vector2 point = new Vector2(origin.x, 1 - origin.y);
            Vector2 ray = new Vector2(direction.x, -direction.y);
            int x = Mathf.FloorToInt(point.x), y = Mathf.FloorToInt(point.y);
            if (!map.IsTileFloor(new TilePosition(x, y))) return 0;
            int stepX = ray.x >= 0 ? 1 : -1, stepY = ray.y >= 0 ? 1 : -1;
            float deltaX = Mathf.Abs(ray.x) > 0.000001f ? Mathf.Abs(1 / ray.x) : float.PositiveInfinity;
            float deltaY = Mathf.Abs(ray.y) > 0.000001f ? Mathf.Abs(1 / ray.y) : float.PositiveInfinity;
            float nextX = (stepX > 0 ? x + 1 - point.x : point.x - x) * deltaX;
            float nextY = (stepY > 0 ? y + 1 - point.y : point.y - y) * deltaY;
            if (float.IsInfinity(deltaX)) nextX = float.PositiveInfinity;
            if (float.IsInfinity(deltaY)) nextY = float.PositiveInfinity;

            // A normalized ray crosses at most two grid edges per world unit, plus its starting cell.
            int maximumSteps = Mathf.CeilToInt(maximumDistance * 2) + 4;
            for (int i = 0; i < maximumSteps; i++)
            {
                float distance = Mathf.Min(nextX, nextY);
                if (distance >= maximumDistance) return maximumDistance;
                if (Mathf.Abs(nextX - nextY) < 0.00001f)
                {
                    // A ray grazing a closed corner must never leak diagonally through two walls.
                    if (!map.IsTileFloor(new TilePosition(x + stepX, y)) ||
                        !map.IsTileFloor(new TilePosition(x, y + stepY)))
                        return distance;
                    x += stepX;
                    y += stepY;
                    nextX += deltaX;
                    nextY += deltaY;
                }
                else if (nextX < nextY)
                {
                    x += stepX;
                    nextX += deltaX;
                }
                else
                {
                    y += stepY;
                    nextY += deltaY;
                }
                if (!map.IsTileFloor(new TilePosition(x, y))) return distance;
            }
            return maximumDistance;
        }
    }
}
