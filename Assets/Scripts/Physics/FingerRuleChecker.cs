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
    //  Modus
    // ════════════════════════════════════════════════════════════

    public enum FingerRuleMode { Lorentz, Swing, Induction }

    [Header("Modus")]
    public FingerRuleMode mode = FingerRuleMode.Lorentz;

    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Referenzen")]
    [Tooltip("Wird automatisch gesucht")]
    public Hand rightHand;
    public VectorArrowDisplay vectorDisplay;
    public ChargedParticle particle;
    public MagneticFieldVolume fieldVolume;
    public ConductorSwing conductorSwing;
    public InductionLoop inductionLoop;

    [Header("Toleranz")]
    [Tooltip("Maximale Abweichung in Grad pro Finger")]
    public float angleThreshold = 30f;

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

    // Finger-Vektorpfeile
    private GameObject fingerArrowRoot;
    private GameObject arrowThumb, arrowIndex, arrowMiddle;
    private GameObject tipThumb, tipIndex, tipMiddle;
    private Material fingerArrowMatThumb, fingerArrowMatIndex, fingerArrowMatMiddle;
    private Material fingerTipMatThumb, fingerTipMatIndex, fingerTipMatMiddle;
    private float fingerArrowLength = 0.08f;
    private float fingerArrowThickness = 0.006f;
    private float fingerTipRadius = 0.008f;

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    public bool IsActive => isActive;
    public bool AllCorrect => allCorrect;

    public void SetActive(bool active)
    {
        isActive = active;
        if (labelRoot != null) labelRoot.SetActive(active);
        if (fingerArrowRoot != null) fingerArrowRoot.SetActive(active);

        if (!active)
        {
            if (vectorDisplay != null)
                vectorDisplay.SetTipIndicators(false, false);
        }
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
        if (fingerArrowRoot != null) fingerArrowRoot.SetActive(true);

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

        // ── Pfeilspitzen-Farbe setzen (am Teilchen) ──
        if (vectorDisplay != null)
            vectorDisplay.SetTipIndicators(true, allCorrect);

        // ── Finger-Vektorpfeile positionieren + Tip-Farbe pro Finger ──
        UpdateFingerArrows(thumbDir, indexDir, middleDir, thumbOk, indexOk, middleOk);

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
        bDir = fieldVolume != null
            ? fieldVolume.GetWorldFieldVector().normalized
            : Vector3.zero;

        switch (mode)
        {
            case FingerRuleMode.Swing:
                GetSwingDirections(out vDir, out bDir, out fDir);
                return;
            case FingerRuleMode.Induction:
                GetInductionDirections(out vDir, out bDir, out fDir);
                return;
            default: // Lorentz
                GetLorentzDirections(out vDir, out bDir, out fDir);
                return;
        }
    }

    private void GetLorentzDirections(out Vector3 vDir, out Vector3 bDir, out Vector3 fDir)
    {
        bDir = fieldVolume != null
            ? fieldVolume.GetWorldFieldVector().normalized
            : Vector3.zero;

        if (particle != null && particle.IsSimulating && particle.Velocity.sqrMagnitude > 0.01f)
        {
            vDir = particle.Velocity.normalized;
            fDir = particle.CurrentForce.normalized;
        }
        else if (particle != null && fieldVolume != null)
        {
            vDir = fieldVolume.transform.TransformDirection(particle.localStartVelocity).normalized;
            fDir = (particle.Charge * Vector3.Cross(bDir, vDir)).normalized;
        }
        else
        {
            vDir = Vector3.right;
            fDir = Vector3.zero;
        }
    }

    private void GetSwingDirections(out Vector3 vDir, out Vector3 bDir, out Vector3 fDir)
    {
        bDir = fieldVolume != null
            ? fieldVolume.GetWorldFieldVector().normalized
            : Vector3.zero;

        if (conductorSwing != null && conductorSwing.IsCurrentOn
            && conductorSwing.CurrentDirectionWorld.sqrMagnitude > 0.01f)
        {
            // Daumen = I-Richtung, Zeigefinger = B, Mittelfinger = F_L
            vDir = conductorSwing.CurrentDirectionWorld.normalized;
            fDir = conductorSwing.CurrentForce.sqrMagnitude > 0.001f
                ? conductorSwing.CurrentForce.normalized
                : Vector3.Cross(bDir, vDir).normalized;
        }
        else
        {
            vDir = Vector3.right;
            fDir = Vector3.zero;
        }
    }

    private void GetInductionDirections(out Vector3 vDir, out Vector3 bDir, out Vector3 fDir)
    {
        bDir = fieldVolume != null
            ? fieldVolume.GetWorldFieldVector().normalized
            : Vector3.zero;

        if (inductionLoop != null && inductionLoop.CurrentFlowSign != 0)
        {
            // v = Geschwindigkeit der Schleife (fallend → runter)
            vDir = new Vector3(0f, Mathf.Sign(inductionLoop.Velocity), 0f);
            if (vDir.sqrMagnitude < 0.01f) vDir = Vector3.down;
            // F auf Ladungsträger: B × v (Unity LHS)
            fDir = Vector3.Cross(bDir, vDir).normalized;
        }
        else
        {
            vDir = Vector3.down;
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

        string thumbText = mode == FingerRuleMode.Swing ? "Ursache: I" : "Ursache: v";
        labelThumb  = CreateLabel(thumbText, new Color(0.2f, 0.9f, 0.3f));
        labelIndex  = CreateLabel("Ursache: B", new Color(0.3f, 0.7f, 1f));
        labelMiddle = CreateLabel("Wirkung: F", new Color(1f, 0.75f, 0.1f));

        // Finger-Vektorpfeile erzeugen
        CreateFingerArrows();
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
    //  Finger-Vektorpfeile (Richtung am Finger, mit Tip-Indikator)
    // ════════════════════════════════════════════════════════════

    private void CreateFingerArrows()
    {
        fingerArrowRoot = new GameObject("Finger-Regel Pfeile");
        fingerArrowRoot.transform.SetParent(transform, false);

        fingerArrowMatThumb = CreateFingerMat(new Color(0.2f, 0.9f, 0.3f));
        fingerArrowMatIndex = CreateFingerMat(new Color(0.3f, 0.7f, 1f));
        fingerArrowMatMiddle = CreateFingerMat(new Color(1f, 0.75f, 0.1f));
        fingerTipMatThumb  = CreateFingerMat(Color.red);
        fingerTipMatIndex  = CreateFingerMat(Color.red);
        fingerTipMatMiddle = CreateFingerMat(Color.red);

        arrowThumb = CreateFingerArrow("FingerPfeil_v", fingerArrowMatThumb);
        arrowIndex = CreateFingerArrow("FingerPfeil_B", fingerArrowMatIndex);
        arrowMiddle = CreateFingerArrow("FingerPfeil_F", fingerArrowMatMiddle);

        tipThumb = CreateFingerTip("FingerTip_v", fingerTipMatThumb);
        tipIndex = CreateFingerTip("FingerTip_B", fingerTipMatIndex);
        tipMiddle = CreateFingerTip("FingerTip_F", fingerTipMatMiddle);
    }

    private Material CreateFingerMat(Color color)
    {
        Shader shader = Shader.Find("Custom/MoleculeUnlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        mat.enableInstancing = true;
        return mat;
    }

    private GameObject CreateFingerArrow(string name, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(fingerArrowRoot.transform, false);

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = VectorArrowDisplay.GetStaticArrowMesh();

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return obj;
    }

    private GameObject CreateFingerTip(string name, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(fingerArrowRoot.transform, false);

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = VectorArrowDisplay.GetStaticSphereMesh();

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        float d = fingerTipRadius * 2f;
        obj.transform.localScale = new Vector3(d, d, d);
        return obj;
    }

    private void UpdateFingerArrows(Vector3 thumbDir, Vector3 indexDir, Vector3 middleDir,
                                     bool thumbOk, bool indexOk, bool middleOk)
    {
        // Per-Finger Tip-Farbe
        Color okColor  = new Color(0.1f, 1f, 0.2f, 1f);
        Color badColor = new Color(1f, 0.15f, 0.15f, 1f);
        if (fingerTipMatThumb  != null) fingerTipMatThumb.color  = thumbOk  ? okColor : badColor;
        if (fingerTipMatIndex  != null) fingerTipMatIndex.color  = indexOk  ? okColor : badColor;
        if (fingerTipMatMiddle != null) fingerTipMatMiddle.color = middleOk ? okColor : badColor;

        UpdateSingleFingerArrow(arrowThumb, tipThumb, HandJointId.HandThumbTip, thumbDir);
        UpdateSingleFingerArrow(arrowIndex, tipIndex, HandJointId.HandIndexTip, indexDir);
        UpdateSingleFingerArrow(arrowMiddle, tipMiddle, HandJointId.HandMiddleTip, middleDir);
    }

    private void UpdateSingleFingerArrow(GameObject arrow, GameObject tip, HandJointId joint, Vector3 dir)
    {
        if (arrow == null || tip == null) return;

        if (dir.sqrMagnitude < 0.01f || !rightHand.GetJointPose(joint, out Pose pose))
        {
            arrow.SetActive(false);
            tip.SetActive(false);
            return;
        }

        arrow.SetActive(true);
        tip.SetActive(true);

        // Pfeil an der Fingerspitze, in Fingerrichtung
        arrow.transform.position = pose.position;
        arrow.transform.rotation = Quaternion.LookRotation(dir);
        arrow.transform.localScale = new Vector3(fingerArrowThickness, fingerArrowThickness, fingerArrowLength);

        // Tip-Kugel an der Pfeilspitze
        tip.transform.position = pose.position + dir * fingerArrowLength;
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
        if (conductorSwing == null)
            conductorSwing = FindObjectOfType<ConductorSwing>();
        if (inductionLoop == null)
            inductionLoop = FindObjectOfType<InductionLoop>();
    }
}
