using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Debug tool to identify and visualize all renderers in the scene
/// Helps track down grey flashes and rendering issues
/// </summary>
public class RendererDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("Show debug overlays on all renderers")]
    public bool showOverlays = true;
    
    [Tooltip("Log renderer info when they become visible")]
    public bool logVisibilityChanges = true;
    
    [Tooltip("Highlight grey materials in red")]
    public bool highlightGreyMaterials = true;
    
    [Tooltip("Press D to dump all renderer info to console")]
    public bool enableDumpKey = true;
    
    [Header("Visualization")]
    public Color overlayColor = new Color(1f, 0f, 0f, 0.3f);
    public float overlaySize = 0.05f;
    
    private Dictionary<Renderer, bool> rendererVisibilityStates = new Dictionary<Renderer, bool>();
    
    void Update()
    {
        if (enableDumpKey && Input.GetKeyDown(KeyCode.D))
        {
            DumpAllRenderers();
        }
        
        if (logVisibilityChanges)
        {
            TrackVisibilityChanges();
        }
    }
    
    /// <summary>
    /// Track when renderers become visible/invisible
    /// </summary>
    void TrackVisibilityChanges()
    {
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        
        foreach (Renderer r in allRenderers)
        {
            bool currentlyVisible = r.enabled && r.isVisible;
            
            if (!rendererVisibilityStates.ContainsKey(r))
            {
                rendererVisibilityStates[r] = currentlyVisible;
            }
            else if (rendererVisibilityStates[r] != currentlyVisible)
            {
                // Visibility changed
                Debug.Log($"[RendererVisibility] {r.gameObject.name} changed to {(currentlyVisible ? "VISIBLE" : "HIDDEN")}");
                
                if (currentlyVisible && r.sharedMaterial != null)
                {
                    // Check if material has _Color property before accessing it
                    if (r.sharedMaterial.HasProperty("_Color"))
                    {
                        Color c = r.sharedMaterial.color;
                        Debug.Log($"  Material: {r.sharedMaterial.name}, Shader: {r.sharedMaterial.shader.name}, Color: {c}");
                        
                        if (IsGreyColor(c))
                        {
                            Debug.LogWarning($"  ⚠️ GREY MATERIAL DETECTED! This might be causing flashes");
                        }
                    }
                    else
                    {
                        Debug.Log($"  Material: {r.sharedMaterial.name}, Shader: {r.sharedMaterial.shader.name} (no _Color property)");
                    }
                }
                
                rendererVisibilityStates[r] = currentlyVisible;
            }
        }
    }
    
    /// <summary>
    /// Dump complete info about all renderers
    /// </summary>
    void DumpAllRenderers()
    {
        Debug.Log("========== RENDERER DUMP ==========");
        
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        Debug.Log($"Total renderers in scene: {allRenderers.Length}\n");
        
        int visibleCount = 0;
        int greyMaterialCount = 0;
        List<string> greyRenderers = new List<string>();
        
        foreach (Renderer r in allRenderers)
        {
            bool isVisible = r.enabled && r.isVisible;
            if (isVisible) visibleCount++;
            
            Material mat = r.sharedMaterial;
            string materialInfo = mat != null ? $"{mat.name} ({mat.shader.name})" : "NULL";
            string colorInfo = mat != null ? mat.color.ToString() : "N/A";
            
            bool isGrey = mat != null && IsGreyColor(mat.color);
            if (isGrey)
            {
                greyMaterialCount++;
                greyRenderers.Add($"  • {r.gameObject.name} - {materialInfo} - {colorInfo}");
            }
            
            string rendererType = r.GetType().Name;
            string bounds = r.bounds.size.ToString("F2");
            
            Debug.Log($"[{rendererType}] {r.gameObject.name}\n" +
                     $"  Path: {GetFullPath(r.transform)}\n" +
                     $"  Enabled: {r.enabled}, Visible: {r.isVisible}, IsVisible: {isVisible}\n" +
                     $"  Material: {materialInfo}\n" +
                     $"  Color: {colorInfo} {(isGrey ? "⚠️ GREY!" : "")}\n" +
                     $"  Bounds: {bounds}\n" +
                     $"  Layer: {LayerMask.LayerToName(r.gameObject.layer)}\n" +
                     $"  ShadowCasting: {r.shadowCastingMode}, ReceiveShadows: {r.receiveShadows}\n" +
                     $"  RenderQueue: {(mat != null ? mat.renderQueue.ToString() : "N/A")}\n");
        }
        
        Debug.Log($"\n========== SUMMARY ==========");
        Debug.Log($"Total: {allRenderers.Length}, Visible: {visibleCount}, Grey Materials: {greyMaterialCount}\n");
        
        if (greyRenderers.Count > 0)
        {
            Debug.LogWarning($"⚠️ GREY MATERIALS FOUND ({greyRenderers.Count}):");
            foreach (string info in greyRenderers)
            {
                Debug.LogWarning(info);
            }
        }
        
        Debug.Log("=================================\n");
    }
    
    /// <summary>
    /// Check if a color is grey-ish
    /// </summary>
    bool IsGreyColor(Color c)
    {
        // Check if RGB values are similar (neutral grey)
        float avgDiff = (Mathf.Abs(c.r - c.g) + Mathf.Abs(c.g - c.b) + Mathf.Abs(c.b - c.r)) / 3f;
        bool isSimilar = avgDiff < 0.15f;
        
        // Check if in grey range (not too bright, not too dark)
        float avg = (c.r + c.g + c.b) / 3f;
        bool isGreyRange = avg > 0.3f && avg < 0.7f;
        
        return isSimilar && isGreyRange;
    }
    
    /// <summary>
    /// Get full hierarchy path of a transform
    /// </summary>
    string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
    
    void OnDrawGizmos()
    {
        if (!showOverlays) return;
        
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        
        foreach (Renderer r in allRenderers)
        {
            if (!r.enabled || !r.isVisible) continue;
            
            // Check if material has _Color property before accessing it
            bool hasColorProperty = r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color");
            bool isGrey = hasColorProperty && IsGreyColor(r.sharedMaterial.color);
            
            if (highlightGreyMaterials && isGrey)
            {
                // Draw red wireframe around grey renderers
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
                
                // Draw warning sphere
                Gizmos.DrawWireSphere(r.bounds.center, 0.1f);
            }
            else
            {
                // Draw normal overlay
                Gizmos.color = overlayColor;
                Gizmos.DrawWireCube(r.bounds.center, r.bounds.size * 1.05f);
            }
        }
    }
}
