using UnityEngine;

/// <summary>
/// Singleton-Manager für die fallende Leiterschleife.
/// Orchestriert MagneticFieldVolume + InductionLoop.
///
/// Position relativ zu Camera.main (wie alle VR-Experimente).
///
/// Phase 1: Physik-Core, Induktionsstrom-Pfeile, Spalt-Modus
/// Phase 2: WebSocket + iPad-Tab (Drop, Reset, Spalt, Parameter)
/// Phase 3: XR-Grab (Schleife per Hand durch das Feld schieben)
/// </summary>
public class InductionLoopManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Singleton
    // ════════════════════════════════════════════════════════════

    public static InductionLoopManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    //  Referenzen
    // ════════════════════════════════════════════════════════════

    [Header("Referenzen")]
    public MagneticFieldVolume fieldVolume;
    public InductionLoop loop;
    public InductionLoopGrab loopGrab;
    public FingerRuleChecker fingerRuleChecker;

    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Magnetfeld")]
    [Tooltip("B-Feld Stärke in Tesla")]
    public float defaultFieldStrength = 0.33f;

    [Tooltip("Lokale Feldrichtung (Z = durch die Schleifenfläche)")]
    public Vector3 defaultFieldDirection = Vector3.forward;

    [Tooltip("Box-Größe des Magnetfelds")]
    public Vector3 defaultVolumeSize = new Vector3(0.3f, 0.25f, 0.3f);

    [Header("Schleife")]
    public float defaultLoopWidth  = 0.15f;
    public float defaultLoopHeight = 0.12f;
    public float defaultResistance = 0.15f;

    [Tooltip("Abstand der Schleife über dem Feld-Volumen (Start)")]
    public float dropHeight = 0.15f;

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        PositionInFrontOfCamera();
        EnsureComponents();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    /// <summary>Schleife fallen lassen.</summary>
    public void Drop()
    {
        if (loop != null) loop.Drop();
    }

    /// <summary>Alles zurücksetzen.</summary>
    public void ResetExperiment()
    {
        if (loop != null) loop.ResetLoop();
    }

    /// <summary>Spalt öffnen/schließen.</summary>
    public void ToggleSlit()
    {
        if (loop != null) loop.ToggleSlit();
    }

    /// <summary>Spalt explizit setzen.</summary>
    public void SetSlit(bool open)
    {
        if (loop != null) loop.SetSlit(open);
    }

    /// <summary>B-Feld Stärke ändern.</summary>
    public void SetFieldStrength(float tesla)
    {
        if (fieldVolume != null) fieldVolume.fieldStrength = tesla;
    }

    /// <summary>Widerstand ändern.</summary>
    public void SetResistance(float ohm)
    {
        if (loop != null) loop.resistance = ohm;
    }

    /// <summary>Ist der Spalt offen?</summary>
    public bool IsSlitOpen => loop != null && loop.IsSlitOpen;

    /// <summary>Stromrichtung: +1=CW, -1=CCW, 0=keiner.</summary>
    public int CurrentFlowSign => loop != null ? loop.CurrentFlowSign : 0;

    /// <summary>Drei-Finger-Regel togglen.</summary>
    public void ToggleFingerRule()
    {
        EnsureFingerRuleChecker();
        if (fingerRuleChecker != null)
            fingerRuleChecker.Toggle();
    }

    /// <summary>Drei-Finger-Regel an/aus.</summary>
    public void SetFingerRule(bool enabled)
    {
        EnsureFingerRuleChecker();
        if (fingerRuleChecker != null)
            fingerRuleChecker.SetActive(enabled);
    }

    public bool IsFingerRuleActive => fingerRuleChecker != null && fingerRuleChecker.IsActive;

    /// <summary>Aktuelle Phase.</summary>
    public string PhaseString
    {
        get
        {
            if (loop == null) return "idle";
            switch (loop.CurrentPhase)
            {
                case InductionLoop.LoopPhase.Idle:        return "idle";
                case InductionLoop.LoopPhase.Above:       return "above";
                case InductionLoop.LoopPhase.Entering:    return "entering";
                case InductionLoop.LoopPhase.FullyInside: return "inside";
                case InductionLoop.LoopPhase.Exiting:     return "exiting";
                case InductionLoop.LoopPhase.Below:       return "below";
                default: return "idle";
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Automatische Erzeugung
    // ════════════════════════════════════════════════════════════

    private void EnsureComponents()
    {
        // B-Feld Volumen (zentriert im Manager)
        if (fieldVolume == null)
        {
            var go = new GameObject("B-Feld Volumen (Induktion)");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            fieldVolume = go.AddComponent<MagneticFieldVolume>();
            fieldVolume.fieldStrength     = defaultFieldStrength;
            fieldVolume.localFieldDirection = defaultFieldDirection;
            fieldVolume.volumeSize        = defaultVolumeSize;
        }

        // Leiterschleife (startet über dem Feld)
        if (loop == null)
        {
            var go = new GameObject("Leiterschleife");
            go.transform.SetParent(transform, false);

            // Startposition: über der Oberkante des Feldes + dropHeight
            float fieldTopLocal = defaultVolumeSize.y * 0.5f;
            go.transform.localPosition = new Vector3(
                0f, fieldTopLocal + dropHeight + defaultLoopHeight * 0.5f, 0f
            );

            loop = go.AddComponent<InductionLoop>();
            loop.fieldVolume    = fieldVolume;
            loop.loopWidth      = defaultLoopWidth;
            loop.loopHeight     = defaultLoopHeight;
            loop.resistance     = defaultResistance;
        }

        // XR Grab (Phase 3)
        if (loopGrab == null)
        {
            loopGrab = gameObject.AddComponent<InductionLoopGrab>();
        }
    }

    private void PositionInFrontOfCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 pos = cam.transform.position + forward * 0.6f;
        pos.y = cam.transform.position.y - 0.05f;
        transform.position = pos;
    }

    private void EnsureFingerRuleChecker()
    {
        if (fingerRuleChecker != null) return;

        fingerRuleChecker = FindObjectOfType<FingerRuleChecker>();
        if (fingerRuleChecker == null)
        {
            GameObject obj = new GameObject("Finger-Regel Checker (Induktion)");
            obj.transform.SetParent(transform, false);
            fingerRuleChecker = obj.AddComponent<FingerRuleChecker>();
        }
        fingerRuleChecker.mode = FingerRuleChecker.FingerRuleMode.Induction;
        fingerRuleChecker.fieldVolume = fieldVolume;
        fingerRuleChecker.inductionLoop = loop;
    }

    // ════════════════════════════════════════════════════════════
    //  Editor-Tests
    // ════════════════════════════════════════════════════════════

    [ContextMenu("▼ Drop")]
    private void TestDrop() { Drop(); }

    [ContextMenu("↺ Reset")]
    private void TestReset() { ResetExperiment(); }

    [ContextMenu("🔌 Spalt Toggle")]
    private void TestSlit() { ToggleSlit(); }
}
