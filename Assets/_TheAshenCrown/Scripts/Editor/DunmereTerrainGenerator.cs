using UnityEngine;
using UnityEditor;

namespace TheAshenCrown.EditorTools
{
    /// <summary>
    /// Far Cry 3 style Island & Ocean Generator for The Ashen Crown.
    /// Creates an organic 1km x 1km island surrounded by the open sea with:
    /// - Organic sandy beaches, coves, and peninsulas
    /// - Lush coastal jungle plateaus and valleys
    /// - Towering central volcanic / limestone ridges and cliffs
    /// - Full 3-layer auto-texturing (Beach Sand -> Jungle Grass -> Cliff Rock)
    /// - Auto-created tropical ocean water plane
    /// </summary>
    public class DunmereTerrainGenerator : EditorWindow
    {
        [Header("Island Dimensions")]
        private float terrainWidth = 1000f;
        private float terrainLength = 1000f;
        private float maxElevation = 180f;
        private float seaLevel = 10f;

        [Header("Island Topography")]
        private int seed = 777;
        private float islandShapeNoiseScale = 0.003f;
        private float mountainRidgeFrequency = 0.005f;
        private float mountainIntensity = 0.75f;
        private float beachWidth = 24f;

        [MenuItem("The Ashen Crown/Generate Far Cry 3 Style Island")]
        public static void QuickGenerateIsland()
        {
            var generator = CreateInstance<DunmereTerrainGenerator>();
            generator.GenerateIslandWorld();
            DestroyImmediate(generator);
        }

