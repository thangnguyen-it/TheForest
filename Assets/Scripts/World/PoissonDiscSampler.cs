using System.Collections.Generic;
using UnityEngine;

namespace TheForest.World
{
    /// <summary>
    /// Poisson Disc Sampling (thuật toán Bridson) trên mặt phẳng 2D (x,z).
    /// Đảm bảo khoảng cách tối thiểu giữa các điểm -> phân bố tự nhiên,
    /// không chồng đống, buộc người chơi đi tìm tài nguyên.
    /// </summary>
    public static class PoissonDiscSampler
    {
        public static List<Vector2> Generate(float width, float height, float minRadius,
                                             int maxSamplesPerPoint = 30, int seed = 0)
        {
            var rng = new System.Random(seed);
            float cellSize = minRadius / Mathf.Sqrt(2f);
            int gridW = Mathf.CeilToInt(width / cellSize);
            int gridH = Mathf.CeilToInt(height / cellSize);

            var grid = new int[gridW, gridH];
            for (int x = 0; x < gridW; x++)
                for (int y = 0; y < gridH; y++)
                    grid[x, y] = -1;

            var points = new List<Vector2>();
            var active = new List<Vector2>();

            // Điểm khởi đầu ngẫu nhiên
            Vector2 first = new Vector2((float)rng.NextDouble() * width,
                                        (float)rng.NextDouble() * height);
            AddPoint(first, points, active, grid, cellSize);

            while (active.Count > 0)
            {
                int idx = rng.Next(active.Count);
                Vector2 center = active[idx];
                bool found = false;

                for (int k = 0; k < maxSamplesPerPoint; k++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = minRadius * (1f + (float)rng.NextDouble()); // [r, 2r)
                    Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                    if (IsValid(candidate, width, height, minRadius, points, grid, cellSize))
                    {
                        AddPoint(candidate, points, active, grid, cellSize);
                        found = true;
                        break;
                    }
                }

                if (!found) active.RemoveAt(idx); // điểm này "cạn" vùng quanh nó
            }

            return points;
        }

        private static void AddPoint(Vector2 p, List<Vector2> points, List<Vector2> active,
                                     int[,] grid, float cellSize)
        {
            int gx = (int)(p.x / cellSize);
            int gy = (int)(p.y / cellSize);
            grid[gx, gy] = points.Count;
            points.Add(p);
            active.Add(p);
        }

        private static bool IsValid(Vector2 c, float width, float height, float minRadius,
                                    List<Vector2> points, int[,] grid, float cellSize)
        {
            if (c.x < 0 || c.x >= width || c.y < 0 || c.y >= height) return false;

            int gx = (int)(c.x / cellSize);
            int gy = (int)(c.y / cellSize);
            int gridW = grid.GetLength(0);
            int gridH = grid.GetLength(1);

            // Kiểm tra các ô lân cận trong bán kính 2 ô
            int startX = Mathf.Max(0, gx - 2), endX = Mathf.Min(gx + 2, gridW - 1);
            int startY = Mathf.Max(0, gy - 2), endY = Mathf.Min(gy + 2, gridH - 1);

            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    int pointIdx = grid[x, y];
                    if (pointIdx >= 0)
                    {
                        float sqrDist = (c - points[pointIdx]).sqrMagnitude;
                        if (sqrDist < minRadius * minRadius) return false;
                    }
                }
            }
            return true;
        }
    }
}
