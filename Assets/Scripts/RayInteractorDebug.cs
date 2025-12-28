using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class RayInteractorDebug : MonoBehaviour
{
    XRRayInteractor ray;

    void Awake()
    {
        ray = GetComponent<XRRayInteractor>();
        if (ray == null)
        {
            Debug.LogError("[RayDebug] XRRayInteractor NOT FOUND");
        }
    }

    void Update()
    {
        if (ray == null) return;

        if (ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            Debug.Log($"[RayDebug] HIT 3D: {hit.collider.gameObject.name}");
        }

        if (ray.TryGetCurrentUIRaycastResult(out var uiHit))
        {
            Debug.Log($"[RayDebug] HIT UI: {uiHit.gameObject.name}");
        }
    }
}
