using UnityEngine;
using UnityEditor;

public class CreateShaderIncluderPrefab : EditorWindow
{
    [MenuItem("Tutorial/Create Shader Includer Prefab")]
    public static void CreatePrefab()
    {
        // Create folder if needed
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Materials");
        }
        
        // Create the GameObject
        GameObject go = new GameObject("ShaderIncluder");
        ShaderIncluder includer = go.AddComponent<ShaderIncluder>();
        
        // Find and assign shaders
        Shader moleculeUnlit = Shader.Find("Custom/MoleculeUnlit");
        Shader unlitColor = Shader.Find("Unlit/Color");
        Shader standard = Shader.Find("Standard");
        Shader mobileDiffuse = Shader.Find("Mobile/Diffuse");
        
        // Build shader array
        var shaderList = new System.Collections.Generic.List<Shader>();
        if (moleculeUnlit != null) shaderList.Add(moleculeUnlit);
        if (unlitColor != null) shaderList.Add(unlitColor);
        if (standard != null) shaderList.Add(standard);
        if (mobileDiffuse != null) shaderList.Add(mobileDiffuse);
        
        includer.shadersToInclude = shaderList.ToArray();
        
        // Create materials that reference these shaders
        var materialList = new System.Collections.Generic.List<Material>();
        
        // Create a material for each important color using the best available shader
        Shader bestShader = moleculeUnlit ?? unlitColor ?? standard;
        if (bestShader != null)
        {
            Material blackMat = new Material(bestShader);
            blackMat.color = Color.black;
            blackMat.name = "ShaderRef_Black";
            AssetDatabase.CreateAsset(blackMat, "Assets/Resources/Materials/ShaderRef_Black.mat");
            materialList.Add(blackMat);
            
            Material carbonMat = new Material(bestShader);
            carbonMat.color = new Color(0.3f, 0.3f, 0.3f);
            carbonMat.name = "ShaderRef_Carbon";
            AssetDatabase.CreateAsset(carbonMat, "Assets/Resources/Materials/ShaderRef_Carbon.mat");
            materialList.Add(carbonMat);
            
            Material hydrogenMat = new Material(bestShader);
            hydrogenMat.color = new Color(0.85f, 0.85f, 0.85f);
            hydrogenMat.name = "ShaderRef_Hydrogen";
            AssetDatabase.CreateAsset(hydrogenMat, "Assets/Resources/Materials/ShaderRef_Hydrogen.mat");
            materialList.Add(hydrogenMat);
            
            Material oxygenMat = new Material(bestShader);
            oxygenMat.color = new Color(0.9f, 0.2f, 0.2f);
            oxygenMat.name = "ShaderRef_Oxygen";
            AssetDatabase.CreateAsset(oxygenMat, "Assets/Resources/Materials/ShaderRef_Oxygen.mat");
            materialList.Add(oxygenMat);
            
            Material nitrogenMat = new Material(bestShader);
            nitrogenMat.color = new Color(0.2f, 0.4f, 0.9f);
            nitrogenMat.name = "ShaderRef_Nitrogen";
            AssetDatabase.CreateAsset(nitrogenMat, "Assets/Resources/Materials/ShaderRef_Nitrogen.mat");
            materialList.Add(nitrogenMat);
        }
        
        includer.materialsToInclude = materialList.ToArray();
        
        // Save as prefab
        string prefabPath = "Assets/Resources/Prefabs/ShaderIncluder.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Shader Includer Created", 
            "ShaderIncluder prefab created!\n\n" +
            "Add this to your scene to ensure shaders are included in standalone builds.\n\n" +
            "Path: " + prefabPath, 
            "OK");
    }
    
    [MenuItem("Tutorial/Create Materials Folder")]
    public static void CreateMaterialsFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Materials");
        }
        AssetDatabase.Refresh();
    }
}
