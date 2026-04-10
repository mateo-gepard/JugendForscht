using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Animiert Enantiomer-Darstellungen in VR:
/// - Side-by-Side Ansicht (Original + Spiegelbild) durch Klonen + X-Spiegelung
/// - Spiegelebene-Visualisierung
/// - Überlagerungstest-Animation
/// </summary>
public class IsomerAnimator : MonoBehaviour
{
    [Header("Layout")]
    public float separation = 0.3f;

    [Header("Mirror Plane")]
    public Color mirrorPlaneColor = new Color(0.5f, 0.8f, 1f, 0.2f);
    public float mirrorPlaneSize = 0.25f;

    [Header("Animation")]
    public float overlayAnimDuration = 3f;
    public float overlayHoldDuration = 2f;

    // State
    private MoleculeRenderer originalRenderer;
    private GameObject enantiomerClone;
    private GameObject mirrorPlaneObj;
    private Vector3 originalPosition;
    private bool isShowingEnantiomer = false;
    private Coroutine overlayCoroutine;

    /// <summary>
    /// Zeigt Original und Enantiomer side-by-side
    /// Arbeitet durch Klonen des gerenderten Moleküls und X-Spiegelung
    /// </summary>
    public void ShowEnantiomer(MoleculeData original, MoleculeData enantiomer)
    {
        ClearEnantiomer();

        originalRenderer = FindObjectOfType<MoleculeRenderer>();
        if (originalRenderer == null)
        {
            Debug.LogWarning("[IsomerAnim] No MoleculeRenderer found");
            return;
        }

        // Save original position
        originalPosition = originalRenderer.transform.position;

        // Move original to the left
        originalRenderer.transform.position = originalPosition + Vector3.left * separation * 0.5f;

        // Clone the entire rendered molecule
        enantiomerClone = Instantiate(originalRenderer.gameObject);
        enantiomerClone.name = "Enantiomer_Clone";

        // Remove scripts from clone (we don't want duplicate renderers/servers)
        RemoveScriptsFromClone(enantiomerClone);

        // Position clone to the right
        enantiomerClone.transform.position = originalPosition + Vector3.right * separation * 0.5f;
        enantiomerClone.transform.rotation = originalRenderer.transform.rotation;

        // Mirror by inverting X scale — this creates the enantiomer!
        Vector3 cloneScale = enantiomerClone.transform.localScale;
        cloneScale.x *= -1f;
        enantiomerClone.transform.localScale = cloneScale;

        // Create mirror plane between them
        CreateMirrorPlane(originalPosition);

        isShowingEnantiomer = true;
        Debug.Log("[IsomerAnim] Enantiomer shown via clone + X-mirror");
    }

    /// <summary>
    /// Entfernt alle MonoBehaviours vom Klon (keine doppelten Renderer/Server)
    /// </summary>
    private void RemoveScriptsFromClone(GameObject clone)
    {
        // Remove all MonoBehaviours except Transform
        var components = clone.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var comp in components)
        {
            // Keep only visual components
            if (comp != null)
            {
                Destroy(comp);
            }
        }

