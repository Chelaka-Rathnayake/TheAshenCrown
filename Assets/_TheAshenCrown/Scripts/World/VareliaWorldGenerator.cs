using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Fully automated Celtic Mountain Valley & Medieval Village Generator for The Ashen Crown.
/// 1-Click generation of:
/// - 2km Glacial U-Valley with jagged alpine ridges & Drake River
/// - Persistent PBR TerrainLayers with Normal Maps (Grass, Dirt, Cliff Rock)
/// - Automated 3D waving grass and wildflower detail layers
/// - Manor Lords style village with thatched cottages, parish chapel, and fenced plots
/// - Dense evergreen pine forests, river boulders, and fence posts
/// - Golden hour sunlight and misty valley atmospheric fog
/// </summary>
public class VareliaWorldGenerator : MonoBehaviour
{
    [Header("Valley Dimensions")]
    public int mapSize = 2000;
    public int heightMapResolution = 513;
    public int maxMountainHeight = 260;
    public int seed = 4321;

    [Header("Optional Custom Prefabs (Leave Empty for Built-in Procedural)")]
    public GameObject customTreePrefab;
    public GameObject customRockPrefab;
    public GameObject customHousePrefab;

    [Header("Forest & Nature Density")]
    public int pineTreeCount = 850;
    public int riverRockCount = 180;
    public int woodenFencePostCount = 120;

    [Header("3D Foliage & Grass")]
    public bool enable3DWavingGrass = true;
    public float detailRenderDistance = 85f;
    [Range(2, 10)] public int grassClumpDensity = 6;

    [Header("Settlement (Dunmere)")]
    public int villageCottageCount = 16;
    public int farmlandPlotCount = 8;

    [Header("Lighting & Atmosphere")]
    public bool autoConfigureCelticLighting = true;

    private Terrain terrain;
    private Transform worldRoot;
    private System.Random random;

    // Materials
    private Material pineTrunkMat;
    private Material pineNeedleMat;
    private Material rockMat;
    private Material cottageWallMat;
    private Material thatchRoofMat;
    private Material stoneChapelMat;
    private Material fenceWoodMat;

    // Landmarks
    private Vector2 villageCenter = new Vector2(0, -60);
    private Vector2 stoneFort = new Vector2(580, 320);
    private Vector2 ancientRuins = new Vector2(-600, -420);

    // River Path
    private Vector2[] riverPath =
    {
        new Vector2(-380, 950),
        new Vector2(-280, 680),
        new Vector2(-150, 420),
        new Vector2(-60, 180),
        new Vector2(-40, -40),
        new Vector2(60, -260),
        new Vector2(180, -520),
        new Vector2(340, -780),
        new Vector2(520, -980)
    };

    [ContextMenu("Generate Varelia World")]
    public void GenerateWorld()
    {
        random = new System.Random(seed);
        InitMaterials();

        if (worldRoot != null)
        {
            DestroyImmediate(worldRoot.gameObject);
        }
        else
        {
            GameObject existing = GameObject.Find("VARELIA_GENERATED_WORLD");
            if (existing != null) DestroyImmediate(existing);
        }

        Terrain[] oldTerrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (Terrain t in oldTerrains)
        {
            if (t.transform.parent == null) DestroyImmediate(t.gameObject);
        }

        worldRoot = new GameObject("VARELIA_GENERATED_WORLD").transform;

        GenerateCelticValleyTerrain();
        SetupTerrainLayersAndSplatmaps();

        if (enable3DWavingGrass)
        {
            Populate3DWavingGrass();
        }

        GenerateRiver();
        GenerateCelticRoads();
        GenerateManorLordsVillage();
        GenerateFortAndRuins();
        GenerateDensePineForests();
        GenerateFencePostsAndStumps();
        PositionPlayerInVillage();

        if (autoConfigureCelticLighting)
        {
            ConfigureCelticAtmosphere();
        }

        Debug.Log("Varelia Celtic Valley & Village generation completed successfully!");
    }