        [MenuItem("The Ashen Crown/Island Generator Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<DunmereTerrainGenerator>("Island Generator");
            window.minSize = new Vector2(360, 480);
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("THE ASHEN CROWN — ISLAND GENERATOR", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Generates a Far Cry 3 style tropical/medieval island surrounded by the sea with organic coves, beaches, jungle valleys, and mountain peaks.", MessageType.Info);

            GUILayout.Space(10);
            GUILayout.Label("Island Dimensions", EditorStyles.boldLabel);
            terrainWidth = EditorGUILayout.FloatField("Map Width (meters)", terrainWidth);
            terrainLength = EditorGUILayout.FloatField("Map Length (meters)", terrainLength);
            maxElevation = EditorGUILayout.FloatField("Peak Elevation (meters)", maxElevation);
            seaLevel = EditorGUILayout.Slider("Sea Level", seaLevel, 5f, 25f);

            GUILayout.Space(10);
            GUILayout.Label("Topography & Shape", EditorStyles.boldLabel);
            seed = EditorGUILayout.IntField("Random Seed", seed);
            mountainIntensity = EditorGUILayout.Slider("Mountain Heights", mountainIntensity, 0.4f, 1.2f);
            beachWidth = EditorGUILayout.Slider("Beach Flatness Zone", beachWidth, 10f, 50f);

            GUILayout.Space(20);
            GUI.backgroundColor = new Color(0.1f, 0.65f, 0.9f);
            if (GUILayout.Button("GENERATE ISLAND & OCEAN", GUILayout.Height(42)))
            {
                GenerateIslandWorld();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(8);
            if (GUILayout.Button("Randomize Seed & Regenerate", GUILayout.Height(28)))
            {
                seed = Random.Range(100, 99999);
                GenerateIslandWorld();
            }
        }

        public void GenerateIslandWorld()
        {
            Terrain terrain = FindOrCreateTerrain();
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Generate Island Landscape");

            TerrainData tData = terrain.terrainData;
            tData.size = new Vector3(terrainWidth, maxElevation, terrainLength);

            int resolution = tData.heightmapResolution;
            float[,] heights = new float[resolution, resolution];

            Vector2 center = new Vector2(resolution * 0.5f, resolution * 0.5f);
            float maxRadius = resolution * 0.44f;

            Random.InitState(seed);
            float seedOffsetX = Random.Range(0f, 9999f);
            float seedOffsetY = Random.Range(0f, 9999f);

            float seaLevelNormalized = seaLevel / maxElevation;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float currentDist = Vector2.Distance(new Vector2(x, y), center);

                    // Organic coastline distortion (creates coves, bays, and natural peninsulas)
                    float coastAngle = Mathf.Atan2(y - center.y, x - center.x);
                    float coastWarp = Mathf.PerlinNoise(Mathf.Cos(coastAngle) * 2.2f + seedOffsetX, Mathf.Sin(coastAngle) * 2.2f + seedOffsetY) * 0.28f;
                    coastWarp += Mathf.PerlinNoise((x * islandShapeNoiseScale) + seedOffsetX, (y * islandShapeNoiseScale) + seedOffsetY) * 0.15f;

                    float effectiveRadius = maxRadius * (0.85f + coastWarp);

                    // Distance falloff from island center to open sea (1 = center, 0 = submerged in sea)
                    float islandMask = Mathf.Clamp01(1f - (currentDist / effectiveRadius));
                    // Smooth S-curve transition into ocean
                    islandMask = Mathf.SmoothStep(0f, 1f, islandMask);

                    if (islandMask <= 0.001f)
                    {
                        // Completely underwater in the deep sea
                        heights[y, x] = 0f;
                        continue;
                    }

                    // Interior Mountain Ridges (Layer 1: Broad mountain masses)
                    float mRidge = Mathf.PerlinNoise((x * mountainRidgeFrequency) + seedOffsetX + 100f, (y * mountainRidgeFrequency) + seedOffsetY + 100f);

                    // Interior Ridges (Layer 2: Sharp alpine ridge noise)
                    float sharpRidge = 1f - Mathf.Abs(mRidge * 2f - 1f); // Ridged multifractal
                    sharpRidge = Mathf.Pow(sharpRidge, 2.0f);

                    // Rolling jungle hills (Layer 3)
                    float jungleHills = Mathf.PerlinNoise((x * 0.012f) + seedOffsetX + 200f, (y * 0.012f) + seedOffsetY + 200f) * 0.12f;

                    // Combine interior heights
                    float interiorHeight = (mRidge * 0.4f + sharpRidge * 0.5f + jungleHills) * mountainIntensity;

                    // Sandy coastal beach shelf (gentle rise just above sea level)
                    float beachShelf = Mathf.Lerp(seaLevelNormalized * 1.05f, 0.45f, islandMask);

                    float totalHeight = Mathf.Lerp(0f, beachShelf + interiorHeight * 0.6f, islandMask);

                    heights[y, x] = Mathf.Clamp01(totalHeight);
                }
            }

            tData.SetHeights(0, 0, heights);

            // Center the terrain
            terrain.transform.position = new Vector3(-terrainWidth * 0.5f, 0f, -terrainLength * 0.5f);

            // Setup Far Cry 3 Layers (Sand -> Grass -> Rock)
            SetupIslandLayers(tData);
            AutoPaintIslandSplatmap(tData, seaLevelNormalized);

            // Create or position Ocean Water Plane
            SetupOceanWater(terrain);

            // Optimize for UHD 620
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 350f;
            terrain.detailObjectDistance = 80f;

            // Place player on the sunny beach looking at the island
            PlacePlayerOnBeach(terrain, seaLevel);

            EditorUtility.SetDirty(tData);
            Debug.Log("[DunmereTerrainGenerator] Far Cry 3 Island & Ocean generated successfully!");
        }

        private Terrain FindOrCreateTerrain()
        {
            Terrain terrain = FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                TerrainData tData = new TerrainData
                {
                    heightmapResolution = 513,
                    size = new Vector3(terrainWidth, maxElevation, terrainLength)
                };

                GameObject terrainObj = Terrain.CreateTerrainGameObject(tData);
                terrainObj.name = "FarCry_IslandTerrain";
                terrain = terrainObj.GetComponent<Terrain>();
            }

            return terrain;
        }

