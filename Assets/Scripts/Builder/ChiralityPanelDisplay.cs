using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction.Input;

/// <summary>
/// VR Control Panel für Chiralität-Werkzeuge.
/// Erscheint wenn "Chiralität"-Tab auf iPad aktiv ist.
/// Enthält alle 8 Werkzeuge aus der iPad-UI.
/// Layout:
///   [Full] Chiralitätszentren erkennen
///   Enantiomer erzeugen    | Konformer erzeugen
///   Diastereomer erzeugen  | Konstitutionsisomer erzeugen
///   cis/trans erzeugen     | Meso-Erkennung
///   [Full] Überlagerung testen
///   [Full] Alles löschen
/// </summary>
public class ChiralityPanelDisplay : MonoBehaviour
{
    [Header("References")]
    public WebSocketServer webSocket;
    public Hand rightHand;
    public Hand leftHand;

    [Header("Layout")]
    public float panelWidth = 0.38f;    // 38cm breit
    public float panelHeight = 0.38f;   // 38cm hoch
    public float buttonW = 0.155f;      // 15.5cm button width (halbe Panelbreite minus Gap)
    public float buttonH = 0.035f;      // 3.5cm button height
    public float buttonGap = 0.008f;

    [Header("Interaction")]
    [Range(0.5f, 1f)] public float pinchThreshold = 0.7f;
    [Range(0.1f, 0.5f)] public float pinchReleaseThreshold = 0.35f;

    private GameObject panelRoot;
    private bool handsInitialized = false;

    // Separate Poke-Cooldowns pro Hand
    private float lastPokeTimeR = -1f;
    private float lastPokeTimeL = -1f;
    private const float POKE_COOLDOWN = 0.5f;

    // Movement
    private bool isMovingPanel = false;
    private Hand movingHand = null;
    private Vector3 moveOffset = Vector3.zero;
    private bool moveModeActive = false;
    private Material moveButtonMat;

    // VR-Fehlermeldungen
    private GameObject messageObj;
    private float messageTimer = 0f;

    private class PanelButton
    {
        public string id;
        public GameObject obj;
        public Material mat;
        public Color baseColor;
        public float halfW, halfH;
    }
    private List<PanelButton> buttons = new List<PanelButton>();

    public void Initialize()
    {
        FindHands();
        BuildPanel();
        PositionPanelInitial();
        Debug.Log("[ChiralityPanel] Initialized – visible");
    }

    private void FindHands()
    {
        Hand[] hands = FindObjectsOfType<Hand>();
        foreach (var h in hands)
        {
            if (h.Handedness == Handedness.Right && rightHand == null) rightHand = h;
            else if (h.Handedness == Handedness.Left && leftHand == null) leftHand = h;
        }
        handsInitialized = (rightHand != null && leftHand != null);
        Debug.Log($"[ChiralityPanel] Hands: R={rightHand != null}, L={leftHand != null}");
    }

    private void PositionPanelInitial()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 groundForward = cam.transform.forward;
        groundForward.y = 0;
        if (groundForward.sqrMagnitude < 0.01f) groundForward = Vector3.forward;
        groundForward.Normalize();

        Vector3 targetPos = cam.transform.position + groundForward * 0.38f;
        targetPos.y = cam.transform.position.y - 0.06f;

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

    // ═══════════════════ PANEL AUFBAU ═══════════════════

