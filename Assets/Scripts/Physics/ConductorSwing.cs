using UnityEngine;

/// <summary>
/// Leiterschaukel: Ein stromdurchflossener Kupferstab hängt pendelnd
/// in einem homogenen Magnetfeld. Fließt Strom, wirkt die Lorentzkraft
/// F = I * L * (dl × B) und lenkt den Stab seitlich aus.
///
/// Physik:
///   F_L = I * L * (dI_hat × B)
///   wobei dI_hat = technische Stromrichtung (normalisiert)
///
/// HANDEDNESS-KORREKTUR (wie ChargedParticle):
///   Unity linkshändig → Cross(B, I_hat) statt Cross(I_hat, B)
///   damit die Ablenkung mit der echten Rechte-Hand-Regel übereinstimmt.
///
/// Aufbau:
///   - HingeJoint am oberen Ende simuliert das Pendel
///   - Rigidbody für physikalische Schwingung + Dämpfung
///   - Strom ein/aus + Richtungsumkehr per API
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ConductorSwing : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Strom")]
    [Tooltip("Stromstärke in Ampere (Betrag)")]
    public float current = 5f;

    [Tooltip("Strom ein oder aus")]
    public bool currentOn = false;

    [Tooltip("Technische Stromrichtung: +1 oder -1 entlang lokaler Y-Achse des Stabs")]
    public int currentDirection = 1;

    [Header("Leiter")]
    [Tooltip("Wirksame Leiterlänge in Metern (= Stablänge im Magnetfeld)")]
    public float conductorLength = 0.3f;

    [Tooltip("Radius des Kupferstabs")]
    public float conductorRadius = 0.006f;

    [Header("Pendel")]
    [Tooltip("Länge der Aufhängefäden")]
    public float pendulumLength = 0.25f;

    [Tooltip("Dämpfung des Pendels (Angular Drag)")]
    public float pendulumDamping = 0.5f;

    [Header("Referenzen")]
    [Tooltip("Wird automatisch gesucht")]
    public MagneticFieldVolume fieldVolume;

    // ════════════════════════════════════════════════════════════
    //  Zustand (Read Only)
    // ════════════════════════════════════════════════════════════

    [Header("Zustand")]
    [SerializeField] private Vector3 currentForce;
    [SerializeField] private Vector3 currentDirection3D;

    public Vector3 CurrentForce => currentForce;
    public Vector3 CurrentDirectionWorld => currentDirection3D;

    /// <summary>Ist der Strom aktiv?</summary>
    public bool IsCurrentOn => currentOn;

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private Rigidbody rb;
    private HingeJoint hinge;
    private MeshRenderer meshRenderer;
    private Material conductorMaterial;

    // Visuals
    private GameObject strandLeft, strandRight;

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    /// <summary>Strom einschalten.</summary>
    [ContextMenu("Strom EIN")]
    public void CurrentOn()
    {
        currentOn = true;
        UpdateVisualColor();
    }

    /// <summary>Strom ausschalten.</summary>
    [ContextMenu("Strom AUS")]
    public void CurrentOff()
    {
        currentOn = false;
        currentForce = Vector3.zero;
        UpdateVisualColor();
    }

    /// <summary>Strom toggeln.</summary>
    public void ToggleCurrent()
    {
        if (currentOn) CurrentOff();
        else CurrentOn();
    }

    /// <summary>Stromrichtung umkehren.</summary>
    [ContextMenu("Stromrichtung umkehren")]
    public void ReverseCurrentDirection()
    {
        currentDirection = -currentDirection;
    }

    /// <summary>Setzt die Stromrichtung explizit (+1 oder -1).</summary>
    public void SetCurrentDirection(int dir)
    {
        currentDirection = dir >= 0 ? 1 : -1;
    }

    /// <summary>Pendel zurück in Ruhelage.</summary>
    [ContextMenu("Pendel Reset")]
    public void ResetPendulum()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 0.05f;
        rb.drag = 0.1f;
        rb.angularDrag = pendulumDamping;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (fieldVolume == null)
            fieldVolume = FindObjectOfType<MagneticFieldVolume>();

        BuildVisuals();
        SetupHinge();
        UpdateVisualColor();
    }

    void FixedUpdate()
    {
        if (!currentOn || fieldVolume == null)
        {
            currentForce = Vector3.zero;
            currentDirection3D = Vector3.zero;
            return;
        }

        // Technische Stromrichtung: entlang lokaler Y-Achse des Stabs
        // (Y weil der Stab waagerecht hängt, Y = Längsachse)
        currentDirection3D = transform.TransformDirection(Vector3.up * currentDirection).normalized;

        Vector3 B = fieldVolume.GetWorldFieldVector();

        // ────────────────────────────────────────────────
        // HANDEDNESS-KORREKTUR (identisch zu ChargedParticle):
        // Reale Physik: F = I * L * (dI × B)   (rechtshändig)
        // Unity (LHS):  Cross(dI, B) → FALSCHES Vorzeichen
        // Korrekt:      F = I * L * Cross(B, dI)
        // ────────────────────────────────────────────────
        currentForce = current * conductorLength * Vector3.Cross(B, currentDirection3D);

        rb.AddForce(currentForce, ForceMode.Force);
    }

    // ════════════════════════════════════════════════════════════
    //  Visuals: Kupferstab + Aufhängefäden
    // ════════════════════════════════════════════════════════════

    private void BuildVisuals()
    {
        // Kupferstab (Zylinder entlang lokaler Y)
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "Kupferstab";
        cylinder.transform.SetParent(transform, false);
        cylinder.transform.localPosition = Vector3.zero;
        cylinder.transform.localScale = new Vector3(
            conductorRadius * 2f, conductorLength * 0.5f, conductorRadius * 2f
        );

        // Collider entfernen (Physik über HingeJoint)
        var col = cylinder.GetComponent<Collider>();
        if (col != null) Destroy(col);

        meshRenderer = cylinder.GetComponent<MeshRenderer>();
        conductorMaterial = new Material(
            Shader.Find("Custom/MoleculeUnlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard")
        );
        meshRenderer.sharedMaterial = conductorMaterial;

        // Aufhängefäden (dünne Stäbe von Stab-Ende nach oben)
        strandLeft = CreateStrand("Faden_Links", -conductorLength * 0.45f);
        strandRight = CreateStrand("Faden_Rechts", conductorLength * 0.45f);
    }

    private GameObject CreateStrand(string name, float yOffset)
    {
        GameObject strand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        strand.name = name;
        strand.transform.SetParent(transform, false);

        // Von Stab-Ende gerade nach oben
        strand.transform.localPosition = new Vector3(0f, yOffset, 0f)
            + Vector3.up * 0f; // Wird vom Pivot angepasst
        // Eigentlich: Faden von der Aufhängung zum Stab. Vereinfacht als
        // dünner Zylinder der in Y geht.
        float halfLen = pendulumLength * 0.5f;
        strand.transform.localPosition = new Vector3(0f, pendulumLength * 0.5f, 0f);
        strand.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        // Pivot-Trick: der Faden ist entlang der Eltern-Y, aber versetzt
        // zum Stab-Ende. Hier vereinfacht.
        strand.transform.localPosition = new Vector3(
            0f, pendulumLength * 0.5f, 0f
        );

        // Wir modellieren die Fäden einfacher: sie hängen von Hinge-Anchor,
        // Der Stab bewegt sich ja nur in eine Ebene. Kosmetisch genug.
        // Hier: Faden vom oberen Fixpunkt zum Stab-Rand
        // Setze als Kind des Stabs. Wird im LateUpdate korrigiert? Nein, zu komplex.

        // Einfach: 2 dünne Zylinder die von den Stab-Enden nach oben gehen
        strand.transform.localPosition = new Vector3(0f, 0f, 0f);
        strand.transform.localScale = new Vector3(0.002f, pendulumLength * 0.5f, 0.002f);

        // Position am Stabende
        // Stab liegt in Y-Richtung, also offset in Y
        var offsetObj = new GameObject(name + "_Offset");
        offsetObj.transform.SetParent(transform, false);
        offsetObj.transform.localPosition = new Vector3(0f, yOffset, 0f);

        strand.transform.SetParent(offsetObj.transform, false);
        strand.transform.localPosition = new Vector3(0f, pendulumLength * 0.5f, 0f);

        var col = strand.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var mr = strand.GetComponent<MeshRenderer>();
        Material mat = new Material(conductorMaterial);
        mat.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        mr.sharedMaterial = mat;

        return offsetObj;
    }

    private void UpdateVisualColor()
    {
        if (conductorMaterial == null) return;

        if (currentOn)
        {
            // Kupfer-Orange-Rot wenn Strom fließt
            conductorMaterial.color = currentDirection > 0
                ? new Color(1f, 0.6f, 0.2f, 1f)   // + Richtung: warm orange
                : new Color(0.3f, 0.6f, 1f, 1f);   // - Richtung: kühl blau
        }
        else
        {
            // Kupferfarbe wenn kein Strom
            conductorMaterial.color = new Color(0.85f, 0.55f, 0.25f, 1f);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  HingeJoint Setup
    // ════════════════════════════════════════════════════════════

    private void SetupHinge()
    {
        // ConfigurableJoint statt HingeJoint für mehr Kontrolle
        // Pendel schwingt in der XZ-Ebene (Stab hängt in Y)
        // Anchor: am Pendel-Aufhängepunkt (direkt über dem Stab)

        hinge = gameObject.AddComponent<HingeJoint>();
        hinge.anchor = new Vector3(0f, 0f, 0f);

        // Verbinden mit dem Parent (Aufhänge-Punkt = connected body = null → Welt)
        // Der Aufhängepunkt ist pendulumLength über dem Stab
        hinge.connectedAnchor = transform.position + Vector3.up * pendulumLength;
        hinge.autoConfigureConnectedAnchor = false;

        // Achse: Pendel schwingt um die Stab-Längsachse (lokale Y)
        // → Hinge-Achse = lokale Y
        hinge.axis = Vector3.up;

        // Grenzen optional (nicht nötig, Physik + Gravity reicht)
        hinge.useLimits = false;
    }
}