    private void InitMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        pineTrunkMat = new Material(litShader) { name = "Mat_PineTrunk", color = new Color(0.20f, 0.13f, 0.08f) };
        pineNeedleMat = new Material(litShader) { name = "Mat_PineNeedle", color = new Color(0.11f, 0.26f, 0.13f) };
        rockMat = new Material(litShader) { name = "Mat_AlpineRock", color = new Color(0.36f, 0.37f, 0.38f) };
        cottageWallMat = new Material(litShader) { name = "Mat_CottagePlaster", color = new Color(0.58f, 0.53f, 0.45f) };
        thatchRoofMat = new Material(litShader) { name = "Mat_ThatchedStraw", color = new Color(0.36f, 0.26f, 0.16f) };
        stoneChapelMat = new Material(litShader) { name = "Mat_AshlarStone", color = new Color(0.32f, 0.33f, 0.35f) };
        fenceWoodMat = new Material(litShader) { name = "Mat_WeatheredPost", color = new Color(0.28f, 0.22f, 0.17f) };
    }

    void GenerateCelticValleyTerrain()
    {
        TerrainData data = new TerrainData();
        data.heightmapResolution = heightMapResolution;
        data.size = new Vector3(mapSize, maxMountainHeight, mapSize);

        float[,] heights = new float[heightMapResolution, heightMapResolution];

        for (int z = 0; z < heightMapResolution; z++)
        {
            for (int x = 0; x < heightMapResolution; x++)
            {
                float worldX = (float)x / (heightMapResolution - 1) * mapSize - mapSize * 0.5f;
                float worldZ = (float)z / (heightMapResolution - 1) * mapSize - mapSize * 0.5f;

                // U-Valley (east and west rise into high mountain ridges)
                float valleyCrossFactor = Mathf.Abs(worldX) / (mapSize * 0.5f);
                float mountainRise = Mathf.Pow(valleyCrossFactor, 2.2f) * 0.65f;

                // North/South mountain boundaries
                float northSouthDist = Mathf.Abs(worldZ) / (mapSize * 0.5f);
                if (northSouthDist > 0.65f)
                {
                    mountainRise += Mathf.Pow(Mathf.InverseLerp(0.65f, 1.0f, northSouthDist), 2f) * 0.35f;
                }

                // Alpine Ridges
                float ridgeNoise = Mathf.PerlinNoise((worldX + seed) * 0.0035f, (worldZ + seed) * 0.0035f);
                float sharpRidge = 1.0f - Mathf.Abs(ridgeNoise * 2.0f - 1.0f);
                sharpRidge = Mathf.Pow(sharpRidge, 2.2f) * 0.35f * valleyCrossFactor;

                // Valley floor meadow rolling
                float valleyMeadow = Mathf.PerlinNoise((worldX + seed + 200f) * 0.006f, (worldZ + seed + 200f) * 0.006f) * 0.045f;

                float finalHeight = 0.08f + mountainRise + sharpRidge + valleyMeadow;

                // Flatten settlement zones
                finalHeight = FlattenZone(finalHeight, worldX, worldZ, villageCenter, 220f, 0.095f);
                finalHeight = FlattenZone(finalHeight, worldX, worldZ, stoneFort, 120f, 0.16f);
                finalHeight = FlattenZone(finalHeight, worldX, worldZ, ancientRuins, 75f, 0.12f);

                // Carve riverbed
                float distToRiver = DistanceToRiver(worldX, worldZ);
                if (distToRiver < 55f)
                {
                    float riverGully = Mathf.Exp(-(distToRiver * distToRiver) / 380f) * 0.038f;
                    finalHeight -= riverGully;
                }

                heights[z, x] = Mathf.Clamp01(finalHeight);
            }
        }

        data.SetHeights(0, 0, heights);

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Celtic_Valley_Terrain";
        terrainObject.transform.position = new Vector3(-mapSize * 0.5f, 0, -mapSize * 0.5f);
        terrainObject.transform.SetParent(worldRoot);

        terrain = terrainObject.GetComponent<Terrain>();
        terrain.heightmapPixelError = 5;
        terrain.basemapDistance = 800;
        terrain.detailObjectDistance = detailRenderDistance;
        terrain.detailObjectDensity = 1.0f;
    }

    void SetupTerrainLayersAndSplatmaps()
    {
#if UNITY_EDITOR
        TerrainData tData = terrain.terrainData;

        // Load Project Textures
        Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Albedo.tif");
        Texture2D grassNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Normal.tif");
        Texture2D dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Albedo.tif");
        Texture2D dirtNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Normal.tif");
        Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Albedo.png");
        Texture2D rockNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Normal.png");

        // Save persistent .terrainlayer assets
        TerrainLayer grassLayer = GetOrCreateTerrainLayer("Assets/_TheAshenCrown/Data/TerrainLayers/Layer_AlpineGrass.terrainlayer", grassTex, grassNormal, new Vector2(6f, 6f));
        TerrainLayer dirtLayer = GetOrCreateTerrainLayer("Assets/_TheAshenCrown/Data/TerrainLayers/Layer_RiverDirt.terrainlayer", dirtTex, dirtNormal, new Vector2(8f, 8f));
        TerrainLayer rockLayer = GetOrCreateTerrainLayer("Assets/_TheAshenCrown/Data/TerrainLayers/Layer_CliffStone.terrainlayer", rockTex, rockNormal, new Vector2(14f, 14f));

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

                float worldX = normX * mapSize - mapSize * 0.5f;
                float worldZ = normZ * mapSize - mapSize * 0.5f;

                float steepness = tData.GetSteepness(normX, normZ);
                float roadDist = DistanceToRoads(new Vector2(worldX, worldZ));
                float riverDist = DistanceToRiver(worldX, worldZ);
                float villageDist = Vector2.Distance(new Vector2(worldX, worldZ), villageCenter);

                float grassWeight = 1f;
                float dirtWeight = 0f;
                float rockWeight = 0f;

                if (steepness > 24f)
                {
                    rockWeight = Mathf.InverseLerp(22f, 38f, steepness);
                    grassWeight = 1f - rockWeight;
                }

                if (roadDist < 7.5f)
                {
                    dirtWeight = Mathf.InverseLerp(7.5f, 1.5f, roadDist);
                    grassWeight *= (1f - dirtWeight);
                    rockWeight *= (1f - dirtWeight);
                }
                else if (villageDist < 60f)
                {
                    dirtWeight = Mathf.InverseLerp(60f, 20f, villageDist) * 0.7f;
                    grassWeight *= (1f - dirtWeight);
                    rockWeight *= (1f - dirtWeight);
                }
                else if (riverDist < 22f)
                {
                    dirtWeight = Mathf.InverseLerp(22f, 8f, riverDist) * 0.9f;
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

#if UNITY_EDITOR
    private TerrainLayer GetOrCreateTerrainLayer(string path, Texture2D diffuse, Texture2D normal, Vector2 tileSize)
    {
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer
            {
                diffuseTexture = diffuse,
                normalMapTexture = normal,
                tileSize = tileSize
            };
            AssetDatabase.CreateAsset(layer, path);
            AssetDatabase.SaveAssets();
        }
        else
        {
            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.tileSize = tileSize;
            EditorUtility.SetDirty(layer);
        }
        return layer;
    }
#endif

    void Populate3DWavingGrass()
    {
#if UNITY_EDITOR
        TerrainData tData = terrain.terrainData;

        Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Tile_Grass.png");
        Texture2D flowerTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Tile_GrassFlowers.png");

        if (grassTex == null) return;

        DetailPrototype grassProto = new DetailPrototype
        {
            prototypeTexture = grassTex,
            renderMode = DetailRenderMode.GrassBillboard,
            minWidth = 1.1f,
            maxWidth = 1.7f,
            minHeight = 0.85f,
            maxHeight = 1.45f,
            healthyColor = new Color(0.24f, 0.52f, 0.20f),
            dryColor = new Color(0.46f, 0.42f, 0.24f)
        };

        DetailPrototype flowerProto = new DetailPrototype
        {
            prototypeTexture = flowerTex != null ? flowerTex : grassTex,
            renderMode = DetailRenderMode.GrassBillboard,
            minWidth = 1.0f,
            maxWidth = 1.5f,
            minHeight = 0.75f,
            maxHeight = 1.3f,
            healthyColor = new Color(0.55f, 0.25f, 0.55f), // Heather purple
            dryColor = new Color(0.65f, 0.35f, 0.65f)
        };

        tData.detailPrototypes = new DetailPrototype[] { grassProto, flowerProto };

        tData.wavingGrassStrength = 0.42f;
        tData.wavingGrassAmount = 0.38f;
        tData.wavingGrassSpeed = 0.30f;
        tData.wavingGrassTint = new Color(0.75f, 0.70f, 0.50f);

        int detailRes = tData.detailResolution;
        int[,] grassMap = new int[detailRes, detailRes];
        int[,] flowerMap = new int[detailRes, detailRes];

        for (int z = 0; z < detailRes; z++)
        {
            for (int x = 0; x < detailRes; x++)
            {
                float normX = (float)x / (detailRes - 1);
                float normZ = (float)z / (detailRes - 1);

                float worldX = normX * mapSize - mapSize * 0.5f;
                float worldZ = normZ * mapSize - mapSize * 0.5f;

                float steepness = tData.GetSteepness(normX, normZ);
                float roadDist = DistanceToRoads(new Vector2(worldX, worldZ));
                float riverDist = DistanceToRiver(worldX, worldZ);

                if (roadDist < 6.5f || riverDist < 18f || steepness > 22f)
                {
                    grassMap[z, x] = 0;
                    flowerMap[z, x] = 0;
                    continue;
                }

                // Noise-based natural meadow grass fields
                float noise = Mathf.PerlinNoise(worldX * 0.022f + 50f, worldZ * 0.022f + 50f);
                if (noise > 0.38f)
                {
                    grassMap[z, x] = Mathf.RoundToInt(Mathf.Lerp(2, grassClumpDensity, (noise - 0.38f) * 2.2f));

                    // Purple wildflower patches (like reference Photo 3!)
                    float flowerNoise = Mathf.PerlinNoise(worldX * 0.04f + 120f, worldZ * 0.04f + 120f);
                    if (flowerNoise > 0.65f)
                    {
                        flowerMap[z, x] = Mathf.RoundToInt(Random.Range(2, 4));
                    }
                }
            }
        }

        tData.SetDetailLayer(0, 0, 0, grassMap);
        tData.SetDetailLayer(0, 0, 1, flowerMap);
#endif
    }

    void GenerateRiver()
    {
        CreateLineRenderer("Drake_River", riverPath, 20f, new Color(0.20f, 0.44f, 0.62f), 0.4f);
    }

    void GenerateCelticRoads()
    {
        Vector2[] southVillageRoad = { new Vector2(-400, -800), new Vector2(-220, -500), new Vector2(-80, -250), villageCenter };
        CreateLineRenderer("South_Valley_Road", southVillageRoad, 8f, new Color(0.38f, 0.30f, 0.19f), 0.35f);

        Vector2[] fortRoad = { villageCenter, new Vector2(160, 40), new Vector2(340, 160), stoneFort };
        CreateLineRenderer("Fort_Ascent_Road", fortRoad, 7.5f, new Color(0.36f, 0.28f, 0.18f), 0.35f);

        Vector2[] ruinsRoad = { villageCenter, new Vector2(-220, -180), new Vector2(-420, -320), ancientRuins };
        CreateLineRenderer("Ruins_Trail", ruinsRoad, 6f, new Color(0.34f, 0.26f, 0.16f), 0.35f);
    }

    void CreateLineRenderer(string objName, Vector2[] pts, float width, Color col, float yOffset)
    {
        GameObject lineObj = new GameObject(objName);
        lineObj.transform.SetParent(worldRoot);

        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = pts.Length;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        line.material = new Material(shader);
        line.startColor = col;
        line.endColor = col;

        for (int i = 0; i < pts.Length; i++)
        {
            float surfaceY = terrain.SampleHeight(new Vector3(pts[i].x, 0, pts[i].y));
            line.SetPosition(i, new Vector3(pts[i].x, surfaceY + yOffset, pts[i].y));
        }
    }

    void GenerateManorLordsVillage()
    {
        // Stone Parish Chapel & Belfry
        Vector3 chapelPos = new Vector3(villageCenter.x - 25f, 0, villageCenter.y + 35f);
        chapelPos.y = terrain.SampleHeight(chapelPos);
        CreateBuildingModel("Dunmere_Stone_Chapel", chapelPos, new Vector3(18f, 14f, 26f), stoneChapelMat, thatchRoofMat);

        Vector3 towerPos = chapelPos + new Vector3(12f, 0f, -8f);
        towerPos.y = terrain.SampleHeight(towerPos);
        CreateBuildingModel("Chapel_BellTower", towerPos, new Vector3(8f, 28f, 8f), stoneChapelMat, thatchRoofMat);

        // Cottages
        for (int i = 0; i < villageCottageCount; i++)
        {
            float side = (i % 2 == 0) ? 1f : -1f;
            float rowOffset = (i / 2) * 22f - 70f;
            float lateralOffset = side * (32f + (float)random.NextDouble() * 18f);

            Vector2 cottageCoord = villageCenter + new Vector2(lateralOffset, rowOffset);
            Vector3 pos = new Vector3(cottageCoord.x, 0, cottageCoord.y);
            pos.y = terrain.SampleHeight(pos);

            float houseWidth = 10f + (float)random.NextDouble() * 4f;
            float houseLength = 12f + (float)random.NextDouble() * 5f;
            CreateBuildingModel("Cottage_" + i, pos, new Vector3(houseWidth, 7f, houseLength), cottageWallMat, thatchRoofMat);
        }

        // Fenced Farmland Plots
        for (int p = 0; p < farmlandPlotCount; p++)
        {
            float angle = p * (Mathf.PI * 2f / farmlandPlotCount);
            float radius = 125f + (float)random.NextDouble() * 35f;
            Vector2 plotCenter = villageCenter + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            CreateFarmlandPlot(plotCenter, 38f, 48f);
        }
    }

    void CreateFarmlandPlot(Vector2 center, float width, float length)
    {
        Vector3 plotPos = new Vector3(center.x, 0, center.y);
        plotPos.y = terrain.SampleHeight(plotPos);

        GameObject field = GameObject.CreatePrimitive(PrimitiveType.Cube);
        field.name = "Fenced_Field_Plot";
        field.transform.SetParent(worldRoot);
        field.transform.position = plotPos + Vector3.up * 0.1f;
        field.transform.localScale = new Vector3(width, 0.2f, length);
        if (fenceWoodMat != null) field.GetComponent<MeshRenderer>().sharedMaterial = fenceWoodMat;

        Vector2[] corners = { new Vector2(-width*0.5f, -length*0.5f), new Vector2(width*0.5f, -length*0.5f), new Vector2(width*0.5f, length*0.5f), new Vector2(-width*0.5f, length*0.5f) };
        foreach (var c in corners)
        {
            Vector3 postPos = new Vector3(center.x + c.x, 0, center.y + c.y);
            postPos.y = terrain.SampleHeight(postPos);
            CreateFencePost(postPos);
        }
    }

    void CreateFencePost(Vector3 pos)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        post.name = "Fence_Post";
        post.transform.SetParent(worldRoot);
        post.transform.position = pos + Vector3.up * 1.1f;
        post.transform.localScale = new Vector3(0.25f, 1.1f, 0.25f);
        if (fenceWoodMat != null) post.GetComponent<MeshRenderer>().sharedMaterial = fenceWoodMat;
        DestroyImmediate(post.GetComponent<Collider>());
    }

    void GenerateFortAndRuins()
    {
        Vector3 fortPos = new Vector3(stoneFort.x, 0, stoneFort.y);
        fortPos.y = terrain.SampleHeight(fortPos);
        CreateBuildingModel("Stonewatch_Keep", fortPos, new Vector3(65f, 28f, 65f), stoneChapelMat, null);

        Vector3 ruinPos = new Vector3(ancientRuins.x, 0, ancientRuins.y);
        ruinPos.y = terrain.SampleHeight(ruinPos);
        CreateBuildingModel("Forgotten_Ruins_Crypt", ruinPos, new Vector3(40f, 12f, 40f), stoneChapelMat, null);
    }

    void CreateBuildingModel(string name, Vector3 pos, Vector3 size, Material wallMat, Material roofMat)
    {
        if (customHousePrefab != null)
        {
            GameObject bld = Instantiate(customHousePrefab, pos, Quaternion.identity, worldRoot);
            bld.name = name;
            return;
        }

        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = name;
        building.transform.SetParent(worldRoot);
        building.transform.position = pos + Vector3.up * size.y * 0.5f;
        building.transform.localScale = size;
        if (wallMat != null) building.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        if (roofMat != null)
        {
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Pitched_Roof";
            roof.transform.SetParent(building.transform);
            roof.transform.localPosition = new Vector3(0, 0.62f, 0);
            roof.transform.localScale = new Vector3(1.1f, 0.38f, 1.1f);
            roof.transform.localRotation = Quaternion.identity;
            roof.GetComponent<MeshRenderer>().sharedMaterial = roofMat;
        }
    }

    void GenerateDensePineForests()
    {
        for (int i = 0; i < pineTreeCount; i++)
        {
            float x = (float)random.NextDouble() * mapSize - mapSize * 0.5f;
            float z = (float)random.NextDouble() * mapSize - mapSize * 0.5f;
            Vector2 pt = new Vector2(x, z);

            if (Vector2.Distance(pt, villageCenter) < 165f) continue;
            if (Vector2.Distance(pt, stoneFort) < 110f) continue;
            if (Vector2.Distance(pt, ancientRuins) < 70f) continue;
            if (DistanceToRiver(x, z) < 28f) continue;
            if (DistanceToRoads(pt) < 14f) continue;

            float steepness = terrain.terrainData.GetSteepness((x + mapSize * 0.5f) / mapSize, (z + mapSize * 0.5f) / mapSize);
            if (steepness > 32f) continue;

            Vector3 treePos = new Vector3(x, terrain.SampleHeight(new Vector3(x, 0, z)), z);

            if (customTreePrefab != null)
            {
                GameObject tree = Instantiate(customTreePrefab, treePos, Quaternion.Euler(0, random.Next(360), 0), worldRoot);
                tree.transform.localScale *= Random.Range(0.85f, 1.35f);
                continue;
            }

            GameObject pine = new GameObject("Celtic_Pine");
            pine.transform.SetParent(worldRoot);
            pine.transform.position = treePos;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(pine.transform);
            trunk.transform.localPosition = new Vector3(0, 4.5f, 0);
            trunk.transform.localScale = new Vector3(0.7f, 4.5f, 0.7f);
            if (pineTrunkMat != null) trunk.GetComponent<MeshRenderer>().sharedMaterial = pineTrunkMat;

            for (int t = 0; t < 3; t++)
            {
                GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cone.transform.SetParent(pine.transform);
                cone.transform.localPosition = new Vector3(0, 7f + (t * 2.8f), 0);
                float radius = 3.8f - (t * 0.9f);
                cone.transform.localScale = new Vector3(radius, 2.5f, radius);
                if (pineNeedleMat != null) cone.GetComponent<MeshRenderer>().sharedMaterial = pineNeedleMat;
            }

            pine.transform.localScale *= Random.Range(0.85f, 1.4f);
            pine.transform.localRotation = Quaternion.Euler(0, random.Next(360), 0);
        }

        // River Rocks
        for (int r = 0; r < riverRockCount; r++)
        {
            float x = (float)random.NextDouble() * mapSize - mapSize * 0.5f;
            float z = (float)random.NextDouble() * mapSize - mapSize * 0.5f;

            if (DistanceToRiver(x, z) < 45f && DistanceToRiver(x, z) > 12f)
            {
                Vector3 rPos = new Vector3(x, terrain.SampleHeight(new Vector3(x, 0, z)), z);
                if (customRockPrefab != null)
                {
                    Instantiate(customRockPrefab, rPos, Quaternion.identity, worldRoot);
                }
                else
                {
                    GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    rock.name = "River_Boulder";
                    rock.transform.SetParent(worldRoot);
                    rock.transform.position = rPos;
                    rock.transform.localScale = new Vector3(Random.Range(1.8f, 4.5f), Random.Range(1f, 2.5f), Random.Range(1.8f, 4.5f));
                    if (rockMat != null) rock.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
                }
            }
        }
    }

    void GenerateFencePostsAndStumps()
    {
        for (int f = 0; f < woodenFencePostCount; f++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float r = 70f + (float)random.NextDouble() * 110f;
            Vector2 postCoord = villageCenter + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);

            Vector3 postPos = new Vector3(postCoord.x, terrain.SampleHeight(new Vector3(postCoord.x, 0, postCoord.y)), postCoord.y);
            CreateFencePost(postPos);
        }
    }

    void PositionPlayerInVillage()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null && terrain != null)
        {
            Vector3 spawn = new Vector3(villageCenter.x, 0, villageCenter.y);
            spawn.y = terrain.SampleHeight(spawn) + 1.5f;
            player.transform.position = spawn;
        }
    }

    void ConfigureCelticAtmosphere()
    {
        Light sun = RenderSettings.sun;
        if (sun == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(22f, -50f, 0f);
            sun.color = new Color(1.0f, 0.94f, 0.84f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
        }

        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.84f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.005f;
    }

    float FlattenZone(float original, float x, float z, Vector2 center, float radius, float targetHeight)
    {
        float d = Vector2.Distance(new Vector2(x, z), center);
        float blend = 1f - Mathf.SmoothStep(radius * 0.65f, radius, d);
        return Mathf.Lerp(original, targetHeight, blend);
    }

    float DistanceToRiver(float x, float z)
    {
        float min = float.MaxValue;
        foreach (Vector2 p in riverPath)
        {
            min = Mathf.Min(min, Vector2.Distance(new Vector2(x, z), p));
        }
        return min;
    }

    float DistanceToRoads(Vector2 pt)
    {
        float d1 = DistanceToPolyline(pt, new Vector2[] { new Vector2(-400, -800), new Vector2(-220, -500), new Vector2(-80, -250), villageCenter });
        float d2 = DistanceToPolyline(pt, new Vector2[] { villageCenter, new Vector2(160, 40), new Vector2(340, 160), stoneFort });
        float d3 = DistanceToPolyline(pt, new Vector2[] { villageCenter, new Vector2(-220, -180), new Vector2(-420, -320), ancientRuins });
        return Mathf.Min(d1, Mathf.Min(d2, d3));
    }

    float DistanceToPolyline(Vector2 pt, Vector2[] line)
    {
        float min = float.MaxValue;
        foreach (Vector2 p in line)
        {
            min = Mathf.Min(min, Vector2.Distance(pt, p));
        }
        return min;
    }
}
