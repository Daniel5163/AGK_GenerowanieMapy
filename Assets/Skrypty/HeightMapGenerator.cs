using UnityEngine;
using System.Collections.Generic;

public static class HeightMapGenerator
{
    public static float[,] GenerateHeightMap(int width, int height, float scale, int octaves, float persistence, float lacunarity, Vector2 offset)
    {
        float[,] map = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noise = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x + offset.x) / scale * frequency;
                    float sampleY = (y + offset.y) / scale * frequency;

                    float p = Mathf.PerlinNoise(sampleX, sampleY);
                    noise += p * amplitude;

                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                map[x, y] = noise;
            }
        }

        Normalize(map, width, height);
        return map;
    }

    static void Normalize(float[,] map, int w, int h)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                min = Mathf.Min(min, map[x, y]);
                max = Mathf.Max(max, map[x, y]);
            }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                map[x, y] = Mathf.InverseLerp(min, max, map[x, y]);
            }
    }

    public static float[,] AddRiver(float[,] map, int width, int height)
    {
        List<Vector2Int> river = GenerateRiverPath(width, height, map);

        CarveRiver(map, width, height, river);

        return map;
    }

    static List<Vector2Int> GenerateRiverPath(int width, int height, float[,] map)
    {
        List<Vector2Int> path = new List<Vector2Int>();

        int x = width - 10;
        int y = height / 2;

        path.Add(new Vector2Int(x, y));

        for (int i = 0; i < width * 2; i++)
        {
            float best = float.MaxValue;
            int bestX = x;
            int bestY = y;

            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -2; dx <= 0; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx < 1 || nx >= width - 1 || ny < 1 || ny >= height - 1)
                        continue;

                    float h = map[nx, ny];

                    float score = h + Mathf.Abs(ny - height / 2f) * 0.01f;

                    if (score < best)
                    {
                        best = score;
                        bestX = nx;
                        bestY = ny;
                    }
                }

            x = bestX;
            y = bestY;

            path.Add(new Vector2Int(x, y));

            if (x < 5)
                break;
        }

        return path;
    }

    static void CarveRiver(float[,] map, int width, int height, System.Collections.Generic.List<Vector2Int> river)
    {
        for (int i = 0; i < river.Count; i++)
        {
            Vector2Int p = river[i];

            float t = i / (float)river.Count;

            float widthR = Mathf.Lerp(2f, 8f, t);
            float depth = Mathf.Lerp(0.02f, 0.06f, t);

            for (int y = -Mathf.CeilToInt(widthR); y <= Mathf.CeilToInt(widthR); y++)
            {
                for (int x = -Mathf.CeilToInt(widthR); x <= Mathf.CeilToInt(widthR); x++)
                {
                    int nx = p.x + x;
                    int ny = p.y + y;

                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    float dist = Mathf.Sqrt(x * x + y * y) / widthR;
                    if (dist > 1f) continue;

                    float shape = Mathf.Pow(1f - dist, 2.2f);

                    float value = shape * depth;

                    float h = map[nx, ny];
                    h -= value;

                    if (float.IsNaN(h) || float.IsInfinity(h))
                        h = 0f;

                    map[nx, ny] = Mathf.Clamp01(h);
                }
            }
        }
    }
}