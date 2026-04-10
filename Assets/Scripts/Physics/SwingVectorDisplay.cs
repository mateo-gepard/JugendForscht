using UnityEngine;

/// <summary>
/// Zeigt drei farbige 3D-Pfeile direkt am Leiterstab (Leiterschaukel):
///   • Technische Stromrichtung  I   (rot)
///   • Magnetfeld                B   (cyan)
///   • Lorentzkraft              F_L (gelb/orange)
///
/// Identisches Konzept wie VectorArrowDisplay, aber für den
/// makroskopischen Leiter. Die Pfeile drehen sich physikalisch
/// korrekt mit dem schwingenden Stab mit.
///
/// Der F_L-Pfeil kann für den Quiz-Modus ausgeblendet werden.
/// </summary>
public class SwingVectorDisplay : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Referenzen")]
    public ConductorSwing conductor;
    public MagneticFieldVolume fieldVolume;

    [Header("Darstellung")]
    public float arrowLength = 0.1f;
    public float arrowThickness = 0.012f;
    public float minVisibleLength = 0.01f;

    [Header("Farben")]
    public Color currentColor = new Color(1f, 0.3f, 0.3f, 1f);       // Rot
    public Color magneticFieldColor = new Color(0.3f, 0.7f, 1f, 1f);  // Cyan
    public Color forceColor = new Color(1f, 0.75f, 0.1f, 1f);         // Orange

    // ════════════════════════════════════════════════════════════
    //  Zustand
    // ════════════════════════════════════════════════════════════

    private bool showForceArrow = true;

    public bool ShowForceArrow
    {
        get => showForceArrow;
        set
        {
            showForceArrow = value;
            if (forceArrow != null) forceArrow.SetActive(value);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private GameObject currentArrow;
    private GameObject fieldArrow;
    private GameObject forceArrow;

    private Material matCurrent;
    private Material matField;
    private Material matForce;

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (conductor == null)
            conductor = GetComponent<ConductorSwing>() ?? GetComponentInParent<ConductorSwing>();
        if (fieldVolume == null)
            fieldVolume = FindObjectOfType<MagneticFieldVolume>();

        BuildArrows();
    }

    void LateUpdate()
    {
        if (conductor == null) return;

        // Position: Mitte des Stabs
        transform.position = conductor.transform.position;

        // I-Pfeil: technische Stromrichtung (nur wenn Strom fließt)
        Vector3 iVec = conductor.IsCurrentOn ? conductor.CurrentDirectionWorld * conductor.current : Vector3.zero;
        UpdateArrow(currentArrow, iVec);

        // B-Pfeil
        Vector3 bVec = fieldVolume != null ? fieldVolume.GetWorldFieldVector() : Vector3.zero;
        UpdateArrow(fieldArrow, bVec);

        // F_L-Pfeil
        if (showForceArrow)
        {
            UpdateArrow(forceArrow, conductor.CurrentForce);
        }
    }

    void OnDestroy()
    {
        if (matCurrent != null) Destroy(matCurrent);
        if (matField != null) Destroy(matField);
        if (matForce != null) Destroy(matForce);
    }

    // ════════════════════════════════════════════════════════════
    //  Pfeil-Erzeugung
    // ════════════════════════════════════════════════════════════

    private void BuildArrows()
    {
        matCurrent = CreateMaterial(currentColor);
        matField = CreateMaterial(magneticFieldColor);
        matForce = CreateMaterial(forceColor);

        currentArrow = CreateArrowObject("Pfeil_I (Strom)", matCurrent);
        fieldArrow = CreateArrowObject("Pfeil_B (Magnetfeld)", matField);
        forceArrow = CreateArrowObject("Pfeil_F (Lorentzkraft)", matForce);
    }

    private GameObject CreateArrowObject(string name, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = VectorArrowDisplay.GetStaticArrowMesh();

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return obj;
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Custom/MoleculeUnlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        mat.enableInstancing = true;
        return mat;
    }

    // ════════════════════════════════════════════════════════════
    //  Pfeil-Update
    // ════════════════════════════════════════════════════════════

    private void UpdateArrow(GameObject arrow, Vector3 worldVector)
    {
        if (arrow == null) return;

        float magnitude = worldVector.magnitude;
        if (magnitude < minVisibleLength)
        {
            arrow.SetActive(false);
            return;
        }

        arrow.SetActive(true);
        arrow.transform.rotation = Quaternion.LookRotation(worldVector.normalized);

        float length = arrowLength * Mathf.Clamp(magnitude, 0.1f, 20f);
        arrow.transform.localScale = new Vector3(arrowThickness, arrowThickness, length);
    }
}
