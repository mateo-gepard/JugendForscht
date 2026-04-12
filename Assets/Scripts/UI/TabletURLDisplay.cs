using UnityEngine;

/// <summary>
/// Zeigt die WebSocket-Server-URL an der "Decke" im VR-Raum an.
/// Nur sichtbar wenn der Nutzer nach oben schaut (> minLookUpAngle).
/// 
/// Workflow:
///   1. Quest startet → WebSocketServer hostet die Steuerungs-UI
///   2. Nutzer schaut nach oben → sieht URL (z.B. http://192.168.2.42:8080)
///   3. Jemand am Computer im gleichen Netzwerk öffnet die URL im Browser
///   4. Die Web-UI steuert die VR-Brille (Moleküle laden, Tutorial starten, etc.)
/// </summary>
public class TabletURLDisplay : MonoBehaviour
{
    [Header("References")]
    public WebSocketServer webSocketServer;

    [Header("Display Settings")]
    [Tooltip("Höhe über dem Kopf des Nutzers")]
    public float heightAboveHead = 2.0f;

    [Tooltip("Minimaler Blickwinkel nach oben (Grad) um URL zu sehen")]
    [Range(15f, 60f)]
    public float minLookUpAngle = 25f;

    [Tooltip("Schriftgröße der URL")]
    public float characterSize = 0.015f;

    [Tooltip("Schriftgröße des URL-Textes")]
    public int fontSize = 36;

    // URL display objects
    private GameObject displayRoot;
    private TextMesh urlText;
    private TextMesh titleText;
    private TextMesh statusText;
    private GameObject backgroundQuad;
    private Camera mainCamera;
    private MeshRenderer bgRenderer;
    private MeshRenderer urlRenderer;
    private MeshRenderer titleRenderer;
    private MeshRenderer statusRenderer;

    // State
    private string lastURL = "";
    private float fadeAlpha = 0f;
    private bool isVisible = false;

    void Start()
    {
        if (webSocketServer == null)
            webSocketServer = FindObjectOfType<WebSocketServer>();

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[URLDisplay] No main camera found, will retry...");
            InvokeRepeating("TryFindCamera", 0.5f, 1f);
        }

