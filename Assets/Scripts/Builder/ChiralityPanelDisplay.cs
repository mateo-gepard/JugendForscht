using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction.Input;

/// <summary>
/// VR Control Panel für Chiralität-Werkzeuge.
/// Erscheint wenn "Chiralität"-Tab auf iPad aktiv ist.
/// Enthält alle 8 Werkzeuge aus der iPad-UI.
/// - Poke: Zeigefinger-Tipp berührt Buttons (lokale Distanzprüfung)
/// - Move: Toggle via Poke, dann Pinch zum Verschieben
/// </summary>
public class ChiralityPanelDisplay : MonoBehaviour
{
    [Header("References")]
    public WebSocketServer webSocket;
    public Hand rightHand;
    public Hand leftHand;

    [Header("Layout")]
    public float panelWidth = 0.35f;   // 35cm
    public float panelHeight = 0.35f;  // 35cm – braucht mehr Platz für 8 Buttons
    public float buttonW = 0.14f;      // 14cm button width
    public float buttonH = 0.032f;     // 3.2cm button height
    public float buttonGap = 0.008f;   // gap between buttons

    [Header("Interaction")]
    [Range(0.5f, 1f)] public float pinchThreshold = 0.7f;
    [Range(0.1f, 0.5f)] public float pinchReleaseThreshold = 0.35f;

    private GameObject panelRoot;

    // Poke cooldown
    private float lastPokeTime = -1f;
    private const float POKE_COOLDOWN = 0.5f;

    // Movement
    private bool isMovingPanel = false;
    private Hand movingHand = null;
    private Vector3 moveOffset = Vector3.zero;
    private bool moveModeActive = false;
    private Material moveButtonMat;

    private class PanelButton
    {
        public string id;
        public string label;
        public GameObject obj;
        public Material mat;
        public Color baseColor;
        public float halfW, halfH;
    }
    private List<PanelButton> buttons = new List<PanelButton>();

    public void Initialize()
    {
        if (rightHand == null) FindHands();
        BuildPanel();
        PositionPanelInitial();
        Debug.Log("[ChiralityPanel] Initialized – visible");
    }

    private void FindHands()
    {
        Hand[] hands = FindObjectsOfType<Hand>();
        foreach (var h in hands)
        {
            if (h.Handedness == Handedness.Right) rightHand = h;
            else if (h.Handedness == Handedness.Left) leftHand = h;
        }
    }

    private void PositionPanelInitial()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 groundForward = cam.transform.forward;
        groundForward.y = 0;
        if (groundForward.sqrMagnitude < 0.01f) groundForward = Vector3.forward;
        groundForward.Normalize();

        // 35cm vor dem User, leicht unter Augenhöhe
        Vector3 targetPos = cam.transform.position + groundForward * 0.35f;
        targetPos.y = cam.transform.position.y - 0.08f;

