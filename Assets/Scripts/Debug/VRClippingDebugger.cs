using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Detects and logs when objects are near the VR camera's near clipping plane
/// Helps identify what's causing grey flashes from camera clipping
/// </summary>
public class VRClippingDebugger : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Distance from camera to consider 'near' (in meters)")]
    public float nearDistance = 0.1f;
    
    [Tooltip("Log when objects enter near zone")]
    public bool logNearObjects = true;
    
    [Tooltip("Highlight near objects in editor")]
    public bool highlightNearObjects = true;
    
    [Tooltip("Check frequency (checks per second)")]
    public float checkFrequency = 10f;
    
    [Header("Visualization")]
    public Color nearColor = new Color(1f, 0f, 0f, 0.5f);
    public Color farColor = new Color(0f, 1f, 0f, 0.2f);
    
    private Camera vrCamera;
    private float checkTimer = 0f;
    private HashSet<GameObject> nearObjects = new HashSet<GameObject>();
    
    void Start()
    {
        vrCamera = Camera.main;
        if (vrCamera == null)
        {
            Debug.LogError("[VRClippingDebugger] No main camera found!");
        }
        else
        {
            Debug.Log($"[VRClippingDebugger] Monitoring camera: {vrCamera.name}");
            Debug.Log($"  Near clip plane: {vrCamera.nearClipPlane}");
            Debug.Log($"  Detection distance: {nearDistance}");
        }
    }
    
    void Update()
    {
        if (vrCamera == null) return;
        
        checkTimer += Time.deltaTime;
        if (checkTimer >= 1f / checkFrequency)
        {
            CheckForNearObjects();
            checkTimer = 0f;
        }
        
        // Press C to dump camera info
        if (Input.GetKeyDown(KeyCode.C))
        {
            DumpCameraInfo();
        }
    }
    
    void CheckForNearObjects()
    {
        Vector3 cameraPos = vrCamera.transform.position;
        HashSet<GameObject> currentNearObjects = new HashSet<GameObject>();
        
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        
        foreach (Renderer r in allRenderers)
        {
            if (!r.enabled) continue;
            
            // Check distance to bounds
            float distance = Vector3.Distance(cameraPos, r.bounds.ClosestPoint(cameraPos));
            
            if (distance < nearDistance)
            {
                currentNearObjects.Add(r.gameObject);
                
                // Log if newly entered near zone
                if (!nearObjects.Contains(r.gameObject))
                {
                    if (logNearObjects)
                    {
                        Debug.LogWarning($"[VRClipping] Object entering near zone: {r.gameObject.name}");
                        Debug.LogWarning($"  Distance: {distance:F4}m");
                        Debug.LogWarning($"  Camera near clip: {vrCamera.nearClipPlane:F4}m");
                        
                        if (r.sharedMaterial != null)
                        {
                            Debug.LogWarning($"  Material: {r.sharedMaterial.name}");
                            Debug.LogWarning($"  Shader: {r.sharedMaterial.shader.name}");
                            
                            // Only access color if material has _Color property
                            if (r.sharedMaterial.HasProperty("_Color"))
                            {
                                Debug.LogWarning($"  Color: {r.sharedMaterial.color}");
                            }
                        }
                    }
                }
            }
        }
        
        // Update tracking
        nearObjects = currentNearObjects;
    }
    
    void DumpCameraInfo()
    {
        if (vrCamera == null) return;
        
        Debug.Log("========== VR CAMERA INFO ==========");
        Debug.Log($"Camera: {vrCamera.name}");
        Debug.Log($"Position: {vrCamera.transform.position}");
        Debug.Log($"Rotation: {vrCamera.transform.rotation.eulerAngles}");
        Debug.Log($"Near Clip Plane: {vrCamera.nearClipPlane}");
        Debug.Log($"Far Clip Plane: {vrCamera.farClipPlane}");
        Debug.Log($"Field of View: {vrCamera.fieldOfView}");
        Debug.Log($"Clear Flags: {vrCamera.clearFlags}");
        Debug.Log($"Background Color: {vrCamera.backgroundColor}");
        
        Debug.Log($"\nObjects within {nearDistance}m: {nearObjects.Count}");
        foreach (GameObject obj in nearObjects)
        {
            float dist = Vector3.Distance(vrCamera.transform.position, obj.transform.position);
            Debug.Log($"  • {obj.name} - {dist:F4}m");
        }
        Debug.Log("====================================\n");
    }
    
    void OnDrawGizmos()
    {
        if (!highlightNearObjects || vrCamera == null) return;
        
        Vector3 cameraPos = vrCamera.transform.position;
        
        // Draw detection sphere
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(cameraPos, nearDistance);
        
        // Draw near clip plane
        Gizmos.color = Color.red;
        DrawClipPlane(vrCamera.transform.position, vrCamera.transform.forward, vrCamera.nearClipPlane);
        
        // Highlight objects
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        
        foreach (Renderer r in allRenderers)
        {
            if (!r.enabled) continue;
            
            float distance = Vector3.Distance(cameraPos, r.bounds.ClosestPoint(cameraPos));
            
            if (distance < nearDistance)
            {
                Gizmos.color = nearColor;
                Gizmos.DrawWireCube(r.bounds.center, r.bounds.size);
                
                // Draw line to camera
                Gizmos.DrawLine(cameraPos, r.bounds.center);
                
                // Check if clipping
                if (distance < vrCamera.nearClipPlane)
                {
                    // Flash red for clipping objects
                    Gizmos.color = Color.red;
                    Gizmos.DrawCube(r.bounds.center, r.bounds.size * 0.1f);
                }
            }
        }
    }
    
    void DrawClipPlane(Vector3 position, Vector3 normal, float distance)
    {
        Vector3 planePos = position + normal * distance;
        Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
        if (right == Vector3.zero) right = Vector3.Cross(normal, Vector3.right).normalized;
        Vector3 up = Vector3.Cross(right, normal).normalized;
        
        float size = 0.3f;
        Vector3 p1 = planePos + (right + up) * size;
        Vector3 p2 = planePos + (-right + up) * size;
        Vector3 p3 = planePos + (-right - up) * size;
        Vector3 p4 = planePos + (right - up) * size;
        
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }
}
