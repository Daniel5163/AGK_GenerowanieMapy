using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.VisualScripting;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class ProceduralTerrain : MonoBehaviour
{
    [Header("General Settings")]
    public int resolution = 500;
    public float heightMultiplier = 140f;
    public float worldScale = 1.65f;
    public bool randomSeedOnPlay = true;
    public int seed;

    [Header("Terrain Noise")]
    public int octaves = 8;
    public float persistence = 0.45f;
    public float lacunarity = 2.25f;
    public float baseFrequency = 1.45f;

    [Header("Water")]
    public float seaLevel = 0.26f;

    [Header("River Settings")]
    public float riverFlowSpeed = 2.0f;
    public float riverWidth = 13.0f;
    public float riverDepth = 4.0f;

    [Header("Lake Settings")]
    public int lakeSearchStep = 6;
    public float lakeDepthMultiplier = 0.78f;
    public int guaranteedLakeCount = 1;

    [Header("Mountain & Snow Settings")]
    public float snowHeightThreshold = 0.68f;
    public float snowBlend = 0.85f;
    public Color mountainBaseColor = new Color(0.40f, 0.41f, 0.45f);

    [Header("Materials")]
    public Material seaLakeMaterial;
    public Material riverMaterial;

    [Header("Prefabs - Nature")]
    public List<GameObject> treePrefabs = new List<GameObject>();
    public List<GameObject> grassPrefabs = new List<GameObject>();
    public List<GameObject> rockPrefabs = new List<GameObject>();

    [Header("Village Prefabs")]
    public List<GameObject> housePrefabs = new List<GameObject>();
    public List<GameObject> shopPrefabs = new List<GameObject>();
    public GameObject churchPrefab;
    public GameObject schoolPrefab;
    public GameObject roadPrefab;

    [Header("Village Decorations")]
    public GameObject lampPostPrefab;
    public GameObject benchPrefab;
    public GameObject flowerPrefab;
    public GameObject binPrefab;
    public GameObject fireplugPrefab;
    public GameObject signPrefab;
    public GameObject fencePrefab;

    [Header("Village Settings")]
    public int villageSize = 45;
    public float minFlatness = 0.85f;
    public int numberOfVillages = 3;
    public int minDistanceBetweenVillages = 110;
    public int roadWidth = 4;

    private Mesh mesh;
    private float[,] heightMap;
    private int[,] biomeMap;
    private bool[,] isRiver;
    private bool[,] isRoad;
    private MeshCollider meshCollider;
    private List<Vector2Int> villageCenters = new List<Vector2Int>();
    private List<List<Vector2Int>> roadPaths = new List<List<Vector2Int>>();
    private bool[,] occupiedMap;
    private List<Vector2Int> riverPositions = new List<Vector2Int>();
    private List<GameObject> riverWaterObjects = new List<GameObject>();

    private struct LakeInfo { public int cx, cz, radius; }
    private List<LakeInfo> lakes = new List<LakeInfo>();
    private List<Vector2Int> bridgePoints = new List<Vector2Int>();

    private int villageGridSpacing = 14;

    void Start()
    {
        meshCollider = GetComponent<MeshCollider>();
        if (randomSeedOnPlay) seed = Random.Range(-999999, 999999);
        Generate();
    }

    public void Generate()
    {
        MeshFilter mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        MeshCollider mc = GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();
        meshCollider = mc;
        ClearOldDetails();

        mesh = new Mesh { name = "Procedural Terrain" };
        mf.sharedMesh = mesh;

        heightMap = new float[resolution + 1, resolution + 1];
        biomeMap = new int[resolution + 1, resolution + 1];
        isRiver = new bool[resolution + 1, resolution + 1];
        isRoad = new bool[resolution + 1, resolution + 1];
        occupiedMap = new bool[resolution + 1, resolution + 1];

        riverPositions.Clear();
        villageCenters.Clear();
        roadPaths.Clear();
        riverWaterObjects.Clear();
        lakes.Clear();
        bridgePoints.Clear();

        GenerateHeightMap();
        EnsureMountains();
        GenerateBiomes();
        CreateLakes();

        GenerateRiver();

        villageCenters = FindVillageLocations(numberOfVillages).Take(numberOfVillages).ToList();

        ConnectVillagesWithRoads();

        foreach (Vector2Int center in villageCenters)
        {
            GenerateSingleVillage(center);
        }

        BuildMeshWithSnow();
        mc.sharedMesh = mesh;

        GenerateLakeWater();
        GenerateRiverWaterMesh();
        BuildBridgeMeshes();

        SpawnDetails();
        SpawnVillageDecorations();
        SpawnUrbanFurniture(); 
        SpawnFieldsAndForests();
        SpawnRoadDecorations();
    }

    void GenerateHeightMap()
    {
        float offsetX = seed * 0.00125f;
        float offsetZ = seed * 0.0013f;

        for (int x = 0; x <= resolution; x++)
            for (int z = 0; z <= resolution; z++)
            {
                float nx = (float)x / resolution * baseFrequency + offsetX;
                float nz = (float)z / resolution * baseFrequency + offsetZ;
                float height = 0f, amp = 1f, freq = 1f, maxAmp = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    height += Mathf.PerlinNoise(nx * freq, nz * freq) * amp;
                    maxAmp += amp;
                    amp *= persistence;
                    freq *= lacunarity;
                }

                height /= maxAmp;
                height = Mathf.Pow(height, 1.25f);

                float normalizedX = (float)x / resolution;
                float mountainBias = Mathf.Pow(normalizedX, 2.0f);
                height *= Mathf.Lerp(0.65f, 1.85f, mountainBias);

                if (normalizedX > 0.55f)
                    height += Mathf.PerlinNoise(nx * 8f, nz * 8f) * 0.28f * (normalizedX - 0.55f) * 4.0f;

                heightMap[x, z] = Mathf.Clamp01(height);
            }
    }

    void EnsureMountains()
    {
        int mountainStartX = (int)(resolution * 0.6f);
        float minMountainHeight = 0.75f;

        for (int x = mountainStartX; x <= resolution; x++)
        {
            float progress = (float)(x - mountainStartX) / (resolution - mountainStartX);
            float requiredHeight = Mathf.Lerp(minMountainHeight, 0.90f, progress);

            for (int z = 0; z <= resolution; z++)
                if (heightMap[x, z] < requiredHeight)
                {
                    float boost = requiredHeight - heightMap[x, z];
                    heightMap[x, z] += boost * 0.55f * progress;
                    heightMap[x, z] = Mathf.Clamp01(heightMap[x, z]);
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
                else if (h < 0.74f) biomeMap[x, z] = 3;
                else biomeMap[x, z] = 4;
            }
    }

    void CreateLakes()
    {
        int bestLakeX = resolution / 2, bestLakeZ = resolution / 2;
        float bestScore = float.MaxValue;
        int searchStartX = (int)(resolution * 0.18f);
        int searchEndX = (int)(resolution * 0.62f);

        for (int x = searchStartX; x <= searchEndX; x += 12)
            for (int z = 20; z <= resolution - 20; z += 12)
            {
                float h = heightMap[x, z];
                if (h < seaLevel + 0.12f || h > 0.58f) continue;

                float localAvg = 0f; int cnt = 0;
                for (int dx = -15; dx <= 15; dx += 5)
                    for (int dz = -15; dz <= 15; dz += 5)
                    {
                        localAvg += heightMap[Mathf.Clamp(x + dx, 0, resolution), Mathf.Clamp(z + dz, 0, resolution)];
                        cnt++;
                    }
                localAvg /= cnt;

                float score = h - localAvg * 0.1f;
                if (score < bestScore) { bestScore = score; bestLakeX = x; bestLakeZ = z; }
            }

        CreateSingleLake(bestLakeX, bestLakeZ, 22);

        int lakesCreated = 1;
        int step = Mathf.Max(lakeSearchStep, 24);

        for (int x = step; x < resolution - step && lakesCreated < 3; x += step)
            for (int z = step; z < resolution - step && lakesCreated < 3; z += step)
            {
                float h = heightMap[x, z];
                if (h <= seaLevel + 0.18f || h >= 0.52f) continue;

                bool isLocalMin = true;
                for (int dx = -10; dx <= 10 && isLocalMin; dx++)
                    for (int dz = -10; dz <= 10 && isLocalMin; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        if (heightMap[Mathf.Clamp(x + dx, 0, resolution), Mathf.Clamp(z + dz, 0, resolution)] < h - 0.018f)
                            isLocalMin = false;
                    }

                if (isLocalMin && Vector2Int.Distance(new Vector2Int(x, z), new Vector2Int(bestLakeX, bestLakeZ)) > 80)
                {
                    CreateSingleLake(x, z, Random.Range(14, 20));
                    lakesCreated++;
                }
            }

        SmoothHeightMapGlobal(3);
    }

    private void CreateSingleLake(int cx, int cz, int radius)
    {
        float centerH = heightMap[cx, cz];
        lakes.Add(new LakeInfo { cx = cx, cz = cz, radius = radius });

        for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                int nx = Mathf.Clamp(cx + dx, 0, resolution);
                int nz = Mathf.Clamp(cz + dz, 0, resolution);
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist > radius) continue;
                float t = dist / radius;
                float factor = Mathf.SmoothStep(1f, 0f, t);

                heightMap[nx, nz] = Mathf.Lerp(heightMap[nx, nz], centerH - 0.05f * lakeDepthMultiplier, factor * 0.9f);
            }
    }

    void SmoothHeightMapGlobal(int passes)
    {
        for (int pass = 0; pass < passes; pass++)
        {
            float[,] tmp = new float[resolution + 1, resolution + 1];
            for (int x = 0; x <= resolution; x++)
                for (int z = 0; z <= resolution; z++)
                {
                    float sum = 0f; int cnt = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            sum += heightMap[Mathf.Clamp(x + dx, 0, resolution), Mathf.Clamp(z + dz, 0, resolution)];
                            cnt++;
                        }
                    tmp[x, z] = Mathf.Lerp(heightMap[x, z], sum / cnt, 0.35f);
                }
            heightMap = tmp;
        }
    }

    void GenerateLakeWater()
    {
        if (seaLakeMaterial == null)
        {
            seaLakeMaterial = new Material(Shader.Find("Standard"));
            seaLakeMaterial.color = new Color(0.1f, 0.45f, 0.85f, 0.75f);
        }

        foreach (var lake in lakes)
        {
            float maxH = 0f;
            for (int dx = -lake.radius; dx <= lake.radius; dx++)
                for (int dz = -lake.radius; dz <= lake.radius; dz++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist > lake.radius - 1 && dist <= lake.radius)
                    {
                        int nx = Mathf.Clamp(lake.cx + dx, 0, resolution);
                        int nz = Mathf.Clamp(lake.cz + dz, 0, resolution);
                        if (heightMap[nx, nz] > maxH) maxH = heightMap[nx, nz];
                    }
                }

            float waterY = (maxH - 0.005f) * heightMultiplier;

            int segs = 32;
            float worldRadius = lake.radius * worldScale * 0.98f;
            Vector3 center3 = new Vector3(lake.cx * worldScale, waterY, lake.cz * worldScale);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();

            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s <= segs; s++)
            {
                float angle = s / (float)segs * Mathf.PI * 2f;
                float ex = Mathf.Cos(angle) * worldRadius;
                float ez = Mathf.Sin(angle) * worldRadius;
                verts.Add(new Vector3(ex, 0f, ez));
                uvs.Add(new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f));

                if (s < segs)
                {
                    tris.Add(0);
                    tris.Add(s + 1);
                    tris.Add(s + 2);
                }
            }

            var lakeGO = new GameObject("LakeWater_" + lake.cx + "_" + lake.cz);
            lakeGO.transform.parent = transform;
            lakeGO.transform.position = center3;

            var lm = new Mesh();
            lm.SetVertices(verts);
            lm.SetTriangles(tris, 0);
            lm.SetUVs(0, uvs);
            lm.RecalculateNormals();

            lakeGO.AddComponent<MeshFilter>().sharedMesh = lm;
            lakeGO.AddComponent<MeshRenderer>().material = seaLakeMaterial;
            lakeGO.AddComponent<LakeWaterFloat>().waterY = waterY;
        }
    }

    void GenerateRiver()
    {
        riverPositions.Clear();

        int startX = (int)(resolution * 0.85f);
        int startZ = resolution / 2;
        float highest = 0f;

        for (int z = 50; z < resolution - 50; z += 10)
        {
            if (heightMap[startX, z] > highest)
            {
                highest = heightMap[startX, z];
                startZ = z;
            }
        }

        int curX = startX;
        int curZ = startZ;
        riverPositions.Add(new Vector2Int(curX, curZ));

        for (int step = 0; step < 2000; step++)
        {
            int nextX = curX;
            int nextZ = curZ;
            float minH = heightMap[curX, curZ];

            for (int dx = -2; dx <= 0; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = Mathf.Clamp(curX + dx, 10, resolution - 10);
                    int nz = Mathf.Clamp(curZ + dz, 10, resolution - 10);

                    if (heightMap[nx, nz] < minH)
                    {
                        minH = heightMap[nx, nz];
                        nextX = nx;
                        nextZ = nz;
                    }
                }
            }

            if (nextX == curX && nextZ == curZ)
            {
                nextX = curX - 1;
                nextZ = curZ + Random.Range(-1, 2);
            }

            curX = Mathf.Clamp(nextX, 0, resolution);
            curZ = Mathf.Clamp(nextZ, 0, resolution);

            bool hitLake = false;
            foreach (var lake in lakes)
            {
                float distToLake = Vector2.Distance(new Vector2(curX, curZ), new Vector2(lake.cx, lake.cz));
                if (distToLake <= lake.radius + 1)
                {
                    hitLake = true;
                    break;
                }
            }

            if (hitLake) break;

            riverPositions.Add(new Vector2Int(curX, curZ));
            if (curX <= 5 || heightMap[curX, curZ] <= seaLevel) break;
        }

        int radiusCells = Mathf.CeilToInt((riverWidth / worldScale) * 0.5f);
        float baseDepthNormalized = riverDepth / heightMultiplier;

        for (int i = 0; i < riverPositions.Count; i++)
        {
            Vector2Int centerNode = riverPositions[i];
            float lakeFadeFactor = 1f;

            foreach (var lake in lakes)
            {
                float distToLake = Vector2.Distance(new Vector2(centerNode.x, centerNode.y), new Vector2(lake.cx, lake.cz));
                if (distToLake < lake.radius + 12)
                {
                    float diff = distToLake - lake.radius;
                    lakeFadeFactor = Mathf.Clamp01(diff / 12f);
                }
            }

            float currentDepth = baseDepthNormalized * lakeFadeFactor;

            for (int dx = -radiusCells - 2; dx <= radiusCells + 2; dx++)
            {
                for (int dz = -radiusCells - 2; dz <= radiusCells + 2; dz++)
                {
                    int nx = Mathf.Clamp(centerNode.x + dx, 0, resolution);
                    int nz = Mathf.Clamp(centerNode.y + dz, 0, resolution);

                    float dist = Vector2.Distance(new Vector2(dx, dz), Vector2.zero);
                    if (dist <= radiusCells + 2)
                    {
                        isRiver[nx, nz] = true;

                        float t = Mathf.Clamp01(dist / (radiusCells + 2));
                        float profileFactor = Mathf.SmoothStep(1f, 0f, t);

                        heightMap[nx, nz] -= currentDepth * profileFactor;
                        heightMap[nx, nz] = Mathf.Max(heightMap[nx, nz], 0.01f);
                    }
                }
            }
        }

        SmoothHeightMapGlobal(2);
    }

    void GenerateRiverWaterMesh()
    {
        if (riverPositions.Count < 2) return;

        if (riverMaterial == null)
        {
            riverMaterial = new Material(Shader.Find("Standard"));
            riverMaterial.color = new Color(0.12f, 0.6f, 0.95f, 0.8f);
            riverMaterial.SetFloat("_Mode", 3);
            riverMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            riverMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            riverMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            riverMaterial.renderQueue = 3000;
        }

        GameObject waterContainer = new GameObject("ProceduralRiverWater");
        waterContainer.transform.parent = this.transform;
        riverWaterObjects.Add(waterContainer);

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float halfW = riverWidth * 0.5f;
        float accumulatedLength = 0f;

        for (int i = 0; i < riverPositions.Count; i++)
        {
            Vector2 forwardDir = Vector2.zero;
            if (i == 0) forwardDir = (riverPositions[1] - riverPositions[0]);
            else if (i == riverPositions.Count - 1) forwardDir = (riverPositions[i] - riverPositions[i - 1]);
            else forwardDir = (riverPositions[i + 1] - riverPositions[i - 1]);

            forwardDir.Normalize();

            Vector3 sideDir = new Vector3(-forwardDir.y, 0f, forwardDir.x);
            Vector3 centerWorld = GridToWorld(riverPositions[i].x, riverPositions[i].y);

            float currentWaterSurfaceY = centerWorld.y + (riverDepth * 0.85f);

            Vector3 leftPoint = new Vector3(centerWorld.x, currentWaterSurfaceY, centerWorld.z) - sideDir * halfW;
            Vector3 rightPoint = new Vector3(centerWorld.x, currentWaterSurfaceY, centerWorld.z) + sideDir * halfW;

            verts.Add(leftPoint);
            verts.Add(rightPoint);

            if (i > 0) accumulatedLength += Vector2Int.Distance(riverPositions[i], riverPositions[i - 1]) * worldScale;

            uvs.Add(new Vector2(0f, accumulatedLength / riverWidth));
            uvs.Add(new Vector2(1f, accumulatedLength / riverWidth));

            if (i < riverPositions.Count - 1)
            {
                int currIndex = i * 2;
                int nextIndex = currIndex + 2;

                tris.Add(currIndex);
                tris.Add(currIndex + 1);
                tris.Add(nextIndex);

                tris.Add(currIndex + 1);
                tris.Add(nextIndex + 1);
                tris.Add(nextIndex);
            }
        }

        Mesh riverMesh = new Mesh
        {
            name = "RiverWaterMesh",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        riverMesh.SetVertices(verts);
        riverMesh.SetTriangles(tris, 0);
        riverMesh.SetUVs(0, uvs);
        riverMesh.RecalculateNormals();
        riverMesh.RecalculateBounds();

        waterContainer.AddComponent<MeshFilter>().sharedMesh = riverMesh;
        waterContainer.AddComponent<MeshRenderer>().material = riverMaterial;
        waterContainer.AddComponent<RiverScroll>().scrollSpeed = riverFlowSpeed * 0.05f;
    }

    void BuildBridgeMeshes()
    {
        if (bridgePoints.Count == 0) return;

        var groups = new List<List<Vector2Int>>();
        foreach (var bp in bridgePoints)
        {
            bool added = false;
            foreach (var g in groups)
                if (Vector2Int.Distance(g[0], bp) < 20) { g.Add(bp); added = true; break; }
            if (!added) groups.Add(new List<Vector2Int> { bp });
        }

        var bridgeMat = new Material(Shader.Find("Standard")) { color = new Color(0.45f, 0.42f, 0.40f) };

        foreach (var g in groups)
        {
            if (g.Count < 3) continue; 

            Vector3 sum = Vector3.zero;
            foreach (var p in g) sum += GridToWorld(p.x, p.y);
            Vector3 bridgeCenter = sum / g.Count;

            Vector2Int first = g[0];
            Vector2Int last = g[g.Count - 1];
            float angle = Mathf.Atan2(last.y - first.y, last.x - first.x) * Mathf.Rad2Deg;

            float bridgeLength = (g.Count * worldScale * 0.5f) + 5f;
            float bridgeWidth = (riverWidth * 1.1f);

            var bGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bGO.name = "Bridge_Single";
            bGO.transform.parent = transform;
            bGO.transform.position = bridgeCenter + Vector3.up * (riverDepth * 0.4f);
            bGO.transform.rotation = Quaternion.Euler(0f, -angle, 0f);
            bGO.transform.localScale = new Vector3(bridgeWidth, 2.0f, bridgeLength);
            bGO.GetComponent<MeshRenderer>().material = bridgeMat;
            DestroyImmediate(bGO.GetComponent<BoxCollider>());
        }
    }

    void BuildMeshWithSnow()
    {
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        Color[] colors = new Color[(resolution + 1) * (resolution + 1)];
        int[] triangles = new int[resolution * resolution * 6];

        int i = 0;
        for (int z = 0; z <= resolution; z++)
            for (int x = 0; x <= resolution; x++)
            {
                float h = heightMap[x, z];
                vertices[i] = new Vector3(x * worldScale, h * heightMultiplier, z * worldScale);

                Color baseCol;
                float snowAmount = 0f;

                if (h < seaLevel + 0.04f) baseCol = new Color(0.2f, 0.5f, 0.85f);
                else if (h < seaLevel + 0.09f) baseCol = new Color(0.85f, 0.82f, 0.6f);
                else if (isRiver[x, z]) baseCol = new Color(0.22f, 0.45f, 0.26f);
                else if (h < 0.53f) baseCol = new Color(0.25f, 0.58f, 0.2f);
                else if (h < 0.68f) baseCol = new Color(0.38f, 0.35f, 0.28f);
                else baseCol = mountainBaseColor;

                if (h > snowHeightThreshold)
                    snowAmount = Mathf.Pow((h - snowHeightThreshold) / (1f - snowHeightThreshold), 1.2f);

                float normalizedX = (float)x / resolution;
                float rightBias = Mathf.Clamp01((normalizedX - 0.52f) * 2.5f);
                snowAmount = Mathf.Clamp01(snowAmount + rightBias * 0.65f);

                colors[i] = Color.Lerp(baseCol, Color.white, snowAmount * snowBlend);
                i++;
            }

        int t = 0;
        for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                int idx = z * (resolution + 1) + x;
                triangles[t++] = idx;
                triangles[t++] = idx + resolution + 1;
                triangles[t++] = idx + 1;
                triangles[t++] = idx + 1;
                triangles[t++] = idx + resolution + 1;
                triangles[t++] = idx + resolution + 2;
            }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.colors = colors;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (meshCollider != null) meshCollider.sharedMesh = mesh;
    }

    private List<Vector2Int> FindVillageLocations(int desiredCount)
    {
        var found = new List<Vector2Int>();
        for (int attempt = 0; attempt < 800 && found.Count < desiredCount; attempt++)
        {
            int x = Random.Range(villageSize + 40, resolution - villageSize - 40);
            int z = Random.Range(villageSize + 40, resolution - villageSize - 40);
            if (!IsValidVillageLocation(x, z)) continue;
            if (found.Any(e => Vector2Int.Distance(new Vector2Int(x, z), e) < minDistanceBetweenVillages)) continue;
            found.Add(new Vector2Int(x, z));
        }
        while (found.Count < desiredCount)
        {
            int x = Mathf.Clamp(resolution / 2 + Random.Range(-150, 150), 100, resolution - 100);
            int z = Mathf.Clamp(resolution / 2 + Random.Range(-150, 150), 100, resolution - 100);
            if (heightMap[x, z] >= seaLevel + 0.12f && !isRiver[x, z])
                found.Add(new Vector2Int(x, z));
        }
        return found;
    }

    private bool IsValidVillageLocation(int x, int z)
    {
        if (heightMap[x, z] < seaLevel + 0.12f) return false;
        if (isRiver[x, z]) return false;
        if (CalculateFlatness(x, z, villageSize / 2) < minFlatness) return false;
        if (heightMap[x, z] > 0.62f) return false;
        return true;
    }

    private void GenerateSingleVillage(Vector2Int center)
    {
        int half = villageSize / 2;
        FlattenArea(center.x, center.y, half + 8);

        TryPlaceSpecialBuilding(churchPrefab, center.x - 7, center.y - 7, center);
        TryPlaceSpecialBuilding(schoolPrefab, center.x + 7, center.y + 7, center);
        TryPlaceSpecialBuilding(GetRandomShopPrefab(), center.x, center.y - 4, center);
        TryPlaceSpecialBuilding(GetRandomShopPrefab(), center.x + 4, center.y, center);
        TryPlaceSpecialBuilding(GetRandomShopPrefab(), center.x - 4, center.y, center);

        for (int x = center.x - half + 4; x <= center.x + half - 4; x += 6)
        {
            for (int z = center.y - half + 4; z <= center.y + half - 4; z += 6)
            {
                if (Random.value < 0.70f)
                {
                    if (IsAreaClearForBuilding(x, z, 2, center))
                    {
                        PlaceBuilding(GetRandomHousePrefab(), x, z, Random.Range(0, 4) * 90f, center);
                    }
                }
            }
        }

        for (int z = center.y - half; z <= center.y + half; z += villageGridSpacing)
        {
            for (int x = center.x - half; x <= center.x + half; x++)
            {
                int cx = Mathf.Clamp(x, 0, resolution);
                int cz = Mathf.Clamp(z, 0, resolution);
                if (!occupiedMap[cx, cz] && !isRoad[cx, cz]) MarkRoadCell(cx, cz);
            }
        }

        for (int x = center.x - half; x <= center.x + half; x += villageGridSpacing)
        {
            for (int z = center.y - half; z <= center.y + half; z++)
            {
                if (Mathf.Abs(z - center.y) % villageGridSpacing != 0)
                {
                    int cx = Mathf.Clamp(x, 0, resolution);
                    int cz = Mathf.Clamp(z, 0, resolution);
                    if (!occupiedMap[cx, cz] && !isRoad[cx, cz]) MarkRoadCell(cx, cz);
                }
            }
        }
    }

    private void ConnectVillagesWithRoads()
    {
        if (villageCenters.Count < 2) return;

        int offsetFromCenter = (villageSize / 2) + 2;

        for (int i = 0; i < villageCenters.Count - 1; i++)
        {
            Vector2Int startNode = villageCenters[i];
            Vector2Int endNode = villageCenters[i + 1];

            Vector2 dir = ((Vector2)(endNode - startNode)).normalized;
            Vector2Int modifiedStart = startNode + Vector2Int.RoundToInt(dir * offsetFromCenter);
            Vector2Int modifiedEnd = endNode - Vector2Int.RoundToInt(dir * offsetFromCenter);

            var path = FindPath(modifiedStart, modifiedEnd);
            if (path != null && path.Count > 2)
            {
                roadPaths.Add(path);
                BuildRoadOnPath(path);
            }
        }
    }

    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
    {
        var openSet = new PriorityQueue<Vector2Int, float>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();

        gScore[start] = 0;
        openSet.Enqueue(start, Vector2Int.Distance(start, end));

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (Vector2Int.Distance(current, end) < 6)
            {
                var path = new List<Vector2Int>();
                var node = current;
                while (cameFrom.ContainsKey(node)) { path.Add(node); node = cameFrom[node]; }
                path.Add(start); path.Reverse();
                return path;
            }
            foreach (var nb in GetNeighbors(current))
            {
                if (heightMap[nb.x, nb.y] < seaLevel + 0.08f) continue;

                float riverWeight = isRiver[nb.x, nb.y] ? 12f : 0f;
                float tg = gScore[current] + Vector2Int.Distance(current, nb) + riverWeight;

                if (!gScore.ContainsKey(nb) || tg < gScore[nb])
                {
                    cameFrom[nb] = current;
                    gScore[nb] = tg;
                    openSet.Enqueue(nb, tg + Vector2Int.Distance(nb, end));
                }
            }
        }
        return null;
    }

    private List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        var neighbors = new List<Vector2Int>();
        Vector2Int[] dirs = {
            new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
            new Vector2Int(1,1), new Vector2Int(1,-1), new Vector2Int(-1,1), new Vector2Int(-1,-1)
        };
        foreach (var d in dirs)
        {
            var n = new Vector2Int(pos.x + d.x, pos.y + d.y);
            if (n.x >= 5 && n.x <= resolution - 5 && n.y >= 5 && n.y <= resolution - 5)
                neighbors.Add(n);
        }
        return neighbors;
    }

    private void BuildRoadOnPath(List<Vector2Int> path)
    {
        var smooth = path.Select(p => new Vector2(p.x, p.y)).ToList();
        for (int pass = 0; pass < 6; pass++)
        {
            var tmp = new List<Vector2>(smooth);
            for (int i = 3; i < smooth.Count - 3; i++)
            {
                Vector2 avg = Vector2.zero;
                for (int k = -3; k <= 3; k++) avg += smooth[i + k];
                tmp[i] = avg / 7f;
            }
            smooth = tmp;
        }

        for (int i = 0; i < smooth.Count; i++)
        {
            int iA = Mathf.Max(0, i - 3), iB = Mathf.Min(smooth.Count - 1, i + 3);
            Vector2 flow = (smooth[iB] - smooth[iA]).normalized;
            if (flow.sqrMagnitude < 0.001f) flow = Vector2.right;
            Vector2 perp = new Vector2(-flow.y, flow.x);

            int cx = Mathf.Clamp(Mathf.RoundToInt(smooth[i].x), 0, resolution);
            int cz = Mathf.Clamp(Mathf.RoundToInt(smooth[i].y), 0, resolution);

            float targetH = heightMap[cx, cz];
            bool currentCellIsRiver = isRiver[cx, cz];

            if (currentCellIsRiver)
            {
                int sampleDist = Mathf.Max(6, Mathf.RoundToInt(riverWidth / worldScale) + 3);
                int prevIdx = Mathf.Clamp(cx - (int)(flow.x * sampleDist), 0, resolution);
                int prevIdz = Mathf.Clamp(cz - (int)(flow.y * sampleDist), 0, resolution);
                targetH = heightMap[prevIdx, prevIdz];
            }

            int safetyBuffer = roadWidth + 3;
            for (int w = -safetyBuffer; w <= safetyBuffer; w++)
            {
                int nx = Mathf.Clamp(Mathf.RoundToInt(smooth[i].x + perp.x * w), 0, resolution);
                int nz = Mathf.Clamp(Mathf.RoundToInt(smooth[i].y + perp.y * w), 0, resolution);

                occupiedMap[nx, nz] = true;

                if (Mathf.Abs(w) <= roadWidth)
                {
                    if (isRiver[nx, nz])
                    {
                        if (!bridgePoints.Contains(new Vector2Int(nx, nz)))
                            bridgePoints.Add(new Vector2Int(nx, nz));
                        currentCellIsRiver = true;
                    }

                    MarkRoadCell(nx, nz);

                    if (!isRiver[nx, nz])
                    {
                        float t = Mathf.Abs(w) / (float)roadWidth;
                        heightMap[nx, nz] = Mathf.Lerp(heightMap[nx, nz], targetH, Mathf.Lerp(0.95f, 0.3f, t * t));
                    }
                }
            }

            if (roadPrefab != null && i < smooth.Count - 1 && !currentCellIsRiver)
            {
                Vector3 pos = GridToWorld(cx, cz);
                pos.y += 0.06f;
                Instantiate(roadPrefab, pos, Quaternion.LookRotation(new Vector3(flow.x, 0, flow.y)), transform);
            }
        }
    }

    void SpawnDetails()
    {
        for (int x = 0; x < resolution; x += 8)
        {
            for (int z = 0; z < resolution; z += 8)
            {
                if (heightMap[x, z] < seaLevel + 0.06f) continue;

                if (isRoad[x, z]) continue;

                if (occupiedMap[x, z] || isRiver[x, z]) continue;

                Vector3 pos = GridToWorld(x, z);
                if (biomeMap[x, z] == 2 && treePrefabs.Count > 0 && Random.value < 0.04f)
                    Instantiate(treePrefabs[Random.Range(0, treePrefabs.Count)], pos, Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
                else if (biomeMap[x, z] == 2 && grassPrefabs.Count > 0 && Random.value < 0.1f)
                    Instantiate(grassPrefabs[Random.Range(0, grassPrefabs.Count)], pos, Quaternion.identity, transform);
            }
        }
    }

    void SpawnFieldsAndForests()
    {
        foreach (var center in villageCenters)
            for (int x = center.x - 80; x <= center.x + 80; x += 5)
                for (int z = center.y - 80; z <= center.y + 80; z += 5)
                {
                    if (x < 0 || z < 0 || x > resolution || z > resolution) continue;

                    if (heightMap[x, z] < seaLevel + 0.06f) continue;

                    if (occupiedMap[x, z] || isRoad[x, z] || isRiver[x, z]) continue;
                    float dist = Vector2.Distance(new Vector2(x, z), center);
                    if (dist > villageSize + 5 && dist < villageSize + 40 && grassPrefabs.Count > 0 && Random.value < 0.3f)
                        Instantiate(grassPrefabs[Random.Range(0, grassPrefabs.Count)], GridToWorld(x, z), Quaternion.identity, transform);
                    else if (dist > villageSize + 40 && dist < villageSize + 90 && treePrefabs.Count > 0 && Random.value < 0.08f)
                        Instantiate(treePrefabs[Random.Range(0, treePrefabs.Count)], GridToWorld(x, z), Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
                }
    }

    void SpawnVillageDecorations()
    {
        foreach (var center in villageCenters)
        {
            int half = villageSize / 2;
            for (int x = center.x - half; x <= center.x + half; x++)
                for (int z = center.y - half; z <= center.y + half; z++)
                {
                    if (x < 0 || z < 0 || x > resolution || z > resolution) continue;

                    if (heightMap[x, z] < seaLevel + 0.06f) continue;

                    if (Vector2.Distance(new Vector2(x, z), center) > half) continue;
                    if (isRiver[x, z]) continue;
                    Vector3 pos = GridToWorld(x, z);

                    if (isRoad[x, z] && x % 5 == 0 && z % 5 == 0)
                    {
                        Vector3 decoratePos = pos + new Vector3(1.2f, 0.05f, 1.2f);
                        float choice = Random.value;

                        if (choice < 0.25f && lampPostPrefab != null)
                            Instantiate(lampPostPrefab, decoratePos, Quaternion.identity, transform);
                        else if (choice < 0.45f && benchPrefab != null)
                            Instantiate(benchPrefab, decoratePos, Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0), transform);
                    }
                }
        }
    }

    void SpawnUrbanFurniture()
    {
        foreach (var center in villageCenters)
        {
            int half = villageSize / 2;
            for (int x = center.x - half; x <= center.x + half; x += 2)
            {
                for (int z = center.y - half; z <= center.y + half; z += 2)
                {
                    if (x < 0 || z < 0 || x > resolution || z > resolution) continue;
                    if (isRoad[x, z] || isRiver[x, z]) continue;

                    bool nearRoad = false;
                    Vector3 roadDirection = Vector3.forward;

                    if (x > 0 && isRoad[x - 1, z]) { nearRoad = true; roadDirection = Vector3.left; }
                    else if (x < resolution && isRoad[x + 1, z]) { nearRoad = true; roadDirection = Vector3.right; }
                    else if (z > 0 && isRoad[x, z - 1]) { nearRoad = true; roadDirection = Vector3.back; }
                    else if (z < resolution && isRoad[x, z + 1]) { nearRoad = true; roadDirection = Vector3.forward; }

                    if (nearRoad && !occupiedMap[x, z])
                    {
                        Vector3 pos = GridToWorld(x, z);
                        Quaternion rotationToRoad = Quaternion.LookRotation(roadDirection);
                        float rnd = Random.value;

                        if (rnd < 0.20f && benchPrefab != null)
                        {
                            Instantiate(benchPrefab, pos, rotationToRoad, transform);
                            occupiedMap[x, z] = true;
                        }
                        else if (rnd < 0.35f && binPrefab != null)
                        {
                            Instantiate(binPrefab, pos, Quaternion.identity, transform);
                            occupiedMap[x, z] = true;
                        }
                        else if (rnd < 0.40f && fireplugPrefab != null)
                        {
                            Instantiate(fireplugPrefab, pos, Quaternion.identity, transform);
                            occupiedMap[x, z] = true;
                        }
                        
                       
                    }
                }
            }
        }
    }

    void SpawnRoadDecorations()
    {
        int totalLampsSpawned = 0;

        foreach (var path in roadPaths)
        {
            if (lampPostPrefab == null)
            {
                Debug.LogError("lampPostPrefab NIE JEST PRZYPISANY w Inspectorze!");
                return;
            }

            for (int i = 10; i < path.Count - 10; i += 18)  
            {
                Vector2Int roadPoint = path[i];

                Vector3 roadDirection;
                if (i < path.Count - 1)
                    roadDirection = new Vector3(path[i + 1].x - path[i].x, 0, path[i + 1].y - path[i].y).normalized;
                else
                    roadDirection = new Vector3(path[i].x - path[i - 1].x, 0, path[i].y - path[i - 1].y).normalized;

                Vector3 right = new Vector3(-roadDirection.z, 0, roadDirection.x);

                bool placeOnLeft = Random.value < 0.5f;

                Vector2Int lampPos;
                if (placeOnLeft)
                    lampPos = new Vector2Int(roadPoint.x + Mathf.RoundToInt(-right.x * 4), roadPoint.y + Mathf.RoundToInt(-right.z * 4));
                else
                    lampPos = new Vector2Int(roadPoint.x + Mathf.RoundToInt(right.x * 4), roadPoint.y + Mathf.RoundToInt(right.z * 4));

                if (SpawnLampAtPosition(lampPos, roadPoint, roadDirection))
                {
                    totalLampsSpawned++;
                }
            }
        }
    }

    bool SpawnLampAtPosition(Vector2Int lampPos, Vector2Int roadPoint, Vector3 roadDirection)
    {
        if (lampPos.x < 2 || lampPos.x > resolution - 2 || lampPos.y < 2 || lampPos.y > resolution - 2)
            return false;

        if (isRiver[lampPos.x, lampPos.y])
            return false;

        float terrainHeight = heightMap[lampPos.x, lampPos.y] * heightMultiplier;

        Vector3 worldPos = new Vector3(lampPos.x * worldScale, terrainHeight + 0.8f, lampPos.y * worldScale);

        Vector3 roadWorldPos = new Vector3(roadPoint.x * worldScale, 0, roadPoint.y * worldScale);
        Vector3 lampWorldPos = new Vector3(lampPos.x * worldScale, 0, lampPos.y * worldScale);
        Vector3 directionToRoad = (roadWorldPos - lampWorldPos).normalized;

        Quaternion rotation = Quaternion.LookRotation(directionToRoad);

        GameObject newLamp = Instantiate(lampPostPrefab, worldPos, rotation, transform);

        occupiedMap[lampPos.x, lampPos.y] = true;

        return true;
    }
    void ClearOldDetails()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    private float CalculateFlatness(int cx, int cz, int radius)
    {
        float centerH = heightMap[cx, cz], totalDiff = 0f; int samples = 0;
        for (int x = -radius; x <= radius; x += 5)
            for (int z = -radius; z <= radius; z += 5)
            {
                totalDiff += Mathf.Abs(heightMap[Mathf.Clamp(cx + x, 0, resolution), Mathf.Clamp(cz + z, 0, resolution)] - centerH);
                samples++;
            }
        return 1f - Mathf.Clamp01((totalDiff / samples) * 8f);
    }

    private void FlattenArea(int cx, int cz, int radius)
    {
        float targetH = heightMap[cx, cz];
        for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                int nx = Mathf.Clamp(cx + x, 0, resolution), nz = Mathf.Clamp(cz + z, 0, resolution);
                float t = Mathf.Clamp01(Vector2.Distance(new Vector2(x, z), Vector2.zero) / radius);
                heightMap[nx, nz] = Mathf.Lerp(heightMap[nx, nz], targetH, (1f - Mathf.SmoothStep(0f, 1f, t)) * 0.95f);
            }
    }

    private bool IsAreaClearForBuilding(int gx, int gz, int radius, Vector2Int vc)
    {
        int roadSafetyMargin = 2;

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                int nx = Mathf.Clamp(gx + x, 0, resolution);
                int nz = Mathf.Clamp(gz + z, 0, resolution);

                if (occupiedMap[nx, nz] || isRiver[nx, nz] || isRoad[nx, nz])
                    return false;

                int distFromCenterX = Mathf.Abs(nx - vc.x);
                int distFromCenterZ = Mathf.Abs(nz - vc.y);

                if (distFromCenterX % villageGridSpacing < roadSafetyMargin ||
                    distFromCenterX % villageGridSpacing > villageGridSpacing - roadSafetyMargin)
                    return false;

                if (distFromCenterZ % villageGridSpacing < roadSafetyMargin ||
                    distFromCenterZ % villageGridSpacing > villageGridSpacing - roadSafetyMargin)
                    return false;
            }
        }
        return true;
    }

    private void MarkAreaAsOccupied(int gx, int gz, int radius)
    {
        for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                int nx = Mathf.Clamp(gx + x, 0, resolution), nz = Mathf.Clamp(gz + z, 0, resolution);
                occupiedMap[nx, nz] = true;
            }
    }

    private void TryPlaceSpecialBuilding(GameObject prefab, int x, int z, Vector2Int vc)
    {
        if (prefab == null) return;
        if (IsAreaClearForBuilding(x, z, 4, vc))
        {
            PlaceBuilding(prefab, x, z, Random.Range(0, 4) * 90f, vc);
            MarkAreaAsOccupied(x, z, 4);
        }
    }

    private void PlaceBuilding(GameObject prefab, int gx, int gz, float rotY, Vector2Int vc)
    {
        if (prefab == null) return;
        Vector3 pos = GridToWorld(gx, gz); pos.y += 0.05f;
        Instantiate(prefab, pos, Quaternion.Euler(0, rotY, 0), transform);
        MarkAreaAsOccupied(gx, gz, 2);
    }

    private void MarkRoadCell(int x, int z)
    {
        int cx = Mathf.Clamp(x, 0, resolution), cz = Mathf.Clamp(z, 0, resolution);
        isRoad[cx, cz] = true;
    }

    private Vector3 GridToWorld(int gx, int gz)
    {
        float y = heightMap[Mathf.Clamp(gx, 0, resolution), Mathf.Clamp(gz, 0, resolution)] * heightMultiplier;
        return new Vector3(gx * worldScale, y, gz * worldScale);
    }

    private GameObject GetRandomHousePrefab() => housePrefabs.Count > 0 ? housePrefabs[Random.Range(0, housePrefabs.Count)] : null;
    private GameObject GetRandomShopPrefab() => shopPrefabs.Count > 0 ? shopPrefabs[Random.Range(0, shopPrefabs.Count)] : null;
}

