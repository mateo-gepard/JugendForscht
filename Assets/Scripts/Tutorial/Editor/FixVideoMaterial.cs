using UnityEngine;
using UnityEditor;

public class FixVideoMaterial : EditorWindow
{
    [MenuItem("Tutorial/Fix Video Material NOW")]
    public static void FixMaterial()
    {
        // Find tutorial manager
        TutorialManager manager = FindObjectOfType<TutorialManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Error", "TutorialManager not found in scene!", "OK");
            return;
        }

        // Get display panel
        GameObject displayPanel = GameObject.Find("TutorialVideoPanel");
        if (displayPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "TutorialVideoPanel not found! Run 'Tutorial → Setup Tutorial System' first.", "OK");
            return;
        }

        Renderer renderer = displayPanel.GetComponent<Renderer>();
        if (renderer == null)
        {
            EditorUtility.DisplayDialog("Error", "No Renderer on TutorialVideoPanel!", "OK");
            return;
        }

        // Find chroma key shader
        Shader chromaShader = Shader.Find("Custom/ChromaKeyBlackToAlpha");
        if (chromaShader == null)
        {
            Debug.LogError("Chroma Key shader not found!");
            EditorUtility.DisplayDialog("Error", "ChromaKeyBlackToAlpha shader not found!\nCheck Assets/Tutorial/ChromaKeyShader.shader", "OK");
            return;
        }

        // Load or create material
        Material videoMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Tutorial/TutorialVideoMat.mat");
        if (videoMat == null)
        {
            Debug.Log("Creating new material...");
            videoMat = new Material(chromaShader);
            videoMat.name = "TutorialVideoMat";
            AssetDatabase.CreateAsset(videoMat, "Assets/Tutorial/TutorialVideoMat.mat");
        }
        else
        {
            Debug.Log("Updating existing material shader...");
            videoMat.shader = chromaShader;
        }

        // Get RenderTexture
        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/Tutorial/TutorialVideoRenderTexture.asset");
        if (rt != null)
        {
            videoMat.mainTexture = rt;
        }

        // Set chroma key properties
        videoMat.SetColor("_KeyColor", Color.green);
        videoMat.SetFloat("_Threshold", 0.2f);
        videoMat.SetFloat("_Smoothness", 0.08f);
        videoMat.renderQueue = 3000;

        // Assign to renderer
        renderer.sharedMaterial = videoMat;

        // Save
        EditorUtility.SetDirty(videoMat);
        EditorUtility.SetDirty(displayPanel);
        AssetDatabase.SaveAssets();

        Debug.Log($"✅ Fixed! Material now uses shader: {videoMat.shader.name}");
        EditorUtility.DisplayDialog("Success!", 
            $"Video material fixed!\n\nShader: {videoMat.shader.name}\nMaterial: {videoMat.name}\n\nTry 'Start Tutorial' again.", 
            "OK");
    }
}
