using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

namespace TheAshenCrown.EditorTools
{
    /// <summary>
    /// Procedurally builds authentic 3D OBJ models for Alpine Pines, Glacial Boulders,
    /// and Thatched Medieval Cottages with persistent URP materials saved to disk so they are never pink!
    /// </summary>
    public static class AlpineAssetBuilder
    {
        private const string ModelsDir = "Assets/_TheAshenCrown/Art/Models";
        private const string PrefabsDir = "Assets/_TheAshenCrown/Prefabs";
        private const string MaterialsDir = "Assets/_TheAshenCrown/Art/Materials";

        [MenuItem("The Ashen Crown/Build Real 3D Alpine Assets & Apply")]
        public static void BuildAllAssets()
        {
            if (!Directory.Exists(ModelsDir)) Directory.CreateDirectory(ModelsDir);
            if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);
            if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);

            string pineObjPath = Path.Combine(ModelsDir, "Alpine_Spruce_Pine.obj");
            string rockObjPath = Path.Combine(ModelsDir, "Glacial_Mountain_Boulder.obj");
            string houseObjPath = Path.Combine(ModelsDir, "Medieval_Timber_Cottage.obj");

            File.WriteAllText(pineObjPath, GeneratePineTreeOBJ());
            File.WriteAllText(rockObjPath, GenerateFracturedBoulderOBJ());
            File.WriteAllText(houseObjPath, GenerateMedievalCottageOBJ());

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // Create persistent disk materials
            Material barkMat = GetOrCreateMaterial("Mat_PineBark.mat", new Color(0.24f, 0.15f, 0.08f), null, 0.12f, false);
            
            Texture2D needleTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/GrassBushyThick_Albedo.tif");
            Material needleMat = GetOrCreateMaterial("Mat_PineNeedles.mat", new Color(0.12f, 0.28f, 0.14f), needleTex, 0.20f, true);

            Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SourceFiles/Textures/Repeating Tiles/Moon_Albedo.png");
            Material rockMat = GetOrCreateMaterial("Mat_GlacialGranite.mat", new Color(0.42f, 0.43f, 0.45f), rockTex, 0.15f, false);

            Material wallMat = GetOrCreateMaterial("Mat_CottageWall.mat", new Color(0.58f, 0.53f, 0.45f), null, 0.10f, false);
            Material thatchMat = GetOrCreateMaterial("Mat_ThatchedRoof.mat", new Color(0.36f, 0.26f, 0.16f), null, 0.15f, false);

            // Create or update Prefabs with persistent materials
            GameObject pinePrefab = CreatePinePrefab(pineObjPath, barkMat, needleMat);
            GameObject rockPrefab = CreateRockPrefab(rockObjPath, rockMat);
            GameObject housePrefab = CreateHousePrefab(houseObjPath, wallMat, thatchMat);

