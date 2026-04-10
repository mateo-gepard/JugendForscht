using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to set up WebSocketServer in the scene
/// </summary>
public class WebSocketServerSetup : Editor
{
    [MenuItem("Tutorial/Setup WebSocket Server")]
    public static void SetupWebSocketServer()
    {
        // Check if WebSocketServer already exists
        WebSocketServer existing = FindObjectOfType<WebSocketServer>();
        if (existing != null)
        {
            Debug.Log("[Setup] WebSocketServer already exists in scene!");
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            
            // Make sure it's enabled
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            
            EditorUtility.DisplayDialog("WebSocketServer existiert bereits", 
                $"WebSocketServer gefunden auf: {existing.gameObject.name}\n\nDas GameObject wurde ausgewählt.", 
                "OK");
            return;
        }
        
        // Create new GameObject
        GameObject serverObj = new GameObject("WebSocketServer");
        
        // Add WebSocketServer component
        WebSocketServer server = serverObj.AddComponent<WebSocketServer>();
        
        // Try to find and assign MoleculeLibrary
        MoleculeLibrary library = FindObjectOfType<MoleculeLibrary>();
        if (library != null)
        {
            server.library = library;
            Debug.Log($"[Setup] MoleculeLibrary automatically assigned: {library.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("[Setup] No MoleculeLibrary found in scene - you may need to assign it manually");
        }
        
        // Select the new object
        Selection.activeGameObject = serverObj;
        EditorGUIUtility.PingObject(serverObj);
        
        // Mark scene as dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        
        Debug.Log("[Setup] WebSocketServer created successfully!");
        Debug.Log("[Setup] Don't forget to save the scene (Ctrl+S)!");
        
        EditorUtility.DisplayDialog("WebSocketServer erstellt!", 
            "WebSocketServer wurde zur Szene hinzugefügt.\n\n" +
            "1. Speichere die Szene (Ctrl+S)\n" +
            "2. Starte Play Mode\n" +
            "3. Die iPad URL wird angezeigt\n\n" +
            (library != null ? "MoleculeLibrary wurde automatisch zugewiesen." : "WARNUNG: MoleculeLibrary nicht gefunden - bitte manuell zuweisen!"),
            "OK");
    }
    
    [MenuItem("Tutorial/Check Server Status")]
    public static void CheckServerStatus()
    {
        WebSocketServer server = FindObjectOfType<WebSocketServer>();
        
        if (server == null)
        {
            EditorUtility.DisplayDialog("Server Status", 
                "Kein WebSocketServer in der Szene gefunden!\n\nVerwende 'Tutorial > Setup WebSocket Server' um einen zu erstellen.", 
                "OK");
            return;
        }
        
        string status = $"WebSocketServer gefunden auf: {server.gameObject.name}\n\n";
        status += $"GameObject aktiv: {server.gameObject.activeSelf}\n";
        status += $"Komponente aktiv: {server.enabled}\n";
        status += $"Port: {server.port}\n";
        
        if (Application.isPlaying)
        {
            status += $"\n--- Runtime Status ---\n";
            status += $"Server läuft: {server.isRunning}\n";
            status += $"Server IP: {server.serverIP}\n";
            status += $"Verbundene Clients: {server.connectedClients}\n";
            
            if (server.isRunning)
            {
                status += $"\niPad URL: http://{server.serverIP}:{server.port}";
            }
        }
        else
        {
            status += "\n(Starte Play Mode für Runtime-Status)";
        }
        
        if (server.library == null)
        {
            status += "\n\nWARNUNG: MoleculeLibrary nicht zugewiesen!";
        }
        
        EditorUtility.DisplayDialog("Server Status", status, "OK");
    }
}