public class LakeWaterFloat : MonoBehaviour
{
    public float waterY;
}

public class PriorityQueue<TElement, TPriority> where TPriority : System.IComparable<TPriority>
{
    private List<(TElement Element, TPriority Priority)> elements = new List<(TElement, TPriority)>();

    public int Count
    {
        get { return elements.Count; }
    }

    public void Enqueue(TElement element, TPriority priority)
    {
        elements.Add((element, priority));
        int ci = elements.Count - 1;
        while (ci > 0)
        {
            int pi = (ci - 1) / 2;
            if (elements[ci].Priority.CompareTo(elements[pi].Priority) >= 0) break;
            (elements[ci], elements[pi]) = (elements[pi], elements[ci]);
            ci = pi;
        }
    }

    public TElement Dequeue()
    {
        if (elements.Count == 0) throw new System.IndexOutOfRangeException("Queue is empty");
        var front = elements[0];
        elements[0] = elements[elements.Count - 1];
        elements.RemoveAt(elements.Count - 1);
        int pi = 0;
        while (true)
        {
            int ci = pi * 2 + 1;
            if (ci >= elements.Count) break;
            int rc = ci + 1;
            if (rc < elements.Count && elements[rc].Priority.CompareTo(elements[ci].Priority) < 0) ci = rc;
            if (elements[pi].Priority.CompareTo(elements[ci].Priority) <= 0) break;
            (elements[pi], elements[ci]) = (elements[ci], elements[pi]);
            pi = ci;
        }
        return front.Element;
    }
}