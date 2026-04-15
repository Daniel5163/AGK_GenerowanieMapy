using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrain : MonoBehaviour
{
    [Header("General Settings")]
    public int resolution = 200;
    public float heightMultiplier = 68f;
    public float worldScale = 1f;
    public bool randomSeedOnPlay = true;
    public int seed;

    [Header("Terrain Noise")]
    public int octaves = 7;
    public float persistence = 0.47f;
    public float lacunarity = 2.12f;
    public float baseFrequency = 1.38f;

    [Header("Water")]
    public float seaLevel = 0.26f;
    public float waterLevelY = 13f;

    [Header("River Settings")]
    public float riverFlowSpeed = 2.0f;
    public float riverTiling = 8f;
    public float riverWidth = 1.8f;

    [Header("Lake Settings")]
    public int lakeSearchStep = 6;
    public float lakeDepthMultiplier = 0.78f;

    [Header("Materials")]
    public Material seaLakeMaterial;

    [Header("Prefabs")]
    public List<GameObject> treePrefabs = new List<GameObject>();
    public List<GameObject> grassPrefabs = new List<GameObject>();
    public List<GameObject> rockPrefabs = new List<GameObject>();
    public GameObject waterPrefab;

    private Mesh mesh;
    private float[,] heightMap;
    private int[,] biomeMap;
    private bool[,] isRiver;                   
    private MeshCollider meshCollider;
    private List<Material> riverMaterials = new List<Material>();

    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
        if (randomSeedOnPlay)
            seed = Random.Range(-999999, 999999);
        Generate();
    }

    public void Generate()
    {
        ClearOldDetails();
        riverMaterials.Clear();

        mesh = new Mesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        heightMap = new float[resolution + 1, resolution + 1];
        biomeMap = new int[resolution + 1, resolution + 1];
        isRiver = new bool[resolution + 1, resolution + 1];   

        GenerateHeightMap();
        GenerateBiomes();
        CreateLakes();
        GenerateRiver();                   
        BuildMesh();
        UpdateCollider();
        SpawnDetails();
        GenerateSeaAndLakesWater();
    }

    void GenerateHeightMap()
    {
        float offsetX = seed * 0.00125f;
        float offsetZ = seed * 0.0013f;

        for (int x = 0; x <= resolution; x++)
        {
            for (int z = 0; z <= resolution; z++)
            {
                float nx = (float)x / resolution * baseFrequency + offsetX;
                float nz = (float)z / resolution * baseFrequency + offsetZ;

                float height = 0f;
                float amp = 1f;
                float freq = 1f;
                float maxAmp = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    float sample = Mathf.PerlinNoise(nx * freq, nz * freq);
                    sample = 1f - Mathf.Abs(sample * 2f - 1f);
                    height += sample * amp;
                    maxAmp += amp;
                    amp *= persistence;
                    freq *= lacunarity;
                }

                height /= maxAmp;
                height = Mathf.Pow(height, 1.15f);

                float normalizedX = (float)x / resolution;
                float ocean = Mathf.Pow(1f - normalizedX, 2.85f);
                height = height * (1f - ocean * 0.78f);
                height *= Mathf.Lerp(0.68f, 1.05f, normalizedX);

                heightMap[x, z] = Mathf.Clamp01(height);
            }
        }
    }

    void GenerateBiomes()
    {
        for (int x = 0; x <= resolution; x++)
            for (int z = 0; z <= resolution; z++)
            {
                float h = heightMap[x, z];
                if (h < seaLevel + 0.04f) biomeMap[x, z] = 0;
                else if (h < seaLevel + 0.09f) biomeMap[x, z] = 1;
                else if (h < 0.53f) biomeMap[x, z] = 2;
                else if (h < 0.76f) biomeMap[x, z] = 3;
                else biomeMap[x, z] = 4;
            }
    }

    void CreateLakes()
    {
        for (int x = lakeSearchStep; x < resolution; x += lakeSearchStep)
        {
            for (int z = lakeSearchStep; z < resolution; z += lakeSearchStep)
            {
                float h = heightMap[x, z];
                if (h <= seaLevel + 0.12f || h >= 0.48f) continue;

                bool isLocalMin = true;
                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = Mathf.Clamp(x + dx, 0, resolution);
                        int nz = Mathf.Clamp(z + dz, 0, resolution);
                        if (heightMap[nx, nz] > h + 0.04f) isLocalMin = false;
                    }
                }

                if (isLocalMin)
                {
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        for (int dz = -3; dz <= 3; dz++)
                        {
                            int nx = Mathf.Clamp(x + dx, 0, resolution);
                            int nz = Mathf.Clamp(z + dz, 0, resolution);
                            float dist = Mathf.Sqrt(dx * dx + dz * dz);
                            float factor = Mathf.Max(0f, 1f - dist / 4f);
                            heightMap[nx, nz] = Mathf.Lerp(heightMap[nx, nz], h - 0.09f, factor * lakeDepthMultiplier);
                        }
                    }
                }
            }
        }
    }

    void BuildMesh()
    {
        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        int[] triangles = new int[resolution * resolution * 6];
        Vector2[] uv = new Vector2[vertices.Length];

        int i = 0;
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                vertices[i] = new Vector3(x * worldScale, heightMap[x, z] * heightMultiplier, z * worldScale);
                uv[i] = new Vector2((float)x / resolution, (float)z / resolution);
                i++;
            }
        }

        int t = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * (resolution + 1) + x;
                triangles[t++] = index;
                triangles[t++] = index + resolution + 1;
                triangles[t++] = index + 1;
                triangles[t++] = index + 1;
                triangles[t++] = index + resolution + 1;
                triangles[t++] = index + resolution + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }

    void UpdateCollider() => meshCollider.sharedMesh = mesh;

    void ClearOldDetails()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    void SpawnDetails()
    {
        float rayHeight = heightMultiplier + 180f;

        for (int x = 0; x < resolution; x += 3)
        {
            for (int z = 0; z < resolution; z += 3)
            {
                if (biomeMap[x, z] == 0 || heightMap[x, z] < seaLevel + 0.07f)
                    continue;

                Vector3 origin = new Vector3(x * worldScale, rayHeight, z * worldScale);

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 400f))
                {
                    float h = hit.point.y;

                    if (h <= waterLevelY + 0.6f)
                        continue;

                    int gridX = Mathf.RoundToInt(hit.point.x / worldScale);
                    int gridZ = Mathf.RoundToInt(hit.point.z / worldScale);
                    gridX = Mathf.Clamp(gridX, 0, resolution);
                    gridZ = Mathf.Clamp(gridZ, 0, resolution);

                    bool inRiver = isRiver[gridX, gridZ];

                    if (!inRiver)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                int cx = Mathf.Clamp(gridX + dx, 0, resolution);
                                int cz = Mathf.Clamp(gridZ + dz, 0, resolution);
                                if (isRiver[cx, cz])
                                {
                                    inRiver = true;
                                    break;
                                }
                            }
                            if (inRiver) break;
                        }
                    }

                    if (inRiver) continue;

                    float terrainHeight01 = h / heightMultiplier;
                    if (terrainHeight01 <= seaLevel + 0.10f)
                        continue;

                    float rnd = Random.value;

                    if (biomeMap[x, z] == 2)
                    {
                        if (rnd < 0.08f && treePrefabs.Count > 0)
                            Instantiate(treePrefabs[Random.Range(0, treePrefabs.Count)],
                                hit.point + Vector3.up * 0.15f,
                                Quaternion.identity,
                                transform);
                        else if (rnd < 0.22f && grassPrefabs.Count > 0)
                            Instantiate(grassPrefabs[Random.Range(0, grassPrefabs.Count)],
                                hit.point + Vector3.up * 0.1f,
                                Quaternion.identity,
                                transform);
                    }
                    else if (biomeMap[x, z] == 3 && rnd < 0.1f && rockPrefabs.Count > 0)
                    {
                        Instantiate(rockPrefabs[Random.Range(0, rockPrefabs.Count)],
                            hit.point + Vector3.up * 0.12f,
                            Quaternion.identity,
                            transform);
                    }
                }
            }
        }
    }

    void GenerateSeaAndLakesWater()
    {
        for (int x = 0; x <= resolution; x += 3)
        {
            for (int z = 0; z <= resolution; z += 3)
            {
                if (heightMap[x, z] < seaLevel + 0.055f)
                {
                    var water = Instantiate(waterPrefab,
                        new Vector3(x * worldScale, waterLevelY, z * worldScale),
                        Quaternion.identity, transform);

                    if (seaLakeMaterial != null)
                        water.GetComponent<MeshRenderer>().material = seaLakeMaterial;
                }
            }
        }
    }

    void GenerateRiver()
    {
        isRiver = new bool[resolution + 1, resolution + 1];

        int x = Random.Range(resolution / 3, resolution - 50);
        int z = Random.Range(60, resolution - 70);

        for (int i = 0; i < 250; i++)
        {
            if (heightMap[x, z] > 0.57f) break;
            x = Random.Range(resolution / 3, resolution - 40);
            z = Random.Range(60, resolution - 60);
        }

        int steps = 0;
        int lastDirectionChange = 0;

        while (steps < 2800 && heightMap[x, z] > seaLevel + 0.055f)
        {
            steps++;

            for (int w = -1; w <= 1; w++)
            {
                int nx = Mathf.Clamp(x + w, 0, resolution);
                isRiver[nx, z] = true;

                if (heightMap[nx, z] > seaLevel + 0.16f)
                    heightMap[nx, z] = Mathf.Lerp(heightMap[nx, z], heightMap[nx, z] - 0.045f, 0.33f);
            }

            for (int w = -1; w <= 1; w++)
            {
                int nx = Mathf.Clamp(x + w, 0, resolution);

                float riverY = heightMap[nx, z] * heightMultiplier + 0.6f;   

                GameObject segment = Instantiate(waterPrefab,
                    new Vector3(nx * worldScale, riverY, z * worldScale),
                    Quaternion.identity, transform);

                MeshRenderer mr = segment.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Material mat = new Material(mr.material);
                    mat.mainTextureScale = new Vector2(riverTiling * 1.1f, riverTiling * 3.2f);
                    mat.mainTextureOffset = new Vector2(Random.Range(0f, 1f), 0f);
                    mr.material = mat;
                    riverMaterials.Add(mat);
                }
            }

            int bestX = x;
            int bestZ = z;
            float bestH = heightMap[x, z] + 10f;

            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -2; dz <= 3; dz++)
                {
                    if (dx == 0 && dz == 0) continue;

                    int nx = Mathf.Clamp(x + dx, 10, resolution - 10);
                    int nz = Mathf.Clamp(z + dz, 10, resolution - 10);

                    float penalty = dx * 0.20f;           
                    float h = heightMap[nx, nz] + penalty;

                    if (steps - lastDirectionChange > 14 && Random.value < 0.22f)
                    {
                        h += Random.Range(-0.06f, 0.06f);
                        lastDirectionChange = steps;
                    }

                    if (h < bestH)
                    {
                        bestH = h;
                        bestX = nx;
                        bestZ = nz;
                    }
                }
            }

            if (bestX == x && bestZ == z) break;

            x = bestX;
            z = bestZ;
        }
    }

    void Update()
    {
        foreach (Material mat in riverMaterials)
        {
            if (mat == null) continue;
            Vector2 offset = mat.mainTextureOffset;
            offset.x -= riverFlowSpeed * Time.deltaTime;
            mat.mainTextureOffset = offset;
        }
    }
}