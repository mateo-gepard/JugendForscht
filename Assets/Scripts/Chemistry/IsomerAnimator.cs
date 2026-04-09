using UnityEngine;
using System.Collections;

/// <summary>
/// Animiert Isomer-Darstellungen in VR:
/// - Side-by-Side Ansicht (Original + Isomer)
/// - Überlagerungstest-Animation
/// - Proximity-basierte Rotation (nähere Hand dreht näheres Molekül)
/// </summary>
public class IsomerAnimator : MonoBehaviour
{
    [Header("Layout")]
    public float separation = 0.5f;

    [Header("Animation")]
    public float overlayDuration = 2.5f;
    public float holdDuration = 2f;

    // State
    private MoleculeRenderer originalRenderer;
    private GameObject isomerClone;
    private Vector3 originalPosition;
    private bool isShowingIsomer = false;
    private bool isConformer = false; // True if clone is a conformer (identical copy)
    private Coroutine activeCoroutine;

    // Public accessors for HandRotationController
    public GameObject IsomerClone => isomerClone;
    public MoleculeRenderer OriginalRenderer => originalRenderer;
    public bool IsShowingIsomer => isShowingIsomer;

    /// <summary>
    /// Berechnet die nötige Separation basierend auf Molekülgröße
    /// </summary>
    private float CalculateSeparation()
    {
        if (originalRenderer == null) return separation;

        // Get approximate molecule radius from renderer bounds
        Renderer[] renderers = originalRenderer.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return separation;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        // Separation = molecule diameter + small gap
        float moleculeWidth = bounds.size.x;
        float minSep = moleculeWidth + 0.08f; // 8cm gap minimum
        return Mathf.Max(separation, minSep);
    }

    /// <summary>
    /// Zeigt ein Isomer (Enantiomer/Diastereomer) als gespiegelten Klon
    /// </summary>
    public void ShowEnantiomer(MoleculeData original, MoleculeData isomer)
    {
        ShowIsomer(original, isomer, mirror: true);
    }

    /// <summary>
    /// Zeigt ein Konformer (identische Kopie, kein Spiegel)
    /// </summary>
    public void ShowConformer(MoleculeData original)
    {
        ShowIsomer(original, original, mirror: false);
    }

    private void ShowIsomer(MoleculeData original, MoleculeData isomer, bool mirror)
    {
        ClearEnantiomer();

        originalRenderer = FindObjectOfType<MoleculeRenderer>();
        if (originalRenderer == null)
        {
            Debug.LogWarning("[IsomerAnim] No MoleculeRenderer found");
            return;
        }

        // Stop any auto-rotation first
        var planeAlign = originalRenderer.GetComponent<MoleculePlaneAlignment>();
        if (planeAlign != null)
        {
            planeAlign.enableAutoRotation = false;
            planeAlign.StopAutoRotation();
        }

        // Save original position
        originalPosition = originalRenderer.transform.position;

        // Calculate dynamic separation based on molecule size
        float actualSep = CalculateSeparation();

        // Move original to the left
        originalRenderer.transform.position = originalPosition + Vector3.left * actualSep * 0.5f;

        // Clone the entire rendered molecule
        isomerClone = Instantiate(originalRenderer.gameObject);
        isomerClone.name = mirror ? "Enantiomer_Clone" : "Conformer_Clone";

        // Remove management scripts from clone
        var cloneRenderer = isomerClone.GetComponent<MoleculeRenderer>();
        if (cloneRenderer != null) Destroy(cloneRenderer);
        var clonePlaneAlign = isomerClone.GetComponent<MoleculePlaneAlignment>();
        if (clonePlaneAlign != null) Destroy(clonePlaneAlign);
        var cloneVisualizer = isomerClone.GetComponent<ChiralityVisualizer>();
        if (cloneVisualizer != null) Destroy(cloneVisualizer);
        var cloneAnimator = isomerClone.GetComponent<IsomerAnimator>();
        if (cloneAnimator != null) Destroy(cloneAnimator);

        // Position the clone to the right
        isomerClone.transform.position = originalPosition + Vector3.right * actualSep * 0.5f;

        if (mirror)
        {
            // Mirror by inverting X scale (creates the enantiomer!)
            Vector3 cloneScale = isomerClone.transform.localScale;
            cloneScale.x *= -1f;
            isomerClone.transform.localScale = cloneScale;
            isConformer = false;
        }
        else
        {
            // Conformer: identical copy, no mirror
            isConformer = true;
        }

        isShowingIsomer = true;
        Debug.Log($"[IsomerAnim] Isomer clone created (mirror={mirror})");
    }

