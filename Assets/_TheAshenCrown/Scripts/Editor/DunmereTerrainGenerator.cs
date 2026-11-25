using UnityEngine;
using UnityEditor;

namespace TheAshenCrown.EditorTools
{
    /// <summary>
    /// 1-Click Procedural Terrain Generator for The Ashen Crown.
    /// Creates realistic multi-octave rolling hills, protective mountain borders,
    /// a flat village plateau, and automatic slope-based texturing.
    /// </summary>
    public class DunmereTerrainGenerator : EditorWindow
    {
        [Header("Terrain Dimensions")]
        private float terrainWidth = 1000f;
        private float terrainLength = 1000f;
        private float terrainHeight = 220f;

        [Header("Landscape Shaping")]
        private float hillFrequency = 0.0035f;
        private float hillIntensity = 0.15f;
        private float mountainBorderHeight = 0.65f;
        private float centerFlatRadius = 90f;
        private int seed = 42;

        [Header("Texture Assignments (Optional)")]
        private Texture2D grassTexture;
        private Texture2D rockTexture;
        private Texture2D dirtTexture;

        [MenuItem("The Ashen Crown/Terrain Generator Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<DunmereTerrainGenerator>("Terrain Generator");
            window.minSize = new Vector2(360, 480);
            window.FindDefaultTextures();
        }

        [MenuItem("The Ashen Crown/Quick Generate Dunmere Landscape")]
        public static void QuickGenerate()
        {
            var generator = CreateInstance<DunmereTerrainGenerator>();
            generator.FindDefaultTextures();
            generator.GenerateWorld();
            DestroyImmediate(generator);
        }

        private void OnEnable()
        {
            FindDefaultTextures();
        }

        private void FindDefaultTextures()
        {
            if (grassTexture == null)
            {
                grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Albedo.tif");
            }
            if (rockTexture == null)
            {
                rockTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Albedo.png");
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("THE ASHEN CROWN — WORLD GENERATOR", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Generates an expansive 1km x 1km medieval landscape with natural hills, mountain barriers, and auto-painted terrain in seconds.", MessageType.Info);

            GUILayout.Space(10);
            GUILayout.Label("World Scale", EditorStyles.boldLabel);
            terrainWidth = EditorGUILayout.FloatField("Width (meters)", terrainWidth);
            terrainLength = EditorGUILayout.FloatField("Length (meters)", terrainLength);
            terrainHeight = EditorGUILayout.FloatField("Max Height (meters)", terrainHeight);

            GUILayout.Space(10);
            GUILayout.Label("Landscape Features", EditorStyles.boldLabel);
            seed = EditorGUILayout.IntField("Random Seed", seed);
            hillFrequency = EditorGUILayout.Slider("Hill Density", hillFrequency, 0.001f, 0.01f);
            hillIntensity = EditorGUILayout.Slider("Hill Height Factor", hillIntensity, 0.05f, 0.4f);
            mountainBorderHeight = EditorGUILayout.Slider("Border Mountains", mountainBorderHeight, 0.2f, 1.0f);
            centerFlatRadius = EditorGUILayout.Slider("Village Plateau Size", centerFlatRadius, 40f, 200f);

            GUILayout.Space(10);
            GUILayout.Label("Ground Textures", EditorStyles.boldLabel);
            grassTexture = (Texture2D)EditorGUILayout.ObjectField("Grass (Plains)", grassTexture, typeof(Texture2D), false);
            rockTexture = (Texture2D)EditorGUILayout.ObjectField("Rock (Cliffs)", rockTexture, typeof(Texture2D), false);
            dirtTexture = (Texture2D)EditorGUILayout.ObjectField("Dirt (Paths)", dirtTexture, typeof(Texture2D), false);

            GUILayout.Space(20);
            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.3f);
            if (GUILayout.Button("GENERATE WORLD (1-Click)", GUILayout.Height(42)))
            {
                GenerateWorld();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(5);
            if (GUILayout.Button("Randomize Seed & Regenerate", GUILayout.Height(28)))
            {
                seed = Random.Range(100, 99999);
                GenerateWorld();
            }
        }

        public void GenerateWorld()
        {
            Terrain terrain = FindOrCreateTerrain();
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Generate Dunmere Landscape");

            TerrainData tData = terrain.terrainData;
            tData.size = new Vector3(terrainWidth, terrainHeight, terrainLength);

            int resolution = tData.heightmapResolution;
            float[,] heights = new float[resolution, resolution];

            Vector2 center = new Vector2(resolution * 0.5f, resolution * 0.5f);
            float maxDistFromCenter = resolution * 0.5f;

            Random.InitState(seed);
            float offsetX = Random.Range(0f, 9999f);
            float offsetY = Random.Range(0f, 9999f);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Distance from center of map (0 = center, 1 = perimeter border)
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDistFromCenter;

                    // Layer 1: Broad rolling landscape
                    float broadNoise = Mathf.PerlinNoise((x * hillFrequency) + offsetX, (y * hillFrequency) + offsetY);

                    // Layer 2: Medium hills
                    float midNoise = Mathf.PerlinNoise((x * hillFrequency * 2.5f) + offsetX + 50f, (y * hillFrequency * 2.5f) + offsetY + 50f) * 0.35f;

                    // Layer 3: High-frequency surface ripples
                    float detailNoise = Mathf.PerlinNoise((x * hillFrequency * 6f) + offsetX + 100f, (y * hillFrequency * 6f) + offsetY + 100f) * 0.1f;

                    float compositeHeight = (broadNoise + midNoise + detailNoise) * hillIntensity;

                    // Mountain rim barrier around map edge (prevents player falling off world)
                    if (dist > 0.45f)
                    {
                        float rimFactor = Mathf.InverseLerp(0.45f, 0.95f, dist);
                        rimFactor = Mathf.Pow(rimFactor, 1.8f);
                        float mountainNoise = Mathf.PerlinNoise((x * 0.008f) + offsetX + 200f, (y * 0.008f) + offsetY + 200f);
                        compositeHeight += rimFactor * mountainBorderHeight * (0.6f + mountainNoise * 0.4f);
                    }

                    // Gentle flattening for Dunmere village plateau at the center
                    float villageDist = Vector2.Distance(new Vector2(x, y), center);
                    float villagePixelRadius = (centerFlatRadius / terrainWidth) * resolution;
                    if (villageDist < villagePixelRadius)
                    {
                        float blend = Mathf.SmoothStep(0f, 1f, villageDist / villagePixelRadius);
                        compositeHeight = Mathf.Lerp(0.06f, compositeHeight, blend);
                    }

                    heights[y, x] = Mathf.Clamp01(compositeHeight);
                }
            }

            tData.SetHeights(0, 0, heights);

            // Setup Auto-Texturing
            SetupTerrainLayers(tData);
            AutoPaintSlopeLayers(tData);

            // Center terrain transform
            terrain.transform.position = new Vector3(-terrainWidth * 0.5f, 0f, -terrainLength * 0.5f);

            // Optimize for Intel UHD 620
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 350f;
            terrain.detailObjectDistance = 80f;

            // Place player on the surface at center
            PlacePlayerAtCenter(terrain);

            EditorUtility.SetDirty(tData);
            Debug.Log("[DunmereTerrainGenerator] World generated successfully! Scale: 1km x 1km.");
        }

        private Terrain FindOrCreateTerrain()
        {
            Terrain terrain = FindFirstObjectByType<Terrain>();
            if (terrain == null)
            {
                TerrainData tData = new TerrainData
                {
                    heightmapResolution = 513,
                    size = new Vector3(terrainWidth, terrainHeight, terrainLength)
                };

                GameObject terrainObj = Terrain.CreateTerrainGameObject(tData);
                terrainObj.name = "Dunmere_KingdomTerrain";
                terrain = terrainObj.GetComponent<Terrain>();
            }

            return terrain;
        }

        private void SetupTerrainLayers(TerrainData tData)
        {
            FindDefaultTextures();

            TerrainLayer[] existingLayers = tData.terrainLayers;
            if (existingLayers != null && existingLayers.Length >= 2) return;

            TerrainLayer grassLayer = new TerrainLayer
            {
                name = "Layer_Grass",
                diffuseTexture = grassTexture,
                tileSize = new Vector2(10f, 10f)
            };

            TerrainLayer rockLayer = new TerrainLayer
            {
                name = "Layer_RockCliff",
                diffuseTexture = rockTexture,
                tileSize = new Vector2(15f, 15f)
            };

            tData.terrainLayers = new TerrainLayer[] { grassLayer, rockLayer };
        }

        private void AutoPaintSlopeLayers(TerrainData tData)
        {
            int alphaWidth = tData.alphamapWidth;
            int alphaHeight = tData.alphamapHeight;
            int layerCount = tData.terrainLayers.Length;

            if (layerCount < 2) return;

            float[,,] splatmapData = new float[alphaHeight, alphaWidth, layerCount];

            for (int y = 0; y < alphaHeight; y++)
            {
                for (int x = 0; x < alphaWidth; x++)
                {
                    float normX = (float)x / (alphaWidth - 1);
                    float normY = (float)y / (alphaHeight - 1);

                    // Compute physical slope steepness in degrees (0 to 90)
                    float steepness = tData.GetSteepness(normX, normY);

                    // If slope > 28 degrees, blend to rock; if flat, stay grass
                    float rockWeight = Mathf.InverseLerp(24f, 42f, steepness);
                    float grassWeight = 1f - rockWeight;

                    splatmapData[y, x, 0] = grassWeight;
                    splatmapData[y, x, 1] = rockWeight;
                }
            }

            tData.SetAlphamaps(0, 0, splatmapData);
        }

        private void PlacePlayerAtCenter(Terrain terrain)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Vector3 spawnPos = new Vector3(0f, 0f, 0f);
                float surfaceY = terrain.SampleHeight(spawnPos);
                player.transform.position = new Vector3(spawnPos.x, surfaceY + 1.2f, spawnPos.z);
            }
        }
    }
}
