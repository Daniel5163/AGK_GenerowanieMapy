using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class TerrainGenerator : MonoBehaviour
{
    public int width = 220;
    public int height = 220;

    public float scale = 60f;
    public int octaves = 3;
    public float persistence = 0.45f;
    public float lacunarity = 2f;

    public float heightMultiplier = 18f;

    float[,] heightMap;

    void Start()
    {
        Generate();
    }

    public void Generate()
    {
        heightMap = GenerateHeightMap();

        CreateLowlands();     
        GenerateRiverSystem(); 
        SmoothTerrain(5);
        Sanitize();
        BuildMesh();
    }

    float[,] GenerateHeightMap()
    {
        float[,] map = new float[width, height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float value = 0;
                float amp = 1;
                float freq = 1;

                for (int i = 0; i < octaves; i++)
                {
                    float sx = x / scale * freq;
                    float sy = y / scale * freq;

                    float n = Mathf.PerlinNoise(sx, sy);

                    n = Mathf.SmoothStep(0f, 1f, n);

                    value += n * amp;

                    amp *= persistence;
                    freq *= lacunarity;
                }

                map[x, y] = value;
            }

        Normalize(map);
        return map;
    }

    void CreateLowlands()
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float h = heightMap[x, y];

                float flatFactor = Mathf.Clamp01(1f - Mathf.Abs(h - 0.3f) * 2.5f);

                h = Mathf.Lerp(h, 0.25f, flatFactor * 0.6f);

                heightMap[x, y] = h;
            }
    }
    void GenerateRiverSystem()
    {
        int x = width - 10;
        int y = height / 2;

        for (int i = 0; i < width * 3; i++)
        {
            Carve(x, y, 6, 0.03f);

            int bestX = x;
            int bestY = y;
            float bestH = heightMap[x, y];

            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -2; dx <= 0; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (nx < 1 || nx >= width - 1 || ny < 1 || ny >= height - 1)
                        continue;

                    float h = heightMap[nx, ny];

                    if (h < bestH)
                    {
                        bestH = h;
                        bestX = nx;
                        bestY = ny;
                    }
                }

            x = bestX;
            y = bestY;

            if (Random.value < 0.01f)
                CreateLake(x, y);

            if (x < 5)
                break;
        }
    }

    void Carve(int cx, int cy, int radius, float depth)
    {
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int nx = cx + x;
                int ny = cy + y;

                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                float dist = Vector2.Distance(Vector2.zero, new Vector2(x, y)) / radius;

                if (dist > 1f) continue;

                heightMap[nx, ny] -= (1f - dist) * depth;
            }
    }

    void CreateLake(int cx, int cy)
    {
        Carve(cx, cy, 12, 0.08f);
    }

    void SmoothTerrain(int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            float[,] copy = (float[,])heightMap.Clone();

            for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    heightMap[x, y] =
                        (copy[x, y] +
                         copy[x + 1, y] +
                         copy[x - 1, y] +
                         copy[x, y + 1] +
                         copy[x, y - 1]) / 5f;
                }
        }
    }

    void Normalize(float[,] map)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                min = Mathf.Min(min, map[x, y]);
                max = Mathf.Max(max, map[x, y]);
            }

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float v = Mathf.InverseLerp(min, max, map[x, y]);

                v = Mathf.Pow(v, 2.2f);

                map[x, y] = v;
            }
    }

    void Sanitize()
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float v = heightMap[x, y];

                if (float.IsNaN(v) || float.IsInfinity(v))
                    v = 0f;

                heightMap[x, y] = Mathf.Clamp01(v);
            }
    }

    void BuildMesh()
    {
        Mesh mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        Vector3[] vertices = new Vector3[width * height];
        Color[] colors = new Color[width * height];
        int[] triangles = new int[(width - 1) * (height - 1) * 6];

        int i = 0;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++, i++)
            {
                float h = heightMap[x, y];

                vertices[i] = new Vector3(
                    x - width / 2f,
                    h * heightMultiplier,
                    y - height / 2f
                );

                colors[i] = new Color(h, 0, 0, 1);
            }

        int ti = 0;

        for (int y = 0; y < height - 1; y++)
            for (int x = 0; x < width - 1; x++)
            {
                int a = y * width + x;
                int b = a + 1;
                int c = a + width;
                int d = c + 1;

                triangles[ti++] = a;
                triangles[ti++] = c;
                triangles[ti++] = b;

                triangles[ti++] = b;
                triangles[ti++] = c;
                triangles[ti++] = d;
            }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;

        mesh.RecalculateNormals();

        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    public float GetHeightAtPosition(float worldX, float worldZ)
    {
        int x = Mathf.RoundToInt(worldX + width / 2f);
        int y = Mathf.RoundToInt(worldZ + height / 2f);

        if (x < 0 || x >= width || y < 0 || y >= height)
            return 0f;

        return heightMap[x, y] * heightMultiplier;
    }
}