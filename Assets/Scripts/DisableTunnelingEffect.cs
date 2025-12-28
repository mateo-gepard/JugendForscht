using UnityEngine;

/// <summary>
/// Disables TunnelingEffect components that cause grey flashes when clipping camera
/// TunnelingEffect is for locomotion comfort - not needed for static molecule viewing
/// </summary>
public class DisableTunnelingEffect : MonoBehaviour
{
    void Start()
    {
        // Find all TunnelingEffect components
        var tunnelingEffects = FindObjectsOfType<Oculus.Interaction.TunnelingEffect>();
        
        if (tunnelingEffects.Length > 0)
        {
            Debug.Log($"[DisableTunneling] Found {tunnelingEffects.Length} TunnelingEffect component(s)");
            
            foreach (var effect in tunnelingEffects)
            {
                Debug.Log($"[DisableTunneling] Disabling TunnelingEffect on {effect.gameObject.name}");
                
                // Disable the component
                effect.enabled = false;
                
                // Also disable the renderer to prevent grey flashes
                MeshRenderer renderer = effect.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                    Debug.Log($"[DisableTunneling] Disabled renderer on {effect.gameObject.name}");
                }
                
                // Optionally, disable the entire GameObject
                // effect.gameObject.SetActive(false);
            }
            
            Debug.Log("[DisableTunneling] All TunnelingEffect components disabled");
        }
        else
        {
            Debug.Log("[DisableTunneling] No TunnelingEffect components found");
        }
        
        // Also find LocomotionTunneling components
        var locomotionTunneling = FindObjectsOfType<Oculus.Interaction.Locomotion.LocomotionTunneling>();
        if (locomotionTunneling.Length > 0)
        {
            Debug.Log($"[DisableTunneling] Found {locomotionTunneling.Length} LocomotionTunneling component(s) - disabling");
            foreach (var lt in locomotionTunneling)
            {
                lt.enabled = false;
            }
        }
        
        // Also find WallPenetrationTunneling components
        var wallPenetrationTunneling = FindObjectsOfType<Oculus.Interaction.Locomotion.WallPenetrationTunneling>();
        if (wallPenetrationTunneling.Length > 0)
        {
            Debug.Log($"[DisableTunneling] Found {wallPenetrationTunneling.Length} WallPenetrationTunneling component(s) - disabling");
            foreach (var wpt in wallPenetrationTunneling)
            {
                wpt.enabled = false;
            }
        }
    }
}
