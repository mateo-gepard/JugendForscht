using UnityEngine;
using Oculus.Interaction.Input;

/// <summary>
/// Drei-Finger-Regel der rechten Hand (UVW-Regel):
///   Daumen      → v   (Geschwindigkeit, grün)
///   Zeigefinger → B   (Magnetfeld, cyan)
///   Mittelfinger → F_L (Lorentzkraft, orange)
///
/// Erkennt per Hand-Tracking, in welche Richtung die drei Finger
/// der rechten Hand zeigen. Vergleicht mit den physikalischen
/// Vektoren und markiert alle Pfeilspitzen grün (richtig) oder
/// rot (falsch).
///
/// Wird über die iPad-UI oder LorentzLabManager aktiviert.
/// </summary>
public class FingerRuleChecker : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Referenzen")]
    [Tooltip("Wird automatisch gesucht")]
    public Hand rightHand;
    public VectorArrowDisplay vectorDisplay;
    public ChargedParticle particle;
    public MagneticFieldVolume fieldVolume;

    [Header("Toleranz")]
    [Tooltip("Maximale Abweichung in Grad pro Finger")]
    public float angleThreshold = 20f;

    // ════════════════════════════════════════════════════════════
    //  Zustand
    // ════════════════════════════════════════════════════════════

    [Header("Zustand (Read Only)")]
    [SerializeField] private bool isActive;
    [SerializeField] private bool allCorrect;

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private GameObject labelRoot;
    private TextMesh labelThumb;
    private TextMesh labelIndex;
    private TextMesh labelMiddle;

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    public bool IsActive => isActive;
    public bool AllCorrect => allCorrect;

    public void SetActive(bool active)
    {
        isActive = active;
        if (labelRoot != null) labelRoot.SetActive(active);

        if (!active && vectorDisplay != null)
            vectorDisplay.SetTipIndicators(false, false);
    }

    public void Toggle()
    {
        SetActive(!isActive);
    }

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Start()
    {
        FindReferences();
        CreateLabels();
        if (labelRoot != null) labelRoot.SetActive(isActive);
    }

    void Update()
    {
        if (!isActive) return;

        // Hand nicht getrackt → Labels + Indikatoren ausblenden
        if (rightHand == null || !rightHand.IsTrackedDataValid)
        {
            if (labelRoot != null) labelRoot.SetActive(false);
            if (vectorDisplay != null) vectorDisplay.SetTipIndicators(false, false);
            return;
        }

        if (labelRoot != null) labelRoot.SetActive(true);

        // ── Finger-Richtungen aus den letzten beiden Knochen ──
        Vector3 thumbDir = GetFingerDirection(HandJointId.HandThumb2, HandJointId.HandThumbTip);
        Vector3 indexDir = GetFingerDirection(HandJointId.HandIndex2, HandJointId.HandIndexTip);
        Vector3 middleDir = GetFingerDirection(HandJointId.HandMiddle2, HandJointId.HandMiddleTip);

        // ── Erwartete physikalische Richtungen ──
        GetExpectedDirections(out Vector3 vDir, out Vector3 bDir, out Vector3 fDir);

        // ── Winkelvergleich ──
        bool thumbOk = vDir.sqrMagnitude > 0.01f
                    && Vector3.Angle(thumbDir, vDir) <= angleThreshold;
        bool indexOk = bDir.sqrMagnitude > 0.01f
                    && Vector3.Angle(indexDir, bDir) <= angleThreshold;
        bool middleOk = fDir.sqrMagnitude > 0.01f
                     && Vector3.Angle(middleDir, fDir) <= angleThreshold;

        allCorrect = thumbOk && indexOk && middleOk;

        // ── Pfeilspitzen-Farbe setzen ──
        if (vectorDisplay != null)
            vectorDisplay.SetTipIndicators(true, allCorrect);

        // ── Labels an Fingerspitzen positionieren ──
        UpdateLabels();
    }

    // ════════════════════════════════════════════════════════════
    //  Finger-Richtung aus Hand-Joints
    // ════════════════════════════════════════════════════════════

    private Vector3 GetFingerDirection(HandJointId baseJoint, HandJointId tipJoint)
    {
        if (rightHand.GetJointPose(baseJoint, out Pose basePose) &&
            rightHand.GetJointPose(tipJoint, out Pose tipPose))
        {
            return (tipPose.position - basePose.position).normalized;
        }
        return Vector3.zero;
    }

    // ════════════════════════════════════════════════════════════
    //  Erwartete Richtungen (aus Physik-Simulation oder Defaults)
    // ════════════════════════════════════════════════════════════

    private void GetExpectedDirections(out Vector3 vDir, out Vector3 bDir, out Vector3 fDir)
    {
        // B-Feld ist immer bekannt
        bDir = fieldVolume != null
            ? fieldVolume.GetWorldFieldVector().normalized
            : Vector3.zero;

        if (particle != null && particle.IsSimulating && particle.Velocity.sqrMagnitude > 0.01f)
        {
            // Simulation läuft → echte Werte
            vDir = particle.Velocity.normalized;
            fDir = particle.CurrentForce.normalized;
        }
        else if (particle != null && fieldVolume != null)
        {
            // Simulation steht → aus Startbedingungen berechnen
            vDir = fieldVolume.transform.TransformDirection(particle.localStartVelocity).normalized;
            // Unity linkshändig: F = q * (B × v) statt q * (v × B)
            fDir = (particle.Charge * Vector3.Cross(bDir, vDir)).normalized;
        }
        else
        {
            vDir = Vector3.right;
            fDir = Vector3.zero;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Beschriftete Labels an Fingerspitzen
    // ════════════════════════════════════════════════════════════

    private void CreateLabels()
    {
        labelRoot = new GameObject("Finger-Regel Labels");
        labelRoot.transform.SetParent(transform, false);

        labelThumb = CreateLabel("v", new Color(0.2f, 0.9f, 0.3f));
        labelIndex = CreateLabel("B", new Color(0.3f, 0.7f, 1f));
        labelMiddle = CreateLabel("F", new Color(1f, 0.75f, 0.1f));
    }

    private TextMesh CreateLabel(string text, Color color)
    {
        GameObject obj = new GameObject("Label_" + text);
        obj.transform.SetParent(labelRoot.transform, false);

        TextMesh tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 36;
        tm.characterSize = 0.01f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return tm;
    }

    private void UpdateLabels()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        PositionLabel(labelThumb, HandJointId.HandThumbTip, cam);
        PositionLabel(labelIndex, HandJointId.HandIndexTip, cam);
        PositionLabel(labelMiddle, HandJointId.HandMiddleTip, cam);
    }

    private void PositionLabel(TextMesh label, HandJointId joint, Camera cam)
    {
        if (label == null) return;

        if (rightHand.GetJointPose(joint, out Pose pose))
        {
            // Leicht über der Fingerspitze
            label.transform.position = pose.position + Vector3.up * 0.025f;
            // Billboard: immer zur Kamera gedreht
            label.transform.rotation = Quaternion.LookRotation(
                label.transform.position - cam.transform.position
            );
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Referenzen finden
    // ════════════════════════════════════════════════════════════

    private void FindReferences()
    {
        if (rightHand == null)
        {
            Hand[] hands = FindObjectsOfType<Hand>();
            foreach (var hand in hands)
            {
                if (hand.Handedness == Handedness.Right)
                {
                    rightHand = hand;
                    break;
                }
            }
        }

        if (vectorDisplay == null)
            vectorDisplay = FindObjectOfType<VectorArrowDisplay>();
        if (particle == null)
            particle = FindObjectOfType<ChargedParticle>();
        if (fieldVolume == null)
            fieldVolume = FindObjectOfType<MagneticFieldVolume>();
    }
}
