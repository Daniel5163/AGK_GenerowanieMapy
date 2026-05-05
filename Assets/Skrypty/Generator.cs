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

    [Header("Prefabs - Nature")]
    public List<GameObject> treePrefabs = new List<GameObject>();
    public List<GameObject> grassPrefabs = new List<GameObject>();
    public List<GameObject> rockPrefabs = new List<GameObject>();
    public GameObject waterPrefab;

    [Header("Village Prefabs")]
    public List<GameObject> housePrefabs = new List<GameObject>();
    public List<GameObject> shopPrefabs = new List<GameObject>();
    public GameObject churchPrefab;
    public GameObject schoolPrefab;

    public GameObject roadPrefab;

    [Header("Village Decorations")]
    public GameObject polePrefab;           
    public GameObject streetLightPrefab;    

    [Header("Village Settings")]
    public int villageSize = 22;
    public float minFlatness = 0.96f;
    public float villageMinHeight = 0.28f;
    public int maxVillageAttempts = 30;

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
        mesh = new Mesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        heightMap = new float[resolution + 1, resolution + 1];
        biomeMap = new int[resolution + 1, resolution + 1];
        isRiver = new bool[resolution + 1, resolution + 1]; 

        GenerateHeightMap();
        GenerateBiomes();
        CreateLakes();

        GenerateRiver();     
        GenerateVillage();   

        BuildMesh();
        UpdateCollider();    

        SpawnDetails();

        GenerateSeaAndLakesWater();
    }

    private Vector2Int? FindVillageLocation()
    {
        int step = 4;
        float bestFlatness = -1f;
        Vector2Int bestPos = new Vector2Int(-1, -1);

        for (int x = villageSize + 10; x <= resolution - villageSize - 10; x += step)
        {
            for (int z = villageSize + 10; z <= resolution - villageSize - 10; z += step)
            {
                if (heightMap[x, z] < villageMinHeight || isRiver[x, z]) continue;

                float flatness = CalculateFlatness(x, z, villageSize / 2 + 2);

                if (flatness > bestFlatness)
                {
                    bestFlatness = flatness;
                    bestPos = new Vector2Int(x, z);
                }
            }
        }

        if (bestPos.x == -1)
        {
            return null;
        }

        if (bestFlatness < minFlatness)
        {
        }

        return bestPos;
    }

    private void GenerateVillage()
    {
        Vector2Int? loc = FindVillageLocation();
        if (loc == null) return;

        Vector2Int center = loc.Value;
        int half = villageSize / 2;

        FlattenArea(center.x, center.y, half + 5);

        int gridSpacing = 15; 

        for (int z = center.y - half; z <= center.y + half; z += gridSpacing)
        {
            for (int x = center.x - half; x <= center.x + half; x++)
                MarkAndSpawnRoad(x, z);
        }

        for (int x = center.x - half; x <= center.x + half; x += gridSpacing)
        {
            for (int z = center.y - half; z <= center.y + half; z++)
            {
                if (Mathf.Abs(z - center.y) % gridSpacing != 0)
                    MarkAndSpawnRoad(x, z);
            }
        }

        TryPlaceSpecialBuilding(churchPrefab, center.x - 6, center.y - 6);
        TryPlaceSpecialBuilding(schoolPrefab, center.x + 6, center.y + 6);

        for (int x = center.x - half + 2; x <= center.x + half - 2; x += 3)
        {
            for (int z = center.y - half + 2; z <= center.y + half - 2; z += 3)
            {
                if (IsAreaClearForBuilding(x, z, 1))
                {
                    if (Random.value < 0.7f) 
                    {
                        GameObject prefab = GetRandomBuildingPrefab();
                        if (prefab != null)
                        {
                            float[] rots = { 0, 90, 180, 270 };
                            PlaceBuilding(prefab, x, z, rots[Random.Range(0, rots.Length)]);

                            MarkAreaAsOccupied(x, z, 3);
                        }
                    }
                }
            }
        }
    }

    private GameObject GetRandomBuildingPrefab()
    {
        bool hasHouses = housePrefabs != null && housePrefabs.Count > 0;
        bool hasShops = shopPrefabs != null && shopPrefabs.Count > 0;

        if (!hasHouses && !hasShops) return null;

        if (Random.value < 0.8f && hasHouses)
        {
            return housePrefabs[Random.Range(0, housePrefabs.Count)];
        }
        else if (hasShops)
        {
            return shopPrefabs[Random.Range(0, shopPrefabs.Count)];
        }
        else if (hasHouses)
        {
            return housePrefabs[Random.Range(0, housePrefabs.Count)];
        }

        return null;
    }

    private bool IsAreaClearForBuilding(int gridX, int gridZ, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                int nx = Mathf.Clamp(gridX + x, 0, resolution);
                int nz = Mathf.Clamp(gridZ + z, 0, resolution);
                if (isRiver[nx, nz]) return false; 
            }
        }
        return true;
    }

    private void MarkAreaAsOccupied(int gridX, int gridZ, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                int nx = Mathf.Clamp(gridX + x, 0, resolution);
                int nz = Mathf.Clamp(gridZ + z, 0, resolution);
                isRiver[nx, nz] = true;
            }
        }
    }

    private void TryPlaceSpecialBuilding(GameObject prefab, int x, int z)
    {
        if (prefab == null) return;

        if (IsAreaClearForBuilding(x, z, 5))
        {
            PlaceBuilding(prefab, x, z, Random.Range(0, 4) * 90);
            MarkAreaAsOccupied(x, z, 5);
        }
    }

    private void MarkAndSpawnRoad(int x, int z)
    {
        int cx = Mathf.Clamp(x, 0, resolution);
        int cz = Mathf.Clamp(z, 0, resolution);

        int roadClearance = 2;
        for (int ix = -roadClearance; ix <= roadClearance; ix++)
        {
            for (int iz = -roadClearance; iz <= roadClearance; iz++)
            {
                int nx = Mathf.Clamp(cx + ix, 0, resolution);
                int nz = Mathf.Clamp(cz + iz, 0, resolution);
                isRiver[nx, nz] = true;
            }
        }

        if (roadPrefab != null)
        {
            Vector3 pos = GridToWorld(cx, cz);
            pos.y += 0.08f;
            GameObject r = Instantiate(roadPrefab, pos, Quaternion.identity, transform);
            r.name = "Road_Segment"; 
        }
    }

    private void PlaceBuilding(GameObject prefab, int gridX, int gridZ, float rotationY)
    {
        if (prefab == null) return;
        Vector3 pos = GridToWorld(gridX, gridZ);
        pos.y += 0.05f;
        Instantiate(prefab, pos, Quaternion.Euler(0, rotationY, 0), transform);
    }

    private float CalculateFlatness(int centerX, int centerZ, int radius)
    {
        float centerH = heightMap[centerX, centerZ];
        float totalDiff = 0f;
        int samples = 0;

        for (int x = -radius; x <= radius; x += 2)
        {
            for (int z = -radius; z <= radius; z += 2)
            {
                int nx = Mathf.Clamp(centerX + x, 0, resolution);
                int nz = Mathf.Clamp(centerZ + z, 0, resolution);

                if (isRiver[nx, nz]) return 0f;

                totalDiff += Mathf.Abs(heightMap[nx, nz] - centerH);
                samples++;
            }
        }

        float averageDiff = totalDiff / samples;
        return Mathf.Clamp01(1f - (averageDiff * 5f));
    }

    private void FlattenArea(int centerX, int centerZ, int radius)
    {
        float targetHeight = heightMap[centerX, centerZ];
        int smoothMargin = 15;
        int totalRadius = radius + smoothMargin;

        for (int x = -totalRadius; x <= totalRadius; x++)
        {
            for (int z = -totalRadius; z <= totalRadius; z++)
            {
                int nx = Mathf.Clamp(centerX + x, 0, resolution);
                int nz = Mathf.Clamp(centerZ + z, 0, resolution);

                float dist = Vector2.Distance(new Vector2(x, z), Vector2.zero);

                if (dist <= radius)
                {
                    heightMap[nx, nz] = Mathf.Lerp(heightMap[nx, nz], targetHeight, 0.95f);
                }
                else if (dist <= totalRadius)
                {
                    float lerpFactor = 1f - ((dist - radius) / smoothMargin);
                    heightMap[nx, nz] = Mathf.Lerp(heightMap[nx, nz], targetHeight, lerpFactor * 0.95f);
                }
            }
        }
    }

    private Vector3 GridToWorld(int gridX, int gridZ)
    {
        float y = heightMap[Mathf.Clamp(gridX, 0, resolution), Mathf.Clamp(gridZ, 0, resolution)] * heightMultiplier + 0.2f;
        return new Vector3(gridX * worldScale, y, gridZ * worldScale);
    }

    private void PlaceDecoration(GameObject prefab, int gridX, int gridZ)
    {
        if (prefab == null) return;
        Vector3 pos = GridToWorld(gridX, gridZ);
        Instantiate(prefab, pos, Quaternion.identity, transform);
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
        for (int x = 0; x < resolution; x += 3)
        {
            for (int z = 0; z < resolution; z += 3)
            {
                if (isRiver[x, z]) continue;

                if (biomeMap[x, z] == 0 || heightMap[x, z] < seaLevel + 0.05f) continue;

                Vector3 origin = new Vector3(x * worldScale, heightMultiplier + 200f, z * worldScale);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 500f))
                {
                    if (hit.collider.name.Contains("Road") || hit.collider.name.Contains("Water")) continue;

                    float h = hit.point.y;
                    float rnd = Random.value;

                    if (biomeMap[x, z] == 2)
                    {
                        if (rnd < 0.07f && treePrefabs.Count > 0)
                            Instantiate(treePrefabs[Random.Range(0, treePrefabs.Count)], hit.point, Quaternion.identity, transform);
                        else if (rnd < 0.15f && grassPrefabs.Count > 0)
                            Instantiate(grassPrefabs[Random.Range(0, grassPrefabs.Count)], hit.point, Quaternion.identity, transform);
                    }
                    else if (biomeMap[x, z] == 3 && rnd < 0.08f && rockPrefabs.Count > 0)
                    {
                        Instantiate(rockPrefabs[Random.Range(0, rockPrefabs.Count)], hit.point, Quaternion.identity, transform);
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
                    var water = Instantiate(waterPrefab, new Vector3(x * worldScale, waterLevelY, z * worldScale), Quaternion.identity, transform);
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
                GameObject segment = Instantiate(waterPrefab, new Vector3(nx * worldScale, riverY, z * worldScale), Quaternion.identity, transform);

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