            // Auto-assign to VareliaWorldGenerator in scene
            VareliaWorldGenerator generator = Object.FindFirstObjectByType<VareliaWorldGenerator>();
            if (generator != null)
            {
                Undo.RecordObject(generator, "Assign Real 3D Prefabs");
                generator.customTreePrefab = pinePrefab;
                generator.customRockPrefab = rockPrefab;
                generator.customHousePrefab = housePrefab;
                EditorUtility.SetDirty(generator);

                // Regenerate world with real models!
                generator.GenerateWorld();
                Debug.Log("[AlpineAssetBuilder] Real 3D Pine Trees, Boulders, and Thatched Cottages built and spawned with full materials across Varelia!");
            }
        }

        private static Material GetOrCreateMaterial(string fileName, Color baseColor, Texture2D mainTex, float smoothness, bool doubleSided)
        {
            string fullPath = Path.Combine(MaterialsDir, fileName);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(fullPath);

            if (mat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard");
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, fullPath);
            }

            mat.color = baseColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mainTex != null)
            {
                mat.mainTexture = mainTex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            // Double sided rendering for pine foliage so needles render from all camera angles
            if (doubleSided)
            {
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static string GeneratePineTreeOBJ()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Alpine Spruce Pine Tree OBJ - The Ashen Crown");
            sb.AppendLine("o AlpinePine");

            int vertOffset = 1;

            // 1. TRUNK (8-sided tapered cylinder, Y = 0 to 12.5m)
            sb.AppendLine("g Trunk");
            sb.AppendLine("usemtl TrunkMat");

            int sides = 8;
            int rings = 6;
            float totalHeight = 12.5f;

            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float y = t * totalHeight;
                float radius = Mathf.Lerp(0.55f, 0.06f, t);

                for (int s = 0; s < sides; s++)
                {
                    float angle = (float)s / sides * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    sb.AppendLine($"v {x:F3} {y:F3} {z:F3}");
                    sb.AppendLine($"vt {(float)s / sides:F3} {t * 3f:F3}");
                    sb.AppendLine($"vn {Mathf.Cos(angle):F3} 0.1 {Mathf.Sin(angle):F3}");
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int nextS = (s + 1) % sides;
                    int i0 = vertOffset + (r * sides) + s;
                    int i1 = vertOffset + (r * sides) + nextS;
                    int i2 = vertOffset + ((r + 1) * sides) + nextS;
                    int i3 = vertOffset + ((r + 1) * sides) + s;

                    sb.AppendLine($"f {i0}/{i0}/{i0} {i1}/{i1}/{i1} {i2}/{i2}/{i2}");
                    sb.AppendLine($"f {i0}/{i0}/{i0} {i2}/{i2}/{i2} {i3}/{i3}/{i3}");
                }
            }

            vertOffset += (rings + 1) * sides;

            // 2. CONICAL PINE BRANCH TIERS (8 Tiers from bottom to top, tapering outwards)
            sb.AppendLine("g PineFoliage");
            sb.AppendLine("usemtl NeedleMat");

            int tiers = 8;
            for (int tier = 0; tier < tiers; tier++)
            {
                float tierProgress = (float)tier / (tiers - 1);
                float tierY = Mathf.Lerp(2.2f, 11.8f, tierProgress);
                float tierRadius = Mathf.Lerp(3.8f, 0.7f, tierProgress);
                int branchesInTier = 7;

                for (int b = 0; b < branchesInTier; b++)
                {
                    float baseAngle = ((float)b / branchesInTier * Mathf.PI * 2f) + (tier * 0.45f);
                    float branchTilt = -0.32f;

                    Vector3 root = new Vector3(Mathf.Cos(baseAngle) * 0.25f, tierY, Mathf.Sin(baseAngle) * 0.25f);
                    Vector3 tip = root + new Vector3(Mathf.Cos(baseAngle) * tierRadius, branchTilt * tierRadius, Mathf.Sin(baseAngle) * tierRadius);
                    Vector3 lateral = Vector3.Cross(tip - root, Vector3.up).normalized * (tierRadius * 0.28f);

                    Vector3 p0 = root - lateral * 0.4f;
                    Vector3 p1 = root + lateral * 0.4f;
                    Vector3 p2 = tip + lateral;
                    Vector3 p3 = tip - lateral;

                    sb.AppendLine($"v {p0.x:F3} {p0.y:F3} {p0.z:F3}");
                    sb.AppendLine($"v {p1.x:F3} {p1.y:F3} {p1.z:F3}");
                    sb.AppendLine($"v {p2.x:F3} {p2.y:F3} {p2.z:F3}");
                    sb.AppendLine($"v {p3.x:F3} {p3.y:F3} {p3.z:F3}");

                    sb.AppendLine("vt 0 0");
                    sb.AppendLine("vt 1 0");
                    sb.AppendLine("vt 1 1");
                    sb.AppendLine("vt 0 1");

                    sb.AppendLine("vn 0 0.8 0.2");
                    sb.AppendLine("vn 0 0.8 0.2");
                    sb.AppendLine("vn 0 0.8 0.2");
                    sb.AppendLine("vn 0 0.8 0.2");

                    int idx0 = vertOffset;
                    int idx1 = vertOffset + 1;
                    int idx2 = vertOffset + 2;
                    int idx3 = vertOffset + 3;

                    sb.AppendLine($"f {idx0}/{idx0}/{idx0} {idx1}/{idx1}/{idx1} {idx2}/{idx2}/{idx2}");
                    sb.AppendLine($"f {idx0}/{idx0}/{idx0} {idx2}/{idx2}/{idx2} {idx3}/{idx3}/{idx3}");
                    sb.AppendLine($"f {idx2}/{idx2}/{idx2} {idx1}/{idx1}/{idx1} {idx0}/{idx0}/{idx0}");
                    sb.AppendLine($"f {idx3}/{idx3}/{idx3} {idx2}/{idx2}/{idx2} {idx0}/{idx0}/{idx0}");

                    vertOffset += 4;
                }
            }

            return sb.ToString();
        }

        private static string GenerateFracturedBoulderOBJ()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Alpine Glacial Boulder OBJ - The Ashen Crown");
            sb.AppendLine("o GlacialBoulder");
            sb.AppendLine("g Rock");
            sb.AppendLine("usemtl RockMat");

            Vector3[] verts =
            {
                new Vector3(-1.8f, 0.0f, -1.5f), new Vector3(1.6f, 0.0f, -1.4f),
                new Vector3(1.7f, 0.0f, 1.5f), new Vector3(-1.5f, 0.0f, 1.6f),
                new Vector3(-2.1f, 1.2f, -0.6f), new Vector3(1.9f, 1.1f, -0.7f),
                new Vector3(1.5f, 1.4f, 1.3f), new Vector3(-1.4f, 1.3f, 1.4f),
                new Vector3(-0.9f, 2.3f, -0.5f), new Vector3(1.1f, 2.4f, -0.2f),
                new Vector3(0.5f, 2.2f, 1.0f), new Vector3(-0.6f, 2.1f, 0.9f),
                new Vector3(0.1f, 2.8f, 0.2f)
            };

            foreach (var v in verts)
            {
                sb.AppendLine($"v {v.x:F3} {v.y:F3} {v.z:F3}");
                sb.AppendLine($"vt {v.x * 0.4f:F3} {v.z * 0.4f:F3}");
                sb.AppendLine($"vn {v.normalized.x:F3} {v.normalized.y:F3} {v.normalized.z:F3}");
            }

            int[][] faces =
            {
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 4, 8, 7 }, new[] { 4, 1, 5, 8 },
                new[] { 5, 6, 10, 9 }, new[] { 6, 7, 11, 10 }, new[] { 7, 8, 12, 11 }, new[] { 8, 5, 9, 12 },
                new[] { 9, 10, 13 }, new[] { 10, 11, 13 }, new[] { 11, 12, 13 }, new[] { 12, 9, 13 }
            };

            foreach (var f in faces)
            {
                if (f.Length == 4)
                {
                    sb.AppendLine($"f {f[0]}/{f[0]}/{f[0]} {f[1]}/{f[1]}/{f[1]} {f[2]}/{f[2]}/{f[2]}");
                    sb.AppendLine($"f {f[0]}/{f[0]}/{f[0]} {f[2]}/{f[2]}/{f[2]} {f[3]}/{f[3]}/{f[3]}");
                }
                else
                {
                    sb.AppendLine($"f {f[0]}/{f[0]}/{f[0]} {f[1]}/{f[1]}/{f[1]} {f[2]}/{f[2]}/{f[2]}");
                }
            }

            return sb.ToString();
        }

        private static string GenerateMedievalCottageOBJ()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Medieval Thatched Cottage OBJ - The Ashen Crown");
            sb.AppendLine("o MedievalCottage");

            float w = 5.0f;
            float l = 7.0f;
            float wallH = 3.2f;
            float roofPeak = 6.4f;

            sb.AppendLine("g CottageWalls");
            sb.AppendLine("usemtl WallMat");

            Vector3[] wallVerts =
            {
                new Vector3(-w*0.5f, 0, -l*0.5f), new Vector3(w*0.5f, 0, -l*0.5f),
                new Vector3(w*0.5f, 0, l*0.5f), new Vector3(-w*0.5f, 0, l*0.5f),
                new Vector3(-w*0.5f, wallH, -l*0.5f), new Vector3(w*0.5f, wallH, -l*0.5f),
                new Vector3(w*0.5f, wallH, l*0.5f), new Vector3(-w*0.5f, wallH, l*0.5f),
                new Vector3(0, roofPeak, -l*0.5f), new Vector3(0, roofPeak, l*0.5f)
            };

            foreach (var v in wallVerts)
            {
                sb.AppendLine($"v {v.x:F3} {v.y:F3} {v.z:F3}");
                sb.AppendLine($"vt {v.x*0.3f:F3} {v.y*0.3f:F3}");
                sb.AppendLine("vn 0 0.5 0.5");
            }

            sb.AppendLine("f 1/1/1 2/2/2 6/6/6"); sb.AppendLine("f 1/1/1 6/6/6 5/5/5");
            sb.AppendLine("f 2/2/2 3/3/3 7/7/7"); sb.AppendLine("f 2/2/2 7/7/7 6/6/6");
            sb.AppendLine("f 3/3/3 4/4/4 8/8/8"); sb.AppendLine("f 3/3/3 8/8/8 7/7/7");
            sb.AppendLine("f 4/4/4 1/1/1 5/5/5"); sb.AppendLine("f 4/4/4 5/5/5 8/8/8");
            sb.AppendLine("f 5/5/5 6/6/6 9/9/9");
            sb.AppendLine("f 8/8/8 10/10/10 7/7/7");

            sb.AppendLine("g ThatchedRoof");
            sb.AppendLine("usemtl ThatchedRoofMat");
            int off = 11;

            float eW = (w * 0.5f) + 0.65f;
            float eL = (l * 0.5f) + 0.65f;
            float eH = wallH - 0.2f;

            Vector3[] roofVerts =
            {
                new Vector3(-eW, eH, -eL), new Vector3(0, roofPeak + 0.35f, -eL),
                new Vector3(0, roofPeak + 0.35f, eL), new Vector3(-eW, eH, eL),
                new Vector3(eW, eH, -eL), new Vector3(eW, eH, eL)
            };

            foreach (var v in roofVerts)
            {
                sb.AppendLine($"v {v.x:F3} {v.y:F3} {v.z:F3}");
                sb.AppendLine($"vt {v.x*0.4f:F3} {v.z*0.4f:F3}");
                sb.AppendLine("vn 0 0.8 0.2");
            }

            sb.AppendLine($"f {off}/{off}/{off} {off+1}/{off+1}/{off+1} {off+2}/{off+2}/{off+2}");
            sb.AppendLine($"f {off}/{off}/{off} {off+2}/{off+2}/{off+2} {off+3}/{off+3}/{off+3}");
            sb.AppendLine($"f {off+1}/{off+1}/{off+1} {off+4}/{off+4}/{off+4} {off+5}/{off+5}/{off+5}");
            sb.AppendLine($"f {off+1}/{off+1}/{off+1} {off+5}/{off+5}/{off+5} {off+2}/{off+2}/{off+2}");

            return sb.ToString();
        }

        private static GameObject CreatePinePrefab(string objPath, Material barkMat, Material needleMat)
        {
            GameObject objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            string prefabPath = Path.Combine(PrefabsDir, "Prefab_AlpineSprucePine.prefab");

            GameObject instance = Object.Instantiate(objAsset);
            instance.name = "Prefab_AlpineSprucePine";

            MeshRenderer mr = instance.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterials = new Material[] { barkMat, needleMat };
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateRockPrefab(string objPath, Material rockMat)
        {
            GameObject objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            string prefabPath = Path.Combine(PrefabsDir, "Prefab_GlacialBoulder.prefab");

            GameObject instance = Object.Instantiate(objAsset);
            instance.name = "Prefab_GlacialBoulder";

            MeshRenderer mr = instance.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = rockMat;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static GameObject CreateHousePrefab(string objPath, Material wallMat, Material thatchMat)
        {
            GameObject objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            string prefabPath = Path.Combine(PrefabsDir, "Prefab_ThatchedCottage.prefab");

            GameObject instance = Object.Instantiate(objAsset);
            instance.name = "Prefab_ThatchedCottage";

            MeshRenderer mr = instance.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterials = new Material[] { wallMat, thatchMat };
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }
    }
}
