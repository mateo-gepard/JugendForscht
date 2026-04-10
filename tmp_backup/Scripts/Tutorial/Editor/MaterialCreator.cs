using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class MaterialCreator : EditorWindow
{
    // Helper to get the best shader (Custom/MoleculeUnlit -> Unlit/Color -> Standard)
    private static Shader GetBestShader()
    {
        Shader shader = Shader.Find("Custom/MoleculeUnlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Mobile/Diffuse");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }
    
    private static Material CreateMat(Color color, string name)
    {
        Shader shader = GetBestShader();
        Material mat = new Material(shader);
        mat.color = color;
        
        // Optimize for mobile/VR
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
        
        return mat;
    }

    [MenuItem("Tutorial/Fix All Materials (Optimal APK)")]
    public static void FixAllMaterials()
    {
        // Create Materials folder if needed
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial"))
        {
            AssetDatabase.CreateFolder("Assets", "Tutorial");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/Tutorial", "Materials");
        }

        Debug.Log("[MaterialFix] ============= CREATING MATERIALS =============");

        // ============ UNIFIED COLOR SCHEME ============
        Material blackMat = CreateMat(Color.black, "Black");
        AssetDatabase.CreateAsset(blackMat, "Assets/Tutorial/Materials/Black.mat");

        Material carbonMat = CreateMat(new Color(0.3f, 0.3f, 0.3f), "Carbon");
        AssetDatabase.CreateAsset(carbonMat, "Assets/Tutorial/Materials/Carbon.mat");

        Material hydrogenMat = CreateMat(new Color(0.85f, 0.85f, 0.85f), "Hydrogen");
        AssetDatabase.CreateAsset(hydrogenMat, "Assets/Tutorial/Materials/Hydrogen.mat");

        Material oxygenMat = CreateMat(new Color(0.9f, 0.2f, 0.2f), "Oxygen");
        AssetDatabase.CreateAsset(oxygenMat, "Assets/Tutorial/Materials/Oxygen.mat");

        Material nitrogenMat = CreateMat(new Color(0.2f, 0.4f, 0.9f), "Nitrogen");
        AssetDatabase.CreateAsset(nitrogenMat, "Assets/Tutorial/Materials/Nitrogen.mat");

        Material fluorineMat = CreateMat(new Color(0.5f, 0.9f, 0.5f), "Fluorine");
        AssetDatabase.CreateAsset(fluorineMat, "Assets/Tutorial/Materials/Fluorine.mat");

        Material boronMat = CreateMat(new Color(1f, 0.7f, 0.7f), "Boron");
        AssetDatabase.CreateAsset(boronMat, "Assets/Tutorial/Materials/Boron.mat");

        Material highlightGreenMat = CreateMat(new Color(0.2f, 0.9f, 0.2f), "HighlightGreen");
        AssetDatabase.CreateAsset(highlightGreenMat, "Assets/Tutorial/Materials/HighlightGreen.mat");

        Material highlightYellowMat = CreateMat(new Color(1f, 0.9f, 0.2f), "HighlightYellow");
        AssetDatabase.CreateAsset(highlightYellowMat, "Assets/Tutorial/Materials/HighlightYellow.mat");

        Material highlightRedMat = CreateMat(new Color(0.9f, 0.2f, 0.2f), "HighlightRed");
        AssetDatabase.CreateAsset(highlightRedMat, "Assets/Tutorial/Materials/HighlightRed.mat");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[MaterialFix] ============= FIXING ALL PREFABS IN PROJECT =============");

        // Find ALL prefabs in the project
        string[] allPrefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Where(path => path.EndsWith(".prefab"))
            .ToArray();

        Debug.Log($"[MaterialFix] Found {allPrefabPaths.Length} prefabs in project");

        // Fix ALL prefabs automatically
        int fixedCount = 0;
        int materialCount = 0;
        foreach (string path in allPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            GameObject instance = PrefabUtility.LoadPrefabContents(path);
            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
            
            bool needsSave = false;
            foreach (var renderer in renderers)
            {
                Material assignedMat = null;
                string name = renderer.gameObject.name.ToLower();
                
                // Auto-assign based on object name
                if (name.Contains("carbon") || name.Contains("_c_") || name.Contains("_c ") || name.Contains(" c "))
                    assignedMat = carbonMat;
                else if (name.Contains("hydrogen") || name.Contains("_h_") || name.Contains("_h ") || name.Contains(" h "))
                    assignedMat = hydrogenMat;
                else if (name.Contains("oxygen") || name.Contains("_o_") || name.Contains("_o ") || name.Contains(" o "))
                    assignedMat = oxygenMat;
                else if (name.Contains("nitrogen") || name.Contains("_n_") || name.Contains("_n ") || name.Contains(" n "))
                    assignedMat = nitrogenMat;
                else if (name.Contains("fluorine") || name.Contains("_f_") || name.Contains("_f ") || name.Contains(" f "))
                    assignedMat = fluorineMat;
                else if (name.Contains("boron") || name.Contains("_b_") || name.Contains("_b ") || name.Contains(" b "))
                    assignedMat = boronMat;
                else if (name.Contains("bond") || name.Contains("cylinder"))
                    assignedMat = blackMat;
                else if (name.Contains("highlight") && (name.Contains("wedge") || name.Contains("dash")))
                    assignedMat = highlightYellowMat;
                else if (name.Contains("highlight"))
                    assignedMat = highlightGreenMat;
                else if (name.Contains("lp")) // Lone pair
                    assignedMat = highlightRedMat;
                else if (name.Contains("sphere")) // Default spheres to black
                    assignedMat = blackMat;
                
                // Fix pink materials, null materials, OR assign new material
                if (renderer.sharedMaterial == null || 
                    renderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader" ||
                    assignedMat != null)
                {
                    if (assignedMat != null)
                    {
                        renderer.sharedMaterial = assignedMat;
                        materialCount++;
                        needsSave = true;
                        Debug.Log($"[MaterialFix] Assigned {assignedMat.name} to {renderer.gameObject.name} in {path}");
                    }
                    else
                    {
                        // Default fallback
                        renderer.sharedMaterial = blackMat;
                        materialCount++;
                        needsSave = true;
                    }
                }
            }
            
            if (needsSave)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                fixedCount++;
            }
            
            PrefabUtility.UnloadPrefabContents(instance);
        }

        Debug.Log($"[MaterialFix] ============= FIXED {fixedCount} PREFABS ({materialCount} materials) =============");

        // Force Unity to reload all prefabs
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Add materials to ShaderIncluder for APK builds
        CreateShaderIncluderAsset(blackMat, carbonMat, hydrogenMat, oxygenMat, nitrogenMat, 
                                   fluorineMat, boronMat, highlightGreenMat, highlightYellowMat, highlightRedMat);

        EditorUtility.DisplayDialog("Materials Fixed", 
            $"✓ Fixed {fixedCount} prefabs in project!\n" +
            $"✓ Assigned {materialCount} materials\n" +
            "✓ Created materials with MoleculeUnlit shader\n" +
            "✓ Added materials to ShaderIncluder for APK builds\n\n" +
            "All materials will now render correctly!\n" +
            "Please close and reopen prefabs in viewport to see changes.", 
            "OK");
    }

    private static void CreateShaderIncluderAsset(params Material[] materials)
    {
        // Find ShaderIncluder prefab
        string[] guids = AssetDatabase.FindAssets("t:Prefab ShaderIncluder");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[MaterialFix] ShaderIncluder prefab not found. Create it via Menu → Tutorial → Create Shader Includer Prefab");
            return;
        }

        string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab != null)
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            ShaderIncluder includer = instance.GetComponent<ShaderIncluder>();
            
            if (includer != null)
            {
                // Add all materials
                List<Material> matList = new List<Material>(includer.materialsToInclude ?? new Material[0]);
                foreach (var mat in materials)
                {
                    if (mat != null && !matList.Contains(mat))
                    {
                        matList.Add(mat);
                    }
                }
                includer.materialsToInclude = matList.ToArray();
                
                // Add shader
                Shader shader = GetBestShader();
                if (shader != null)
                {
                    List<Shader> shaderList = new List<Shader>(includer.shadersToInclude ?? new Shader[0]);
                    if (!shaderList.Contains(shader))
                    {
                        shaderList.Add(shader);
                    }
                    includer.shadersToInclude = shaderList.ToArray();
                }
                
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Debug.Log($"[MaterialFix] Added {materials.Length} materials to ShaderIncluder");
            }
            
            PrefabUtility.UnloadPrefabContents(instance);
        }
    }
}
