using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VareliaWorldGenerator))]
public class VareliaWorldGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        VareliaWorldGenerator generator = (VareliaWorldGenerator)target;

        GUILayout.Space(15);
        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
        if (GUILayout.Button("GENERATE VARELIA WORLD", GUILayout.Height(40)))
        {
            generator.GenerateWorld();
        }
        GUI.backgroundColor = Color.white;
    }

    [MenuItem("The Ashen Crown/Generate Varelia World (2km World)")]
    public static void GenerateVareliaMenu()
    {
        VareliaWorldGenerator gen = Object.FindFirstObjectByType<VareliaWorldGenerator>();
        if (gen == null)
        {
            GameObject go = new GameObject("Varelia_World_Manager");
            gen = go.AddComponent<VareliaWorldGenerator>();
        }

        gen.GenerateWorld();
        Selection.activeGameObject = gen.gameObject;
    }
}
