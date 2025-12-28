using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;
using System.Collections;

/// <summary>
/// API-Client für PubChem REST API
/// Ermöglicht Molekül-Suche und SDF-Download
/// </summary>
public class PubChemAPI : MonoBehaviour
{
    private const string BASE_URL = "https://pubchem.ncbi.nlm.nih.gov/rest/pug";
    private const float RATE_LIMIT_DELAY = 0.2f; // 200ms zwischen Requests (5 req/sec limit)
    
    private float lastRequestTime = 0f;
    
    /// <summary>
    /// Sucht ein Molekül nach Name und gibt die PubChem CID zurück
    /// </summary>
    /// <param name="moleculeName">Name des Moleküls (z.B. "ethanol")</param>
    /// <returns>PubChem Compound ID oder -1 bei Fehler</returns>
    public async Task<int> SearchMoleculeByName(string moleculeName)
    {
        await EnforceRateLimit();
        
        string url = $"{BASE_URL}/compound/name/{UnityWebRequest.EscapeURL(moleculeName)}/cids/JSON";
        
        Debug.Log($"[PubChem] Searching for: {moleculeName}");
        Debug.Log($"[PubChem] URL: {url}");
        
        try
        {
            string jsonResponse = await SendGetRequest(url);
            
            Debug.Log($"[PubChem] Response: {jsonResponse.Substring(0, Mathf.Min(200, jsonResponse.Length))}...");
            
            // Parse JSON manually since Unity JsonUtility has issues with nested objects
            // Expected: {"IdentifierList":{"CID":[702]}} or {"IdentifierList": {"CID": [702]}}
            // First, try with space after colon
            int cidStart = jsonResponse.IndexOf("\"CID\": [");
            int skipLength = 8; // Length of "CID": [
            
            // If not found, try without space
            if (cidStart < 0)
            {
                cidStart = jsonResponse.IndexOf("\"CID\":[");
                skipLength = 7; // Length of "CID":[
            }
            
            if (cidStart >= 0)
            {
                cidStart += skipLength;
                int cidEnd = jsonResponse.IndexOf("]", cidStart);
                if (cidEnd > cidStart)
                {
                    string cidStr = jsonResponse.Substring(cidStart, cidEnd - cidStart).Trim();
                    // Handle multiple CIDs, take first one
                    if (cidStr.Contains(","))
                    {
                        cidStr = cidStr.Split(',')[0].Trim();
                    }
                    
                    if (int.TryParse(cidStr, out int cid))
                    {
                        Debug.Log($"[PubChem] Found CID: {cid} for '{moleculeName}'");
                        return cid;
                    }
                }
            }
            
            Debug.LogWarning($"[PubChem] No CID found for '{moleculeName}'");
            Debug.LogWarning($"[PubChem] Full response: {jsonResponse}");
            return -1;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PubChem] Search failed: {e.Message}");
            Debug.LogError($"[PubChem] Stack trace: {e.StackTrace}");
            return -1;
        }
    }
    
    /// <summary>
    /// Lädt SDF-Datei von PubChem
    /// </summary>
    /// <param name="cid">PubChem Compound ID</param>
    /// <param name="use3D">3D-Konformer verwenden (empfohlen)</param>
    /// <returns>SDF-Inhalt als String oder null bei Fehler</returns>
    public async Task<string> DownloadSDF(int cid, bool use3D = true)
    {
        await EnforceRateLimit();
        
        string recordType = use3D ? "?record_type=3d" : "";
        string url = $"{BASE_URL}/compound/cid/{cid}/SDF{recordType}";
        
        Debug.Log($"[PubChem] Downloading SDF for CID {cid} ({(use3D ? "3D" : "2D")})");
        
        try
        {
            string sdfContent = await SendGetRequest(url);
            
            if (string.IsNullOrEmpty(sdfContent) || sdfContent.Contains("Status: 404"))
            {
                if (use3D)
                {
                    Debug.LogWarning($"[PubChem] No 3D structure for CID {cid}, trying 2D...");
                    return await DownloadSDF(cid, false);
                }
                
                Debug.LogError($"[PubChem] No structure data for CID {cid}");
                return null;
            }
            
            Debug.Log($"[PubChem] Successfully downloaded SDF ({sdfContent.Length} chars)");
            return sdfContent;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PubChem] Download failed: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Kombinierte Suche + Download
    /// </summary>
    public async Task<string> GetMoleculeSDF(string moleculeName, bool use3D = true)
    {
        int cid = await SearchMoleculeByName(moleculeName);
        
        if (cid <= 0)
        {
            return null;
        }
        
        return await DownloadSDF(cid, use3D);
    }
    
    // === Helper Methods ===
    
    private async Task<string> SendGetRequest(string url)
    {
        Debug.Log($"[PubChem] Sending request to: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30; // 30 second timeout
            var operation = request.SendWebRequest();
            
            float startTime = Time.time;
            
            // Await bis Request fertig ist
            while (!operation.isDone)
            {
                await Task.Yield();
                
                // Log progress every 5 seconds
                if (Time.time - startTime > 5f && Mathf.FloorToInt(Time.time - startTime) % 5 == 0)
                {
                    Debug.Log($"[PubChem] Still waiting... ({Mathf.FloorToInt(Time.time - startTime)}s)");
                }
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[PubChem] Request failed: {request.error}");
                throw new Exception($"HTTP Error: {request.error}");
            }
            
            Debug.Log($"[PubChem] Request successful ({request.downloadHandler.text.Length} bytes)");
            return request.downloadHandler.text;
        }
    }
    
    private async Task EnforceRateLimit()
    {
        float timeSinceLastRequest = Time.time - lastRequestTime;
        
        if (timeSinceLastRequest < RATE_LIMIT_DELAY)
        {
            float waitTime = RATE_LIMIT_DELAY - timeSinceLastRequest;
            await Task.Delay((int)(waitTime * 1000));
        }
        
        lastRequestTime = Time.time;
    }
    
    // === JSON Response Classes ===
    
    [Serializable]
    private class CIDResponse
    {
        public IdentifierList IdentifierList;
    }
    
    [Serializable]
    private class IdentifierList
    {
        public int[] CID;
    }
}
