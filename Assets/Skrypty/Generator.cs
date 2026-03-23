using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralTerrain : MonoBehaviour
{
    public int resolution = 200;
    public float heightMultiplier = 25f;
    public float oceanLevel = 0.18f;
    public float oceanWidth = 0.25f;
    public int seed = 0;
    public float riverErosionStrength = 0.6f;
    public int riverSmoothIterations = 3;

    Mesh mesh;
    float[,] heightMap;
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uv;
    List<Vector2> riverPath;

    void Start()
    {
        Generate();
    }

    void Generate()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        heightMap = new float[resolution + 1, resolution + 1];
        riverPath = new List<Vector2>();
        GenerateTerrain();
        FindAndCarveRiver();
        SmoothRiver();
        ApplyOcean();
        BuildMesh();
        UpdateMesh();
    }

    void GenerateTerrain()
    {
        Random.InitState(seed);
        for (int x = 0; x <= resolution; x++)
        {
            for (int y = 0; y <= resolution; y++)
            {
                float nx = (float)x / resolution;
                float ny = (float)y / resolution;

                float h = Mathf.PerlinNoise(nx * 3.2f + seed * 0.17f, ny * 3.2f + seed * 0.23f);
                h += Mathf.PerlinNoise(nx * 8f + 1.2f, ny * 8f + 2.3f) * 0.38f;
                h += Mathf.PerlinNoise(nx * 16f - 3.1f, ny * 16f + 1.7f) * 0.14f;

                h = Mathf.Pow(h, 1.15f);
                heightMap[x, y] = Mathf.Clamp01(h * 0.72f);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            float[,] smoothed = new float[resolution + 1, resolution + 1];
            for (int x = 1; x < resolution; x++)
            {
                for (int y = 1; y < resolution; y++)
                {
                    float avg = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            avg += heightMap[x + dx, y + dy];
                        }
                    }
                    smoothed[x, y] = avg / 9f;
                }
            }

            for (int x = 1; x < resolution; x++)
            {
                for (int y = 1; y < resolution; y++)
                {
                    heightMap[x, y] = smoothed[x, y];
                }
            }
        }
    }

    void FindAndCarveRiver()
    {
        Vector2 highestPoint = FindHighestPointInMountains();
        Vector2 currentPos = highestPoint;
        riverPath.Clear();
        riverPath.Add(currentPos);

        int maxSteps = resolution * 8;
        int stuckCounter = 0;

        for (int step = 0; step < maxSteps; step++)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(currentPos.x * resolution), 0, resolution);
            int y = Mathf.Clamp(Mathf.RoundToInt(currentPos.y * resolution), 0, resolution);

            float currentHeight = heightMap[x, y];

            float lowestNeighbor = currentHeight;
            Vector2 bestDirection = currentPos;
            bool foundLower = false;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    float nx = currentPos.x + dx * 0.01f;
                    float ny = currentPos.y + dy * 0.01f;

                    if (nx < 0 || nx > 1 || ny < 0 || ny > 1) continue;

                    int ix = Mathf.RoundToInt(nx * resolution);
                    int iy = Mathf.RoundToInt(ny * resolution);
                    float neighborHeight = heightMap[ix, iy];

                    if (neighborHeight < lowestNeighbor - 0.005f)
                    {
                        lowestNeighbor = neighborHeight;
                        bestDirection = new Vector2(nx, ny);
                        foundLower = true;
                    }
                }
            }

            if (!foundLower)
            {
                stuckCounter++;
                if (stuckCounter > 20)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            float nx = currentPos.x + dx * 0.01f;
                            float ny = currentPos.y + dy * 0.01f;
                            if (nx >= 0 && nx <= 1 && ny >= 0 && ny <= 1)
                            {
                                bestDirection = new Vector2(nx, ny);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    currentPos = bestDirection;
                    continue;
                }
            }

            currentPos = bestDirection;
            riverPath.Add(currentPos);

            if (currentPos.x < oceanWidth + 0.05f)
                break;

            if (currentPos.y < 0.05f || currentPos.y > 0.95f)
                break;
        }

        CarveRiverPath();
    }

    Vector2 FindHighestPointInMountains()
    {
        float maxHeight = 0;
        Vector2 highest = new Vector2(0.85f, 0.5f);

        for (int x = (int)(resolution * 0.7f); x <= resolution; x++)
        {
            for (int y = 0; y <= resolution; y++)
            {
                float ny = (float)y / resolution;
                if (ny < 0.15f || ny > 0.85f) continue;

                float h = heightMap[x, y];
                if (h > maxHeight)
                {
                    maxHeight = h;
                    highest = new Vector2((float)x / resolution, ny);
                }
            }
        }

        return highest;
    }

    void CarveRiverPath()
    {
        float riverDepth = oceanLevel + 0.025f;
        float erosionRadius = 2.2f;
        float sandRadius = 3.5f;

        foreach (Vector2 point in riverPath)
        {
            int centerX = Mathf.RoundToInt(point.x * resolution);
            int centerY = Mathf.RoundToInt(point.y * resolution);

            int radius = Mathf.CeilToInt(erosionRadius);
            int sandRad = Mathf.CeilToInt(sandRadius);

            for (int dx = -sandRad; dx <= sandRad; dx++)
            {
                for (int dy = -sandRad; dy <= sandRad; dy++)
                {
                    int nx = centerX + dx;
                    int ny = centerY + dy;

                    if (nx < 0 || nx > resolution || ny < 0 || ny > resolution) continue;

                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance <= erosionRadius)
                    {
                        float erosionFactor = 1f - (distance / erosionRadius);
                        erosionFactor = Mathf.Pow(erosionFactor, 1.5f);

                        float targetHeight = Mathf.Lerp(riverDepth, heightMap[nx, ny], erosionFactor * 0.4f);
                        heightMap[nx, ny] = Mathf.Min(heightMap[nx, ny], targetHeight);

                        if (distance < erosionRadius * 0.6f)
                        {
                            float bankFactor = Mathf.Sin(distance / erosionRadius * Mathf.PI) * 0.15f;
                            heightMap[nx, ny] = Mathf.Min(heightMap[nx, ny], riverDepth + bankFactor);
                        }
                    }
                    else if (distance <= sandRadius && distance > erosionRadius)
                    {
                        float sandFactor = 1f - ((distance - erosionRadius) / (sandRadius - erosionRadius));
                        sandFactor = Mathf.Pow(sandFactor, 1.2f);

                        float targetHeight = Mathf.Lerp(riverDepth + 0.03f, heightMap[nx, ny], sandFactor * 0.5f);
                        heightMap[nx, ny] = Mathf.Min(heightMap[nx, ny], targetHeight);
                    }
                }
            }
        }

        for (int i = 0; i < riverSmoothIterations; i++)
        {
            SmoothRiverbanks();
        }
    }

    void SmoothRiverbanks()
    {
        float[,] newHeightMap = (float[,])heightMap.Clone();

        for (int x = 2; x < resolution - 1; x++)
        {
            for (int y = 2; y < resolution - 1; y++)
            {
                bool isRiver = false;
                float riverValue = 0;
                int riverCount = 0;

                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        float h = heightMap[x + dx, y + dy];
                        if (h < oceanLevel + 0.06f)
                        {
                            isRiver = true;
                            riverValue += h;
                            riverCount++;
                        }
                    }
                }

                if (isRiver && riverCount > 0)
                {
                    float avgRiver = riverValue / riverCount;
                    newHeightMap[x, y] = Mathf.Lerp(heightMap[x, y], avgRiver, 0.5f);
                }
            }
        }

        heightMap = newHeightMap;
    }

    void SmoothRiver()
    {
        for (int i = 0; i < 3; i++)
        {
            for (int x = 1; x < resolution; x++)
            {
                for (int y = 1; y < resolution; y++)
                {
                    if (heightMap[x, y] < oceanLevel + 0.08f)
                    {
                        float avg = 0;
                        int count = 0;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (heightMap[x + dx, y + dy] < oceanLevel + 0.1f)
                                {
                                    avg += heightMap[x + dx, y + dy];
                                    count++;
                                }
                            }
                        }

                        if (count > 0)
                        {
                            heightMap[x, y] = avg / count;
                        }
                    }
                }
            }
        }
    }

    void ApplyOcean()
    {
        for (int x = 0; x <= resolution; x++)
        {
            float nx = (float)x / resolution;
            if (nx >= oceanWidth) continue;
            for (int y = 0; y <= resolution; y++)
            {
                if (heightMap[x, y] > oceanLevel)
                {
                    heightMap[x, y] = Mathf.Lerp(heightMap[x, y], oceanLevel, 0.8f);
                }
            }
        }
    }

    void BuildMesh()
    {
        vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        triangles = new int[resolution * resolution * 6];
        uv = new Vector2[vertices.Length];

        for (int x = 0; x <= resolution; x++)
        {
            for (int y = 0; y <= resolution; y++)
            {
                float h = heightMap[x, y];
                vertices[y * (resolution + 1) + x] = new Vector3(x, h * heightMultiplier, y);
                uv[y * (resolution + 1) + x] = new Vector2((float)x / resolution, (float)y / resolution);
            }
        }

        int t = 0;
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                int i = y * (resolution + 1) + x;
                triangles[t++] = i;
                triangles[t++] = i + resolution + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + resolution + 1;
                triangles[t++] = i + resolution + 2;
            }
        }
    }

    void UpdateMesh()
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
    }

    public float GetHeightAtPosition(float x, float z)
    {
        if (heightMap == null) return 0;

        float nx = x / resolution;
        float nz = z / resolution;

        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);

        int xIndex = Mathf.FloorToInt(nx * resolution);
        int zIndex = Mathf.FloorToInt(nz * resolution);

        xIndex = Mathf.Clamp(xIndex, 0, resolution - 1);
        zIndex = Mathf.Clamp(zIndex, 0, resolution - 1);

        float xFraction = nx * resolution - xIndex;
        float zFraction = nz * resolution - zIndex;

        float h00 = heightMap[xIndex, zIndex];
        float h10 = heightMap[xIndex + 1, zIndex];
        float h01 = heightMap[xIndex, zIndex + 1];
        float h11 = heightMap[xIndex + 1, zIndex + 1];

        float h0 = Mathf.Lerp(h00, h10, xFraction);
        float h1 = Mathf.Lerp(h01, h11, xFraction);
        float height = Mathf.Lerp(h0, h1, zFraction);

        return height * heightMultiplier;
    }

}