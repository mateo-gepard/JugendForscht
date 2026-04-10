using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction.Input;

/// <summary>
/// VR Control Panel für chirality functions.
/// Erscheint wenn "Chiralität"-Tab auf iPad aktiv ist.
/// - Poke: Zeigefinger-Tipp berührt Buttons (lokale Distanzprüfung)
/// - Move: Pinch zum verschieben, Toggle via Pointer-Klick
/// </summary>
public class ChiralityPanelDisplay : MonoBehaviour
{
    [Header("References")]
    public WebSocketServer webSocket;
    public Hand rightHand;
    public Hand leftHand;

    [Header("Layout")]
    public float panelWidth = 0.3f;    // 30cm
    public float panelHeight = 0.18f;  // 18cm
    public float buttonSize = 0.055f;  // 5.5cm buttons
    public float buttonSpacing = 0.015f;

    [Header("Interaction")]
    [Range(0.5f, 1f)] public float pinchThreshold = 0.7f;
    [Range(0.1f, 0.5f)] public float pinchReleaseThreshold = 0.35f;
    
    private GameObject panelRoot;
    
    // Poke cooldown
    private float lastPokeTimeR = -1f, lastPokeTimeL = -1f;
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
        public GameObject obj;
        public Material mat;
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
        
        // Horizontal-forward (kein nach-unten-Kippen)
        Vector3 groundForward = cam.transform.forward;
        groundForward.y = 0;
        if (groundForward.sqrMagnitude < 0.01f) groundForward = Vector3.forward;
        groundForward.Normalize();

        // 35cm vor dem User, leicht unter Augenhöhe
        Vector3 targetPos = cam.transform.position + groundForward * 0.35f;
        targetPos.y = cam.transform.position.y - 0.1f;

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

        // Background Quad
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.transform.SetParent(panelRoot.transform, false);
        bg.transform.localScale = new Vector3(panelWidth, panelHeight, 1f);
        Destroy(bg.GetComponent<Collider>());
        
        var bgMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        bgMat.color = new Color(0.06f, 0.03f, 0.08f); // dark purple
        bg.GetComponent<Renderer>().material = bgMat;

        // Title
        CreateLabel("Chiralität", new Vector3(0, panelHeight * 0.33f, -0.005f), 40, 0.0015f, Color.white);

        // 3 Buttons in einer Reihe
        float totalBtnWidth = 3 * buttonSize + 2 * buttonSpacing;
        float startX = -totalBtnWidth / 2 + buttonSize / 2;
        float btnY = -0.005f;

        CreateButton("detect",       "Erkennen",    new Color(0.2f, 0.55f, 1f),  new Vector3(startX, btnY, -0.005f));
        CreateButton("isomers",      "Isomere",     new Color(0.7f, 0.25f, 0.85f), new Vector3(startX + buttonSize + buttonSpacing, btnY, -0.005f));
        CreateButton("superposition","Überlapp.",    new Color(1f, 0.5f, 0.15f),  new Vector3(startX + 2*(buttonSize + buttonSpacing), btnY, -0.005f));