    /// <summary>
    /// Startet den Überlagerungstest
    /// </summary>
    public void StartOverlayTest()
    {
        if (!isShowingIsomer || isomerClone == null)
        {
            Debug.LogWarning("[IsomerAnim] No isomer to overlay");
            return;
        }

        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        activeCoroutine = StartCoroutine(OverlayAnimation());
    }

    /// <summary>
    /// Meso-Erkennung: Prüft ob Molekül eine Meso-Verbindung ist.
    /// Meso → erzeugt Enantiomer, überlagert perfekt, löscht Clone.
    /// Nicht-Meso → wackelt das Original.
    /// </summary>
    public void TestMeso(MoleculeData original)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        activeCoroutine = StartCoroutine(MesoAnimation(original));
    }

    private IEnumerator MesoAnimation(MoleculeData original)
    {
        // Detect chirality centers
        var centers = ChiralityDetector.DetectChiralCenters(original);

        if (centers.Count < 2)
        {
            Debug.Log("[IsomerAnim] Meso test: not enough chiral centers");
            yield return StartCoroutine(WobbleOriginal("Keine Meso-Verbindung (weniger als 2 Chiralitätszentren)"));
            yield break;
        }

        // Generate enantiomer and check identity
        var enantiomer = IsomerGenerator.GenerateEnantiomer(original);
        bool isMeso = IsomerGenerator.AreMoleculesIdentical(original, enantiomer);

        if (!isMeso)
        {
            Debug.Log("[IsomerAnim] Meso test: NOT a meso compound");
            yield return StartCoroutine(WobbleOriginal("Keine Meso-Verbindung (Enantiomer unterscheidet sich)"));
            yield break;
        }

        // IS MESO! Show enantiomer side-by-side
        Debug.Log("[IsomerAnim] Meso test: IS a meso compound!");
        ShowEnantiomer(original, enantiomer);

        // Wait so user sees both molecules
        yield return new WaitForSeconds(1.5f);

        // Slide + 180° rotation (same as standard overlay)
        Vector3 startPos = isomerClone.transform.position;
        Vector3 targetPos = originalRenderer.transform.position;
        Quaternion startRot = isomerClone.transform.rotation;
        Quaternion targetRot = originalRenderer.transform.rotation * Quaternion.Euler(0, 180f, 0);

        float elapsed = 0;
        while (elapsed < overlayDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / overlayDuration);
            isomerClone.transform.position = Vector3.Lerp(startPos, targetPos, t);
            isomerClone.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
        isomerClone.transform.position = targetPos;
        isomerClone.transform.rotation = targetRot;

        // Hold to show perfect overlap
        yield return new WaitForSeconds(holdDuration);

        // MESO confirmed → delete clone
        Destroy(isomerClone);
        isomerClone = null;
        isShowingIsomer = false;

        // Move original back to center
        originalRenderer.transform.position = originalPosition;

        Debug.Log("[IsomerAnim] Meso test complete - clone deleted");
    }

    private IEnumerator WobbleOriginal(string reason)
    {
        originalRenderer = FindObjectOfType<MoleculeRenderer>();
        if (originalRenderer == null) yield break;

        Debug.Log($"[IsomerAnim] Wobble: {reason}");
        Quaternion baseRot = originalRenderer.transform.rotation;
        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            float wobble = Mathf.Sin(elapsed * 10f) * 8f * (1f - elapsed / 1.5f);
            originalRenderer.transform.rotation = baseRot * Quaternion.Euler(0, wobble, 0);
            yield return null;
        }
        originalRenderer.transform.rotation = baseRot;
    }

    /// <summary>
    /// Animation: Isomer gleitet zum Original
    /// Conformer/achiral: perfekte Überlagerung. Chiral: wackelt, gleitet zurück.
    /// Nutzt aktuelle Positionen/Rotationen (funktioniert auch nach manuellem Drehen)
    /// </summary>
    private IEnumerator OverlayAnimation()
    {
        Vector3 startPos = isomerClone.transform.position;
        Vector3 targetPos = originalRenderer.transform.position;
        Quaternion startRot = isomerClone.transform.rotation;
        Vector3 startScale = isomerClone.transform.localScale;

        // Determine if molecules are identical
        bool isIdentical = isConformer;
        if (!isConformer)
        {
            var renderer = FindObjectOfType<MoleculeRenderer>();
            if (renderer != null && renderer.CurrentMolecule != null)
            {
                var original = renderer.CurrentMolecule;
                var enantiomer = IsomerGenerator.GenerateEnantiomer(original);
                isIdentical = IsomerGenerator.AreMoleculesIdentical(original, enantiomer);
            }
        }
        Debug.Log($"[IsomerAnim] Overlay: identical={isIdentical}, conformer={isConformer}");

        // Calculate target rotation:
        // For mirrored clones: original's rotation + 180° Y
        //   (scale(-1,1,1) + Ry(180°) = identity for symmetric molecules)
        // For conformers (no mirror): just match original's rotation
        Quaternion origRot = originalRenderer.transform.rotation;
        Quaternion targetRot;
        if (isConformer)
        {
            targetRot = origRot;
        }
        else
        {
            // 180° Y rotation combined with original's angle
            targetRot = origRot * Quaternion.Euler(0, 180f, 0);
        }

        // Phase 1: Slide to position + smooth 180° rotation
        float elapsed = 0;
        while (elapsed < overlayDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / overlayDuration);
            isomerClone.transform.position = Vector3.Lerp(startPos, targetPos, t);
            isomerClone.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }
        isomerClone.transform.position = targetPos;
        isomerClone.transform.rotation = targetRot;

        if (isIdentical)
        {
            // Molecules superimpose! Hold then slide back
            Debug.Log("[IsomerAnim] Molecules ARE superimposable!");
            yield return new WaitForSeconds(holdDuration);

            // Slide back with reverse rotation
            elapsed = 0;
            float returnDuration = overlayDuration * 0.6f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / returnDuration);
                isomerClone.transform.position = Vector3.Lerp(targetPos, startPos, t);
                isomerClone.transform.rotation = Quaternion.Slerp(targetRot, startRot, t);
                yield return null;
            }
        }
        else
        {
            // CHIRAL: wobble to show it doesn't fit, then slide back
            Quaternion wobbleBase = isomerClone.transform.rotation;
            elapsed = 0;
            while (elapsed < holdDuration)
            {
                elapsed += Time.deltaTime;
                float wobbleAngle = Mathf.Sin(elapsed * 8f) * 15f;
                isomerClone.transform.rotation = wobbleBase * Quaternion.Euler(0, wobbleAngle, 0);
                yield return null;
            }

            // Slide back
            elapsed = 0;
            float returnDuration = overlayDuration * 0.6f;
            Quaternion currentRot = isomerClone.transform.rotation;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / returnDuration);
                isomerClone.transform.position = Vector3.Lerp(targetPos, startPos, t);
                isomerClone.transform.rotation = Quaternion.Slerp(currentRot, startRot, t);
                yield return null;
            }
        }

        // Reset to pre-animation state
        isomerClone.transform.position = startPos;
        isomerClone.transform.rotation = startRot;
        isomerClone.transform.localScale = startScale;

        Debug.Log($"[IsomerAnim] Overlay complete - {(isIdentical ? "SUPERIMPOSABLE" : "NOT superimposable")}");
    }

    /// <summary>
    /// Räumt alles auf
    /// </summary>
    public void ClearEnantiomer()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        if (isomerClone != null)
        {
            Destroy(isomerClone);
            isomerClone = null;
        }

        // Move original back to center
        if (originalRenderer != null && isShowingIsomer)
        {
            originalRenderer.transform.position = originalPosition;
        }

        isShowingIsomer = false;
        isConformer = false;
    }

    void OnDestroy()
    {
        ClearEnantiomer();
    }
}
