using UnityEngine;

/// <summary>
/// Fixes stereo rendering issues for plane visualization in Quest builds
/// Ensures materials and shaders are properly configured for mobile VR
/// </summary>
public class VRPlaneRendererFix : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The plane GameObject to fix (leave empty to auto-find)")]
    public GameObject targetPlane;
    
    [Header("Settings")]
    [Tooltip("Force shader replacement on start")]
    public bool forceShaderFix = true;
    
    [Tooltip("Check and fix every frame")]
    public bool continuousCheck = false;
    
    private MeshRenderer planeRenderer;
    private Material fixedMaterial;
    
    void Start()
    {
        if (targetPlane == null)
        {
            // Auto-find plane
            GameObject plane = GameObject.Find("MoleculePlane_Visual");
            if (plane != null)
            {
                targetPlane = plane;
                Debug.Log($"[VRPlaneFix] Auto-found plane: {plane.name}");
            }
        }
        
        if (targetPlane != null && forceShaderFix)
        {
            FixPlaneRenderer();
        }
    }
    
    void Update()
    {
        if (continuousCheck && targetPlane != null)
        {
            CheckAndFix();
        }
    }
    
    void FixPlaneRenderer()
    {
        planeRenderer = targetPlane.GetComponent<MeshRenderer>();
        if (planeRenderer == null)
        {
            Debug.LogError("[VRPlaneFix] No MeshRenderer found on plane!");
            return;
        }
        
        Material currentMaterial = planeRenderer.sharedMaterial;
        
        // Check if material is pink (shader missing)
        if (currentMaterial == null || currentMaterial.shader == null || 
            currentMaterial.shader.name.Contains("Hidden") || currentMaterial.shader.name.Contains("Error"))
        {
            Debug.LogWarning($"[VRPlaneFix] Plane has invalid shader, replacing...");
            CreateFixedMaterial();
            planeRenderer.material = fixedMaterial;
        }
        else if (currentMaterial.color == Color.magenta || currentMaterial.color == new Color(1, 0, 1, 1))
        {
            Debug.LogWarning($"[VRPlaneFix] Plane is magenta, shader not found in build");
            CreateFixedMaterial();
            planeRenderer.material = fixedMaterial;
        }
        else
        {
            Debug.Log($"[VRPlaneFix] Current shader: {currentMaterial.shader.name}");
            
            // Try to fix shader anyway for Quest compatibility
            if (!currentMaterial.shader.name.Contains("Unlit") && !currentMaterial.shader.name.Contains("Mobile"))
            {
                Debug.LogWarning($"[VRPlaneFix] Shader '{currentMaterial.shader.name}' may not be Quest-compatible, replacing...");
                CreateFixedMaterial();
                planeRenderer.material = fixedMaterial;
            }
        }
        
        // Ensure render settings are correct for VR
        planeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        planeRenderer.receiveShadows = false;
        planeRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        planeRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        
        Debug.Log($"[VRPlaneFix] Plane fixed - Shader: {planeRenderer.sharedMaterial.shader.name}, Color: {planeRenderer.sharedMaterial.color}");
    }
    
    void CreateFixedMaterial()
    {
        // Try multiple mobile-compatible shaders in order of preference
        Shader shader = Shader.Find("Unlit/Transparent");
        
        if (shader == null)
        {
            shader = Shader.Find("Mobile/Particles/Alpha Blended");
            Debug.Log("[VRPlaneFix] Using Mobile/Particles/Alpha Blended shader");
        }
        
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
            Debug.LogWarning("[VRPlaneFix] Using Unlit/Color shader (no alpha)");
        }
        
        if (shader == null)
        {
            // Ultimate fallback
            shader = Shader.Find("Standard");
            Debug.LogError("[VRPlaneFix] Using Standard shader as last resort");
        }
        
        fixedMaterial = new Material(shader);
        fixedMaterial.name = "VR_PlaneFixed";
        fixedMaterial.color = new Color(0.6f, 0.7f, 0.8f, 0.15f);
        
        // Set proper render queue for transparency
        fixedMaterial.renderQueue = 3000;
        
        // If using Unlit/Transparent, set proper blend mode
        if (shader.name == "Unlit/Transparent")
        {
            fixedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fixedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fixedMaterial.SetInt("_ZWrite", 0);
        }
    }
    
    void CheckAndFix()
    {
        if (planeRenderer == null)
        {
            planeRenderer = targetPlane.GetComponent<MeshRenderer>();
            if (planeRenderer == null) return;
        }
        
        // Check if material is still valid
        Material mat = planeRenderer.sharedMaterial;
        if (mat == null || mat.shader == null)
        {
            Debug.LogWarning("[VRPlaneFix] Material lost, recreating...");
            FixPlaneRenderer();
        }
    }
}
