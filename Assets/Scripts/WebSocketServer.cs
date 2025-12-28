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

    [Header("Status")]
    public bool isRunning = false;
    public string serverIP = "Not started";
    public int connectedClients = 0;

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
        
        Debug.Log($"[WebSocket] HTML loaded, length: {cachedHTML?.Length ?? 0} bytes");

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

            Debug.Log($"[WebSocket] Server started on {serverIP}:{port}");
            Debug.Log($"[WebSocket] iPad URL: http://{serverIP}:{port}");
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

                Debug.Log($"[WebSocket] Client connected! Total: {connectedClients}");

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
            Debug.Log($"[WebSocket] Request: {firstLine}");

            if (request.Contains("Upgrade: websocket") || request.Contains("upgrade: websocket"))
            {
                Debug.Log("[WebSocket] WebSocket upgrade detected");
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

            Debug.Log($"[WebSocket] Client disconnected. Remaining: {connectedClients}");
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

            Debug.Log("[WebSocket] Handshake completed successfully");
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
            Debug.Log($"[WebSocket] Raw message received: {message}");
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
        Debug.Log($"[WebSocket] Received: {message}");

        try
        {
            // Parse JSON: {"type":"load","molecule":"ethanol"}
            var data = JsonUtility.FromJson<WebSocketMessage>(message);

            if (data.type == "load" && !string.IsNullOrEmpty(data.molecule))
            {
                if (library != null)
                {
                    library.LoadAndDisplayMolecule(data.molecule);
                    BroadcastMessage($"{{\"type\":\"status\",\"message\":\"Loading {data.molecule}...\"}}");
                }
            }
            else if (data.type == "tutorial")
            {
                // Tutorial commands
                HandleTutorialCommand(data.action);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Failed to parse message: {e.Message}");
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
    /// Event Handler: Molekül erfolgreich geladen
    /// </summary>
    private void HandleMoleculeLoaded(MoleculeData molecule)
    {
        string json = $"{{\"type\":\"loaded\",\"molecule\":\"{molecule.name}\",\"atoms\":{molecule.atoms.Count}}}";
        BroadcastMessage(json);
        Debug.Log($"[WebSocket] Sent molecule loaded notification: {molecule.name}");
    }

    /// <summary>
    /// Event Handler: Fehler beim Laden
    /// </summary>
    private void HandleLoadError(string error)
    {
        string json = $"{{\"type\":\"error\",\"message\":\"{error}\"}}";
        BroadcastMessage(json);
        Debug.Log($"[WebSocket] Sent error notification: {error}");
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
            Debug.Log("[WebSocket] Loaded HTML from Resources");
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
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Molecule Controller</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            padding: 20px;
        }
        .container { max-width: 600px; margin: 0 auto; }
        .header {
            background: rgba(255,255,255,0.95);
            padding: 30px;
            border-radius: 20px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            margin-bottom: 20px;
        }
        h1 { color: #333; margin-bottom: 10px; }
        .status { color: #666; font-size: 14px; }
        .tutorial-box {
            background: rgba(255,255,255,0.95);
            padding: 20px;
            border-radius: 15px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }
        .tutorial-box h2 { color: #333; margin-bottom: 15px; font-size: 18px; }
        .tutorial-buttons {
            display: flex;
            gap: 10px;
        }
        .tutorial-btn {
            flex: 1;
            padding: 15px;
            border: none;
            border-radius: 10px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
        }
        .btn-start {
            background: #22c55e;
            color: white;
        }
        .btn-continue {
            background: #3b82f6;
            color: white;
            display: none;
        }
        .btn-continue.visible { display: block; }
        .btn-close {
            background: #ef4444;
            color: white;
            display: none;
        }
        .btn-close.visible { display: block; }
        .search-box {
            background: white;
            padding: 20px;
            border-radius: 15px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }
        input {
            width: 100%;
            padding: 15px;
            border: 2px solid #ddd;
            border-radius: 10px;
            font-size: 16px;
            margin-bottom: 10px;
        }
        button {
            width: 100%;
            padding: 15px;
            background: #667eea;
            color: white;
            border: none;
            border-radius: 10px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
        }
        button:active { background: #5568d3; }
        .molecules {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 15px;
        }
        .mol-btn {
            background: white;
            padding: 20px;
            border-radius: 15px;
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
            text-align: center;
            cursor: pointer;
            font-weight: 600;
            color: #333;
        }
        .mol-btn:active { transform: scale(0.95); }
        .connected { color: #22c55e; }
        .disconnected { color: #ef4444; }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🧪 Molecule Viewer</h1>
            <div class=""status"">
                <span id=""status"" class=""disconnected"">● Connecting...</span>
            </div>
        </div>
        
        <div class=""tutorial-box"">
            <h2>📚 Tutorial</h2>
            <div class=""tutorial-buttons"">
                <button class=""tutorial-btn btn-start"" id=""btnStart"" onclick=""tutorialStart()"">▶ Start Tutorial</button>
                <button class=""tutorial-btn btn-continue"" id=""btnContinue"" onclick=""tutorialContinue()"">➡ Continue</button>
                <button class=""tutorial-btn btn-close"" id=""btnClose"" onclick=""tutorialClose()"">✕ Close</button>
            </div>
        </div>
        
        <div class=""search-box"">
            <input type=""text"" id=""searchInput"" placeholder=""Enter molecule name..."">
            <button onclick=""searchMolecule()"">Load Molecule</button>
        </div>
        
        <div class=""molecules"">
            <div class=""mol-btn"" onclick=""loadMolecule('water')"">Water</div>
            <div class=""mol-btn"" onclick=""loadMolecule('ethanol')"">Ethanol</div>
            <div class=""mol-btn"" onclick=""loadMolecule('benzene')"">Benzene</div>
            <div class=""mol-btn"" onclick=""loadMolecule('methane')"">Methane</div>
            <div class=""mol-btn"" onclick=""loadMolecule('propanon')"">Propanon</div>
            <div class=""mol-btn"" onclick=""loadMolecule('ammonia')"">Ammonia</div>
        </div>
    </div>
    
    <script>
        console.log('[TABLET] Script loaded at', new Date().toISOString());
        let ws;
        let tutorialActive = false;
        const statusEl = document.getElementById('status');
        const btnStart = document.getElementById('btnStart');
        const btnContinue = document.getElementById('btnContinue');
        const btnClose = document.getElementById('btnClose');
        console.log('[TABLET] Elements found:', {statusEl, btnStart, btnContinue, btnClose});
        
        function connect() {
            // Connect to same host but with ws protocol (no specific path needed)
            const wsUrl = 'ws://' + window.location.host;
            console.log('Attempting WebSocket connection to:', wsUrl);
            
            try {
                ws = new WebSocket(wsUrl);
            } catch (err) {
                console.error('Failed to create WebSocket:', err);
                statusEl.textContent = '● Connection failed';
                statusEl.className = 'disconnected';
                setTimeout(connect, 2000);
                return;
            }
            
            ws.onopen = () => {
                console.log('WebSocket connected');
                statusEl.textContent = '● Connected';
                statusEl.className = 'connected';
            };
            
            ws.onclose = () => {
                console.log('WebSocket closed');
                statusEl.textContent = '● Disconnected';
                statusEl.className = 'disconnected';
                setTimeout(connect, 2000);
            };
            
            ws.onmessage = (e) => {
                console.log('Received message:', e.data);
                try {
                    const data = JSON.parse(e.data);
                    if (data.type === 'loaded') {
                        statusEl.textContent = '● Loaded: ' + data.molecule + ' (' + data.atoms + ' atoms)';
                    } else if (data.type === 'error') {
                        statusEl.textContent = '● Error: ' + data.message;
                    } else if (data.type === 'status') {
                        statusEl.textContent = '● ' + data.message;
                    } else if (data.type === 'tutorial') {
                        handleTutorialStatus(data);
                    } else if (data.type === 'tutorialContinue') {
                        btnContinue.classList.add('visible');
                    }
                } catch (err) {
                    console.error('Parse error:', err);
                }
            };
            
            ws.onerror = (err) => {
                console.error('WebSocket error:', err);
                statusEl.textContent = '● Connection error';
                statusEl.className = 'disconnected';
            };
        }
        
        function handleTutorialStatus(data) {
            if (data.status === 'started') {
                tutorialActive = true;
                btnStart.style.display = 'none';
                btnClose.classList.add('visible');
                statusEl.textContent = '● Tutorial started';
            } else if (data.status === 'closed') {
                tutorialActive = false;
                btnStart.style.display = 'block';
                btnContinue.classList.remove('visible');
                btnClose.classList.remove('visible');
                statusEl.textContent = '● Tutorial closed';
            } else if (data.status === 'continued') {
                btnContinue.classList.remove('visible');
            } else if (data.status === 'waitingContinue') {
                btnContinue.classList.add('visible');
            }
        }
        
        function tutorialStart() {
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'tutorial', action: 'start' }));
            }
        }
        
        function tutorialContinue() {
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'tutorial', action: 'continue' }));
            }
        }
        
        function tutorialClose() {
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'tutorial', action: 'close' }));
            }
        }
        
        function loadMolecule(name) {
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'load', molecule: name }));
                statusEl.textContent = '● Loading ' + name + '...';
            } else {
                statusEl.textContent = '● Not connected!';
                statusEl.className = 'disconnected';
            }
        }
        
        function searchMolecule() {
            const input = document.getElementById('searchInput');
            const name = input.value.trim();
            if (name) {
                loadMolecule(name);
                input.value = '';
            }
        }
        
        document.getElementById('searchInput').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                searchMolecule();
            }
        });
        
        connect();
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
        public string action; // For tutorial commands: start, close, continue
    }
}