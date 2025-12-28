using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Verwaltet geladene Molek�le und Recent-Liste
/// </summary>
public class MoleculeLibrary : MonoBehaviour
{
    [Header("References")]
    public ElementDatabase elementDatabase;
    public new MoleculeRenderer renderer;

    [Header("Settings")]
    [Tooltip("Maximale Anzahl an Recent Molecules")]
    public int maxRecentMolecules = 6;

    // Events f�r WebSocket und UI
    public event System.Action<MoleculeData> OnMoleculeLoaded;
    public event System.Action<List<MoleculeData>> OnRecentListChanged;
    public event System.Action<string> OnLoadError;

    // Current State
    private MoleculeData currentMolecule;
    private List<MoleculeData> recentMolecules = new List<MoleculeData>();

    // Cache (verhindert doppeltes Laden)
    private Dictionary<string, MoleculeData> moleculeCache = new Dictionary<string, MoleculeData>();

    // Components
    private PubChemAPI pubchemAPI;
    
    // Loading state
    private bool isLoading = false;
    private UnityEngine.Coroutine activeAlignmentCoroutine = null;

    // Public properties for debug display
    public bool IsLoading => isLoading;
    public int CachedMoleculeCount => moleculeCache.Count;

    private void Awake()
    {
        pubchemAPI = gameObject.AddComponent<PubChemAPI>();
    }

