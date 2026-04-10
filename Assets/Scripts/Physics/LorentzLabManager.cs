using UnityEngine;

/// <summary>
/// Singleton-Manager für das Lorentz-Labor.
/// Orchestriert MagneticFieldVolume, ChargedParticle und VectorArrowDisplay.
///
/// Phase 1: Reine Physik + Visualisierung.
///          Steuerung über [ContextMenu] im Unity-Editor.
///          Kein Netzwerk, kein XR-Grab.
///
/// Phase 2: Anbindung an WebSocketServer + Control Panel.
/// Phase 3: XR-Interaktion (Box greifbar machen).
/// </summary>
public class LorentzLabManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Singleton
    // ════════════════════════════════════════════════════════════

    public static LorentzLabManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    //  Referenzen
    // ════════════════════════════════════════════════════════════

    [Header("Referenzen")]
    [Tooltip("Wird automatisch erzeugt, wenn leer")]
    public MagneticFieldVolume fieldVolume;

    [Tooltip("Wird automatisch erzeugt, wenn leer")]
    public ChargedParticle particle;

    [Tooltip("Wird automatisch erzeugt, wenn leer")]
    public VectorArrowDisplay vectorDisplay;

    [Tooltip("Wird automatisch erzeugt, wenn leer")]
    public FingerRuleChecker fingerRuleChecker;

    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Standard-Parameter")]
    [Tooltip("B-Feld Stärke in Tesla")]
    public float defaultFieldStrength = 0.33f;

    [Tooltip("Lokale Feldrichtung")]
    public Vector3 defaultFieldDirection = Vector3.forward;

    [Tooltip("Box-Größe in Metern")]
    public Vector3 defaultVolumeSize = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("Start-Geschwindigkeit des Teilchens (lokal zum Feld)")]
    public Vector3 defaultStartVelocity = new Vector3(0.15f, 0f, 0f);

    [Tooltip("+1 = Proton (rot), -1 = Elektron (blau)")]
    public int defaultChargeSign = 1;

    // ════════════════════════════════════════════════════════════
    //  Zustand
    // ════════════════════════════════════════════════════════════

    [Header("Zustand (Read Only)")]
    [SerializeField] private LorentzLabState state = LorentzLabState.Idle;
    [SerializeField] private bool quizMode;

    public enum LorentzLabState { Idle, Running, Paused }

    /// <summary>Aktuelle Simulation aktiv?</summary>
    public bool IsRunning => state == LorentzLabState.Running;

    /// <summary>Ist der Quiz-Modus aktiv (F_L-Pfeil ausgeblendet)?</summary>
    public bool IsQuizMode => quizMode;

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureComponents();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API (Phase 2 wird diese aufrufen)
    // ════════════════════════════════════════════════════════════

    /// <summary>Simulation starten.</summary>
    public void Play()
    {
        if (particle == null) return;

        if (state == LorentzLabState.Paused)
        {
            particle.Resume();
        }
        else
        {
            particle.ResetParticle();
            particle.Play();
        }
        state = LorentzLabState.Running;
    }

    /// <summary>Simulation pausieren.</summary>
    public void Pause()
    {
        if (particle == null || state != LorentzLabState.Running) return;
        particle.Pause();
        state = LorentzLabState.Paused;
    }

    /// <summary>Alles zurücksetzen.</summary>
    public void Reset()
    {
        if (particle == null) return;
        particle.ResetParticle();
        state = LorentzLabState.Idle;
    }

    /// <summary>Ladung umschalten (+/-).</summary>
    public void ToggleCharge()
    {
        if (particle == null) return;
        particle.ToggleCharge();

        // Bei laufender Simulation: Reset + sofort neu starten,
        // damit die neue Physikspur sichtbar wird.
        if (state == LorentzLabState.Running)
        {
            particle.ResetParticle();
            particle.Play();
        }
    }

    /// <summary>Ladung explizit setzen.</summary>
    public void SetCharge(int sign)
    {
        if (particle == null) return;
        particle.SetCharge(sign);

        if (state == LorentzLabState.Running)
        {
            particle.ResetParticle();
            particle.Play();
        }
    }

    /// <summary>Quiz-Modus: F_L-Pfeil ausblenden.</summary>
    public void SetQuizMode(bool enabled)
    {
        quizMode = enabled;
        if (vectorDisplay != null)
            vectorDisplay.ShowForceArrow = !enabled;
    }

    /// <summary>Quiz-Modus togglen.</summary>
    public void ToggleQuizMode()
    {
        SetQuizMode(!quizMode);
    }

    /// <summary>B-Feld Stärke ändern.</summary>
    public void SetFieldStrength(float tesla)
    {
        if (fieldVolume != null)
            fieldVolume.fieldStrength = tesla;
    }

    /// <summary>Startgeschwindigkeit ändern.</summary>
    public void SetStartVelocity(Vector3 localVelocity)
    {
        if (particle != null)
            particle.localStartVelocity = localVelocity;
    }

    /// <summary>Drei-Finger-Regel Modus setzen.</summary>
    public void SetFingerRule(bool enabled)
    {
        EnsureFingerRuleChecker();
        if (fingerRuleChecker != null)
            fingerRuleChecker.SetActive(enabled);
    }

    /// <summary>Drei-Finger-Regel Modus togglen.</summary>
    public void ToggleFingerRule()
    {
        EnsureFingerRuleChecker();
        if (fingerRuleChecker != null)
            fingerRuleChecker.Toggle();
    }

    /// <summary>Ist die Drei-Finger-Regel aktiv?</summary>
    public bool IsFingerRuleActive => fingerRuleChecker != null && fingerRuleChecker.IsActive;

    // ════════════════════════════════════════════════════════════
    //  Automatische Erzeugung
    // ════════════════════════════════════════════════════════════

    private void EnsureComponents()
    {
        // B-Feld Box — immer eigenes erzeugen (nicht FindObjectOfType,
        // da ein gerade-zerstörtes Experiment als Zombie gefunden werden könnte)
        if (fieldVolume == null)
        {
            GameObject boxObj = new GameObject("B-Feld Volumen");
            boxObj.transform.SetParent(transform, false);
            fieldVolume = boxObj.AddComponent<MagneticFieldVolume>();
            fieldVolume.fieldStrength = defaultFieldStrength;
            fieldVolume.localFieldDirection = defaultFieldDirection;
            fieldVolume.volumeSize = defaultVolumeSize;
            fieldVolume.RebuildArrows();
        }

        // Teilchen — eigenes erzeugen
        if (particle == null)
        {
            GameObject partObj = new GameObject("Geladenes Teilchen");
            partObj.transform.SetParent(transform, false);
            // Startposition: linke Seite des Volumens, Mitte
            // Startposition: AUSSERHALB links vom Volumen → fliegt hinein
            partObj.transform.localPosition = new Vector3(
                -defaultVolumeSize.x * 0.8f, 0f, 0f
            );
            particle = partObj.AddComponent<ChargedParticle>();
            particle.fieldVolume = fieldVolume;
            particle.localStartVelocity = defaultStartVelocity;
            particle.chargeSign = defaultChargeSign;
        }

        // Vektor-Pfeile — eigenes erzeugen
        if (vectorDisplay == null)
        {
            GameObject vecObj = new GameObject("Vektor-Pfeile");
            vecObj.transform.SetParent(transform, false);
            vectorDisplay = vecObj.AddComponent<VectorArrowDisplay>();
            vectorDisplay.particle = particle;
            vectorDisplay.fieldVolume = fieldVolume;
        }
    }

    private void EnsureFingerRuleChecker()
    {
        if (fingerRuleChecker != null) return;

        fingerRuleChecker = FindObjectOfType<FingerRuleChecker>();
        if (fingerRuleChecker == null)
        {
            GameObject obj = new GameObject("Finger-Regel Checker");
            obj.transform.SetParent(transform, false);
            fingerRuleChecker = obj.AddComponent<FingerRuleChecker>();
            fingerRuleChecker.vectorDisplay = vectorDisplay;
            fingerRuleChecker.particle = particle;
            fingerRuleChecker.fieldVolume = fieldVolume;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Editor-Tests (ContextMenu)
    // ════════════════════════════════════════════════════════════

    [ContextMenu("▶ Play")]
    private void TestPlay() { Play(); }

    [ContextMenu("⏸ Pause")]
    private void TestPause() { Pause(); }

    [ContextMenu("⏹ Reset")]
    private void TestReset() { Reset(); }

    [ContextMenu("± Ladung umschalten")]
    private void TestToggleCharge() { ToggleCharge(); }

    [ContextMenu("Quiz-Modus togglen")]
    private void TestToggleQuiz() { ToggleQuizMode(); }

    [ContextMenu("▶ Play + Quiz (Lehrer-Demo)")]
    private void TestPlayQuiz()
    {
        SetQuizMode(true);
        Play();
    }

    [ContextMenu("Komplett neu aufbauen")]
    private void TestRebuild()
    {
        // Alle Kinder löschen und neu erzeugen
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
            else DestroyImmediate(transform.GetChild(i).gameObject);
        }
        fieldVolume = null;
        particle = null;
        vectorDisplay = null;
        EnsureComponents();
    }
}
