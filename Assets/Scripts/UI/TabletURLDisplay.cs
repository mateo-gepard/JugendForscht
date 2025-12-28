using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Displays the tablet/iPad WebUI URL and debug info in world space for VR
/// Simple debug overlay that follows the camera
/// </summary>
public class TabletURLDisplay : MonoBehaviour
{
    [Header("References")]
    public WebSocketServer webSocketServer;
    public MoleculeLibrary moleculeLibrary;

    [Header("Settings")]
    [Tooltip("Distance in front of the camera")]
    public float distanceFromCamera = 2.0f;
    [Tooltip("Height above head - only visible when looking up")]
    public float heightAboveHead = 1.5f;
    [Tooltip("Minimum angle (degrees) player must look up to see display")]
    public float minLookUpAngle = 30f;
    public int maxDebugLines = 10;
    
    private GameObject textObject;
    private TextMesh textMesh;
    private Camera mainCamera;
    private string lastDisplayedText = "";
    private Queue<string> debugMessages = new Queue<string>();

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Only capture relevant debug messages
        if (logString.Contains("[PubChem]") || 
            logString.Contains("[MoleculeLibrary]") || 
            logString.Contains("[WebSocket]") ||
            logString.Contains("[PlaneAlignment]") ||
            logString.Contains("[MoleculeRenderer]"))
        {
            // Extract just the message part
            string shortLog = logString;
            if (logString.Length > 80)
            {
                shortLog = logString.Substring(0, 77) + "...";
            }
            
            debugMessages.Enqueue($"{type}: {shortLog}");
            
            // Keep only last N messages
            while (debugMessages.Count > maxDebugLines)
            {
                debugMessages.Dequeue();
            }
        }
    }

    void Start()
    {
        // Find WebSocketServer if not assigned
        if (webSocketServer == null)
        {
            webSocketServer = FindObjectOfType<WebSocketServer>();
        }
        
        // Find MoleculeLibrary if not assigned
        if (moleculeLibrary == null)
        {
            moleculeLibrary = FindObjectOfType<MoleculeLibrary>();
        }
        
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[TabletURLDisplay] No main camera found!");
            return;
        }
        
        CreateTextDisplay();
        
        // Update after a delay to let server start
        Invoke("ForceUpdateDisplay", 2f);
        
        Debug.Log("[TabletURLDisplay] Debug overlay initialized");
    }

    void CreateTextDisplay()
    {
        // Create the text object
        textObject = new GameObject("TabletURL_Display");
        
        // Add TextMesh
        textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = "Initializing...";
        textMesh.characterSize = 0.02f;
        textMesh.fontSize = 24;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.cyan;
        textMesh.fontStyle = FontStyle.Normal;
        textMesh.richText = false;
        
        // Use LegacyRuntime font (Arial.ttf is deprecated)
        textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        // Set up material with GUI/Text Shader (supports font atlases)
        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        if (renderer != null && textMesh.font != null)
        {
            Material textMat = new Material(Shader.Find("GUI/Text Shader"));
            textMat.mainTexture = textMesh.font.material.mainTexture;
            textMat.color = Color.yellow;
            renderer.material = textMat;
        }
        
        Debug.Log("[TabletURLDisplay] Text display created with debug support");
    }

    void LateUpdate()
    {
        if (textObject == null || mainCamera == null) return;
        
        // Position the text above the player - only visible when looking up
        Vector3 cameraPos = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;
        
        // Calculate how much the player is looking up (pitch angle)
        float lookUpAngle = -mainCamera.transform.eulerAngles.x;
        if (lookUpAngle < -180f) lookUpAngle += 360f;
        if (lookUpAngle > 180f) lookUpAngle -= 360f;
        
        // Only show when looking up past the threshold
        bool shouldBeVisible = lookUpAngle > minLookUpAngle;
        
        // Get renderer and control visibility
        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = shouldBeVisible;
        }
        
        // Position above the player, facing down
        Vector3 horizontalForward = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
        if (horizontalForward.magnitude < 0.01f)
        {
            horizontalForward = Vector3.forward;
        }
        
        Vector3 targetPos = cameraPos 
            + Vector3.up * heightAboveHead
            + horizontalForward * distanceFromCamera;
        
        textObject.transform.position = targetPos;
        
        // Face down toward the player
        textObject.transform.rotation = Quaternion.LookRotation(Vector3.down, horizontalForward);
        
        // Update text periodically
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (textMesh == null) return;
        
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // Server status
        if (webSocketServer != null && webSocketServer.isRunning)
        {
            sb.AppendLine($"Tablet: {webSocketServer.serverIP}:{webSocketServer.port}");
            sb.AppendLine($"Clients: {webSocketServer.connectedClients}");
        }
        else
        {
            sb.AppendLine("Tablet: Not Running");
        }
        
        // Library status
        if (moleculeLibrary != null)
        {
            sb.AppendLine($"Loading: {moleculeLibrary.IsLoading}");
            sb.AppendLine($"Cache: {moleculeLibrary.CachedMoleculeCount} molecules");
        }
        
        sb.AppendLine("--- Debug Log ---");
        
        // Debug messages
        foreach (string msg in debugMessages)
        {
            sb.AppendLine(msg);
        }
        
        string newText = sb.ToString();
        if (newText != lastDisplayedText)
        {
            textMesh.text = newText;
            lastDisplayedText = newText;
        }
    }
    
    void ForceUpdateDisplay()
    {
        lastDisplayedText = ""; // Force refresh
        UpdateDisplay();
    }

    void OnDestroy()
    {
        if (textObject != null)
        {
            Destroy(textObject);
        }
    }
}
