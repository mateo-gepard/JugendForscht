using UnityEngine;

/// <summary>
/// Holds references to shaders and materials needed for standalone VR builds.
/// Place this on a GameObject in the scene to ensure shaders are included in builds.
/// </summary>
public class ShaderIncluder : MonoBehaviour
{
    [Header("Shaders to Include in Build")]
    [Tooltip("These shaders will be included in the build")]
    public Shader[] shadersToInclude;
    
    [Header("Materials to Include in Build")]
    [Tooltip("Reference materials to ensure they're included")]
    public Material[] materialsToInclude;
    
    // Static cached materials for runtime use
    private static Material s_BlackMaterial;
    private static Material s_CarbonMaterial;
    private static Material s_HydrogenMaterial;
    private static Material s_OxygenMaterial;
    private static Material s_NitrogenMaterial;
    private static Material s_HighlightGreenMaterial;
    private static Material s_HighlightYellowMaterial;
    
    private static ShaderIncluder s_Instance;
    
    void Awake()
    {
        s_Instance = this;
        CreateCachedMaterials();
    }
    
    private void CreateCachedMaterials()
    {
        // Create materials using a guaranteed-to-exist shader
        Shader shader = GetBestShader();
        
        s_BlackMaterial = CreateMat(shader, Color.black);
        s_CarbonMaterial = CreateMat(shader, new Color(0.3f, 0.3f, 0.3f));
        s_HydrogenMaterial = CreateMat(shader, new Color(0.85f, 0.85f, 0.85f));
        s_OxygenMaterial = CreateMat(shader, new Color(0.9f, 0.2f, 0.2f));
        s_NitrogenMaterial = CreateMat(shader, new Color(0.2f, 0.4f, 0.9f));
        s_HighlightGreenMaterial = CreateMat(shader, new Color(0.2f, 0.9f, 0.2f));
        s_HighlightYellowMaterial = CreateMat(shader, new Color(1f, 0.9f, 0.2f));
        
        Debug.Log($"[ShaderIncluder] Created cached materials using shader: {shader.name}");
    }
    
    private static Shader GetBestShader()
    {
        // Try shaders in order of preference
        string[] shaderNames = new string[]
        {
            "Custom/MoleculeUnlit",
            "Unlit/Color",
            "Mobile/Unlit (Supports Lightmap)",
            "Mobile/Diffuse",
            "Standard"
        };
        
        foreach (string name in shaderNames)
        {
            Shader shader = Shader.Find(name);
            if (shader != null)
            {
                Debug.Log($"[ShaderIncluder] Found shader: {name}");
                return shader;
            }
        }
        
        // Last resort - this should always exist
        Debug.LogWarning("[ShaderIncluder] No preferred shader found, using fallback");
        return Shader.Find("Diffuse");
    }
    
    private static Material CreateMat(Shader shader, Color color)
    {
        Material mat = new Material(shader);
        mat.color = color;
        
        // Set properties based on shader type
        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
        if (mat.HasProperty("_Metallic"))
        {
            mat.SetFloat("_Metallic", 0f);
        }
        if (mat.HasProperty("_Glossiness"))
        {
            mat.SetFloat("_Glossiness", 0.3f);
        }
        
        return mat;
    }
    
    // Static accessors for materials
    public static Material GetBlackMaterial() => s_BlackMaterial ?? CreateFallbackMaterial(Color.black);
    public static Material GetCarbonMaterial() => s_CarbonMaterial ?? CreateFallbackMaterial(new Color(0.3f, 0.3f, 0.3f));
    public static Material GetHydrogenMaterial() => s_HydrogenMaterial ?? CreateFallbackMaterial(new Color(0.85f, 0.85f, 0.85f));
    public static Material GetOxygenMaterial() => s_OxygenMaterial ?? CreateFallbackMaterial(new Color(0.9f, 0.2f, 0.2f));
    public static Material GetNitrogenMaterial() => s_NitrogenMaterial ?? CreateFallbackMaterial(new Color(0.2f, 0.4f, 0.9f));
    public static Material GetHighlightGreenMaterial() => s_HighlightGreenMaterial ?? CreateFallbackMaterial(new Color(0.2f, 0.9f, 0.2f));
    public static Material GetHighlightYellowMaterial() => s_HighlightYellowMaterial ?? CreateFallbackMaterial(new Color(1f, 0.9f, 0.2f));
    
    private static Material CreateFallbackMaterial(Color color)
    {
        // Create a fallback material if static ones aren't initialized
        Material mat = new Material(GetBestShader());
        mat.color = color;
        return mat;
    }
    
    public static Material GetMaterialForElement(string element)
    {
        switch (element.ToUpper())
        {
            case "C": return GetCarbonMaterial();
            case "H": return GetHydrogenMaterial();
            case "O": return GetOxygenMaterial();
            case "N": return GetNitrogenMaterial();
            case "F": return CreateFallbackMaterial(new Color(0.5f, 0.9f, 0.5f)); // Fluorine
            case "B": return CreateFallbackMaterial(new Color(1f, 0.7f, 0.7f)); // Boron
            default: return CreateFallbackMaterial(Color.magenta); // Unknown
        }
    }
    
    public static Material GetBondMaterial(bool highlighted = false)
    {
        return highlighted ? GetHighlightGreenMaterial() : GetBlackMaterial();
    }
}
