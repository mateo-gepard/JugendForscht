using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

/// <summary>
/// Ensures shaders are included in builds and ShaderIncluder is in all scenes
/// </summary>
public class BuildSetup : EditorWindow
{
    [MenuItem("Tutorial/Setup Build (Fix Pink Materials)")]
    public static void SetupBuildForStandalone()
    {
        // Step 1: Add shaders to Always Included Shaders in Graphics Settings
        Debug.Log("[BuildSetup] ============= ADDING SHADERS TO GRAPHICS SETTINGS =============");
        
        // Get Graphics Settings asset
        var graphicsSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
        SerializedObject graphicsSettings = new SerializedObject(graphicsSettingsAsset);
        SerializedProperty alwaysIncludedShaders = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
        
        // Find shaders
        Shader moleculeUnlit = Shader.Find("Custom/MoleculeUnlit");
        Shader unlitColor = Shader.Find("Unlit/Color");
        Shader mobileDiffuse = Shader.Find("Mobile/Diffuse");
        Shader standard = Shader.Find("Standard");
        
        // Add shaders if not already present
        int addedShaders = 0;
        if (moleculeUnlit != null && !IsShaderInArray(alwaysIncludedShaders, moleculeUnlit))
        {
            alwaysIncludedShaders.InsertArrayElementAtIndex(alwaysIncludedShaders.arraySize);
            alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1).objectReferenceValue = moleculeUnlit;
            addedShaders++;
            Debug.Log("[BuildSetup] Added Custom/MoleculeUnlit to Always Included Shaders");
        }
        
        if (unlitColor != null && !IsShaderInArray(alwaysIncludedShaders, unlitColor))
        {
            alwaysIncludedShaders.InsertArrayElementAtIndex(alwaysIncludedShaders.arraySize);
            alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1).objectReferenceValue = unlitColor;
            addedShaders++;
            Debug.Log("[BuildSetup] Added Unlit/Color to Always Included Shaders");
        }
        
        if (mobileDiffuse != null && !IsShaderInArray(alwaysIncludedShaders, mobileDiffuse))
        {
            alwaysIncludedShaders.InsertArrayElementAtIndex(alwaysIncludedShaders.arraySize);
            alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1).objectReferenceValue = mobileDiffuse;
            addedShaders++;
            Debug.Log("[BuildSetup] Added Mobile/Diffuse to Always Included Shaders");
        }
        
        graphicsSettings.ApplyModifiedProperties();
        
        Debug.Log($"[BuildSetup] Added {addedShaders} shaders to Graphics Settings");
        
        // Step 2: Add ShaderIncluder to all scenes
        Debug.Log("[BuildSetup] ============= ADDING SHADER INCLUDER TO SCENES =============");
        
        // Find ShaderIncluder prefab
        string[] guids = AssetDatabase.FindAssets("t:Prefab ShaderIncluder");
        GameObject shaderIncluderPrefab = null;
        
        if (guids.Length > 0)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            shaderIncluderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Debug.Log($"[BuildSetup] Found ShaderIncluder prefab at: {prefabPath}");
        }
        else
        {
            Debug.LogError("[BuildSetup] ShaderIncluder prefab not found! Create it via Menu → Tutorial → Create Shader Includer Prefab");
            EditorUtility.DisplayDialog("Error", 
                "ShaderIncluder prefab not found!\n\n" +
                "Please run: Menu → Tutorial → Create Shader Includer Prefab first", 
                "OK");
            return;
        }
        
        // Get all scenes in build settings
        var buildScenes = EditorBuildSettings.scenes;
        int scenesFixed = 0;
        
        foreach (var buildScene in buildScenes)
        {
            if (!buildScene.enabled) continue;
            
            // Open scene
            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            
            // Check if ShaderIncluder already exists
            bool hasShaderIncluder = false;
            foreach (GameObject rootObj in scene.GetRootGameObjects())
            {
                if (rootObj.GetComponent<ShaderIncluder>() != null)
                {
                    hasShaderIncluder = true;
                    Debug.Log($"[BuildSetup] Scene {scene.name} already has ShaderIncluder");
                    break;
                }
            }
            
            // Add ShaderIncluder if missing
            if (!hasShaderIncluder && shaderIncluderPrefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(shaderIncluderPrefab, scene);
                instance.name = "ShaderIncluder";
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenesFixed++;
                Debug.Log($"[BuildSetup] Added ShaderIncluder to scene: {scene.name}");
            }
        }
        
        Debug.Log($"[BuildSetup] ============= SETUP COMPLETE =============");
        Debug.Log($"[BuildSetup] Added {addedShaders} shaders to Graphics Settings");
        Debug.Log($"[BuildSetup] Added ShaderIncluder to {scenesFixed} scenes");
        
        EditorUtility.DisplayDialog("Build Setup Complete", 
            $"✓ Added {addedShaders} shaders to Always Included Shaders\n" +
            $"✓ Added ShaderIncluder to {scenesFixed} scenes\n\n" +
            "Your build should now render correctly on Quest!\n\n" +
            "Next steps:\n" +
            "1. Build and deploy to Quest\n" +
            "2. If still pink, check Unity Console for [PubChem] or [MoleculeRenderer] errors", 
            "OK");
    }
    
    private static bool IsShaderInArray(SerializedProperty array, Shader shader)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == shader)
            {
                return true;
            }
        }
        return false;
    }
}