        // Move Button (klein, rechts oben)
        CreateButton("move", "✥", new Color(0.45f, 0.45f, 0.45f),
            new Vector3(panelWidth * 0.42f, panelHeight * 0.35f, -0.005f), isSmall: true);
    }

    private void CreateButton(string id, string label, Color color, Vector3 localPos, bool isSmall = false)
    {
        var obj = new GameObject($"Btn_{id}");
        obj.transform.SetParent(panelRoot.transform, false);
        obj.transform.localPosition = localPos;

        float size = isSmall ? buttonSize * 0.5f : buttonSize;

        // Button visual
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.transform.SetParent(obj.transform, false);
        vis.transform.localScale = new Vector3(size, size, 0.008f);
        Destroy(vis.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        mat.color = color;
        vis.GetComponent<Renderer>().material = mat;

        // Label
        float charSize = isSmall ? 0.0012f : 0.0015f;
        int fontSize = isSmall ? 30 : 36;
        CreateLabel(label, new Vector3(0, 0, -0.006f), fontSize, charSize, Color.white, obj.transform);

        var btn = new PanelButton { id = id, obj = obj, mat = mat };
        buttons.Add(btn);

        if (id == "move") moveButtonMat = mat;
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

    void Update()
    {
        HandlePokes();
        if (moveModeActive)
            HandleMovePinch();
    }

    // ═══════════════════ POKE DETECTION ═══════════════════

    private void HandlePokes()
    {
        if (isMovingPanel) return;
        CheckPoke(rightHand, ref lastPokeTimeR);
        CheckPoke(leftHand, ref lastPokeTimeL);
    }

    private void CheckPoke(Hand hand, ref float lastPokeTime)
    {
        if (hand == null || !hand.IsTrackedDataValid) return;
        if (Time.time - lastPokeTime < POKE_COOLDOWN) return;

        if (!hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip)) return;

        // Transformiere Fingerspitze in lokalen Panel-Raum
        Vector3 localTip = panelRoot.transform.InverseTransformPoint(tip.position);

        // Prüfe ob Finger innerhalb der Panel-Tiefe ist (lokales Z)
        float depthThresh = 0.04f;
        if (Mathf.Abs(localTip.z) > depthThresh) return;

        // Prüfe jeden Button in lokalen Koordinaten
        foreach (var b in buttons)
        {
            if (b.obj == null) continue;
            Vector3 localBtn = panelRoot.transform.InverseTransformPoint(b.obj.transform.position);
            
            float halfSize = (b.id == "move" ? buttonSize * 0.25f : buttonSize * 0.5f);
            
            if (Mathf.Abs(localTip.x - localBtn.x) < halfSize &&
                Mathf.Abs(localTip.y - localBtn.y) < halfSize)
            {
                OnButtonClicked(b.id);
                lastPokeTime = Time.time;
                
                // Kurzes visuelles Feedback
                StartCoroutine(FlashButton(b.mat, b.id == "move"));
                return;
            }
        }
    }

    private System.Collections.IEnumerator FlashButton(Material mat, bool isMoveBtn)
    {
        if (mat == null) yield break;
        Color original = mat.color;
        mat.color = Color.white;
        yield return new WaitForSeconds(0.15f);
        if (isMoveBtn)
            mat.color = moveModeActive ? new Color(0.15f, 0.75f, 0.5f) : new Color(0.45f, 0.45f, 0.45f);
        else
            mat.color = original;
    }

    private void OnButtonClicked(string id)
    {
        Debug.Log($"[ChiralityPanel] Button pressed: {id}");
        
        if (id == "move")
        {
            moveModeActive = !moveModeActive;
            if (moveButtonMat != null)
            {
                moveButtonMat.color = moveModeActive ? new Color(0.15f, 0.75f, 0.5f) : new Color(0.45f, 0.45f, 0.45f);
            }
            return;
        }

        if (webSocket == null)
            webSocket = FindObjectOfType<WebSocketServer>();

        if (webSocket != null)
        {
            switch (id)
            {
                case "detect":
                    webSocket.HandleVRControlPanelCommand("chirality_detect");
                    break;
                case "isomers":
                    webSocket.HandleVRControlPanelCommand("generate_isomers");
                    break;
                case "superposition":
                    webSocket.HandleVRControlPanelCommand("superposition_mode");
                    break;
            }
        }
    }

    // ═══════════════════ MOVE (PINCH) ═══════════════════

    private void HandleMovePinch()
    {
        bool rPinching = IsPinching(rightHand);
        bool lPinching = IsPinching(leftHand);

        if (!isMovingPanel && (rPinching || lPinching))
        {
            Hand activeHand = rPinching ? rightHand : leftHand;
            if (activeHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            {
                isMovingPanel = true;
                movingHand = activeHand;
                moveOffset = transform.position - tip.position;
                FaceCamera();
            }
        }
        else if (isMovingPanel)
        {
            if (movingHand == null || !movingHand.IsTrackedDataValid || !IsPinching(movingHand, pinchReleaseThreshold))
            {
                isMovingPanel = false;
                movingHand = null;
                return;
            }

            if (movingHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            {
                transform.position = tip.position + moveOffset;
                FaceCamera();
            }
        }
    }

    private bool IsPinching(Hand hand, float threshold = -1)
    {
        if (hand == null || !hand.IsTrackedDataValid) return false;
        if (threshold < 0) threshold = pinchThreshold;
        return hand.GetFingerIsPinching(HandFinger.Index) && hand.GetFingerPinchStrength(HandFinger.Index) > threshold;
    }
}