        transform.position = targetPos;
        FaceCamera();
    }

    private void FaceCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 lookDir = transform.position - cam.transform.position;
            if (lookDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    private void BuildPanel()
    {
        if (panelRoot != null) Destroy(panelRoot);
        buttons.Clear();

        panelRoot = new GameObject("PanelRoot");
        panelRoot.transform.SetParent(transform, false);

        // ─────────── Background ───────────
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.transform.SetParent(panelRoot.transform, false);
        bg.transform.localScale = new Vector3(panelWidth, panelHeight, 1f);
        Destroy(bg.GetComponent<Collider>());
        var bgMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        bgMat.color = new Color(0.06f, 0.03f, 0.08f); // dark purple
        bg.GetComponent<Renderer>().material = bgMat;

        // ─────────── Title ───────────
        float topY = panelHeight * 0.42f;
        CreateLabel("Chiralität", new Vector3(0, topY, -0.003f), 48, 0.0014f, Color.white);

        // ─────────── Werkzeug-Buttons (2 Spalten × 4 Reihen) ───────────
        // Links: Erkennen, Enantiomer, Diastereomer, Konformer
        // Rechts: Meso, cis/trans, Konstitution, Überlagerung

        float colLeft  = -0.075f;
        float colRight =  0.075f;
        float startY   =  0.10f;
        float rowStep  = -(buttonH + buttonGap);

        // Row 0
        AddToolButton("chirality_detect",      "Erkennen",      new Color(0.2f, 0.55f, 1f),   colLeft,  startY + rowStep * 0);
        AddToolButton("test_meso",             "Meso-Test",     new Color(0.06f, 0.73f, 0.51f),colRight, startY + rowStep * 0);
        // Row 1
        AddToolButton("generate_enantiomer",   "Enantiomer",    new Color(0.7f, 0.25f, 0.85f), colLeft,  startY + rowStep * 1);
        AddToolButton("generate_cistrans",     "cis/trans",     new Color(0.93f, 0.27f, 0.37f),colRight, startY + rowStep * 1);
        // Row 2
        AddToolButton("generate_diastereomer", "Diastereomer",  new Color(0.02f, 0.71f, 0.83f),colLeft,  startY + rowStep * 2);
        AddToolButton("generate_constitutional","Konstitution", new Color(0.39f, 0.40f, 0.95f),colRight, startY + rowStep * 2);
        // Row 3
        AddToolButton("generate_conformer",    "Konformer",     new Color(0.96f, 0.62f, 0.04f),colLeft,  startY + rowStep * 3);
        AddToolButton("test_overlay",          "Überlagerung",  new Color(1f, 0.5f, 0.15f),    colRight, startY + rowStep * 3);

        // ─────────── Clear + Move (unten) ───────────
        float bottomY = startY + rowStep * 4 - 0.01f;
        AddToolButton("chirality_clear", "Alles löschen", new Color(0.7f, 0.15f, 0.15f),
                      0, bottomY, fullWidth: true);

        // Move-Button (rechts oben, klein)
        AddMoveButton(panelWidth * 0.40f, topY);
    }

    private void AddToolButton(string id, string label, Color color, float x, float y, bool fullWidth = false)
    {
        var obj = new GameObject($"Btn_{id}");
        obj.transform.SetParent(panelRoot.transform, false);
        obj.transform.localPosition = new Vector3(x, y, -0.003f);

        float w = fullWidth ? (buttonW * 2 + buttonGap) : buttonW;
        float h = buttonH;

        // Visual
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.transform.SetParent(obj.transform, false);
        vis.transform.localScale = new Vector3(w, h, 0.006f);
        Destroy(vis.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        mat.color = color;
        vis.GetComponent<Renderer>().material = mat;

        // Label
        CreateLabel(label, new Vector3(0, 0, -0.005f), 32, 0.0009f, Color.white, obj.transform);

        buttons.Add(new PanelButton
        {
            id = id, label = label, obj = obj, mat = mat,
            baseColor = color, halfW = w / 2, halfH = h / 2
        });
    }

    private void AddMoveButton(float x, float y)
    {
        var obj = new GameObject("Btn_move");
        obj.transform.SetParent(panelRoot.transform, false);
        obj.transform.localPosition = new Vector3(x, y, -0.003f);

        float s = 0.025f;
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.transform.SetParent(obj.transform, false);
        vis.transform.localScale = new Vector3(s, s, 0.006f);
        Destroy(vis.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        mat.color = new Color(0.45f, 0.45f, 0.45f);
        vis.GetComponent<Renderer>().material = mat;
        moveButtonMat = mat;

        CreateLabel("+", new Vector3(0, 0, -0.005f), 32, 0.0009f, Color.white, obj.transform);

        buttons.Add(new PanelButton
        {
            id = "move", label = "+", obj = obj, mat = mat,
            baseColor = new Color(0.45f, 0.45f, 0.45f),
            halfW = s / 2, halfH = s / 2
        });
    }

    private void CreateLabel(string text, Vector3 localPos, int fontSize, float charSize, Color color, Transform parent = null)
    {
        if (parent == null) parent = panelRoot.transform;
        var obj = new GameObject("Label");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        var tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = fontSize;
        tm.characterSize = charSize;
        tm.color = color;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
    }

    // ═══════════════════ UPDATE ═══════════════════

    void Update()
    {
        // Always check pokes (unless we're mid-move)
        if (!isMovingPanel)
            HandlePokes();

        // Handle movement if move mode is active
        if (moveModeActive)
            HandleMovePinch();
    }

    // ═══════════════════ POKE DETECTION ═══════════════════

    private void HandlePokes()
    {
        CheckPoke(rightHand);
        CheckPoke(leftHand);
    }

    private void CheckPoke(Hand hand)
    {
        if (hand == null || !hand.IsTrackedDataValid) return;
        if (Time.time - lastPokeTime < POKE_COOLDOWN) return;
        if (!hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip)) return;

        // Transformiere Fingerspitze in lokalen Panel-Raum
        Vector3 localTip = panelRoot.transform.InverseTransformPoint(tip.position);

        // Prüfe ob Finger innerhalb der Panel-Tiefe ist (lokales Z)
        // Negatives Z = vor dem Panel (Panel-Front zeigt in -Z Richtung von local space)
        float depthThresh = 0.05f;
        if (Mathf.Abs(localTip.z) > depthThresh) return;

        // Prüfe jeden Button
        foreach (var b in buttons)
        {
            if (b.obj == null) continue;
            Vector3 localBtn = panelRoot.transform.InverseTransformPoint(b.obj.transform.position);

            if (Mathf.Abs(localTip.x - localBtn.x) < b.halfW &&
                Mathf.Abs(localTip.y - localBtn.y) < b.halfH)
            {
                OnButtonClicked(b);
                lastPokeTime = Time.time;
                return;
            }
        }
    }

    private void OnButtonClicked(PanelButton btn)
    {
        Debug.Log($"[ChiralityPanel] Button pressed: {btn.id}");

        // Flash feedback
        StartCoroutine(FlashButton(btn));

        if (btn.id == "move")
        {
            moveModeActive = !moveModeActive;
            if (moveButtonMat != null)
                moveButtonMat.color = moveModeActive
                    ? new Color(0.15f, 0.75f, 0.5f)
                    : btn.baseColor;
            Debug.Log($"[ChiralityPanel] Move mode: {moveModeActive}");
            return;
        }

        // Route all tool commands to WebSocketServer
        if (webSocket == null)
            webSocket = FindObjectOfType<WebSocketServer>();

        if (webSocket != null)
        {
            webSocket.HandleVRControlPanelCommand(btn.id);
        }
        else
        {
            Debug.LogWarning("[ChiralityPanel] WebSocketServer not found!");
        }
    }

    private System.Collections.IEnumerator FlashButton(PanelButton btn)
    {
        if (btn.mat == null) yield break;
        btn.mat.color = Color.white;
        yield return new WaitForSeconds(0.15f);
        // Restore: move button depends on toggle state
        if (btn.id == "move")
            btn.mat.color = moveModeActive ? new Color(0.15f, 0.75f, 0.5f) : btn.baseColor;
        else
            btn.mat.color = btn.baseColor;
    }

    // ═══════════════════ MOVE (PINCH-TO-DRAG) ═══════════════════

    private void HandleMovePinch()
    {
        if (!isMovingPanel)
        {
            // Check if either hand starts pinching
            Hand activeHand = GetPinchingHand();
            if (activeHand != null && activeHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            {
                isMovingPanel = true;
                movingHand = activeHand;
                moveOffset = transform.position - tip.position;
                FaceCamera();
                Debug.Log("[ChiralityPanel] Move started");
            }
        }
        else
        {
            // Release?
            if (movingHand == null || !movingHand.IsTrackedDataValid ||
                movingHand.GetFingerPinchStrength(HandFinger.Index) < pinchReleaseThreshold)
            {
                isMovingPanel = false;
                movingHand = null;
                Debug.Log("[ChiralityPanel] Move ended");
                return;
            }

            // Follow hand
            if (movingHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            {
                transform.position = tip.position + moveOffset;
                FaceCamera();
            }
        }
    }

    private Hand GetPinchingHand()
    {
        if (rightHand != null && rightHand.IsTrackedDataValid &&
            rightHand.GetFingerPinchStrength(HandFinger.Index) > pinchThreshold)
            return rightHand;
        if (leftHand != null && leftHand.IsTrackedDataValid &&
            leftHand.GetFingerPinchStrength(HandFinger.Index) > pinchThreshold)
            return leftHand;
        return null;
    }
}
