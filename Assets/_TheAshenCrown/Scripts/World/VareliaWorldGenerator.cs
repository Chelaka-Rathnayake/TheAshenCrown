using UnityEngine;
using System.Collections.Generic;

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
    public int treeCount = 350;
    public int rockCount = 100;

    [Header("Buildings")]
    public GameObject housePrefab;
    public GameObject fortPrefab;
    public GameObject ruinPrefab;

    [Header("Generation")]
    public bool generateOnStart = false;

    private Terrain terrain;
    private Transform worldRoot;
    private System.Random random;

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

        // Clean up any standalone previous terrain so terrains don't overlap
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
        GenerateRiver();
        GenerateRoads();
        GenerateSettlements();
        GenerateVegetation();
        GenerateLandmarks();
        PositionPlayerAtVillage();

        Debug.Log("Varelia world generation completed!");
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

                // Flatten settlement locations
                height = Flatten(height, worldX, worldZ,
                    village, 170f, 0.12f);

                height = Flatten(height, worldX, worldZ,
                    fort, 110f, 0.19f);

                height = Flatten(height, worldX, worldZ,
                    ruins, 65f, 0.14f);

                height = Flatten(height, worldX, worldZ,
                    banditCamp, 60f, 0.12f);

                // Carve river valley
                float riverDistance = DistanceToRiver(
                    worldX, worldZ
                );

                float riverCarve = Mathf.Exp(
                    -(riverDistance * riverDistance) / 1800f
                );

                height -= riverCarve * 0.055f;

                heights[z, x] = Mathf.Clamp01(height);
            }
        }

        data.SetHeights(0, 0, heights);

        GameObject terrainObject =
            Terrain.CreateTerrainGameObject(data);

        terrainObject.name = "Varelia_Terrain";
        terrainObject.transform.position =
            new Vector3(-mapSize / 2f, 0, -mapSize / 2f);

        terrainObject.transform.SetParent(worldRoot);

        terrain = terrainObject.GetComponent<Terrain>();
        terrain.heightmapPixelError = 8;
        terrain.basemapDistance = 1500;
    }

    float Flatten(
        float original,
        float x,
        float z,
        Vector2 center,
        float radius,
        float targetHeight)
    {
        float distance = Vector2.Distance(
            new Vector2(x, z), center
        );

        float blend = 1f - Mathf.SmoothStep(
            radius * 0.65f,
            radius,
            distance
        );

        return Mathf.Lerp(original, targetHeight, blend);
    }

    float DistanceToRiver(float x, float z)
    {
        float minimum = float.MaxValue;

        foreach (Vector2 point in riverPoints)
        {
            float distance = Vector2.Distance(
                new Vector2(x, z), point
            );

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
        CreateLine(
            "River",
            riverPoints,
            25f,
            new Color(0.15f, 0.35f, 0.55f)
        );
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

        CreateLine(
            "Main_Road",
            mainRoad,
            10f,
            new Color(0.35f, 0.25f, 0.16f)
        );

        Vector2[] forestRoad =
        {
            village,
            new Vector2(-250, 250),
            ruins
        };

        CreateLine(
            "Forest_Road",
            forestRoad,
            7f,
            new Color(0.4f, 0.3f, 0.2f)
        );

        Vector2[] campRoad =
        {
            village,
            new Vector2(150, -350),
            banditCamp
        };

        CreateLine(
            "Bandit_Road",
            campRoad,
            6f,
            new Color(0.35f, 0.25f, 0.16f)
        );
    }

    void CreateLine(
        string objectName,
        Vector2[] points,
        float width,
        Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(worldRoot);

        LineRenderer line =
            lineObject.AddComponent<LineRenderer>();

        line.positionCount = points.Length;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        line.material = new Material(shader);

        line.startColor = color;
        line.endColor = color;

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 position = new Vector3(
                points[i].x,
                GetGroundHeight(points[i].x, points[i].y) + 2f,
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

            Vector2 position = center +
                new Vector2(
                    Mathf.Cos(angle) * 65f,
                    Mathf.Sin(angle) * 65f
                );

            CreateBuilding(
                "Village_House_" + i,
                position,
                housePrefab,
                new Vector3(12, 9, 12)
            );
        }

        CreateBuilding(
            "Village_Center",
            center,
            housePrefab,
            new Vector3(18, 12, 18)
        );
    }

    void CreateFort(Vector2 center)
    {
        CreateBuilding(
            "Stonewatch_Fort",
            center,
            fortPrefab,
            new Vector3(70, 35, 70)
        );

        for (int i = 0; i < 4; i++)
        {
            Vector2 position = center +
                new Vector2(
                    i % 2 == 0 ? -45 : 45,
                    i < 2 ? -45 : 45
                );

            CreateBuilding(
                "Fort_Tower_" + i,
                position,
                null,
                new Vector3(12, 30, 12)
            );
        }
    }

    void GenerateLandmarks()
    {
        CreateBuilding(
            "Abandoned_Ruins",
            ruins,
            ruinPrefab,
            new Vector3(45, 15, 45)
        );

        CreateBuilding(
            "Bandit_Camp",
            banditCamp,
            null,
            new Vector3(25, 8, 25)
        );
    }

    void GenerateVegetation()
    {
        for (int i = 0; i < treeCount; i++)
        {
            float x = RandomCoordinate();
            float z = RandomCoordinate();

            Vector2 position = new Vector2(x, z);

            if (Vector2.Distance(position, village) < 190f)
                continue;

            if (Vector2.Distance(position, fort) < 125f)
                continue;

            if (Vector2.Distance(position, ruins) < 90f)
                continue;

            if (Vector2.Distance(position, banditCamp) < 90f)
                continue;

            if (DistanceToRiver(x, z) < 45f)
                continue;

            if (DistanceToRoad(position) < 18f)
                continue;

            float steepness = terrain.terrainData.GetSteepness(
                (x + mapSize / 2f) / mapSize,
                (z + mapSize / 2f) / mapSize
            );

            if (steepness > 30f)
                continue;

            CreateTree(x, z);
        }

        for (int i = 0; i < rockCount; i++)
        {
            float x = RandomCoordinate();
            float z = RandomCoordinate();

            if (DistanceToRiver(x, z) < 40f)
                continue;

            CreateRock(x, z);
        }
    }

    float RandomCoordinate()
    {
        return (float)random.NextDouble() * mapSize
            - mapSize / 2f;
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
            minimum = Mathf.Min(
                minimum,
                Vector2.Distance(point, p)
            );
        }

        return minimum;
    }

    void CreateTree(float x, float z)
    {
        Vector3 position = new Vector3(
            x, GetGroundHeight(x, z), z
        );

        if (treePrefab != null)
        {
            GameObject tree = Instantiate(
                treePrefab,
                position,
                Quaternion.Euler(0, random.Next(360), 0),
                worldRoot
            );

            tree.transform.localScale *=
                Random.Range(0.8f, 1.4f);
        }
        else
        {
            GameObject trunk = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder
            );

            trunk.name = "Placeholder_Tree";
            trunk.transform.SetParent(worldRoot);
            trunk.transform.position =
                position + Vector3.up * 4f;

            trunk.transform.localScale =
                new Vector3(0.8f, 4f, 0.8f);

            GameObject leaves = GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

            leaves.transform.SetParent(trunk.transform);
            leaves.transform.localPosition =
                new Vector3(0, 1.3f, 0);

            leaves.transform.localScale =
                new Vector3(3f, 2.5f, 3f);

            DestroyImmediate(
                trunk.GetComponent<Collider>()
            );

            DestroyImmediate(
                leaves.GetComponent<Collider>()
            );
        }
    }

    void CreateRock(float x, float z)
    {
        Vector3 position = new Vector3(
            x, GetGroundHeight(x, z), z
        );

        GameObject rock;

        if (rockPrefab != null)
        {
            rock = Instantiate(
                rockPrefab,
                position,
                Quaternion.identity,
                worldRoot
            );
        }
        else
        {
            rock = GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

            rock.transform.SetParent(worldRoot);
            rock.transform.position = position;
        }

        rock.name = "World_Rock";

        rock.transform.localScale = new Vector3(
            Random.Range(1f, 4f),
            Random.Range(1f, 3f),
            Random.Range(1f, 4f)
        );
    }

    void CreateBuilding(
        string objectName,
        Vector2 location,
        GameObject prefab,
        Vector3 size)
    {
        Vector3 position = new Vector3(
            location.x,
            GetGroundHeight(location.x, location.y),
            location.y
        );

        GameObject building;

        if (prefab != null)
        {
            building = Instantiate(
                prefab,
                position,
                Quaternion.identity,
                worldRoot
            );
        }
        else
        {
            building = GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

            building.transform.SetParent(worldRoot);
            building.transform.position =
                position + Vector3.up * size.y / 2f;

            building.transform.localScale = size;
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