        private void SetupIslandLayers(TerrainData tData)
        {
            Texture2D sandTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Albedo.tif");
            Texture2D sandNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/SandWavey_Normal.tif");

            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Albedo.tif");
            Texture2D grassNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Normal.tif");

            Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Albedo.png");
            Texture2D rockNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Normal.png");

            TerrainLayer sandLayer = new TerrainLayer
            {
                name = "Layer_BeachSand",
                diffuseTexture = sandTex,
                normalMapTexture = sandNormal,
                tileSize = new Vector2(8f, 8f)
            };

            TerrainLayer grassLayer = new TerrainLayer
            {
                name = "Layer_JungleGrass",
                diffuseTexture = grassTex,
                normalMapTexture = grassNormal,
                tileSize = new Vector2(10f, 10f)
            };

            TerrainLayer rockLayer = new TerrainLayer
            {
                name = "Layer_CliffRock",
                diffuseTexture = rockTex,
                normalMapTexture = rockNormal,
                tileSize = new Vector2(18f, 18f)
            };

            tData.terrainLayers = new TerrainLayer[] { sandLayer, grassLayer, rockLayer };
        }

        private void AutoPaintIslandSplatmap(TerrainData tData, float seaLevelNorm)
        {
            int alphaW = tData.alphamapWidth;
            int alphaH = tData.alphamapHeight;
            float[,,] splat = new float[alphaH, alphaW, 3];

            float beachUpperLimit = seaLevelNorm + 0.045f; // Beach ends ~8m above sea level

            for (int y = 0; y < alphaH; y++)
            {
                for (int x = 0; x < alphaW; x++)
                {
                    float normX = (float)x / (alphaW - 1);
                    float normY = (float)y / (alphaH - 1);

                    float heightNorm = tData.GetInterpolatedHeight(normX, normY) / maxElevation;
                    float steepness = tData.GetSteepness(normX, normY);

                    float sandWeight = 0f;
                    float grassWeight = 0f;
                    float rockWeight = 0f;

                    // 1. Beach Zone (Around sea level)
                    if (heightNorm <= beachUpperLimit)
                    {
                        sandWeight = 1f;
                    }
                    else
                    {
                        // 2. Steep slopes become mountain cliff rock
                        if (steepness > 26f)
                        {
                            rockWeight = Mathf.InverseLerp(24f, 42f, steepness);
                            grassWeight = 1f - rockWeight;
                        }
                        else
                        {
                            // Lush inland grassland
                            grassWeight = 1f;
                        }

                        // Soft blend between beach sand and jungle grass
                        float sandTransition = Mathf.InverseLerp(beachUpperLimit + 0.03f, beachUpperLimit, heightNorm);
                        sandWeight = sandTransition;
                        grassWeight *= (1f - sandTransition);
                        rockWeight *= (1f - sandTransition);
                    }

                    splat[y, x, 0] = sandWeight;
                    splat[y, x, 1] = grassWeight;
                    splat[y, x, 2] = rockWeight;
                }
            }

            tData.SetAlphamaps(0, 0, splat);
        }

        private void SetupOceanWater(Terrain terrain)
        {
            GameObject oceanObj = GameObject.Find("Ocean_WaterPlane");
            if (oceanObj == null)
            {
                oceanObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                oceanObj.name = "Ocean_WaterPlane";
            }

            // Cover 2.5km of open sea around the island
            oceanObj.transform.position = new Vector3(0f, seaLevel, 0f);
            oceanObj.transform.localScale = new Vector3(250f, 1f, 250f);

            // Apply tropical AquaMarine material
            Material waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Material_Simple_AquaMarine.mat");
            if (waterMat != null)
            {
                oceanObj.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
            }

            // Remove box/mesh collider so player can swim/walk into water
            Collider col = oceanObj.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void PlacePlayerOnBeach(Terrain terrain, float seaHeight)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                // Place player on a sunny beach near the shore
                Vector3 beachSpawn = new Vector3(0f, 0f, -280f);
                float surfaceY = terrain.SampleHeight(beachSpawn);
                player.transform.position = new Vector3(beachSpawn.x, Mathf.Max(surfaceY, seaHeight) + 1.2f, beachSpawn.z);
            }
        }
    }
}