    /// <summary>
    /// L�dt und zeigt ein Molek�l
    /// </summary>
    public async void LoadAndDisplayMolecule(string moleculeName)
    {
        if (string.IsNullOrWhiteSpace(moleculeName))
        {
            Debug.LogWarning("[MoleculeLibrary] Empty molecule name");
            return;
        }
        
        // Stop any active alignment coroutine from previous load
        if (activeAlignmentCoroutine != null)
        {
            StopCoroutine(activeAlignmentCoroutine);
            activeAlignmentCoroutine = null;
            Debug.Log("[MoleculeLibrary] Stopped previous alignment coroutine");
        }
        
        // Prevent concurrent loads
        if (isLoading)
        {
            Debug.LogWarning("[MoleculeLibrary] Already loading a molecule, please wait");
            return;
        }

        moleculeName = moleculeName.Trim().ToLower();
        isLoading = true;

        Debug.Log($"[MoleculeLibrary] Loading molecule: {moleculeName}");

        // Check cache first
        if (moleculeCache.ContainsKey(moleculeName))
        {
            Debug.Log($"[MoleculeLibrary] Using cached molecule: {moleculeName}");
            DisplayMolecule(moleculeCache[moleculeName]);
            return;
        }

        // Download from PubChem
        try
        {
            string sdf = await pubchemAPI.GetMoleculeSDF(moleculeName);

            if (string.IsNullOrEmpty(sdf))
            {
                Debug.LogError($"[MoleculeLibrary] Failed to download: {moleculeName}");
                OnLoadError?.Invoke($"Could not find molecule '{moleculeName}'");
                return;
            }

            // Parse SDF
            MoleculeData molecule = SDFParser.Parse(sdf, moleculeName);

            if (molecule == null)
            {
                Debug.LogError($"[MoleculeLibrary] Failed to parse: {moleculeName}");
                OnLoadError?.Invoke($"Failed to parse molecule data");
                return;
            }

            // Cache it
            moleculeCache[moleculeName] = molecule;

            // Display it
            DisplayMolecule(molecule);

            Debug.Log($"[MoleculeLibrary] Successfully loaded: {moleculeName} ({molecule.atoms.Count} atoms)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MoleculeLibrary] Error loading molecule: {e.Message}");
            OnLoadError?.Invoke($"Network error: {e.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    /// <summary>
    /// Zeigt ein Molek�l an und f�gt es zu Recent hinzu
    /// </summary>
    private void DisplayMolecule(MoleculeData molecule)
    {
        if (renderer == null)
        {
            Debug.LogError("[MoleculeLibrary] Renderer not assigned!");
            return;
        }

        currentMolecule = molecule;

        // Position molecule in front of camera BEFORE rendering
        PositionMoleculeInFrontOfCamera();

        // Get and assign plane alignment component to renderer
        var planeAlignment = renderer.GetComponent<MoleculePlaneAlignment>();
        if (planeAlignment != null)
        {
            renderer.planeAlignment = planeAlignment;
            Debug.Log("[MoleculeLibrary] Assigned planeAlignment to renderer");
        }
        else
        {
            Debug.LogWarning("[MoleculeLibrary] No MoleculePlaneAlignment component found on renderer!");
        }

        // Render molecule
        renderer.RenderMolecule(molecule);

        // Initialize plane alignment AFTER rendering with a frame delay
        // This ensures all transforms are properly updated
        if (planeAlignment != null)
        {
            // Use coroutine to delay by one frame - ensures rendering is complete
            activeAlignmentCoroutine = StartCoroutine(InitializePlaneAlignmentDelayed(planeAlignment, molecule));
        }

        // Add to recent (if not already there)
        AddToRecent(molecule);

        // Notify listeners
        OnMoleculeLoaded?.Invoke(molecule);
    }

    /// <summary>
    /// Positioniert das Molek�l vor der Kamera
    /// </summary>
    private void PositionMoleculeInFrontOfCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[MoleculeLibrary] No main camera found");
            return;
        }

        // Position: 0.6m in front of camera at eye level (further away for better view)
        Vector3 forward = mainCam.transform.forward;
        Vector3 targetPos = mainCam.transform.position + forward * 0.6f;

        renderer.transform.position = targetPos;

        // Optional: Molek�l zur Kamera drehen
        renderer.transform.rotation = Quaternion.LookRotation(forward);
        
        // Scale down larger molecules: -15% per 10 atoms, capped at 50%
        MoleculeRenderer molRenderer = renderer.GetComponent<MoleculeRenderer>();
        int atomCount = molRenderer?.CurrentMolecule?.atoms.Count ?? 0;
        if (atomCount > 10)
        {
            // Calculate reduction: 15% per 10 atoms
            float reductionPer10Atoms = 0.15f;
            float totalReduction = ((atomCount - 10) / 10f) * reductionPer10Atoms;
            
            // Cap at 50% reduction (minimum scale = 0.5)
            totalReduction = Mathf.Min(totalReduction, 0.5f);
            
            float scaleFactor = 1.0f - totalReduction;
            renderer.transform.localScale = Vector3.one * scaleFactor;
            
            Debug.Log($"[MoleculeLibrary] Scaled molecule with {atomCount} atoms to {scaleFactor:F2}x");
        }
        else
        {
            renderer.transform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// F�gt Molek�l zur Recent-Liste hinzu
    /// </summary>
    private void AddToRecent(MoleculeData molecule)
    {
        // Remove if already in list
        recentMolecules.RemoveAll(m => m.name.ToLower() == molecule.name.ToLower());

        // Add to front
        recentMolecules.Insert(0, molecule);

        // Limit size
        if (recentMolecules.Count > maxRecentMolecules)
        {
            recentMolecules.RemoveAt(recentMolecules.Count - 1);
        }

        // Notify listeners
        OnRecentListChanged?.Invoke(recentMolecules);
    }

    /// <summary>
    /// Gibt Recent Molecules zur�ck
    /// </summary>
    public List<MoleculeData> GetRecentMolecules()
    {
        return new List<MoleculeData>(recentMolecules);
    }

    /// <summary>
    /// L�scht aktuelles Molek�l
    /// </summary>
    public void ClearCurrentMolecule()
    {
        if (renderer != null)
        {
            renderer.ClearMolecule();
        }
        currentMolecule = null;
    }

    /// <summary>
    /// Gibt aktuelles Molek�l zur�ck
    /// </summary>
    public MoleculeData GetCurrentMolecule()
    {
        return currentMolecule;
    }

    /// <summary>
    /// Initialisiert Library mit Standard-Molekülen
    /// </summary>
    public void InitializeWithDefaults()
    {
        // Lade ein Standard-Molekül beim Start
        LoadAndDisplayMolecule("ethanol");
    }

    /// <summary>
    /// Initialisiert Plane Alignment mit einem Frame Delay
    /// Dann RE-RENDER das Molekül um Stereo-Bonds zu aktualisieren
    /// </summary>
    private System.Collections.IEnumerator InitializePlaneAlignmentDelayed(MoleculePlaneAlignment planeAlignment, MoleculeData molecule)
    {
        // Wait one frame for rendering to complete
        yield return null;
        
        // Initialize plane alignment
        planeAlignment.InitializeForMolecule(molecule);
        Debug.Log("[MoleculeLibrary] Plane alignment initialized (delayed)");
        
        // RE-RENDER molecule with stereo display now that plane is initialized
        if (renderer.enableStereoDisplay)
        {
            Debug.Log("[MoleculeLibrary] Re-rendering molecule with stereo display");
            
            // Wait another frame to ensure old GameObjects are destroyed
            yield return null;
            
            renderer.RenderMolecule(molecule);
            Debug.Log("[MoleculeLibrary] Stereo re-rendering complete");
        }
    }
}