        CreateDisplay();
        SetVisibility(false);
    }

    void TryFindCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
            CancelInvoke("TryFindCamera");
    }

    void CreateDisplay()
    {
        displayRoot = new GameObject("CeilingURLDisplay");

        // ── Background panel ──
        backgroundQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backgroundQuad.name = "URLBackground";
        backgroundQuad.transform.SetParent(displayRoot.transform, false);
        backgroundQuad.transform.localScale = new Vector3(0.65f, 0.18f, 1f);
        backgroundQuad.transform.localPosition = new Vector3(0f, 0f, 0.01f);

        // Remove collider
        var col = backgroundQuad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // Semi-transparent dark background
        bgRenderer = backgroundQuad.GetComponent<MeshRenderer>();
        var bgMat = new Material(Shader.Find("Unlit/Color"));
        if (bgMat != null)
        {
            bgMat.color = new Color(0.05f, 0.05f, 0.12f, 0.92f);
            bgRenderer.material = bgMat;
        }

        // ── Title text ──
        var titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(displayRoot.transform, false);
        titleObj.transform.localPosition = new Vector3(0f, 0.055f, 0f);
        titleText = titleObj.AddComponent<TextMesh>();
        titleText.text = "Steuerungs-URL";
        titleText.characterSize = characterSize * 0.6f;
        titleText.fontSize = fontSize - 8;
        titleText.anchor = TextAnchor.MiddleCenter;
        titleText.alignment = TextAlignment.Center;
        titleText.color = new Color(0.6f, 0.75f, 1f);
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleRenderer = titleObj.GetComponent<MeshRenderer>();
        SetupTextMaterial(titleObj, new Color(0.6f, 0.75f, 1f));

        // ── URL text (main) ──
        var urlObj = new GameObject("URLText");
        urlObj.transform.SetParent(displayRoot.transform, false);
        urlObj.transform.localPosition = new Vector3(0f, 0f, 0f);
        urlText = urlObj.AddComponent<TextMesh>();
        urlText.text = "Starte Server...";
        urlText.characterSize = characterSize;
        urlText.fontSize = fontSize;
        urlText.anchor = TextAnchor.MiddleCenter;
        urlText.alignment = TextAlignment.Center;
        urlText.color = Color.white;
        urlText.fontStyle = FontStyle.Bold;
        urlText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        urlRenderer = urlObj.GetComponent<MeshRenderer>();
        SetupTextMaterial(urlObj, Color.white);

        // ── Status text ──
        var statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(displayRoot.transform, false);
        statusObj.transform.localPosition = new Vector3(0f, -0.055f, 0f);
        statusText = statusObj.AddComponent<TextMesh>();
        statusText.text = "Im Browser auf Computer eingeben";
        statusText.characterSize = characterSize * 0.5f;
        statusText.fontSize = fontSize - 12;
        statusText.anchor = TextAnchor.MiddleCenter;
        statusText.alignment = TextAlignment.Center;
        statusText.color = new Color(0.5f, 1f, 0.5f);
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusRenderer = statusObj.GetComponent<MeshRenderer>();
        SetupTextMaterial(statusObj, new Color(0.5f, 1f, 0.5f));

        // ── Border lines (decorative) ──
        CreateBorderLine(displayRoot.transform, new Vector3(0f, 0.08f, -0.001f), 0.6f, new Color(0.3f, 0.5f, 1f));
        CreateBorderLine(displayRoot.transform, new Vector3(0f, -0.08f, -0.001f), 0.6f, new Color(0.3f, 0.5f, 1f));
    }

    void SetupTextMaterial(GameObject obj, Color color)
    {
        var renderer = obj.GetComponent<MeshRenderer>();
        var tm = obj.GetComponent<TextMesh>();
        if (renderer != null && tm != null && tm.font != null)
        {
            Material textMat = new Material(Shader.Find("GUI/Text Shader"));
            if (textMat != null)
            {
                textMat.mainTexture = tm.font.material.mainTexture;
                textMat.color = color;
                renderer.material = textMat;
            }
        }
    }

    void CreateBorderLine(Transform parent, Vector3 localPos, float width, Color color)
    {
        var obj = new GameObject("Border");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        var lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.002f;
        lr.endWidth = 0.002f;
        lr.useWorldSpace = false;
        lr.SetPosition(0, new Vector3(-width * 0.5f, 0, 0));
        lr.SetPosition(1, new Vector3(width * 0.5f, 0, 0));
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
    }

    void LateUpdate()
    {
        if (displayRoot == null || mainCamera == null) return;

        // ── Update URL text ──
        UpdateURLText();

        // ── Calculate look-up angle ──
        float pitch = -mainCamera.transform.eulerAngles.x;
        if (pitch < -180f) pitch += 360f;
        if (pitch > 180f) pitch -= 360f;

        bool shouldBeVisible = pitch > minLookUpAngle;

        // ── Smooth fade ──
        float targetAlpha = shouldBeVisible ? 1f : 0f;
        fadeAlpha = Mathf.MoveTowards(fadeAlpha, targetAlpha, Time.deltaTime * 4f);
        isVisible = fadeAlpha > 0.01f;

        SetVisibility(isVisible);

        if (!isVisible) return;

        // ── Position: always above the user's head ──
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 horizontalForward = new Vector3(
            mainCamera.transform.forward.x, 0f, mainCamera.transform.forward.z
        ).normalized;

        if (horizontalForward.magnitude < 0.01f)
            horizontalForward = Vector3.forward;

        // Place directly above the player, slightly forward
        Vector3 position = cameraPos
            + Vector3.up * heightAboveHead
            + horizontalForward * 0.3f;

        displayRoot.transform.position = position;

        // Face downward toward the player
        displayRoot.transform.rotation = Quaternion.LookRotation(Vector3.down, horizontalForward);

        // ── Apply fade alpha to materials ──
        ApplyFade(fadeAlpha);
    }

    void UpdateURLText()
    {
        if (urlText == null) return;

        if (webSocketServer == null)
            webSocketServer = FindObjectOfType<WebSocketServer>();

        if (webSocketServer != null && webSocketServer.isRunning)
        {
            string url = $"http://{webSocketServer.serverIP}:{webSocketServer.port}";
            if (url != lastURL)
            {
                urlText.text = url;
                lastURL = url;
            }

            int clients = webSocketServer.connectedClients;
            if (clients > 0)
                statusText.text = $"{clients} Gerät(e) verbunden";
            else
                statusText.text = "Im Browser auf Computer eingeben";
        }
        else
        {
            urlText.text = "Server startet...";
            statusText.text = "Bitte warten";
        }
    }

    void SetVisibility(bool visible)
    {
        if (bgRenderer != null) bgRenderer.enabled = visible;
        if (urlRenderer != null) urlRenderer.enabled = visible;
        if (titleRenderer != null) titleRenderer.enabled = visible;
        if (statusRenderer != null) statusRenderer.enabled = visible;

        // Border lines
        var lines = displayRoot?.GetComponentsInChildren<LineRenderer>();
        if (lines != null)
            foreach (var lr in lines)
                lr.enabled = visible;
    }

    void ApplyFade(float alpha)
    {
        if (bgRenderer != null && bgRenderer.material != null)
        {
            Color c = bgRenderer.material.color;
            c.a = 0.92f * alpha;
            bgRenderer.material.color = c;
        }

        if (urlRenderer != null && urlRenderer.material != null)
        {
            Color c = urlRenderer.material.color;
            c.a = alpha;
            urlRenderer.material.color = c;
        }

        if (titleRenderer != null && titleRenderer.material != null)
        {
            Color c = titleRenderer.material.color;
            c.a = alpha;
            titleRenderer.material.color = c;
        }

        if (statusRenderer != null && statusRenderer.material != null)
        {
            Color c = statusRenderer.material.color;
            c.a = alpha;
            statusRenderer.material.color = c;
        }
    }

    void OnDestroy()
    {
        if (displayRoot != null)
            Destroy(displayRoot);
    }
}