    private void BuildPanel()
    {
        if (panelRoot != null) Destroy(panelRoot);
        buttons.Clear();

        panelRoot = new GameObject("PanelRoot");
        panelRoot.transform.SetParent(transform, false);

        // Background
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.transform.SetParent(panelRoot.transform, false);
        bg.transform.localScale = new Vector3(panelWidth, panelHeight, 1f);
        Destroy(bg.GetComponent<Collider>());
        var bgMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        bgMat.color = new Color(0.06f, 0.03f, 0.08f);
        bg.GetComponent<Renderer>().material = bgMat;

        // ─────────── Titel ───────────
        float topY = panelHeight * 0.42f;
        CreateLabel("Chiralität", new Vector3(0, topY, -0.003f), 56, 0.0016f, Color.white);

        // ─────────── Buttons ───────────
        // Full-Width   = buttonW * 2 + buttonGap
        // Half-Width   = buttonW
        float colL = -buttonW / 2 - buttonGap / 2;  // linke Spalte
        float colR =  buttonW / 2 + buttonGap / 2;  // rechte Spalte
        float startY = 0.115f;
        float rowH = buttonH + buttonGap;

        // Row 0: FULL – Chiralitätszentren erkennen
        AddButton("chirality_detect", "Chiralitätszentren erkennen",
                  new Color(0.2f, 0.55f, 1f),
                  0, startY, fullWidth: true);

        // Row 1: Enantiomer | Konformer
        AddButton("generate_enantiomer", "Enantiomer erzeugen",
                  new Color(0.7f, 0.25f, 0.85f),
                  colL, startY - rowH * 1);
        AddButton("generate_conformer", "Konformer erzeugen",
                  new Color(0.96f, 0.62f, 0.04f),
                  colR, startY - rowH * 1);

        // Row 2: Diastereomer | Konstitutionsisomer
        AddButton("generate_diastereomer", "Diastereomer erzeugen",
                  new Color(0.02f, 0.71f, 0.83f),
                  colL, startY - rowH * 2);
        AddButton("generate_constitutional", "Konstitutionsisomer",
                  new Color(0.39f, 0.40f, 0.95f),
                  colR, startY - rowH * 2);

        // Row 3: cis/trans | Meso
        AddButton("generate_cistrans", "cis/trans-Isomer",
                  new Color(0.93f, 0.27f, 0.37f),
                  colL, startY - rowH * 3);
        AddButton("test_meso", "Meso-Erkennung",
                  new Color(0.06f, 0.73f, 0.51f),
                  colR, startY - rowH * 3);

        // Row 4: FULL – Überlagerung testen
        AddButton("test_overlay", "Überlagerung testen",
                  new Color(1f, 0.5f, 0.15f),
                  0, startY - rowH * 4, fullWidth: true);

        // Row 5: FULL – Alles löschen
        AddButton("chirality_clear", "Alles löschen",
                  new Color(0.7f, 0.15f, 0.15f),
                  0, startY - rowH * 5 - 0.005f, fullWidth: true);

        // Move-Button (rechts oben, klein)
        AddMoveButton(panelWidth * 0.41f, topY);
    }

