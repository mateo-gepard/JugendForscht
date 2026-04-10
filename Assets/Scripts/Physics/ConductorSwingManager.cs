using UnityEngine;

/// <summary>
/// Singleton-Manager für die Leiterschaukel.
/// Orchestriert MagneticFieldVolume (geteilt mit Lorentz-Labor),
/// ConductorSwing und SwingVectorDisplay.
///
/// Position wird relativ zu Camera.main berechnet (wie alle VR-Objekte).
///
/// Phase 1: Physik-Core, Vektorpfeile, ContextMenu-Tests
/// Phase 2: WebSocket-Anbindung + iPad-Controller-Tab
/// Phase 3: XR-Grab für Hufeisenmagnet
/// </summary>
public class ConductorSwingManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Singleton
    // ════════════════════════════════════════════════════════════

    public static ConductorSwingManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    //  Referenzen
    // ════════════════════════════════════════════════════════════

    [Header("Referenzen")]
    public MagneticFieldVolume fieldVolume;
    public ConductorSwing conductor;
    public SwingVectorDisplay vectorDisplay;
    public SwingFieldGrab fieldGrab;

    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Standard-Parameter")]
    [Tooltip("B-Feld Stärke in Tesla")]
    public float defaultFieldStrength = 0.33f;

    [Tooltip("Lokale Feldrichtung")]
    public Vector3 defaultFieldDirection = Vector3.forward;

    [Tooltip("Box-Größe des Magnetfelds")]
    public Vector3 defaultVolumeSize = new Vector3(0.4f, 0.4f, 0.4f);

    [Tooltip("Stromstärke in Ampere")]
    public float defaultCurrent = 5f;

    [Tooltip("Leiterlänge in Metern")]
    public float defaultConductorLength = 0.3f;

    [Tooltip("Pendellänge in Metern")]
    public float defaultPendulumLength = 0.25f;

    // ════════════════════════════════════════════════════════════
    //  Zustand
    // ════════════════════════════════════════════════════════════

    [Header("Zustand (Read Only)")]
    [SerializeField] private bool quizMode;

    public bool IsQuizMode => quizMode;

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Position relativ zur Kamera
        PositionInFrontOfCamera();

        EnsureComponents();
    }

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    /// <summary>Strom ein/aus toggeln.</summary>
    public void ToggleCurrent()
    {
        if (conductor != null) conductor.ToggleCurrent();
    }

    /// <summary>Strom einschalten.</summary>
    public void CurrentOn()
    {
        if (conductor != null) conductor.CurrentOn();
    }

    /// <summary>Strom ausschalten.</summary>
    public void CurrentOff()
    {
        if (conductor != null) conductor.CurrentOff();
    }

    /// <summary>Stromrichtung umkehren.</summary>
    public void ReverseCurrentDirection()
    {
        if (conductor != null) conductor.ReverseCurrentDirection();
    }

    /// <summary>Stromrichtung setzen.</summary>
    public void SetCurrentDirection(int dir)
    {
        if (conductor != null) conductor.SetCurrentDirection(dir);
    }

    /// <summary>Magnetfeld umkehren (N/S flip).</summary>
    public void ReverseField()
    {
        if (fieldVolume != null)
        {
            fieldVolume.localFieldDirection = -fieldVolume.localFieldDirection;
            fieldVolume.RebuildArrows();
        }
    }

    /// <summary>Pendel Reset.</summary>
    public void ResetSwing()
    {
        if (conductor != null)
        {
            conductor.CurrentOff();
            conductor.ResetPendulum();
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

    /// <summary>Stromstärke ändern.</summary>
    public void SetCurrent(float ampere)
    {
        if (conductor != null)
            conductor.current = ampere;
    }

    // ════════════════════════════════════════════════════════════
    //  Automatische Erzeugung
    // ════════════════════════════════════════════════════════════

    private void EnsureComponents()
    {
        // B-Feld Box (eigene, nicht mit Lorentz-Labor geteilt)
        if (fieldVolume == null)
        {
            GameObject boxObj = new GameObject("B-Feld Volumen (Schaukel)");
            boxObj.transform.SetParent(transform, false);
            fieldVolume = boxObj.AddComponent<MagneticFieldVolume>();
            fieldVolume.fieldStrength = defaultFieldStrength;
            fieldVolume.localFieldDirection = defaultFieldDirection;
            fieldVolume.volumeSize = defaultVolumeSize;
        }

        // Aufhänge-Punkt: oben in der Mitte der Box
        if (conductor == null)
        {
            // Pivot = oberer Rand des Magnetfeld-Volumens
            GameObject pivotObj = new GameObject("Pendel-Pivot");
            pivotObj.transform.SetParent(transform, false);
            pivotObj.transform.localPosition = new Vector3(
                0f, defaultVolumeSize.y * 0.5f, 0f
            );

            // Stab hängt unter dem Pivot
            GameObject stabObj = new GameObject("Leiterschaukel");
            stabObj.transform.SetParent(transform, false);
            stabObj.transform.localPosition = new Vector3(
                0f, defaultVolumeSize.y * 0.5f - defaultPendulumLength, 0f
            );

            conductor = stabObj.AddComponent<ConductorSwing>();
            conductor.fieldVolume = fieldVolume;
            conductor.current = defaultCurrent;
            conductor.conductorLength = defaultConductorLength;
            conductor.pendulumLength = defaultPendulumLength;
        }

        // Vektor-Pfeile
        if (vectorDisplay == null)
        {
            GameObject vecObj = new GameObject("Schaukel-Vektor-Pfeile");
            vecObj.transform.SetParent(transform, false);
            vectorDisplay = vecObj.AddComponent<SwingVectorDisplay>();
            vectorDisplay.conductor = conductor;
            vectorDisplay.fieldVolume = fieldVolume;
        }

        // XR Grab (Phase 3) – greift das gesamte Setup
        if (fieldGrab == null)
        {
            fieldGrab = gameObject.AddComponent<SwingFieldGrab>();
        }
    }

    private void PositionInFrontOfCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        // Etwas rechts vom Lorentz-Labor, damit beide nebeneinander stehen
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 pos = cam.transform.position + forward * 0.6f + right * 0.4f;
        pos.y = cam.transform.position.y - 0.1f;
        transform.position = pos;
    }

    // ════════════════════════════════════════════════════════════
    //  Editor-Tests (ContextMenu)
    // ════════════════════════════════════════════════════════════

    [ContextMenu("⚡ Strom EIN")]
    private void TestCurrentOn() { CurrentOn(); }

    [ContextMenu("⏹ Strom AUS")]
    private void TestCurrentOff() { CurrentOff(); }

    [ContextMenu("↔ Stromrichtung umkehren")]
    private void TestReverse() { ReverseCurrentDirection(); }

    [ContextMenu("🧲 Magnetfeld umkehren")]
    private void TestReverseField() { ReverseField(); }

    [ContextMenu("⏹ Pendel Reset")]
    private void TestReset() { ResetSwing(); }

    [ContextMenu("Quiz-Modus togglen")]
    private void TestToggleQuiz() { ToggleQuizMode(); }
}
