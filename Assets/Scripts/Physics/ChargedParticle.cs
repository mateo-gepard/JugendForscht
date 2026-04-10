using UnityEngine;

/// <summary>
/// Geladenes Teilchen, das durch ein MagneticFieldVolume fliegt.
/// 
/// Physik-Kern:
///   F_L = q * (v × B)
///
/// KRITISCH – Unity benutzt ein LINKSHÄNDIGES Koordinatensystem,
/// die reale Physik ist RECHTSHÄNDIG. Damit die VR-Ablenkung exakt
/// mit der menschlichen Drei-Finger-Regel übereinstimmt, wird das
/// Kreuzprodukt NEGIERT:
///   F_unity = -q * (v × B)_unity  =  q * (B × v)_unity
///
/// Das Teilchen nutzt Rigidbody + FixedUpdate für eine saubere,
/// stetige Kreisbahn. Masse wird so gewählt, dass der Gyrationsradius
/// sichtbar bleibt.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ChargedParticle : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Ladung")]
    [Tooltip("Vorzeichen der Ladung: +1 (Proton, rot) oder -1 (Elektron, blau)")]
    public int chargeSign = 1;

    [Tooltip("Betrag der Ladung in Coulomb (skaliert für Visualisierung)")]
    public float chargeMagnitude = 1f;

    [Header("Startbedingungen")]
    [Tooltip("Startgeschwindigkeit in lokalen Koordinaten des Volumens (m/s)")]
    public Vector3 localStartVelocity = new Vector3(0.15f, 0f, 0f);

    [Header("Darstellung")]
    [Tooltip("Radius der Teilchen-Kugel")]
    public float particleRadius = 0.02f;

    [Tooltip("Farbe für positive Ladung")]
    public Color positiveColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Tooltip("Farbe für negative Ladung")]
    public Color negativeColor = new Color(0.3f, 0.5f, 1f, 1f);

    [Header("Trail")]
    [Tooltip("Breite der Leuchtspur")]
    public float trailWidth = 0.008f;

    [Tooltip("Wie lange die Spur sichtbar bleibt (Sekunden)")]
    public float trailTime = 8f;

    [Header("Referenzen")]
    [Tooltip("Wird automatisch gesucht, wenn leer")]
    public MagneticFieldVolume fieldVolume;

    // ════════════════════════════════════════════════════════════
    //  Zustand (Read Only)
    // ════════════════════════════════════════════════════════════

    [Header("Zustand (Read Only)")]
    [SerializeField] private bool isSimulating;
    [SerializeField] private Vector3 currentForce;

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private Rigidbody rb;
    private MeshRenderer meshRenderer;
    private TrailRenderer trail;
    private Material particleMaterial;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    /// <summary>Effektive Ladung (Vorzeichen × Betrag).</summary>
    public float Charge => chargeSign * chargeMagnitude;

    /// <summary>Ist die Simulation aktiv?</summary>
    public bool IsSimulating => isSimulating;

    /// <summary>Aktuelle Lorentzkraft in Weltkoordinaten.</summary>
    public Vector3 CurrentForce => currentForce;

    /// <summary>Aktuelle Geschwindigkeit in Weltkoordinaten.</summary>
    public Vector3 Velocity => rb != null ? rb.velocity : Vector3.zero;

    /// <summary>
    /// Startet die Simulation: setzt Geschwindigkeit und aktiviert Physik.
    /// </summary>
    public void Play()
    {
        if (fieldVolume == null) return;

        rb.isKinematic = false;
        rb.velocity = fieldVolume.transform.TransformDirection(localStartVelocity);
        isSimulating = true;

        if (trail != null) trail.emitting = true;
    }

    /// <summary>
    /// Pausiert die Simulation (Teilchen friert ein).
    /// </summary>
    public void Pause()
    {
        if (!isSimulating) return;
        rb.isKinematic = true;
        isSimulating = false;
    }

    /// <summary>
    /// Setzt Fortsetzen nach Pause.
    /// </summary>
    public void Resume()
    {
        if (isSimulating) return;
        Vector3 vel = rb.velocity; // velocity bleibt gespeichert
        rb.isKinematic = false;
        rb.velocity = vel;
        isSimulating = true;
    }

    /// <summary>
    /// Setzt alles auf Anfangszustand zurück.
    /// </summary>
    public void ResetParticle()
    {
        isSimulating = false;
        currentForce = Vector3.zero;
        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
    }

    /// <summary>
    /// Wechselt das Vorzeichen der Ladung und aktualisiert die Farbe.
    /// </summary>
    public void ToggleCharge()
    {
        chargeSign = -chargeSign;
        ApplyColor();
    }

    /// <summary>
    /// Setzt die Ladung explizit (+1 oder -1).
    /// </summary>
    public void SetCharge(int sign)
    {
        chargeSign = sign >= 0 ? 1 : -1;
        ApplyColor();
    }

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        // Rigidbody konfigurieren
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.drag = 0f;
        rb.angularDrag = 0f;

        // Masse: bestimmt den Gyrationsradius r = mv / (qB)
        // Kleine Masse → enger Kreis, große Masse → weiter Kreis
        // Bei 0.5m Box + v=0.5 m/s + B=1T → r ≈ 0.17m (passt in Box)
        rb.mass = 0.15f;

        // Referenzen
        if (fieldVolume == null)
            fieldVolume = FindObjectOfType<MagneticFieldVolume>();

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // Visuals aufbauen
        BuildVisuals();
        ApplyColor();
    }

    void FixedUpdate()
    {
        if (!isSimulating || fieldVolume == null) return;

        Vector3 v = rb.velocity;
        if (v.sqrMagnitude < 1e-8f) return;

        // Prüfe ob Teilchen noch im Feld ist
        if (!fieldVolume.ContainsPoint(transform.position))
        {
            // Außerhalb: keine Kraft, Teilchen fliegt geradeaus weiter
            currentForce = Vector3.zero;
            return;
        }

        Vector3 B = fieldVolume.GetWorldFieldVector();

        // ────────────────────────────────────────────────────────
        // HANDEDNESS-KORREKTUR:
        // Reale Physik (rechtshändig):  F = q * (v × B)
        // Unity (linkshändig):          Vector3.Cross(v, B) liefert
        //                               das ENTGEGENGESETZTE Ergebnis.
        //
        // Lösung: F_unity = q * Vector3.Cross(B, v)
        //         (Argumente vertauscht == Negation des Kreuzprodukts)
        //
        // So zeigt der Kraftpfeil in VR exakt dorthin, wo die
        // menschliche rechte Hand (+) bzw. linke Hand (-) hinzeigt.
        // ────────────────────────────────────────────────────────
        float q = Charge;
        currentForce = q * Vector3.Cross(B, v);

        rb.AddForce(currentForce, ForceMode.Force);
    }

    void OnDestroy()
    {
        if (particleMaterial != null) Destroy(particleMaterial);
    }

    // ════════════════════════════════════════════════════════════
    //  Visuals
    // ════════════════════════════════════════════════════════════

    private void BuildVisuals()
    {
        // Kugel
        MeshFilter mf = gameObject.GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = LowPolyMeshes.GetSphere();

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Shader shader = Shader.Find("Custom/MoleculeUnlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");
        particleMaterial = new Material(shader);
        particleMaterial.enableInstancing = true;
        meshRenderer.sharedMaterial = particleMaterial;

        transform.localScale = Vector3.one * particleRadius * 2f;

        // Trail
        trail = gameObject.GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = trailTime;
        trail.startWidth = trailWidth;
        trail.endWidth = trailWidth * 0.3f;
        trail.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        trail.numCornerVertices = 4;
        trail.numCapVertices = 2;
        trail.minVertexDistance = 0.01f;
        trail.emitting = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void ApplyColor()
    {
        Color c = chargeSign >= 0 ? positiveColor : negativeColor;

        if (particleMaterial != null)
            particleMaterial.color = c;

        if (trail != null)
        {
            Color trailStart = c;
            Color trailEnd = new Color(c.r, c.g, c.b, 0f);
            trail.startColor = trailStart;
            trail.endColor = trailEnd;
            if (trail.material != null)
                trail.material.color = c;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Editor-Tests
    // ════════════════════════════════════════════════════════════

    [ContextMenu("Test: Play")]
    private void TestPlay() { Play(); }

    [ContextMenu("Test: Pause")]
    private void TestPause() { Pause(); }

    [ContextMenu("Test: Resume")]
    private void TestResume() { Resume(); }

    [ContextMenu("Test: Reset")]
    private void TestReset() { ResetParticle(); }

    [ContextMenu("Test: Toggle Ladung")]
    private void TestToggleCharge() { ToggleCharge(); }
}