        // Also remove colliders
        var colliders = clone.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            Destroy(col);
        }
    }

    /// <summary>
    /// Überlagerungstest: Enantiomer gleitet zum Original
    /// Zeigt dass Spiegelbild nicht überlagert werden kann
    /// </summary>
    public void StartOverlayTest()
    {
        if (!isShowingEnantiomer || enantiomerClone == null)
        {
            Debug.LogWarning("[IsomerAnim] No enantiomer to overlay. Generate enantiomer first.");
            return;
        }

        if (overlayCoroutine != null)
            StopCoroutine(overlayCoroutine);

        overlayCoroutine = StartCoroutine(OverlayAnimation());
    }

    /// <summary>
    /// Animation: Enantiomer gleitet zum Original, wackelt, pulsiert rot, gleitet zurück
    /// </summary>
    private IEnumerator OverlayAnimation()
    {
        if (enantiomerClone == null || originalRenderer == null)
            yield break;

        Vector3 startPos = enantiomerClone.transform.position;
        Vector3 targetPos = originalRenderer.transform.position;
        Quaternion startRot = enantiomerClone.transform.rotation;

        // Save original colors
        var renderers = enantiomerClone.GetComponentsInChildren<MeshRenderer>();
        var originalColors = new Dictionary<MeshRenderer, Color>();
        foreach (var r in renderers)
        {
            if (r.material != null)
                originalColors[r] = r.material.color;
        }

        // Phase 1: Slide enantiomer toward original (smooth)
        float elapsed = 0;
        while (elapsed < overlayAnimDuration)
        {
            if (enantiomerClone == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / overlayAnimDuration);

            enantiomerClone.transform.position = Vector3.Lerp(startPos, targetPos, t);

            // Wobble to show it doesn't fit
            float wobble = Mathf.Sin(elapsed * 4f) * 8f * t;
            enantiomerClone.transform.rotation = startRot * Quaternion.Euler(wobble, wobble * 0.5f, 0);

            yield return null;
        }

        // Phase 2: At target — pulse red to show mismatch
        for (int pulse = 0; pulse < 3; pulse++)
        {
            if (enantiomerClone == null) yield break;

            // Red
            foreach (var r in renderers)
            {
                if (r != null && r.material != null)
                    r.material.color = Color.Lerp(r.material.color, Color.red, 0.6f);
            }
            yield return new WaitForSeconds(0.3f);

            // Restore
            foreach (var r in renderers)
            {
                if (r != null && r.material != null && originalColors.ContainsKey(r))
                    r.material.color = originalColors[r];
            }
            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(overlayHoldDuration * 0.5f);

        // Phase 3: Slide back
        elapsed = 0;
        float returnDuration = overlayAnimDuration * 0.6f;
        while (elapsed < returnDuration)
        {
            if (enantiomerClone == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / returnDuration);

            enantiomerClone.transform.position = Vector3.Lerp(targetPos, startPos, t);
            enantiomerClone.transform.rotation = Quaternion.Slerp(
                startRot * Quaternion.Euler(3, 2, 0), startRot, t);

            yield return null;
        }

        if (enantiomerClone != null)
        {
            enantiomerClone.transform.position = startPos;
            enantiomerClone.transform.rotation = startRot;
        }

        Debug.Log("[IsomerAnim] Overlay test complete — enantiomers don't superimpose!");
    }

    /// <summary>
    /// Erstellt eine halbtransparente Spiegelebene zwischen den Molekülen
    /// </summary>
    private void CreateMirrorPlane(Vector3 position)
    {
        mirrorPlaneObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mirrorPlaneObj.name = "MirrorPlane";
        mirrorPlaneObj.transform.position = position;
        mirrorPlaneObj.transform.localScale = Vector3.one * mirrorPlaneSize;

        // Remove collider
        var col = mirrorPlaneObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Use Sprites/Default shader for proper transparency
        var mr = mirrorPlaneObj.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = mirrorPlaneColor;
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Face the camera
        mirrorPlaneObj.AddComponent<BillboardFacer>();
    }

    /// <summary>
    /// Entfernt Enantiomer-Darstellung und stellt Original zurück
    /// </summary>
    public void ClearEnantiomer()
    {
        if (overlayCoroutine != null)
        {
            StopCoroutine(overlayCoroutine);
            overlayCoroutine = null;
        }

        if (enantiomerClone != null)
        {
            Destroy(enantiomerClone);
            enantiomerClone = null;
        }

        if (mirrorPlaneObj != null)
        {
            Destroy(mirrorPlaneObj);
            mirrorPlaneObj = null;
        }

        // Move original back to center
        if (originalRenderer != null && isShowingEnantiomer)
        {
            originalRenderer.transform.position = originalPosition;
        }

        isShowingEnantiomer = false;
    }

    void OnDestroy()
    {
        ClearEnantiomer();
    }
}
