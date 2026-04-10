using UnityEngine;
using UnityEditor;

/// <summary>
/// Creates a clean hand material using Custom/HandTrackingClean and assigns it
/// to both hand tracking objects in the scene.
/// Menu: Tutorial > Fix Hand Tracking Material
/// </summary>
public class HandMaterialFixer : Editor
{
    [MenuItem("Tutorial/Fix Hand Tracking Material")]
    public static void FixHandMaterial()
    {
        // 1. Find or create the shader
        Shader handShader = Shader.Find("Custom/HandTrackingClean");
        if (handShader == null)
        {
            EditorUtility.DisplayDialog("Error",
                "Shader 'Custom/HandTrackingClean' not found!\nMake sure HandTrackingClean.shader is in Assets/Shaders/.",
                "OK");
            return;
        }

        // 2. Create material asset
        string matDir = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(matDir))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string matPath = $"{matDir}/CleanHandMaterial.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (mat == null)
        {
            mat = new Material(handShader);
            mat.name = "CleanHandMaterial";
            // Match the original Meta hand look
            mat.SetColor("_ColorPrimary", new Color(0.55f, 0.55f, 0.55f, 1f));
            mat.SetColor("_ColorTop", new Color(1f, 1f, 1f, 1f));
            mat.SetColor("_ColorBottom", new Color(0.36f, 0.56f, 0.81f, 1f));
            mat.SetFloat("_RimFactor", 0.75f);
            mat.SetFloat("_FresnelPower", 0.22f);
            mat.SetFloat("_HandAlpha", 1f);
            mat.SetFloat("_MinVisibleAlpha", 0.15f);
            mat.renderQueue = 3000; // Transparent
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log($"[HandFix] Created material: {matPath}");
        }
        else
        {
            mat.shader = handShader;
            EditorUtility.SetDirty(mat);
            Debug.Log($"[HandFix] Updated existing material: {matPath}");
        }

        // 3. Find hand tracking objects in scene and swap material
        int swapped = 0;
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in renderers)
            {
                // Match hand tracking objects by name pattern
                string goName = smr.gameObject.name.ToLower();
                if (goName.Contains("hand tracking") || goName.Contains("handtracking") ||
                    goName.Contains("ovrhand") || goName.Contains("hand_"))
                {
                    Undo.RecordObject(smr, "Fix Hand Material");
                    smr.sharedMaterial = mat;
                    EditorUtility.SetDirty(smr);
                    swapped++;
                    Debug.Log($"[HandFix] Swapped material on: {smr.gameObject.name}");
                }
            }
        }

        AssetDatabase.SaveAssets();

        if (swapped > 0)
        {
            EditorUtility.DisplayDialog("Hand Material Fixed",
                $"Assigned CleanHandMaterial to {swapped} hand renderer(s).\n\n" +
                "The new shader uses stereo-aware depth rendering and\n" +
                "view-based rim lighting (no more glitchy artifacts).",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("No Hands Found",
                "Could not find hand tracking objects in the scene.\n\n" +
                "You can manually assign Assets/Materials/CleanHandMaterial.mat\n" +
                "to the SkinnedMeshRenderer on your hand tracking GameObjects.",
                "OK");
        }
    }
}
