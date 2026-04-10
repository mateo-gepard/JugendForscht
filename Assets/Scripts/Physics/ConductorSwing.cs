using UnityEngine;

/// <summary>
/// Leiterschaukel: Ein stromdurchflossener Kupferstab hängt pendelnd
/// in einem homogenen Magnetfeld. Fließt Strom, wirkt die Lorentzkraft
/// F = I * L * (dl × B) und lenkt den Stab seitlich aus.
///
/// Physik (analytisches Pendel statt HingeJoint):
///   θ̈ = -(g/L)·sin(θ) - γ·θ̇ + F_ext/(m·L) · cos(θ)
///
///   Lorentzkraft: F_L = I * L * Cross(B, Î)   (Unity-LHS-Korrektur)
///   Nur die Z-Komponente (senkrecht zur Schwing-Ebene) erzeugt Drehmoment.
///
/// Aufbau:
///   - Kein Rigidbody/HingeJoint → rein kinematisch via Euler-Integration
///   - Stab + 2 Aufhängefäden als visuelle Kinder
///   - Fäden strecken sich dynamisch vom Fixpunkt zum Stabende
/// </summary>
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

    [Tooltip("Technische Stromrichtung: +1 oder -1 entlang Stab")]
    public int currentDirection = 1;

    [Header("Leiter")]
    [Tooltip("Wirksame Leiterlänge in Metern")]
    public float conductorLength = 0.3f;

    [Tooltip("Radius des Kupferstabs")]
    public float conductorRadius = 0.006f;

    [Header("Pendel")]
    [Tooltip("Länge der Aufhängefäden")]
    public float pendulumLength = 0.25f;

    [Tooltip("Dämpfung (γ) – je größer, desto schneller beruhigt sich das Pendel")]
    public float damping = 1.2f;

    [Tooltip("Effektive Fallbeschleunigung")]
    public float gravity = 3f;

    [Tooltip("Masse des Stabs in kg")]
    public float mass = 0.05f;

    [Header("Referenzen")]
    [Tooltip("Wird automatisch gesucht")]
    public MagneticFieldVolume fieldVolume;

    // ════════════════════════════════════════════════════════════
    //  Zustand (Read Only)
    // ════════════════════════════════════════════════════════════

    [Header("Zustand")]
    [SerializeField] private Vector3 currentForce;
    [SerializeField] private Vector3 currentDirection3D;
    [SerializeField] private float theta;       // Auslenkwinkel in Rad
    [SerializeField] private float thetaDot;    // Winkelgeschwindigkeit

    public Vector3 CurrentForce => currentForce;
    public Vector3 CurrentDirectionWorld => currentDirection3D;
    public bool IsCurrentOn => currentOn;

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private MeshRenderer meshRenderer;
    private Material conductorMaterial;

    // Visuals
    private Transform barVisual;
    private Transform strandLeftCyl, strandRightCyl;

    // Pivot = Aufhängepunkt (lokal zum Parent)
    private Vector3 pivotLocal;

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    [ContextMenu("Strom EIN")]
    public void CurrentOn()
    {
        currentOn = true;
        UpdateVisualColor();
    }

    [ContextMenu("Strom AUS")]
    public void CurrentOff()
    {
        currentOn = false;
        currentForce = Vector3.zero;
        UpdateVisualColor();
    }

    public void ToggleCurrent()
    {
        if (currentOn) CurrentOff();
        else CurrentOn();
    }

    [ContextMenu("Stromrichtung umkehren")]
    public void ReverseCurrentDirection()
    {
        currentDirection = -currentDirection;
    }

    public void SetCurrentDirection(int dir)
    {
        currentDirection = dir >= 0 ? 1 : -1;
    }

    [ContextMenu("Pendel Reset")]
    public void ResetPendulum()
    {
        theta = 0f;
        thetaDot = 0f;
        ApplyPendulumPose();
    }

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        // fieldVolume wird vom Manager gesetzt — kein FindObjectOfType
        // (könnte ein Zombie-Feld eines gerade zerstörten Experiments finden)

        // Pivot ist pendulumLength über der Startposition
        pivotLocal = new Vector3(0f, pendulumLength, 0f);

        BuildVisuals();
        UpdateVisualColor();
        ApplyPendulumPose();
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // ── Lorentzkraft berechnen ──
        if (currentOn && fieldVolume != null)
        {
            // Stromrichtung entlang lokaler X des Eltern-Transforms
            // (der Stab dreht sich ja mit dem Pendel, also rotieren wir mit)
            Vector3 barRight = BarWorldRight();
            currentDirection3D = barRight * currentDirection;
            Vector3 B = fieldVolume.GetWorldFieldVector();
            currentForce = current * conductorLength * Vector3.Cross(B, currentDirection3D);
        }
        else
        {
            currentForce = Vector3.zero;
            currentDirection3D = Vector3.zero;
        }

        // ── Pendel-ODE: θ̈ = -(g/L)·sin(θ) - γ·θ̇ + F_tangential/(m·L) ──
        // Tangentialkomponente der Lorentz-Kraft in Schwingrichtung (Z im Parent-Space)
        Vector3 forceLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(currentForce)
            : currentForce;
        float F_tangential = forceLocal.z; // Z = Schwingrichtung

        float thetaDDot = -(gravity / pendulumLength) * Mathf.Sin(theta)
                        - damping * thetaDot
                        + F_tangential * Mathf.Cos(theta) / (mass * pendulumLength);

        // Symplektisches Euler (energieerhaltend genug für VR)
        thetaDot += thetaDDot * dt;
        theta += thetaDot * dt;

        // Sicherheitsbegrenzung
        theta = Mathf.Clamp(theta, -Mathf.PI * 0.45f, Mathf.PI * 0.45f);

        ApplyPendulumPose();
    }

    // ════════════════════════════════════════════════════════════
    //  Pendel-Geometrie
    // ════════════════════════════════════════════════════════════

    /// <summary>Setzt Position + Rotation anhand von θ.</summary>
    private void ApplyPendulumPose()
    {
        // Stab-Position: hängt am Pivot, schwingt in Z-Richtung (lokal)
        //   y = pivot.y - L·cos(θ)
        //   z = L·sin(θ)
        float y = pivotLocal.y - pendulumLength * Mathf.Cos(theta);
        float z = pendulumLength * Mathf.Sin(theta);
        transform.localPosition = new Vector3(0f, y, z);

        // Rotation: Stab kippt um die X-Achse (Stab liegt in X)
        transform.localRotation = Quaternion.Euler(theta * Mathf.Rad2Deg, 0f, 0f);

        // Fäden: von den Stabenden zum Fixpunkt strecken
        UpdateStrands();
    }

    /// <summary>Aktuelle Welt-Rechts-Richtung des Stabs.</summary>
    private Vector3 BarWorldRight()
    {
        if (transform.parent != null)
            return transform.parent.TransformDirection(Vector3.right);
        return Vector3.right;
    }

    // ════════════════════════════════════════════════════════════
    //  Visuals
    // ════════════════════════════════════════════════════════════

    private void BuildVisuals()
    {
        // Kupferstab (Zylinder waagerecht entlang lokaler X)
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "Kupferstab";
        cylinder.transform.SetParent(transform, false);
        cylinder.transform.localPosition = Vector3.zero;
        cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        cylinder.transform.localScale = new Vector3(
            conductorRadius * 2f, conductorLength * 0.5f, conductorRadius * 2f
        );
        barVisual = cylinder.transform;

        var col = cylinder.GetComponent<Collider>();
        if (col != null) Destroy(col);

        meshRenderer = cylinder.GetComponent<MeshRenderer>();
        conductorMaterial = new Material(
            Shader.Find("Custom/MoleculeUnlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard")
        );
        meshRenderer.sharedMaterial = conductorMaterial;

        // Aufhängefäden
        strandLeftCyl = CreateStrandCylinder("Faden_Links");
        strandRightCyl = CreateStrandCylinder("Faden_Rechts");
    }

    private Transform CreateStrandCylinder(string name)
    {
        GameObject strand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        strand.name = name;
        // Fäden sind Kinder des ConductorSwing-Parents (nicht des Stabs),
        // damit sie sich unabhängig zwischen Fixpunkt und Stabende strecken
        Transform parent = transform.parent != null ? transform.parent : transform;
        strand.transform.SetParent(parent, false);

        var c = strand.GetComponent<Collider>();
        if (c != null) Destroy(c);

        var mr = strand.GetComponent<MeshRenderer>();
        Material mat = new Material(conductorMaterial);
        mat.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        mr.sharedMaterial = mat;

        return strand.transform;
    }

    private void UpdateStrands()
    {
        if (strandLeftCyl == null || strandRightCyl == null) return;

        // Fixpunkte (lokal im Parent): gleiche X wie Stabenden, Y = Pivot-Höhe
        float halfBar = conductorLength * 0.45f;

        UpdateSingleStrand(strandLeftCyl, -halfBar);
        UpdateSingleStrand(strandRightCyl, halfBar);
    }

    private void UpdateSingleStrand(Transform strandT, float xOffset)
    {
        // Stabende in Parent-Space
        Vector3 barEnd = transform.localPosition
            + transform.localRotation * new Vector3(xOffset, 0f, 0f);

        // Fixpunkt oben
        Vector3 fixPoint = new Vector3(xOffset, pivotLocal.y, 0f);

        // Mitte + Ausrichtung
        Vector3 mid = (fixPoint + barEnd) * 0.5f;
        Vector3 diff = fixPoint - barEnd;
        float len = diff.magnitude;

        strandT.localPosition = mid;
        if (len > 0.001f)
            strandT.localRotation = Quaternion.FromToRotation(Vector3.up, diff.normalized);
        strandT.localScale = new Vector3(0.002f, len * 0.5f, 0.002f);
    }

    private void UpdateVisualColor()
    {
        if (conductorMaterial == null) return;

        if (currentOn)
        {
            conductorMaterial.color = currentDirection > 0
                ? new Color(1f, 0.6f, 0.2f, 1f)
                : new Color(0.3f, 0.6f, 1f, 1f);
        }
        else
        {
            conductorMaterial.color = new Color(0.85f, 0.55f, 0.25f, 1f);
        }
    }
}
