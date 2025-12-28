using UnityEngine;
using UnityEditor;
using UnityEngine.Video;

/// <summary>
/// Automatic setup for Tutorial system - assigns videos and configures TutorialManager
/// </summary>
public class TutorialSetup : MonoBehaviour
{
    [MenuItem("Tutorial/Setup Tutorial System")]
    public static void SetupTutorialSystem()
    {
        Debug.Log("[TutorialSetup] Starting automatic setup...");
        
        // 0. Ensure Tutorial folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial"))
        {
            AssetDatabase.CreateFolder("Assets", "Tutorial");
            Debug.Log("[TutorialSetup] Created Assets/Tutorial folder");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial/Steps"))
        {
            AssetDatabase.CreateFolder("Assets/Tutorial", "Steps");
        }
        
        // 1. Load Step01 asset
        TutorialStep step01 = AssetDatabase.LoadAssetAtPath<TutorialStep>("Assets/Tutorial/Steps/Step01_BondIntroduction.asset");
        if (step01 == null)
        {
            Debug.LogError("[TutorialSetup] Step01_BondIntroduction.asset not found! Run 'Tutorial → Create Step 1 - Bond Introduction' first.");
            return;
        }
        
        // 2. Load video clip
        VideoClip videoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Videos/Tutorial/Snippet1.mp4");
        if (videoClip == null)
        {
            Debug.LogError("[TutorialSetup] Snippet1.mp4 not found in Assets/Videos/Tutorial/");
            return;
        }
        
        // 3. Assign video to step - at origin center, in front of viewer
        step01.videoClip = videoClip;
        step01.videoPosition = new Vector3(0f, 0f, 0f); // At tutorial origin (will be placed in front of viewer)
        step01.videoScale = 1.0f; // Comfortable viewing size
        step01.autoAdvance = true; // Auto-advance to next step after video ends
        step01.continueDelay = 0f; // No delay before auto-advancing
        
        EditorUtility.SetDirty(step01);
        AssetDatabase.SaveAssets();
        Debug.Log("[TutorialSetup] ✓ Video assigned to Step01");
        
        // 4. Find TutorialManager in scene
        TutorialManager tutorialManager = FindObjectOfType<TutorialManager>();
        if (tutorialManager == null)
        {
            Debug.LogError("[TutorialSetup] TutorialManager not found in scene!");
            return;
        }
        
        // 5. Assign step to TutorialManager
        SerializedObject so = new SerializedObject(tutorialManager);
        SerializedProperty stepsProp = so.FindProperty("tutorialSteps");
        
        // Clear existing steps
        stepsProp.ClearArray();
        
        // Add Step01
        stepsProp.InsertArrayElementAtIndex(0);
        stepsProp.GetArrayElementAtIndex(0).objectReferenceValue = step01;
        
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tutorialManager);
        
        Debug.Log("[TutorialSetup] ✓ Step01 assigned to TutorialManager");
        
        // 6. Setup VideoPlayer component
        VideoPlayer videoPlayer = tutorialManager.GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            videoPlayer = tutorialManager.gameObject.AddComponent<VideoPlayer>();
            Debug.Log("[TutorialSetup] ✓ VideoPlayer component added");
        }
        
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
        
        // Create RenderTexture if needed
        RenderTexture rt = tutorialManager.videoRenderTexture;
        if (rt == null)
        {
            rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            rt.name = "TutorialVideoRT";
            AssetDatabase.CreateAsset(rt, "Assets/Tutorial/TutorialVideoRT.renderTexture");
            tutorialManager.videoRenderTexture = rt;
            Debug.Log("[TutorialSetup] ✓ RenderTexture created");
        }
        videoPlayer.targetTexture = rt;
        
        // 7. Create video display panel (Quad)
        GameObject displayPanel = tutorialManager.videoDisplayPanel;
        if (displayPanel == null)
        {
            displayPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            displayPanel.name = "TutorialVideoPanel";
            displayPanel.transform.SetParent(tutorialManager.transform);
            displayPanel.transform.localPosition = Vector3.zero;
            displayPanel.transform.localScale = new Vector3(1.6f, 0.9f, 1f); // 16:9 aspect
            
            // Remove collider
            Collider col = displayPanel.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            
            tutorialManager.videoDisplayPanel = displayPanel;
            Debug.Log("[TutorialSetup] ✓ Video display panel created");
        }
        
        // 8. Create transparent material for video with chroma key (black to alpha)
        Material videoMat = tutorialManager.transparentVideoMaterial;
        if (videoMat == null)
        {
            Shader chromaShader = Shader.Find("Custom/ChromaKeyBlackToAlpha");
            if (chromaShader == null)
            {
                Debug.LogError("[TutorialSetup] ChromaKeyBlackToAlpha shader not found! Make sure ChromaKeyShader.shader exists.");
                chromaShader = Shader.Find("Unlit/Transparent");
            }
            videoMat = new Material(chromaShader);
            videoMat.name = "TutorialVideoMat";
            videoMat.mainTexture = rt;
            videoMat.SetColor("_KeyColor", Color.green);
            videoMat.SetFloat("_Threshold", 0.2f);
            videoMat.SetFloat("_Smoothness", 0.08f);
            videoMat.renderQueue = 3000;
            AssetDatabase.CreateAsset(videoMat, "Assets/Tutorial/TutorialVideoMat.mat");
            tutorialManager.transparentVideoMaterial = videoMat;
            Debug.Log("[TutorialSetup] ✓ Video material created with chroma key (black to alpha)");
        }
        else
        {
            videoMat.mainTexture = rt;
            videoMat.SetFloat("_Threshold", 0.2f);
            videoMat.SetFloat("_Smoothness", 0.08f);
            videoMat.renderQueue = 3000;
        }
        
        // Assign material to panel
        Renderer renderer = displayPanel.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = videoMat;
            Debug.Log($"[TutorialSetup] ✓ Material '{videoMat.name}' with shader '{videoMat.shader.name}' assigned to display panel");
        }
        else
        {
            Debug.LogError("[TutorialSetup] ✗ No Renderer found on display panel!");
        }
        
        // 9. Assign videoPlayer reference to TutorialManager
        SerializedObject soManager = new SerializedObject(tutorialManager);
        soManager.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
        soManager.ApplyModifiedProperties();
        
        EditorUtility.SetDirty(tutorialManager);
        AssetDatabase.SaveAssets();
        
        Debug.Log("[TutorialSetup] ✅ Tutorial system setup complete!");
        Debug.Log("[TutorialSetup] → Video will play when you click 'Start Tutorial' on the iPad");
        Debug.Log("[TutorialSetup] → Events will trigger at specified timestamps (11s-54s)");
        
        EditorUtility.DisplayDialog("Tutorial Setup Complete", 
            "Tutorial system is ready!\n\n" +
            "✓ Video assigned to Step 1\n" +
            "✓ TutorialManager configured\n" +
            "✓ VideoPlayer component added\n" +
            "✓ Video display panel created\n" +
            "✓ Materials and RenderTexture set up\n\n" +
            "Click 'Start Tutorial' on the iPad to test!", 
            "OK");
    }
}
