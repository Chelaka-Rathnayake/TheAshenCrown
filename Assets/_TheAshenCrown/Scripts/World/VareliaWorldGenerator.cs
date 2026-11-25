using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Celtic Mountain Valley & Medieval Village Generator for The Ashen Crown.
/// Inspired by Kingdom Come, Manor Lords, and Celtic alpine valleys:
/// - Glacial U-valley with winding pebble riverbed (Drake River)
/// - Majestic alpine peaks with snow and cliff rock
/// - Manor Lords style village with thatched cottages, chapel, and fenced farmland plots
/// - Dense pine forests wrapping the valley hillsides
/// - Weathered wooden fence posts and tree stumps
/// - Automatic golden hour lighting & misty valley fog configuration
/// </summary>
public class VareliaWorldGenerator : MonoBehaviour
{
    [Header("Valley Dimensions")]
    public int mapSize = 2000;
    public int heightMapResolution = 513;
    public int maxMountainHeight = 260;
    public int seed = 4321;

    [Header("Forest & Nature Density")]
    public int pineTreeCount = 850;
    public int riverRockCount = 180;
    public int woodenFencePostCount = 120;

    [Header("Settlement (Dunmere)")]
    public int villageCottageCount = 16;
    public int farmlandPlotCount = 8;

    [Header("Lighting & Atmosphere")]
    public bool autoConfigureCelticLighting = true;

    private Terrain terrain;
    private Transform worldRoot;
    private System.Random random;

    // Medieval Materials
    private Material pineTrunkMat;
    private Material pineNeedleMat;
    private Material oakLeafMat;
    private Material rockMat;
    private Material cottageWallMat;
    private Material thatchRoofMat;
    private Material stoneChapelMat;
    private Material fenceWoodMat;

    // Landmark coordinates
    private Vector2 villageCenter = new Vector2(0, -60);
    private Vector2 stoneFort = new Vector2(580, 320);
    private Vector2 ancientRuins = new Vector2(-600, -420);
    private Vector2 mountainCamp = new Vector2(350, -580);

    // Winding Celtic Drake River (flows from north mountains down through the valley)
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

    [ContextMenu("Generate Celtic Valley & Village")]
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

        // Clean up standalone terrain
        Terrain[] oldTerrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        foreach (Terrain t in oldTerrains)
        {
            if (t.transform.parent == null) DestroyImmediate(t.gameObject);
        }

        worldRoot = new GameObject("VARELIA_GENERATED_WORLD").transform;

        GenerateCelticValleyTerrain();
        SetupTerrainSplatmaps();
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

