using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

/// <summary>
/// Simple WebSocket Server für iPad Companion App
/// Empfängt Molekül-Namen und sendet Status-Updates
/// </summary>
public class WebSocketServer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Port für WebSocket Server (Standard: 8080)")]
    public int port = 8080;

    [Header("References")]
    public MoleculeLibrary library;
    public MoleculeRenderer moleculeRenderer;

    [Header("Status")]
    public bool isRunning = false;
    public string serverIP = "Not started";
    public int connectedClients = 0;

    // Current UI mode
    private string currentMode = "keilstrich";
    private ChiralityPanelDisplay chiralityPanel;

    private TcpListener tcpListener;
    private Thread listenerThread;
    private List<TcpClient> clients = new List<TcpClient>();
    private Queue<string> messageQueue = new Queue<string>();
    private readonly object queueLock = new object();

    // Cache HTML on main thread
    private string cachedHTML = null;

    void Start()
    {
        // Force reload HTML on main thread (no caching between sessions)
        cachedHTML = null;
        cachedHTML = LoadHTMLContent();
        
        // Debug.Log($"[WebSocket] HTML loaded, length: {cachedHTML?.Length ?? 0} bytes");

        // Auto-find moleculeRenderer if not assigned
        if (moleculeRenderer == null)
        {
            moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
            if (moleculeRenderer != null)
            { } // was Debug.Log
            else
                Debug.LogWarning("[WebSocket] MoleculeRenderer not found!");
        }

        // Subscribe to library events (mit null check!)
        if (library != null)
        {
            library.OnMoleculeLoaded += HandleMoleculeLoaded;
            library.OnLoadError += HandleLoadError;
        }

        StartServer();
    }

    void Update()
    {
        // Process messages on main thread
        lock (queueLock)
        {
            while (messageQueue.Count > 0)
            {
                string message = messageQueue.Dequeue();
                ProcessMessage(message);
            }
        }
    }

    /// <summary>
    /// Startet den WebSocket Server
    /// </summary>
    public void StartServer()
    {
        if (isRunning) return;

        try
        {
            // Get local IP
            serverIP = GetLocalIPAddress();

            // Start TCP listener
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();

            // Start listening thread
            listenerThread = new Thread(ListenForClients);
            listenerThread.IsBackground = true;
            listenerThread.Start();

            isRunning = true;

            // Debug.Log($"[WebSocket] Server started on {serverIP}:{port}");
            // Debug.Log($"[WebSocket] iPad URL: http://{serverIP}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocket] Failed to start server: {e.Message}");
        }
    }

    /// <summary>
    /// Listener Thread - wartet auf neue Verbindungen
    /// </summary>
    private void ListenForClients()
    {
        try
        {
            while (isRunning)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                
                lock (clients)
                {
                    clients.Add(client);
                    connectedClients = clients.Count;
                }

                // Debug.Log($"[WebSocket] Client connected! Total: {connectedClients}");

                // Handle client in separate thread
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }
        catch (Exception e)
        {
            if (isRunning)
            {
                Debug.LogError($"[WebSocket] Listener error: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Handled einen einzelnen Client
    /// </summary>
    private void HandleClient(TcpClient client)
    {
        NetworkStream stream = null;

        try
        {
            stream = client.GetStream();
            byte[] buffer = new byte[4096];

            // First message should be HTTP upgrade request
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) return;

            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            // Debug: Log first line of request
            string firstLine = request.Split('\n')[0];
            // Debug.Log($"[WebSocket] Request: {firstLine}");

            if (request.Contains("Upgrade: websocket") || request.Contains("upgrade: websocket"))
            {
                // Debug.Log("[WebSocket] WebSocket upgrade detected");
                // WebSocket handshake
                PerformWebSocketHandshake(stream, request);

                // Now handle WebSocket messages
                while (client.Connected && isRunning)
                {
                    try
                    {
                        if (stream.DataAvailable)
                        {
                            string message = ReadWebSocketMessage(stream);
                            if (!string.IsNullOrEmpty(message))
                            {
                                lock (queueLock)
                                {
                                    messageQueue.Enqueue(message);
                                }
                            }
                        }
                        else
                        {
                            Thread.Sleep(10);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[WebSocket] Message read error: {e.Message}");
                        break;
                    }
                }
            }
            else if (request.Contains("GET /"))
            {
                // Serve HTML page (use cached version)
                ServeWebPage(stream);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Client handler error: {e.Message}");
        }
        finally
        {
            lock (clients)
            {
                clients.Remove(client);
                connectedClients = clients.Count;
            }

            if (stream != null)
            {
                try { stream.Close(); } catch { }
            }

            if (client != null)
            {
                try { client.Close(); } catch { }
            }

            // Debug.Log($"[WebSocket] Client disconnected. Remaining: {connectedClients}");
        }
    }

    /// <summary>
    /// WebSocket Handshake
    /// </summary>
    private void PerformWebSocketHandshake(NetworkStream stream, string request)
    {
        try
        {
            // Extract WebSocket key
            string key = "";
            string[] lines = request.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                {
                    key = line.Substring(18).Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("[WebSocket] No Sec-WebSocket-Key found in request");
                return;
            }

            // Generate accept key
            string acceptKey = Convert.ToBase64String(
                System.Security.Cryptography.SHA1.Create().ComputeHash(
                    Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
                )
            );

            // Send handshake response
            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {acceptKey}\r\n" +
                "\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Flush();

            // Debug.Log("[WebSocket] Handshake completed successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"[WebSocket] Handshake error: {e.Message}");
        }
    }

    /// <summary>
    /// Liest WebSocket Message
    /// </summary>
    private string ReadWebSocketMessage(NetworkStream stream)
    {
        try
        {
            byte[] header = new byte[2];
            int bytesRead = stream.Read(header, 0, 2);

            if (bytesRead < 2) return null;

            bool isMasked = (header[1] & 0b10000000) != 0;
            int msgLength = header[1] & 0b01111111;

            // Read extended length if needed
            if (msgLength == 126)
            {
                byte[] extLength = new byte[2];
                stream.Read(extLength, 0, 2);
                msgLength = (extLength[0] << 8) | extLength[1];
            }
            else if (msgLength == 127)
            {
                // Very long message (not implemented, shouldn't happen for our use case)
                Debug.LogWarning("[WebSocket] Message too long");
                return null;
            }

            // Read mask
            byte[] mask = new byte[4];
            if (isMasked)
            {
                stream.Read(mask, 0, 4);
            }

            // Read payload
            byte[] payload = new byte[msgLength];
            int totalRead = 0;
            while (totalRead < msgLength)
            {
                int read = stream.Read(payload, totalRead, msgLength - totalRead);
                if (read == 0) break;
                totalRead += read;
            }

            // Unmask
            if (isMasked)
            {
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(payload[i] ^ mask[i % 4]);
                }
            }

            string message = Encoding.UTF8.GetString(payload);
            // Debug.Log($"[WebSocket] Raw message received: {message}");
            return message;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Read error: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sendet Message an alle Clients
    /// </summary>
    public new void BroadcastMessage(string message)
    {
        // Forward errors to VR panel if command came from there
        if (vrPanelCommandActive && (message.Contains("\"error\"") || message.Contains("\"status\"")))
        {
            if (chiralityPanel != null)
            {
                // Extract the message text from JSON
                string displayMsg = ExtractJsonMessage(message);
                if (!string.IsNullOrEmpty(displayMsg))
                {
                    bool isError = message.Contains("\"error\"");
                    chiralityPanel.ShowVRMessage(displayMsg, isError);
                }
            }
        }

        byte[] payload = Encoding.UTF8.GetBytes(message);
        byte[] frame = new byte[payload.Length + 2];

        frame[0] = 0x81; // Text frame
        frame[1] = (byte)payload.Length;
        Array.Copy(payload, 0, frame, 2, payload.Length);

        // Lock to prevent collection modification during enumeration
        lock (clients)
        {
            foreach (var client in clients)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    stream.Write(frame, 0, frame.Length);
                }
                catch
                {
                    // Client disconnected
                }
            }
        }
    }

    /// <summary>
    /// Extrahiert den "message"-Wert aus einem einfachen JSON-String
    /// </summary>
    private string ExtractJsonMessage(string json)
    {
        string key = "\"message\":\"";
        int start = json.IndexOf(key);
        if (start < 0) return null;
        start += key.Length;
        int end = json.IndexOf("\"", start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    /// <summary>
    /// Serviert HTML Web-Page
    /// </summary>
    private void ServeWebPage(NetworkStream stream)
    {
        string html = cachedHTML ?? GetEmbeddedHTML();
        
        // Calculate actual byte length (not character length!) for Content-Length header
        byte[] htmlBytes = Encoding.UTF8.GetBytes(html);
        int contentLength = htmlBytes.Length;

        string headers =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {contentLength}\r\n" +
            "Cache-Control: no-cache, no-store, must-revalidate\r\n" +
            "Pragma: no-cache\r\n" +
            "Expires: 0\r\n" +
            "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.UTF8.GetBytes(headers);
        
        // Send headers + body separately to ensure correct length
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(htmlBytes, 0, htmlBytes.Length);
        stream.Flush();
    }

    /// <summary>
    /// Verarbeitet empfangene Nachricht
    /// </summary>
    private void ProcessMessage(string message)
    {
        // Debug.Log($"[WebSocket] Received: {message}");

        try
        {
            // Parse JSON
            var data = JsonUtility.FromJson<WebSocketMessage>(message);

            if (data.type == "load" && !string.IsNullOrEmpty(data.molecule))
            {
                // Keilstrich mode: load with stereo display
                SetStereoDisplay(true);

                // Clear any existing chirality/isomer visuals
                var vis = FindObjectOfType<ChiralityVisualizer>();
                if (vis != null) vis.ClearMarkers();
                var anim = FindObjectOfType<IsomerAnimator>();
                if (anim != null) anim.ClearEnantiomer();

                // Show plane in keilstrich mode
                if (moleculeRenderer == null)
                    moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
                if (moleculeRenderer != null)
                {
                    var planeAlign = moleculeRenderer.GetComponent<MoleculePlaneAlignment>();
                    if (planeAlign != null)
                    {
                        planeAlign.showPlaneInVR = true;
                        planeAlign.SetPlaneVisibility(true);
                    }
                }

                if (library != null)
                {
                    library.LoadAndDisplayMolecule(data.molecule);
                    BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Loading {data.molecule}...\"}}");
                }
            }
            else if (data.type == "iso_load" && !string.IsNullOrEmpty(data.molecule))
            {
                // Isomerie mode: load WITHOUT stereo display (no wedge/dash)
                SetStereoDisplay(false);

                // Clear any existing chirality markers
                var vis = FindObjectOfType<ChiralityVisualizer>();
                if (vis != null) vis.ClearMarkers();

                // Clear any existing enantiomer display
                var anim = FindObjectOfType<IsomerAnimator>();
                if (anim != null) anim.ClearEnantiomer();

                // Stop auto-rotation in isomerie mode
                if (moleculeRenderer == null)
                    moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
                if (moleculeRenderer != null)
                {
                    var planeAlign = moleculeRenderer.GetComponent<MoleculePlaneAlignment>();
                    if (planeAlign != null)
                    {
                        planeAlign.enableAutoRotation = false;
                        planeAlign.StopAutoRotation();
                        planeAlign.showPlaneInVR = false;  // Prevent plane creation during load
                        planeAlign.SetPlaneVisibility(false);
                    }
                }

                if (library != null)
                {
                    library.LoadAndDisplayMolecule(data.molecule);
                    BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Loading {data.molecule} (Isomerie)...\"}}");
                }
            }
            else if (data.type == "mode")
            {
                // Switch UI mode
                HandleModeSwitch(data.mode);
            }
            else if (data.type == "tutorial")
            {
                HandleTutorialCommand(data.action);
            }
            else if (data.type == "chirality")
            {
                HandleChiralityCommand(data.action);
            }
            else if (data.type == "isomer")
            {
                HandleIsomerCommand(data.action, data.center);
            }
            else if (data.type == "quiz")
            {
                HandleQuizCommand(data.action, data.answer, data.mode);
            }
            else if (data.type == "builder")
            {
                HandleBuilderCommand(data.action);
            }
            else if (data.type == "clear_all")
            {
                // Clear everything: molecule, chirality markers, enantiomer, plane
                var vis = FindObjectOfType<ChiralityVisualizer>();
                if (vis != null) vis.ClearMarkers();
                var anim = FindObjectOfType<IsomerAnimator>();
                if (anim != null) anim.ClearEnantiomer();
                if (library != null) library.ClearCurrentMolecule();

                if (moleculeRenderer == null)
                    moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
                if (moleculeRenderer != null)
                {
                    var planeAlign = moleculeRenderer.GetComponent<MoleculePlaneAlignment>();
                    if (planeAlign != null)
                        planeAlign.SetPlaneVisibility(false);
                }

                BroadcastMessage("{\"type\":\"status\",\"message\":\"Alles gelöscht\"}");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Failed to parse message: {e.Message}");
        }
    }

    /// <summary>
    /// Switches stereo display on/off on the renderer
    /// </summary>
    private void SetStereoDisplay(bool enabled)
    {
        // Auto-find if not set
        if (moleculeRenderer == null)
            moleculeRenderer = FindObjectOfType<MoleculeRenderer>();

        if (moleculeRenderer != null)
        {
            moleculeRenderer.enableStereoDisplay = enabled;
            // Re-render bonds to apply the change immediately
            moleculeRenderer.RerenderBondsOnly();
            // Debug.Log($"[WebSocket] Stereo display: {enabled}");
        }
        else
        {
            Debug.LogWarning("[WebSocket] Cannot set stereo display: renderer not found");
        }
    }

    /// <summary>
    /// Handle mode switch from web UI
    /// </summary>
    private void HandleModeSwitch(string mode)
    {
        currentMode = mode ?? "keilstrich";
        Debug.Log($"[WebSocket] Mode switched to: {currentMode}");

        if (moleculeRenderer == null)
            moleculeRenderer = FindObjectOfType<MoleculeRenderer>();

        var planeAlign = moleculeRenderer?.GetComponent<MoleculePlaneAlignment>();
        
        // Handle Chirality VR Panel Visibility
        // The iPad UI tab "Chiralität" sends mode "isomerie"
        if (currentMode == "isomerie")
        {
            if (chiralityPanel == null)
            {
                var go = new GameObject("ChiralityPanel");
                chiralityPanel = go.AddComponent<ChiralityPanelDisplay>();
                chiralityPanel.Initialize();
                Debug.Log("[WebSocket] ChiralityPanel created and initialized");
            }
            chiralityPanel.gameObject.SetActive(true);
            Debug.Log("[WebSocket] ChiralityPanel shown");
        }
        else
        {
            if (chiralityPanel != null) chiralityPanel.gameObject.SetActive(false);
        }

        if (currentMode == "isomerie")
        {
            SetStereoDisplay(false);
            if (planeAlign != null)
            {
                planeAlign.showPlaneInVR = false;
                planeAlign.SetPlaneVisibility(false);
            }
        }
        else
        {
            SetStereoDisplay(true);
            if (planeAlign != null)
            {
                planeAlign.showPlaneInVR = true;
                planeAlign.SetPlaneVisibility(true);
            }
        }
    }

    /// <summary>
    /// Flag: wenn true, werden BroadcastMessage-Fehler auch im VR-Panel angezeigt
    /// </summary>
    private bool vrPanelCommandActive = false;

    /// <summary>
    /// Router for VR panel commands
    /// </summary>
    public void HandleVRControlPanelCommand(string command)
    {
        Debug.Log($"[WebSocket] VR Panel command: {command}");
        vrPanelCommandActive = true;
        
        switch (command)
        {
            case "chirality_detect":
                HandleChiralityCommand("detect");
                break;
            case "chirality_clear":
                // Clear BOTH chirality markers AND isomer displays
                HandleChiralityCommand("clear");
                var animator = FindObjectOfType<IsomerAnimator>();
                if (animator != null) animator.ClearEnantiomer();
                break;
            case "generate_enantiomer":
                HandleIsomerCommand("mirror", 0);
                break;
            case "generate_diastereomer":
                HandleIsomerCommand("diastereomer", 0);
                break;
            case "generate_conformer":
                HandleIsomerCommand("conformer", 0);
                break;
            case "test_meso":
                HandleIsomerCommand("meso", 0);
                break;
            case "generate_cistrans":
                HandleIsomerCommand("cistrans", 0);
                break;
            case "generate_constitutional":
                HandleIsomerCommand("constitutional", 0);
                break;
            case "test_overlay":
                HandleIsomerCommand("overlay", 0);
                break;
        }
        
        vrPanelCommandActive = false;
    }

    /// <summary>
    /// Handle chirality detection commands (Phase 3)
    /// </summary>
    private void HandleChiralityCommand(string action)
    {
        // Debug.Log($"[WebSocket] Chirality command: {action}");

        if (action == "detect")
        {
            // Get current molecule from library
            if (library != null && library.GetCurrentMolecule() != null)
            {
                var molecule = library.GetCurrentMolecule();
                // Debug.Log($"[WebSocket] Running chirality detection on: {molecule.name}");

                // Run chirality detection
                var centers = ChiralityDetector.DetectChiralCenters(molecule);

                // Show visual markers in VR
                var visualizer = FindObjectOfType<ChiralityVisualizer>();
                if (visualizer == null)
                {
                    // Auto-create visualizer on the renderer
                    if (moleculeRenderer == null)
                        moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
                    if (moleculeRenderer != null)
                    {
                        visualizer = moleculeRenderer.gameObject.AddComponent<ChiralityVisualizer>();
                        // Debug.Log("[WebSocket] Auto-created ChiralityVisualizer");
                    }
                }
                if (visualizer != null)
                {
                    visualizer.ShowChiralCenters(centers, molecule);
                }

                // Build JSON response for web UI
                var centersJson = new System.Text.StringBuilder("[");
                for (int i = 0; i < centers.Count; i++)
                {
                    var c = centers[i];
                    if (i > 0) centersJson.Append(",");
                    string neighborsStr = string.Join("\",\"", c.neighborLabels);
                    centersJson.Append($"{{\"atomId\":{c.atomId},\"config\":\"{c.configuration}\"," +
                                     $"\"element\":\"{c.element}\"," +
                                     $"\"neighbors\":[\"{neighborsStr}\"]}}");
                }
                centersJson.Append("]");

                string json = $"{{\"type\":\"chirality_result\",\"centers\":{centersJson},\"molecule\":\"{molecule.name}\"}}";
                BroadcastMessage(json);
                // Debug.Log($"[WebSocket] Chirality result sent: {centers.Count} centers");
            }
            else
            {
                BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Molek\u00fcl geladen\"}");
            }
        }
        else if (action == "clear")
        {
            // Clear chirality markers
            var visualizer = FindObjectOfType<ChiralityVisualizer>();
            if (visualizer != null)
                visualizer.ClearMarkers();
        }
    }

    /// <summary>
    /// Handle isomer generation commands (Phase 4)
    /// </summary>
    private void HandleIsomerCommand(string action, int center)
    {
        // Debug.Log($"[WebSocket] Isomer command: {action}, center: {center}");

        // Auto-find or create IsomerAnimator
        var animator = FindObjectOfType<IsomerAnimator>();
        if (animator == null)
        {
            if (moleculeRenderer == null)
                moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
            if (moleculeRenderer != null)
            {
                animator = moleculeRenderer.gameObject.AddComponent<IsomerAnimator>();
                // Debug.Log("[WebSocket] Auto-created IsomerAnimator");
            }
        }

        if (action == "mirror")
        {
            if (library != null && library.GetCurrentMolecule() != null)
            {
                var original = library.GetCurrentMolecule();
                var enantiomer = IsomerGenerator.GenerateEnantiomer(original);

                // Check if molecule is identical to its mirror image (achiral/meso)
                if (IsomerGenerator.AreMoleculesIdentical(original, enantiomer))
                {
                    BroadcastMessage("{\"type\":\"error\",\"message\":\"Dieses Molekül ist identisch mit seinem Spiegelbild (achiral). Nutze Konformationsisomerie.\"}");
                    return;
                }

                if (enantiomer != null && animator != null)
                {
                    animator.ShowEnantiomer(original, enantiomer);
                    BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Enantiomer von {original.name} erzeugt\"}}");
                }
            }
            else
            {
                BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Molekül geladen\"}");
            }
        }
        else if (action == "overlay")
        {
            if (animator != null)
            {
                animator.StartOverlayTest();
                BroadcastMessage("{\"type\":\"status\",\"message\":\"\u00dcberlagerungstest gestartet\"}");
            }
            else
            {
                BroadcastMessage("{\"type\":\"error\",\"message\":\"Erst Isomer erzeugen\"}");
            }
        }
        else if (action == "diastereomer")
        {
            if (library != null && library.GetCurrentMolecule() != null)
            {
                var original = library.GetCurrentMolecule();
                var adjacency = ChiralityDetector.BuildAdjacencyGraph(original);
                var centers = ChiralityDetector.DetectChiralCenters(original);

                if (centers.Count >= 2)
                {
                    MoleculeData diastereomer = null;
                    int invertedCenter = -1;

                    foreach (var center_item in centers)
                    {
                        var candidate = IsomerGenerator.GenerateDiastereomer(
                            original, center_item.atomId, adjacency);

                        if (candidate == null) continue;

                        // Reject if identical to original
                        if (IsomerGenerator.AreMoleculesIdentical(original, candidate)) continue;

                        // Reject if it's an enantiomer (all configs flipped)
                        if (IsomerGenerator.IsEnantiomer(original, candidate)) continue;

                        diastereomer = candidate;
                        invertedCenter = center_item.atomId;
                        break;
                    }

                    if (diastereomer != null && animator != null)
                    {
                        animator.ShowEnantiomer(original, diastereomer);
                        BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Diastereomer von {original.name} erzeugt (Zentrum {invertedCenter} invertiert)\"}}");
                    }
                    else
                    {
                        BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Diastereomer möglich. Nutze Konformationsisomerie oder Enantiomere.\"}");
                    }
                }
                else
                {
                    BroadcastMessage("{\"type\":\"error\",\"message\":\"Mindestens 2 Chiralitätszentren nötig für Diastereomere\"}");
                }
            }
            else
            {
                BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Molekül geladen\"}");
            }
        }
        else if (action == "conformer")
        {
            // Konformationsisomerie: identische Kopie
            if (library != null && library.GetCurrentMolecule() != null)
            {
                var original = library.GetCurrentMolecule();
                if (animator != null)
                {
                    animator.ShowConformer(original);
                    BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Konformer von {original.name} erzeugt\"}}");
                }
            }
            else
            {
                BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Molekül geladen\"}");
            }
        }
        else if (action == "meso")
        {
            // Meso-Erkennung
            if (library != null && library.GetCurrentMolecule() != null)
            {
                var original = library.GetCurrentMolecule();
                if (animator != null)
                {
                    animator.TestMeso(original);
                    BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Meso-Test für {original.name} gestartet\"}}");
                }
            }
            else
            {
            BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Molekül geladen\"}");
            }
        }
        else if (action == "cistrans")
        {
            // cis/trans (E/Z) Isomerie
            if (library != null && library.GetCurrentMolecule() != null)
            {
                var original = library.GetCurrentMolecule();
                if (!IsomerGenerator.HasDoubleBond(original))
                {
                    BroadcastMessage("{\"type\":\"error\",\"message\":\"Keine C=C-Doppelbindung gefunden. cis/trans-Isomerie benötigt eine Doppelbindung.\"}");
                    return;
                }
                var cisTransIsomer = IsomerGenerator.GenerateCisTransIsomer(original);
                if (cisTransIsomer != null && animator != null)
                {
                    animator.ShowCisTransIsomer(original, cisTransIsomer);
                    BroadcastMessage($"{{\"type\":\"status\",\"message\":\"cis/trans-Isomer von {original.name} erzeugt\"}}");
                }
                else
                {
                    BroadcastMessage("{\"type\":\"error\",\"message\":\"cis/trans-Isomer konnte nicht erzeugt werden\"}");
                }
            }
            else
            {
                BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Molekül geladen\"}");
            }
        }
        else if (action == "constitutional")
        {
            // Konstitutionsisomere: Lade Partner-Molekül von PubChem
            if (library != null && library.GetCurrentMolecule() != null)
            {
                var original = library.GetCurrentMolecule();
                string partnerName = IsomerGenerator.GetConstitutionalPartner(original.name);
                if (partnerName == null)
                {
                    BroadcastMessage($"{{\"type\":\"error\",\"message\":\"Kein bekanntes Konstitutionsisomer für '{original.name}'. Probiere: Ethanol, Buthan, Aceton, Glucose.\"}}");
                    return;
                }

                // Load partner molecule asynchronously
                LoadConstitutionalPartnerAsync(original, partnerName, animator);
            }
            else
            {
                BroadcastMessage("{\"type\":\"error\",\"message\":\"Kein Molekül geladen\"}");
            }
        }
    }

    /// <summary>
    /// Lädt ein Konstitutionsisomer asynchron von PubChem und zeigt es an
    /// </summary>
    private async void LoadConstitutionalPartnerAsync(MoleculeData original, string partnerName, IsomerAnimator animator)
    {
        BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Lade Konstitutionsisomer '{partnerName}' von PubChem...\"}}");

        try
        {
            var pubchem = FindObjectOfType<PubChemAPI>();
            if (pubchem == null)
            {
                var go = new GameObject("TempPubChem");
                pubchem = go.AddComponent<PubChemAPI>();
            }

            string sdf = await pubchem.GetMoleculeSDF(partnerName);
            if (string.IsNullOrEmpty(sdf))
            {
                BroadcastMessage($"{{\"type\":\"error\",\"message\":\"Konnte '{partnerName}' nicht von PubChem laden\"}}");
                return;
            }

            MoleculeData partner = SDFParser.Parse(sdf, partnerName);
            if (partner == null)
            {
                BroadcastMessage($"{{\"type\":\"error\",\"message\":\"Fehler beim Parsen von '{partnerName}'\"}}");
                return;
            }

            if (animator != null)
            {
                animator.ShowConstitutionalIsomer(original, partner);
                BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Konstitutionsisomer: {original.name} ↔ {partner.name}\"}}");
            }
        }
        catch (System.Exception e)
        {
            BroadcastMessage($"{{\"type\":\"error\",\"message\":\"Fehler: {e.Message}\"}}");
        }
    }

    /// <summary>
    /// Handle builder (Molekülbaukasten) commands from iPad
    /// </summary>
    private void HandleBuilderCommand(string action)
    {
        var builder = BuilderManager.Instance;

        // Auto-create BuilderManager if needed
        if (builder == null)
        {
            var go = new GameObject("BuilderManager");
            builder = go.AddComponent<BuilderManager>();
            builder.moleculeLibrary = library;
            // ElementDatabase will be auto-found from MoleculeLibrary
            Debug.Log("[WebSocket] Auto-created BuilderManager");
        }
        
        // Ensure webSocket reference is always set
        builder.webSocket = this;

        if (action == "start")
        {
            builder.StartBuilder();
            BroadcastMessage("{\"type\":\"status\",\"message\":\"Molekülbaukasten gestartet\"}");
        }
        else if (action == "stop")
        {
            builder.StopBuilder();
            BroadcastMessage("{\"type\":\"status\",\"message\":\"Molekülbaukasten beendet\"}");
        }
    }

    /// <summary>
    /// Handle tutorial commands from iPad
    /// </summary>
    private void HandleTutorialCommand(string action)
    {
        var tutorialManager = TutorialManager.Instance;
        if (tutorialManager == null)
        {
            Debug.LogWarning("[WebSocket] TutorialManager not found!");
            return;
        }

        switch (action)
        {
            case "start":
                tutorialManager.StartTutorial();
                BroadcastMessage("{\"type\":\"tutorial\",\"status\":\"started\"}");
                break;
            case "close":
                tutorialManager.CloseTutorial();
                BroadcastMessage("{\"type\":\"tutorial\",\"status\":\"closed\"}");
                break;
            case "continue":
                tutorialManager.ContinueToNextStep();
                BroadcastMessage("{\"type\":\"tutorial\",\"status\":\"continued\"}");
                break;
            case "previous":
                tutorialManager.GoToPreviousStep();
                BroadcastMessage("{\"type\":\"tutorial\",\"status\":\"previous\"}");
                break;
            default:
                Debug.LogWarning($"[WebSocket] Unknown tutorial action: {action}");
                break;
        }
    }

    /// <summary>
    /// Handle Quiz-Kommandos von der Web-UI
    /// Actions: start, answer, next, end
    /// </summary>
    private void HandleQuizCommand(string action, int answer, string categoryMode)
    {
        // Debug.Log($"[WebSocket] Quiz command: {action}, answer: {answer}, category: {categoryMode}");

        var quiz = QuizManager.Instance;
        if (quiz == null)
        {
            // Auto-create QuizManager
            var go = new GameObject("QuizManager");
            quiz = go.AddComponent<QuizManager>();
            quiz.webSocket = this;
            quiz.moleculeLibrary = library;
            // Debug.Log("[WebSocket] Auto-created QuizManager");
        }

        switch (action)
        {
            case "start":
                // Parse category from mode string
                QuizCategory? cat = null;
                if (categoryMode == "Keilstrich") cat = QuizCategory.Keilstrich;
                else if (categoryMode == "Chirality") cat = QuizCategory.Chirality;
                quiz.StartQuiz(cat);
                break;
            case "answer":
                quiz.SubmitAnswer(answer);
                break;
            case "next":
                quiz.NextQuestion();
                break;
            case "end":
                quiz.EndQuiz();
                break;
            default:
                Debug.LogWarning($"[WebSocket] Unknown quiz action: {action}");
                break;
        }
    }

    /// <summary>
    /// Event Handler: Molekül erfolgreich geladen
    /// </summary>
    private void HandleMoleculeLoaded(MoleculeData molecule)
    {
        string json = $"{{\"type\":\"loaded\",\"molecule\":\"{molecule.name}\",\"atoms\":{molecule.atoms.Count}}}";
        BroadcastMessage(json);
        // Debug.Log($"[WebSocket] Sent molecule loaded notification: {molecule.name}");
    }

    /// <summary>
    /// Event Handler: Fehler beim Laden
    /// </summary>
    private void HandleLoadError(string error)
    {
        string json = $"{{\"type\":\"error\",\"message\":\"{error}\"}}";
        BroadcastMessage(json);
        // Debug.Log($"[WebSocket] Sent error notification: {error}");
    }

    /// <summary>
    /// Gibt lokale IP-Adresse zurück
    /// </summary>
    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch { }

        return "127.0.0.1";
    }

    /// <summary>
    /// Lädt HTML Content (Main Thread)
    /// </summary>
    private string LoadHTMLContent()
    {
        // Try to load from Resources first (note: filename has typo "Contoller" with one L)
        TextAsset htmlFile = Resources.Load<TextAsset>("MoleculeContoller");
        if (htmlFile != null)
        {
            // Debug.Log("[WebSocket] Loaded HTML from Resources");
            return htmlFile.text;
        }

        Debug.LogWarning("[WebSocket] MoleculeContoller.html not found in Resources, using embedded HTML");
        return GetEmbeddedHTML();
    }

    /// <summary>
    /// Embedded HTML (funktioniert immer)
    /// </summary>
    private string GetEmbeddedHTML()
    {
        return @"<!DOCTYPE html>
<html lang=""de"">
<head>
<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1,user-scalable=no"">
<title>Molek&uuml;l-Betrachter</title>
<style>
:root{--bg:#0f1117;--card:#1a1d27;--el:#252937;--acc:#6c8aff;--ok:#34d399;--err:#f87171;--t1:#e8eaed;--t2:#9aa0b0;--t3:#5c6378;--brd:rgba(255,255,255,.06);--r:14px}
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:-apple-system,BlinkMacSystemFont,system-ui,sans-serif;background:var(--bg);color:var(--t1);min-height:100dvh;padding:16px;-webkit-font-smoothing:antialiased}
.c{max-width:520px;margin:0 auto}
.hd{display:flex;align-items:center;justify-content:space-between;padding:20px 0 24px}
.br{display:flex;align-items:center;gap:12px}
.bi{width:40px;height:40px;background:linear-gradient(135deg,var(--acc),#a78bfa);border-radius:12px;display:flex;align-items:center;justify-content:center;font-size:20px;box-shadow:0 4px 12px rgba(108,138,255,.3)}
.br h1{font-size:20px;font-weight:700}.br span{display:block;font-size:12px;color:var(--t3);font-weight:500;letter-spacing:.5px;text-transform:uppercase;margin-top:1px}
.sb{display:flex;align-items:center;gap:6px;padding:6px 12px;border-radius:20px;font-size:12px;font-weight:600;background:var(--card);border:1px solid var(--brd)}
.sd{width:7px;height:7px;border-radius:50%;background:var(--err)}
.sb.on .sd{background:var(--ok);box-shadow:0 0 8px rgba(52,211,153,.5)}.sb.on{border-color:rgba(52,211,153,.2);background:rgba(52,211,153,.12)}
.cd{background:var(--card);border:1px solid var(--brd);border-radius:20px;padding:20px;margin-bottom:14px;box-shadow:0 2px 16px rgba(0,0,0,.3)}
.cl{font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:1px;color:var(--t3);margin-bottom:14px}
.btn{flex:1;padding:14px 16px;border:none;border-radius:var(--r);font-size:15px;font-weight:600;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;transition:.2s}
.btn:active{transform:scale(.97)}.btn:disabled{opacity:.3;pointer-events:none}
.ba{background:linear-gradient(135deg,var(--acc),#8b7cf6);color:#fff;box-shadow:0 4px 12px rgba(108,138,255,.3)}
.bc{background:linear-gradient(135deg,var(--ok),#2dd4a0);color:#064e36;box-shadow:0 4px 12px rgba(52,211,153,.3);font-size:16px}
.bg{background:var(--el);color:var(--t2);border:1px solid var(--brd)}
.bd{background:rgba(248,113,113,.12);color:var(--err);border:1px solid rgba(248,113,113,.15)}
.row{display:flex;gap:10px}
.sr{display:flex;gap:10px}
.si{flex:1;padding:13px 16px;background:var(--el);border:1px solid var(--brd);border-radius:var(--r);color:var(--t1);font-size:15px;font-family:inherit;outline:none}
.si:focus{border-color:var(--acc);box-shadow:0 0 0 3px rgba(108,138,255,.25)}
.si::placeholder{color:var(--t3)}
.mg{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}
.mt{background:var(--el);border:1px solid var(--brd);border-radius:var(--r);padding:16px 10px;text-align:center;cursor:pointer;transition:.2s}
.mt:active{transform:scale(.95)}.mt .e{font-size:28px;margin-bottom:6px;display:block}.mt .n{font-size:13px;font-weight:600}.mt .f{font-size:11px;color:var(--t3);margin-top:2px}
.toast{position:fixed;bottom:24px;left:50%;transform:translateX(-50%) translateY(80px);background:var(--card);border:1px solid var(--brd);color:var(--t1);padding:12px 20px;border-radius:var(--r);font-size:14px;font-weight:500;box-shadow:0 8px 32px rgba(0,0,0,.4);transition:transform .35s cubic-bezier(.34,1.56,.64,1);z-index:100;pointer-events:none}
.toast.v{transform:translateX(-50%) translateY(0)}
.ft{text-align:center;padding:16px 0 8px;font-size:11px;color:var(--t3)}
</style>
</head>
<body>
<div class=""c"">
<div class=""hd""><div class=""br""><div class=""bi"">&#9883;</div><div><h1>Molek&uuml;l-Betrachter</h1><span>VR Lernumgebung</span></div></div><div class=""sb"" id=""sb""><div class=""sd""></div><span id=""st"">Verbinde&hellip;</span></div></div>
<div class=""cd""><div class=""cl"">Tutorial</div>
<div id=""idle"" class=""row""><button class=""btn ba"" onclick=""tS()"">&#9654; Tutorial starten</button></div>
<div id=""act"" style=""display:none""><div class=""row"" style=""margin-bottom:10px""><button class=""btn bg"" id=""bp"" onclick=""tP()"">&lsaquo; Zur&uuml;ck</button><button class=""btn bc"" id=""bn"" onclick=""tN()"">Weiter &rsaquo;</button></div><button class=""btn bd"" onclick=""tC()"" style=""width:100%"">Tutorial beenden</button></div>
</div>
<div class=""cd""><div class=""cl"">Molek&uuml;l laden</div><div class=""sr""><input class=""si"" type=""text"" id=""q"" placeholder=""z.B. Aspirin, Glucose&hellip;"" autocomplete=""off""><button class=""btn ba"" onclick=""sM()"" style=""flex-shrink:0;padding:13px 20px"">Laden</button></div></div>
<div class=""cd""><div class=""cl"">Schnellauswahl</div><div class=""mg"">
<div class=""mt"" onclick=""lM('water')""><span class=""e"">&#128167;</span><div class=""n"">Wasser</div><div class=""f"">H&#8322;O</div></div>
<div class=""mt"" onclick=""lM('ethanol')""><span class=""e"">&#129514;</span><div class=""n"">Ethanol</div><div class=""f"">C&#8322;H&#8325;OH</div></div>
<div class=""mt"" onclick=""lM('benzene')""><span class=""e"">&#11041;</span><div class=""n"">Benzol</div><div class=""f"">C&#8326;H&#8326;</div></div>
<div class=""mt"" onclick=""lM('methane')""><span class=""e"">&#128311;</span><div class=""n"">Methan</div><div class=""f"">CH&#8324;</div></div>
<div class=""mt"" onclick=""lM('propanon')""><span class=""e"">&#9879;</span><div class=""n"">Propanon</div><div class=""f"">C&#8323;H&#8326;O</div></div>
<div class=""mt"" onclick=""lM('ammonia')""><span class=""e"">&#128168;</span><div class=""n"">Ammoniak</div><div class=""f"">NH&#8323;</div></div>
</div></div>
<div class=""ft"">Jugend Forscht &middot; Meta Quest 3 &middot; Molekulare Geometrie</div>
</div>
<div class=""toast"" id=""toast""></div>
<script>
var ws,ta=false,cu=0,tu=11,wc=false;
function cn(){var u='ws://'+location.host+'/ws';ws=new WebSocket(u);
ws.onopen=function(){document.getElementById('sb').className='sb on';document.getElementById('st').textContent='Verbunden'};
ws.onclose=function(){document.getElementById('sb').className='sb';document.getElementById('st').textContent='Getrennt';setTimeout(cn,2000)};
ws.onmessage=function(e){try{var d=JSON.parse(e.data);if(d.type==='loaded')sT(d.molecule+' geladen');else if(d.type==='error')sT('Fehler: '+d.message);else if(d.type==='status')sT(d.message);else if(d.type==='tutorial'){if(d.status==='started'){ta=true;cu=1;wc=false;uU()}else if(d.status==='closed'){ta=false;cu=0;wc=false;uU()}else if(d.status==='continued'){cu++;wc=false;if(cu>tu){ta=false;cu=0}uU()}else if(d.status==='previous'){cu=Math.max(1,cu-1);wc=false;uU()}else if(d.status==='waitingContinue'){wc=true;if(d.unit!==undefined)cu=d.unit+1;uU()}}}catch(x){}};ws.onerror=function(){}}
function uU(){if(ta){document.getElementById('idle').style.display='none';document.getElementById('act').style.display='block';document.getElementById('bp').disabled=cu<=1;document.getElementById('bn').disabled=!wc;document.getElementById('bn').innerHTML=cu>=tu?'\u2713 Fertig':'Weiter \u203A'}else{document.getElementById('idle').style.display='flex';document.getElementById('act').style.display='none'}}
function sd(o){if(ws&&ws.readyState===1){ws.send(JSON.stringify(o));return true}sT('Keine Verbindung');return false}
function tS(){sd({type:'tutorial',action:'start'})}function tC(){sd({type:'tutorial',action:'close'})}
function tN(){cu>=tu?sd({type:'tutorial',action:'close'}):sd({type:'tutorial',action:'continue'})}
function tP(){sd({type:'tutorial',action:'previous'})}
function lM(n){if(sd({type:'load',molecule:n}))sT('Lade '+n+'\u2026')}
function sM(){var n=document.getElementById('q').value.trim();if(n){lM(n);document.getElementById('q').value=''}}
document.getElementById('q').addEventListener('keypress',function(e){if(e.key==='Enter')sM()});
var tt;function sT(m){var t=document.getElementById('toast');t.textContent=m;t.classList.add('v');clearTimeout(tt);tt=setTimeout(function(){t.classList.remove('v')},2500)}
uU();cn();
</script>
</body>
</html>";
    }

    void OnDestroy()
    {
        isRunning = false;

        // Unsubscribe from events
        if (library != null)
        {
            library.OnMoleculeLoaded -= HandleMoleculeLoaded;
            library.OnLoadError -= HandleLoadError;
        }

        if (tcpListener != null)
        {
            tcpListener.Stop();
        }

        if (listenerThread != null)
        {
            listenerThread.Abort();
        }

        foreach (var client in clients)
        {
            client.Close();
        }
    }

    [Serializable]
    public class WebSocketMessage
    {
        public string type;
        public string molecule;
        public string action;   // For tutorial/chirality/isomer/quiz commands
        public string mode;     // For mode switch: "keilstrich" or "isomerie"
        public int center;      // For isomer inversion: chiral center atom ID
        public int answer;      // For quiz: selected answer index
    }
}