    private void AddButton(string id, string label, Color color, float x, float y, bool fullWidth = false)
    {
        var obj = new GameObject($"Btn_{id}");
        obj.transform.SetParent(panelRoot.transform, false);
        obj.transform.localPosition = new Vector3(x, y, -0.003f);

        float w = fullWidth ? (buttonW * 2 + buttonGap) : buttonW;

        // Visual
        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.transform.SetParent(obj.transform, false);
        vis.transform.localScale = new Vector3(w, buttonH, 0.006f);
        Destroy(vis.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        mat.color = color;
        vis.GetComponent<Renderer>().material = mat;

        // Label – doppelt so groß wie vorher
        CreateLabel(label, new Vector3(0, 0, -0.005f), 40, 0.0014f, Color.white, obj.transform);

        buttons.Add(new PanelButton
        {
            id = id, obj = obj, mat = mat,
            baseColor = color, halfW = w / 2, halfH = buttonH / 2
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

        CreateLabel("✥", new Vector3(0, 0, -0.005f), 36, 0.0012f, Color.white, obj.transform);

        buttons.Add(new PanelButton
        {
            id = "move", obj = obj, mat = mat,
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
        // Retry hand finding if one hand wasn't ready at init time
        if (!handsInitialized)
            FindHands();

        // Poke-Erkennung (wenn nicht gerade bewegt wird)
        if (!isMovingPanel)
            HandlePokes();

        // Bewegungs-Logik
        if (moveModeActive)
            HandleMovePinch();

        // VR-Message Timer
        if (messageObj != null)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0)
            {
                Destroy(messageObj);
                messageObj = null;
            }
        }
    }

    // ═══════════════════ POKE DETECTION ═══════════════════

    private void HandlePokes()
    {
        // BEIDE Hände prüfen, mit separatem Cooldown
        CheckPoke(rightHand, ref lastPokeTimeR);
        CheckPoke(leftHand, ref lastPokeTimeL);
    }

    private void CheckPoke(Hand hand, ref float lastPokeTime)
    {
        if (hand == null || !hand.IsTrackedDataValid) return;
        if (Time.time - lastPokeTime < POKE_COOLDOWN) return;
        if (!hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip)) return;

        // Transformiere in lokalen Panel-Raum
        Vector3 localTip = panelRoot.transform.InverseTransformPoint(tip.position);

        // Tiefe-Check (lokales Z)
        if (Mathf.Abs(localTip.z) > 0.05f) return;

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
        Debug.Log($"[ChiralityPanel] Button pressed: {btn.id} (hand poke)");
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

        if (webSocket == null)
            webSocket = FindObjectOfType<WebSocketServer>();

        if (webSocket != null)
            webSocket.HandleVRControlPanelCommand(btn.id);
        else
            Debug.LogWarning("[ChiralityPanel] WebSocketServer nicht gefunden!");
    }

    private System.Collections.IEnumerator FlashButton(PanelButton btn)
    {
        if (btn.mat == null) yield break;
        btn.mat.color = Color.white;
        yield return new WaitForSeconds(0.15f);
        if (btn.id == "move")
            btn.mat.color = moveModeActive ? new Color(0.15f, 0.75f, 0.5f) : btn.baseColor;
        else
            btn.mat.color = btn.baseColor;
    }

    // ═══════════════════ VR ERROR/STATUS MESSAGES ═══════════════════

    /// <summary>
    /// Zeigt eine schwebende Nachricht über dem Panel in VR an.
    /// Wird vom WebSocketServer aufgerufen wenn VR-Panel-Commands Fehler/Status erzeugen.
    /// </summary>
    public void ShowVRMessage(string text, bool isError)
    {
        if (messageObj != null) Destroy(messageObj);

        messageObj = new GameObject("VRMessage");
        messageObj.transform.SetParent(panelRoot.transform, false);
        // Position: unter dem Panel
        messageObj.transform.localPosition = new Vector3(0, -panelHeight * 0.55f, -0.01f);

        // Hintergrund
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.transform.SetParent(messageObj.transform, false);
        bg.transform.localScale = new Vector3(panelWidth * 0.9f, 0.04f, 1f);
        Destroy(bg.GetComponent<Collider>());
        var bgMat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("UI/Default"));
        bgMat.color = isError ? new Color(0.6f, 0.08f, 0.08f) : new Color(0.05f, 0.4f, 0.15f);
        bg.GetComponent<Renderer>().material = bgMat;

        // Text
        var label = new GameObject("MsgText");
        label.transform.SetParent(messageObj.transform, false);
        label.transform.localPosition = new Vector3(0, 0, -0.005f);
        var tm = label.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 36;
        tm.characterSize = 0.0012f;
        tm.color = Color.white;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;

        messageTimer = 4f; // 4 Sekunden sichtbar
        Debug.Log($"[ChiralityPanel] VR Message: {text} (error={isError})");
    }

    // ═══════════════════ MOVE (PINCH-TO-DRAG) ═══════════════════

    private void HandleMovePinch()
    {
        if (!isMovingPanel)
        {
            Hand activeHand = GetPinchingHand();
            if (activeHand != null && activeHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            {
                isMovingPanel = true;
                movingHand = activeHand;
                moveOffset = transform.position - tip.position;
                FaceCamera();
            }
        }
        else
        {
            if (movingHand == null || !movingHand.IsTrackedDataValid ||
                movingHand.GetFingerPinchStrength(HandFinger.Index) < pinchReleaseThreshold)
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
