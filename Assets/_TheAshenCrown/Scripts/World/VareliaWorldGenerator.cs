using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class VareliaWorldGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int mapSize = 2000;
    public int heightMapResolution = 513;
    public int terrainHeight = 220;
    public int seed = 12345;

    [Header("Vegetation")]
    public GameObject treePrefab;
    public GameObject rockPrefab;
    public int treeCount = 750;
    public int rockCount = 220;

    [Header("3D Grass Settings")]
    public bool enableDetailGrass = true;
    public int grassDensity = 6;
    public float detailDistance = 75f;

    [Header("Buildings")]
    public GameObject housePrefab;
    public GameObject fortPrefab;
    public GameObject ruinPrefab;

    [Header("Generation")]
    public bool generateOnStart = false;

    private Terrain terrain;
    private Transform worldRoot;
    private System.Random random;

    // Medieval Materials Cache
    private Material woodBarkMat;
    private Material leafMat;
    private Material rockMat;
    private Material houseWallMat;
    private Material houseRoofMat;
    private Material fortStoneMat;

    // Important locations in Varelia
    private Vector2 village = new Vector2(0, 0);
    private Vector2 fort = new Vector2(650, 300);
    private Vector2 ruins = new Vector2(-650, -450);
    private Vector2 banditCamp = new Vector2(300, -650);

    private Vector2[] riverPoints =
    {
        new Vector2(-1000, 500),
        new Vector2(-650, 250),
        new Vector2(-300, 100),
        new Vector2(0, -100),
        new Vector2(350, -250),
        new Vector2(650, -500),
        new Vector2(1000, -700)
    };

    private void Start()
    {
        if (generateOnStart)
            GenerateWorld();
    }

    [ContextMenu("Generate Varelia World")]
    public void GenerateWorld()
    {
        random = new System.Random(seed);
        InitMedievalMaterials();

        if (worldRoot != null)
        {
            DestroyImmediate(worldRoot.gameObject);
        }
        else
        {
            GameObject existing = GameObject.Find("VARELIA_GENERATED_WORLD");
            if (existing != null)
            {
                DestroyImmediate(existing);
            }
        }

        Terrain[] existingTerrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (Terrain t in existingTerrains)
        {
            if (t.transform.parent == null)
            {
                DestroyImmediate(t.gameObject);
            }
        }

        worldRoot = new GameObject("VARELIA_GENERATED_WORLD").transform;

        GenerateTerrain();
        SetupTerrainTexturesAndSplatmap();

        if (enableDetailGrass)
        {
            Generate3DWavingGrass();
        }

        GenerateRiver();
        GenerateRoads();
        GenerateSettlements();
        GenerateVegetation();
        GenerateLandmarks();
        PositionPlayerAtVillage();

        Debug.Log("Varelia living medieval world generation completed!");
    }

    private void InitMedievalMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        woodBarkMat = new Material(litShader) { name = "Mat_WoodBark" };
        woodBarkMat.color = new Color(0.24f, 0.15f, 0.08f);

        leafMat = new Material(litShader) { name = "Mat_ForestLeaves" };
        leafMat.color = new Color(0.14f, 0.35f, 0.14f);

        rockMat = new Material(litShader) { name = "Mat_GraniteRock" };
        rockMat.color = new Color(0.38f, 0.39f, 0.40f);

        houseWallMat = new Material(litShader) { name = "Mat_HouseTimber" };
        houseWallMat.color = new Color(0.55f, 0.50f, 0.44f);

        houseRoofMat = new Material(litShader) { name = "Mat_ThatchRoof" };
        houseRoofMat.color = new Color(0.38f, 0.24f, 0.15f);

        fortStoneMat = new Material(litShader) { name = "Mat_FortressStone" };
        fortStoneMat.color = new Color(0.32f, 0.33f, 0.35f);
    }

    void GenerateTerrain()
    {
        TerrainData data = new TerrainData();

        data.heightmapResolution = heightMapResolution;
        data.size = new Vector3(mapSize, terrainHeight, mapSize);

        float[,] heights = new float[
            heightMapResolution,
            heightMapResolution
        ];

        for (int z = 0; z < heightMapResolution; z++)
        {
            for (int x = 0; x < heightMapResolution; x++)
            {
                float worldX = (float)x / (heightMapResolution - 1)
                    * mapSize - mapSize / 2f;

                float worldZ = (float)z / (heightMapResolution - 1)
                    * mapSize - mapSize / 2f;

                float noise = Mathf.PerlinNoise(
                    (worldX + seed) * 0.002f,
                    (worldZ + seed) * 0.002f
                );

                float detail = Mathf.PerlinNoise(
                    (worldX + seed) * 0.008f,
                    (worldZ + seed) * 0.008f
                );

                float mountainDistance = Mathf.Max(
                    Mathf.Abs(worldX),
                    Mathf.Abs(worldZ)
                );

                float mountains = Mathf.InverseLerp(
                    650f, 1000f, mountainDistance
                );

                float height =
                    0.09f +
                    noise * 0.15f +
                    detail * 0.035f +
                    mountains * 0.35f;

                height = Flatten(height, worldX, worldZ, village, 170f, 0.12f);
                height = Flatten(height, worldX, worldZ, fort, 110f, 0.19f);
                height = Flatten(height, worldX, worldZ, ruins, 65f, 0.14f);
                height = Flatten(height, worldX, worldZ, banditCamp, 60f, 0.12f);

                float riverDistance = DistanceToRiver(worldX, worldZ);
                float riverCarve = Mathf.Exp(-(riverDistance * riverDistance) / 1800f);
                height -= riverCarve * 0.055f;

                heights[z, x] = Mathf.Clamp01(height);
            }
        }

        data.SetHeights(0, 0, heights);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Varelia_Terrain";
        terrainObject.transform.position = new Vector3(-mapSize / 2f, 0, -mapSize / 2f);
        terrainObject.transform.SetParent(worldRoot);

        terrain = terrainObject.GetComponent<Terrain>();
        terrain.heightmapPixelError = 6;
        terrain.basemapDistance = 800;
        terrain.detailObjectDistance = detailDistance;
        terrain.detailObjectDensity = 1.0f;
    }

    void SetupTerrainTexturesAndSplatmap()
    {
#if UNITY_EDITOR
        TerrainData tData = terrain.terrainData;

        Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Albedo.tif");
        Texture2D grassNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Normal.tif");

        Texture2D dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Albedo.tif");
        Texture2D dirtNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Normal.tif");

        Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Albedo.png");
        Texture2D rockNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Normal.png");

        TerrainLayer grassLayer = new TerrainLayer
        {
            name = "Layer_MeadowGrass",
            diffuseTexture = grassTex,
            normalMapTexture = grassNormal,
            tileSize = new Vector2(8f, 8f)
        };

        TerrainLayer dirtLayer = new TerrainLayer
        {
            name = "Layer_DirtRoad",
            diffuseTexture = dirtTex,
            normalMapTexture = dirtNormal,
            tileSize = new Vector2(10f, 10f)
        };

        TerrainLayer rockLayer = new TerrainLayer
        {
            name = "Layer_MountainRock",
            diffuseTexture = rockTex,
            normalMapTexture = rockNormal,
            tileSize = new Vector2(16f, 16f)
        };

        tData.terrainLayers = new TerrainLayer[] { grassLayer, dirtLayer, rockLayer };

        int alphaW = tData.alphamapWidth;
        int alphaH = tData.alphamapHeight;
        float[,,] splat = new float[alphaH, alphaW, 3];

        for (int z = 0; z < alphaH; z++)
        {
            for (int x = 0; x < alphaW; x++)
            {
                float normX = (float)x / (alphaW - 1);
                float normZ = (float)z / (alphaH - 1);

                float worldX = normX * mapSize - mapSize / 2f;
                float worldZ = normZ * mapSize - mapSize / 2f;

                float steepness = tData.GetSteepness(normX, normZ);
                float roadDist = DistanceToRoad(new Vector2(worldX, worldZ));
                float riverDist = DistanceToRiver(worldX, worldZ);

                float grassWeight = 1f;
                float dirtWeight = 0f;
                float rockWeight = 0f;

                // 1. Mountain cliffs get rugged stone
                if (steepness > 26f)
                {
                    rockWeight = Mathf.InverseLerp(24f, 40f, steepness);
                    grassWeight = 1f - rockWeight;
                }

                // 2. Roads and riverbanks get packed dirt
                if (roadDist < 9f)
                {
                    dirtWeight = Mathf.InverseLerp(9f, 2f, roadDist);
                    grassWeight *= (1f - dirtWeight);
                    rockWeight *= (1f - dirtWeight);
                }
                else if (riverDist < 26f)
                {
                    dirtWeight = Mathf.InverseLerp(26f, 10f, riverDist) * 0.85f;
                    grassWeight *= (1f - dirtWeight);
                    rockWeight *= (1f - dirtWeight);
                }

                splat[z, x, 0] = grassWeight;
                splat[z, x, 1] = dirtWeight;
                splat[z, x, 2] = rockWeight;
            }
        }

        tData.SetAlphamaps(0, 0, splat);
#endif
    }

    void Generate3DWavingGrass()
    {
#if UNITY_EDITOR
        TerrainData tData = terrain.terrainData;

        Texture2D grassDetailTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Tile_Grass.png");
        Texture2D flowerDetailTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Tile_GrassFlowers.png");

        if (grassDetailTex == null) return;

        DetailPrototype grassProto = new DetailPrototype
        {
            prototypeTexture = grassDetailTex,
            renderMode = DetailRenderMode.GrassBillboard,
            minWidth = 1.0f,
            maxWidth = 1.6f,
            minHeight = 0.8f,
            maxHeight = 1.4f,
            healthyColor = new Color(0.25f, 0.55f, 0.22f),
            dryColor = new Color(0.48f, 0.44f, 0.24f)
        };

        DetailPrototype flowerProto = new DetailPrototype
        {
            prototypeTexture = flowerDetailTex != null ? flowerDetailTex : grassDetailTex,
            renderMode = DetailRenderMode.GrassBillboard,
            minWidth = 0.9f,
            maxWidth = 1.4f,
            minHeight = 0.7f,
            maxHeight = 1.2f,
            healthyColor = new Color(0.35f, 0.65f, 0.32f),
            dryColor = new Color(0.55f, 0.50f, 0.30f)
        };

        tData.detailPrototypes = new DetailPrototype[] { grassProto, flowerProto };

        // Wind sway
        tData.wavingGrassStrength = 0.38f;
        tData.wavingGrassAmount = 0.35f;
        tData.wavingGrassSpeed = 0.28f;
        tData.wavingGrassTint = new Color(0.7f, 0.65f, 0.45f);

        int detailRes = tData.detailResolution;
        int[,] grassMap = new int[detailRes, detailRes];
        int[,] flowerMap = new int[detailRes, detailRes];

        for (int z = 0; z < detailRes; z++)
        {
            for (int x = 0; x < detailRes; x++)
            {
                float normX = (float)x / (detailRes - 1);
                float normZ = (float)z / (detailRes - 1);

                float worldX = normX * mapSize - mapSize / 2f;
                float worldZ = normZ * mapSize - mapSize / 2f;

                float steepness = tData.GetSteepness(normX, normZ);
                float roadDist = DistanceToRoad(new Vector2(worldX, worldZ));
                float riverDist = DistanceToRiver(worldX, worldZ);

                // No grass on roads, in the river channel, or on cliffs
                if (roadDist < 7f || riverDist < 20f || steepness > 22f)
                {
                    grassMap[z, x] = 0;
                    flowerMap[z, x] = 0;
                    continue;
                }

                // Noise-based natural meadow grass clumps
                float noise = Mathf.PerlinNoise(worldX * 0.025f, worldZ * 0.025f);
                if (noise > 0.45f)
                {
                    grassMap[z, x] = Mathf.RoundToInt(Mathf.Lerp(2, grassDensity, (noise - 0.45f) * 2f));
                    if (noise > 0.68f)
                    {
                        flowerMap[z, x] = Mathf.RoundToInt(Random.Range(1, 3));
                    }
                }
            }
        }

        tData.SetDetailLayer(0, 0, 0, grassMap);
        tData.SetDetailLayer(0, 0, 1, flowerMap);
#endif
    }

    float Flatten(float original, float x, float z, Vector2 center, float radius, float targetHeight)
    {
        float distance = Vector2.Distance(new Vector2(x, z), center);
        float blend = 1f - Mathf.SmoothStep(radius * 0.65f, radius, distance);
        return Mathf.Lerp(original, targetHeight, blend);
    }

    float DistanceToRiver(float x, float z)
    {
        float minimum = float.MaxValue;
        foreach (Vector2 point in riverPoints)
        {
            float distance = Vector2.Distance(new Vector2(x, z), point);
            minimum = Mathf.Min(minimum, distance);
        }
        return minimum;
    }

    float GetGroundHeight(float x, float z)
    {
        Vector3 worldPosition = new Vector3(x, 0, z);
        return terrain.SampleHeight(worldPosition);
    }

    void GenerateRiver()
    {
        CreateLine("River", riverPoints, 22f, new Color(0.18f, 0.42f, 0.65f));
    }

    void GenerateRoads()
    {
        Vector2[] mainRoad =
        {
            new Vector2(-950, -850),
            new Vector2(-600, -500),
            new Vector2(-250, -200),
            village,
            new Vector2(300, 100),
            fort
        };

        CreateLine("Main_Road", mainRoad, 9f, new Color(0.36f, 0.28f, 0.18f));

        Vector2[] forestRoad =
        {
            village,
            new Vector2(-250, 250),
            ruins
        };

        CreateLine("Forest_Road", forestRoad, 7f, new Color(0.40f, 0.32f, 0.20f));

        Vector2[] campRoad =
        {
            village,
            new Vector2(150, -350),
            banditCamp
        };

        CreateLine("Bandit_Road", campRoad, 6f, new Color(0.35f, 0.26f, 0.17f));
    }

    void CreateLine(string objectName, Vector2[] points, float width, Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(worldRoot);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.positionCount = points.Length;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        line.material = new Material(shader);

        line.startColor = color;
        line.endColor = color;

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 position = new Vector3(
                points[i].x,
                GetGroundHeight(points[i].x, points[i].y) + 0.6f,
                points[i].y
            );

            line.SetPosition(i, position);
        }
    }

    void GenerateSettlements()
    {
        CreateVillage(village);
        CreateFort(fort);
    }

    void CreateVillage(Vector2 center)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            Vector2 position = center + new Vector2(Mathf.Cos(angle) * 65f, Mathf.Sin(angle) * 65f);
            CreateBuilding("Village_House_" + i, position, housePrefab, new Vector3(12, 8, 12), houseWallMat, houseRoofMat);
        }

        CreateBuilding("Village_Center_Hall", center, housePrefab, new Vector3(20, 12, 18), houseWallMat, houseRoofMat);
    }

    void CreateFort(Vector2 center)
    {
        CreateBuilding("Stonewatch_Fort", center, fortPrefab, new Vector3(70, 32, 70), fortStoneMat, null);

        for (int i = 0; i < 4; i++)
        {
            Vector2 position = center + new Vector2(i % 2 == 0 ? -45 : 45, i < 2 ? -45 : 45);
            CreateBuilding("Fort_Tower_" + i, position, null, new Vector3(12, 34, 12), fortStoneMat, null);
        }
    }

    void GenerateLandmarks()
    {
        CreateBuilding("Abandoned_Ruins", ruins, ruinPrefab, new Vector3(45, 14, 45), fortStoneMat, null);
        CreateBuilding("Bandit_Camp", banditCamp, null, new Vector3(26, 7, 26), woodBarkMat, null);
    }

    void GenerateVegetation()
    {
        for (int i = 0; i < treeCount; i++)
        {
            float x = RandomCoordinate();
            float z = RandomCoordinate();

            Vector2 position = new Vector2(x, z);

            if (Vector2.Distance(position, village) < 180f) continue;
            if (Vector2.Distance(position, fort) < 120f) continue;
            if (Vector2.Distance(position, ruins) < 85f) continue;
            if (Vector2.Distance(position, banditCamp) < 85f) continue;
            if (DistanceToRiver(x, z) < 35f) continue;
            if (DistanceToRoad(position) < 16f) continue;

            float steepness = terrain.terrainData.GetSteepness(
                (x + mapSize / 2f) / mapSize,
                (z + mapSize / 2f) / mapSize
            );

            if (steepness > 28f) continue;

            CreateTree(x, z);
        }

        for (int i = 0; i < rockCount; i++)
        {
            float x = RandomCoordinate();
            float z = RandomCoordinate();

            if (DistanceToRiver(x, z) < 30f) continue;

            CreateRock(x, z);
        }
    }

    float RandomCoordinate()
    {
        return (float)random.NextDouble() * mapSize - mapSize / 2f;
    }

    float DistanceToRoad(Vector2 point)
    {
        return Mathf.Min(
            DistanceToPolyline(point, new Vector2[]
            {
                new Vector2(-950, -850),
                new Vector2(-600, -500),
                new Vector2(-250, -200),
                village,
                new Vector2(300, 100),
                fort
            }),
            DistanceToPolyline(point, new Vector2[]
            {
                village,
                new Vector2(-250, 250),
                ruins
            }),
            DistanceToPolyline(point, new Vector2[]
            {
                village,
                new Vector2(150, -350),
                banditCamp
            })
        );
    }

    float DistanceToPolyline(Vector2 point, Vector2[] path)
    {
        float minimum = float.MaxValue;
        foreach (Vector2 p in path)
        {
            minimum = Mathf.Min(minimum, Vector2.Distance(point, p));
        }
        return minimum;
    }

    void CreateTree(float x, float z)
    {
        Vector3 position = new Vector3(x, GetGroundHeight(x, z), z);

        if (treePrefab != null)
        {
            GameObject tree = Instantiate(treePrefab, position, Quaternion.Euler(0, random.Next(360), 0), worldRoot);
            tree.transform.localScale *= Random.Range(0.85f, 1.4f);
        }
        else
        {
            GameObject treeObj = new GameObject("Medieval_Tree");
            treeObj.transform.SetParent(worldRoot);
            treeObj.transform.position = position;

            // Trunk
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(treeObj.transform);
            trunk.transform.localPosition = new Vector3(0, 3.5f, 0);
            trunk.transform.localScale = new Vector3(0.9f, 3.5f, 0.9f);
            if (woodBarkMat != null) trunk.GetComponent<MeshRenderer>().sharedMaterial = woodBarkMat;

            // Foliage Canopy
            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.SetParent(treeObj.transform);
            leaves.transform.localPosition = new Vector3(0, 7.5f, 0);
            leaves.transform.localScale = new Vector3(3.8f, 3.6f, 3.8f);
            if (leafMat != null) leaves.GetComponent<MeshRenderer>().sharedMaterial = leafMat;

            treeObj.transform.localScale *= Random.Range(0.85f, 1.35f);
            treeObj.transform.localRotation = Quaternion.Euler(0, random.Next(360), 0);
        }
    }

    void CreateRock(float x, float z)
    {
        Vector3 position = new Vector3(x, GetGroundHeight(x, z), z);
        GameObject rock;

        if (rockPrefab != null)
        {
            rock = Instantiate(rockPrefab, position, Quaternion.identity, worldRoot);
        }
        else
        {
            rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.transform.SetParent(worldRoot);
            rock.transform.position = position;
            if (rockMat != null) rock.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
        }

        rock.name = "Granite_Rock";
        rock.transform.localScale = new Vector3(
            Random.Range(1.8f, 4.5f),
            Random.Range(1.2f, 3.2f),
            Random.Range(1.8f, 4.5f)
        );
        rock.transform.localRotation = Quaternion.Euler(Random.Range(0, 30), random.Next(360), Random.Range(0, 30));
    }

    void CreateBuilding(string objectName, Vector2 location, GameObject prefab, Vector3 size, Material wallMat, Material roofMat)
    {
        Vector3 position = new Vector3(location.x, GetGroundHeight(location.x, location.y), location.y);
        GameObject building;

        if (prefab != null)
        {
            building = Instantiate(prefab, position, Quaternion.identity, worldRoot);
        }
        else
        {
            building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.transform.SetParent(worldRoot);
            building.transform.position = position + Vector3.up * size.y / 2f;
            building.transform.localScale = size;
            if (wallMat != null) building.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Add simple roof prism if roof material provided
            if (roofMat != null)
            {
                GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.name = "Roof";
                roof.transform.SetParent(building.transform);
                roof.transform.localPosition = new Vector3(0, 0.65f, 0);
                roof.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
                roof.transform.localRotation = Quaternion.identity;
                roof.GetComponent<MeshRenderer>().sharedMaterial = roofMat;
            }
        }

        building.name = objectName;
    }

    void PositionPlayerAtVillage()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null && terrain != null)
        {
            float groundY = terrain.SampleHeight(new Vector3(village.x, 0, village.y));
            player.transform.position = new Vector3(village.x, groundY + 1.5f, village.y);
        }
    }
}