        Debug.Log("Celtic Mountain Valley & Manor Lords Village generated successfully!");
    }

    private void InitMaterials()
    {
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        pineTrunkMat = new Material(litShader) { name = "Mat_PineTrunk", color = new Color(0.20f, 0.13f, 0.08f) };
        pineNeedleMat = new Material(litShader) { name = "Mat_PineNeedle", color = new Color(0.12f, 0.28f, 0.15f) };
        oakLeafMat = new Material(litShader) { name = "Mat_OakLeaves", color = new Color(0.20f, 0.42f, 0.18f) };
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

                // 1. Broad U-Valley Shape (East and West borders rise into towering alpine peaks)
                float valleyCrossFactor = Mathf.Abs(worldX) / (mapSize * 0.5f);
                float mountainRise = Mathf.Pow(valleyCrossFactor, 2.2f) * 0.65f;

                // Also add north/south mountain ridges
                float northSouthDist = Mathf.Abs(worldZ) / (mapSize * 0.5f);
                if (northSouthDist > 0.65f)
                {
                    mountainRise += Mathf.Pow(Mathf.InverseLerp(0.65f, 1.0f, northSouthDist), 2f) * 0.35f;
                }

                // 2. Alpine Jagged Ridge Noise (Sharp glacial ridges, not rounded hills!)
                float ridgeNoise = Mathf.PerlinNoise((worldX + seed) * 0.0035f, (worldZ + seed) * 0.0035f);
                float sharpRidge = 1.0f - Mathf.Abs(ridgeNoise * 2.0f - 1.0f);
                sharpRidge = Mathf.Pow(sharpRidge, 2.2f) * 0.35f * valleyCrossFactor;

                // 3. Gentle rolling meadow noise for the valley floor
                float valleyMeadow = Mathf.PerlinNoise((worldX + seed + 200f) * 0.006f, (worldZ + seed + 200f) * 0.006f) * 0.045f;

                float finalHeight = 0.08f + mountainRise + sharpRidge + valleyMeadow;

                // 4. Flatten Village settlement area
                finalHeight = FlattenZone(finalHeight, worldX, worldZ, villageCenter, 220f, 0.095f);
                finalHeight = FlattenZone(finalHeight, worldX, worldZ, stoneFort, 120f, 0.16f);
                finalHeight = FlattenZone(finalHeight, worldX, worldZ, ancientRuins, 75f, 0.12f);

                // 5. Carve the Drake Riverbed
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
        terrain.basemapDistance = 900;
        terrain.detailObjectDistance = 80;
    }

    void SetupTerrainSplatmaps()
    {
#if UNITY_EDITOR
        TerrainData tData = terrain.terrainData;

        Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Albedo.tif");
        Texture2D grassNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Normal.tif");
        Texture2D dirtTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Albedo.tif");
        Texture2D dirtNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Normal.tif");
        Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Albedo.png");
        Texture2D rockNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Normal.png");

        TerrainLayer grassLayer = new TerrainLayer { name = "Alpine_Meadow", diffuseTexture = grassTex, normalMapTexture = grassNormal, tileSize = new Vector2(7f, 7f) };
        TerrainLayer dirtLayer = new TerrainLayer { name = "Tilled_Dirt", diffuseTexture = dirtTex, normalMapTexture = dirtNormal, tileSize = new Vector2(9f, 9f) };
        TerrainLayer rockLayer = new TerrainLayer { name = "Mountain_Cliff", diffuseTexture = rockTex, normalMapTexture = rockNormal, tileSize = new Vector2(16f, 16f) };

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

                // Alpine steep cliff walls
                if (steepness > 24f)
                {
                    rockWeight = Mathf.InverseLerp(22f, 38f, steepness);
                    grassWeight = 1f - rockWeight;
                }

                // Packed dirt roads & village square
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

    void GenerateRiver()
    {
        CreateLineRenderer("Drake_River", riverPath, 20f, new Color(0.20f, 0.44f, 0.62f), 0.4f);
    }

    void GenerateCelticRoads()
    {
        Vector2[] southVillageRoad =
        {
            new Vector2(-400, -800),
            new Vector2(-220, -500),
            new Vector2(-80, -250),
            villageCenter
        };
        CreateLineRenderer("South_Valley_Road", southVillageRoad, 8f, new Color(0.38f, 0.30f, 0.19f), 0.35f);

        Vector2[] fortRoad =
        {
            villageCenter,
            new Vector2(160, 40),
            new Vector2(340, 160),
            stoneFort
        };
        CreateLineRenderer("Fort_Ascent_Road", fortRoad, 7.5f, new Color(0.36f, 0.28f, 0.18f), 0.35f);

        Vector2[] ruinsRoad =
        {
            villageCenter,
            new Vector2(-220, -180),
            new Vector2(-420, -320),
            ancientRuins
        };
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
        // 1. Dunmere Chapel & Stone Belfry (The focal point of the story!)
        Vector3 chapelPos = new Vector3(villageCenter.x - 25f, 0, villageCenter.y + 35f);
        chapelPos.y = terrain.SampleHeight(chapelPos);
        CreateBuildingModel("Dunmere_Stone_Chapel", chapelPos, new Vector3(18f, 14f, 26f), stoneChapelMat, thatchRoofMat);

        // Chapel Bell Tower
        Vector3 towerPos = chapelPos + new Vector3(12f, 0f, -8f);
        towerPos.y = terrain.SampleHeight(towerPos);
        CreateBuildingModel("Chapel_BellTower", towerPos, new Vector3(8f, 28f, 8f), stoneChapelMat, thatchRoofMat);

        // 2. Village Cottages along organic road curves
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

        // 3. Fenced Farmland Plots around village perimeter (Manor Lords style fields!)
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

        // Add 4 corner wooden fence posts
        Vector2[] corners =
        {
            new Vector2(-width*0.5f, -length*0.5f),
            new Vector2(width*0.5f, -length*0.5f),
            new Vector2(width*0.5f, length*0.5f),
            new Vector2(-width*0.5f, length*0.5f)
        };

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
        GameObject bld = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bld.name = name;
        bld.transform.SetParent(worldRoot);
        bld.transform.position = pos + Vector3.up * size.y * 0.5f;
        bld.transform.localScale = size;
        if (wallMat != null) bld.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        if (roofMat != null)
        {
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Pitched_Roof";
            roof.transform.SetParent(bld.transform);
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

            // Tall Mountain Pine
            GameObject pine = new GameObject("Celtic_Pine");
            pine.transform.SetParent(worldRoot);
            pine.transform.position = treePos;

            // Trunk
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(pine.transform);
            trunk.transform.localPosition = new Vector3(0, 4.5f, 0);
            trunk.transform.localScale = new Vector3(0.7f, 4.5f, 0.7f);
            if (pineTrunkMat != null) trunk.GetComponent<MeshRenderer>().sharedMaterial = pineTrunkMat;

            // Conical Pine Foliage (3 tiered cones)
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

            // Cluster rocks along riverbanks
            if (DistanceToRiver(x, z) < 45f && DistanceToRiver(x, z) > 12f)
            {
                Vector3 rPos = new Vector3(x, terrain.SampleHeight(new Vector3(x, 0, z)), z);
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = "River_Boulder";
                rock.transform.SetParent(worldRoot);
                rock.transform.position = rPos;
                rock.transform.localScale = new Vector3(Random.Range(1.8f, 4.5f), Random.Range(1f, 2.5f), Random.Range(1.8f, 4.5f));
                if (rockMat != null) rock.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
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
        // 1. Warm Golden Hour Sunlight (Matches Drake River Image 2)
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

        // 2. Low-hanging Morning Mist / Fog
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
