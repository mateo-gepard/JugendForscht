using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main controller for the tutorial system
/// Manages tutorial flow, video playback, and event execution
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }
    
    [Header("Tutorial Steps")]
    [Tooltip("List of tutorial steps in order")]
    public List<TutorialStep> tutorialSteps = new List<TutorialStep>();
    
    [Header("Video Display")]
    [Tooltip("Video player component")]
    public VideoPlayer videoPlayer;
    
    [Tooltip("Render texture for video with alpha")]
    public RenderTexture videoRenderTexture;
    
    [Tooltip("Quad/Panel to display video")]
    public GameObject videoDisplayPanel;
    
    [Tooltip("Material for transparent video")]
    public Material transparentVideoMaterial;
    
    [Header("Camera Reference")]
    [Tooltip("Main camera/headset for positioning (auto-finds if null)")]
    public Camera mainCamera;
    
    [Tooltip("Distance from camera for tutorial content")]
    public float spawnDistance = 0.35f;
    
    [Header("Text Display")]
    [Tooltip("TextMeshPro for displaying text")]
    public TMPro.TextMeshProUGUI tutorialText;
    
    [Tooltip("Parent object for text panel")]
    public GameObject textPanel;
    
    [Header("Object Pool")]
    [Tooltip("Parent transform containing reusable display objects")]
    public Transform objectPoolParent;
    
    [Tooltip("Dictionary of pooled objects by name")]
    private Dictionary<string, GameObject> objectPool = new Dictionary<string, GameObject>();
    
    [Header("State")]
    public bool isTutorialActive = false;
    public int currentStepIndex = -1;
    public bool isWaitingForContinue = false;
    
    private TutorialStep currentStep;
    private float videoTime = 0f;
    private List<Coroutine> activeAnimations = new List<Coroutine>();
    private List<GameObject> currentActiveObjects = new List<GameObject>(); // Track currently visible objects
    
    // Tutorial anchor point (set when tutorial starts, based on camera)
    private Vector3 tutorialOrigin;
    private Quaternion tutorialRotation;
    
    // Events for UI updates
    public System.Action<bool> OnTutorialStateChanged;
    public System.Action<bool> OnContinueButtonStateChanged;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Find main camera
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // Try to find CenterEyeAnchor
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null)
                {
                    mainCamera = centerEye.GetComponent<Camera>();
                }
            }
            Debug.Log($"[Tutorial] Kamera gefunden: {(mainCamera != null ? mainCamera.name : "NULL")}");
        }
        
        // Initialize object pool
        InitializeObjectPool();
    }
    
    void Start()
    {
        // Setup video player
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            
            // Configure audio output
            if (videoPlayer.audioOutputMode == VideoAudioOutputMode.None)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                Debug.Log("[Tutorial] Video Audio Output Mode auf AudioSource gesetzt");
            }
            
            // Ensure AudioSource is assigned
            if (videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource)
            {
                AudioSource audioSource = videoPlayer.GetTargetAudioSource(0);
                if (audioSource == null)
                {
                    // Try to find or create AudioSource
                    audioSource = videoPlayer.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = videoPlayer.gameObject.AddComponent<AudioSource>();
                        Debug.Log("[Tutorial] AudioSource automatisch erstellt");
                    }
                    videoPlayer.SetTargetAudioSource(0, audioSource);
                    Debug.Log("[Tutorial] AudioSource zugewiesen");
                }
                audioSource.playOnAwake = false;
                audioSource.volume = 1f;
            }
            
            Debug.Log("[Tutorial] VideoPlayer gefunden und konfiguriert");
        }
        else
        {
            Debug.LogError("[Tutorial] VideoPlayer ist NULL! Im Inspector zuweisen oder VideoPlayer Komponente hinzufügen.");
        }
        
        // Hide tutorial elements initially
        if (videoDisplayPanel != null) videoDisplayPanel.SetActive(false);
        if (textPanel != null) textPanel.SetActive(false);
        
        Debug.Log($"[Tutorial] Start vollständig - Schritte: {tutorialSteps.Count}, VideoPlayer: {videoPlayer != null}, DisplayPanel: {videoDisplayPanel != null}");
    }
    
    void Update()
    {
        if (!isTutorialActive || currentStep == null) return;
        
        // Update video time and check for events
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoTime = (float)videoPlayer.time;
            ProcessEvents();
        }
    }
    
    /// <summary>
    /// Initialize the object pool from children
    /// </summary>
    private void InitializeObjectPool()
    {
        if (objectPoolParent == null) return;
        
        // First, add existing children to pool
        foreach (Transform child in objectPoolParent)
        {
            objectPool[child.name] = child.gameObject;
            child.gameObject.SetActive(false);
        }
        
        // Then load any missing prefabs from Resources
        Debug.Log("[Tutorial] Prüfe auf fehlende Prefabs...");
        LoadMissingPrefabs();
        
        // Log alle geladenen Prefabs
        Debug.Log($"[Tutorial] Objekt-Pool initialisiert mit {objectPool.Count} Objekten:");
        foreach (var key in objectPool.Keys)
        {
            Debug.Log($"[Tutorial]   - {key}");
        }
    }
    
    /// <summary>
    /// Load any missing prefabs from Resources folder
    /// </summary>
    private void LoadMissingPrefabs()
    {
        string[] prefabNames = {
            // Step 1 - Bindungsarten
            "NormalBond",
            "DashedBond", 
            "WedgeBond",
            "AmmoniaDashedHighlight",
            "AmmoniaWedgeHighlight",
            // Step 2 - Methan Keilstrich
            "Methan3D",
            "MethanKeilstrich",
            // Step 3 - Molekülgeometrie
            "CO2Linear",
            "H2OGewinkelt",
            "BF3TrigonalPlanar",
            "NH3TrigonalPyramidal",
            "CH4Tetraedrisch",
            // Step 4 - Linearer Bau Highlight
            "CO2LinearHighlight",
            // Step 5 - Gewinkelter Bau Highlights
            "H2OGewinkeltHighlight",
            "H2OGewinkeltEPHighlight",
            // Step 6 - Trigonal Planar Highlight
            "BF3TrigonalPlanarHighlight",
            // Step 7 - Trigonal Pyramidal Highlights
            "NH3TrigonalPyramidalHighlight",
            "NH3TrigonalPyramidalEPHighlight",
            // Step 8 - Tetraedrisch Highlight
            "CH4TetraedrischHighlight"
        };
        
        foreach (string prefabName in prefabNames)
        {
            // Skip if already in pool
            if (objectPool.ContainsKey(prefabName))
            {
                continue;
            }
            
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{prefabName}");
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, objectPoolParent);
                instance.name = prefabName; // Remove "(Clone)" suffix
                instance.SetActive(false);
                objectPool[prefabName] = instance; // Add to pool dictionary
                Debug.Log($"[Tutorial] Nachgeladen: {prefabName}");
            }
            else
            {
                Debug.LogWarning($"[Tutorial] Prefab nicht gefunden: Prefabs/{prefabName}");
            }
        }
    }
    
    /// <summary>
    /// Start the tutorial from the beginning
    /// </summary>
    public void StartTutorial()
    {
        if (tutorialSteps == null || tutorialSteps.Count == 0)
        {
            Debug.LogWarning("[Tutorial] Keine Tutorial-Schritte definiert!");
            return;
        }
        
        // Clear any currently loaded molecules
        var moleculeLibrary = FindObjectOfType<MoleculeLibrary>();
        if (moleculeLibrary != null)
        {
            moleculeLibrary.ClearCurrentMolecule();
            Debug.Log("[Tutorial] Aktuelles Molekül entfernt");
        }
        
        // Make sure camera is available
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null)
                {
                    mainCamera = centerEye.GetComponent<Camera>();
                }
            }
        }
        
        // Position tutorial closer in front of camera at eye level
        if (mainCamera != null)
        {
            Vector3 cameraForward = mainCamera.transform.forward;
            
            tutorialOrigin = mainCamera.transform.position + cameraForward * 0.35f; // 35cm in front at eye level (closer for better visibility)
            tutorialRotation = Quaternion.LookRotation(cameraForward);
            
            Debug.Log($"[Tutorial] Tutorial an Augenhöhe gespawnt: Ursprung={tutorialOrigin}, Rotation={tutorialRotation.eulerAngles}");
        }
        else
        {
            tutorialOrigin = new Vector3(0, 1.5f, 0.35f); // Default at eye level
            tutorialRotation = Quaternion.identity;
            Debug.LogWarning("[Tutorial] Keine Kamera gefunden, verwende Standard-Augenhöhe-Position");
        }
        
        Debug.Log("[Tutorial] Starte Tutorial");
        isTutorialActive = true;
        currentStepIndex = -1;
        
        OnTutorialStateChanged?.Invoke(true);
        
        // Start first step
        NextStep();
    }
    
    /// <summary>
    /// Close/exit the tutorial
    /// </summary>
    public void CloseTutorial()
    {
        Debug.Log("[Tutorial] Schließe Tutorial");
        
        // Stop video
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        
        // Hide all elements
        if (videoDisplayPanel != null) videoDisplayPanel.SetActive(false);
        if (textPanel != null) textPanel.SetActive(false);
        
        // Hide all pooled objects
        foreach (var obj in objectPool.Values)
        {
            obj.SetActive(false);
        }
        
        // Stop all animations
        foreach (var coroutine in activeAnimations)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        activeAnimations.Clear();
        
        isTutorialActive = false;
        isWaitingForContinue = false;
        currentStepIndex = -1;
        currentStep = null;
        
        OnTutorialStateChanged?.Invoke(false);
        OnContinueButtonStateChanged?.Invoke(false);
    }
    
    /// <summary>
    /// Continue to next step (called from iPad UI)
    /// </summary>
    public void ContinueToNextStep()
    {
        if (!isTutorialActive) return;
        
        Debug.Log("[Tutorial] Fortfahren gedrückt - gehe zu nächstem Schritt");
        
        // Stop current video if playing
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
        
        // Hide all objects from previous step
        foreach (var obj in currentActiveObjects)
        {
            if (obj != null) obj.SetActive(false);
        }
        currentActiveObjects.Clear();
        
        isWaitingForContinue = false;
        OnContinueButtonStateChanged?.Invoke(false);
        
        NextStep();
    }
    
    /// <summary>
    /// Go to previous step (called from iPad UI)
    /// </summary>
    public void GoToPreviousStep()
    {
        if (!isTutorialActive) return;
        
        // Kann nur zurück wenn wir nicht beim ersten Schritt sind
        if (currentStepIndex <= 0)
        {
            Debug.Log("[Tutorial] Bereits beim ersten Schritt - kann nicht zurück");
            return;
        }
        
        Debug.Log("[Tutorial] Zurück gedrückt - gehe zu vorherigem Schritt");
        
        // Stop current video
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
        
        // Hide all objects from current step
        foreach (var obj in currentActiveObjects)
        {
            if (obj != null) obj.SetActive(false);
        }
        currentActiveObjects.Clear();
        
        // Decrement step counter and go to previous step
        currentStepIndex -= 2; // -2 because NextStep() will increment by 1
        isWaitingForContinue = false;
        OnContinueButtonStateChanged?.Invoke(false);
        
        NextStep();
    }

    /// <summary>
    /// Advance to the next tutorial step
    /// </summary>
    private void NextStep()
    {
        currentStepIndex++;
        
        if (currentStepIndex >= tutorialSteps.Count)
        {
            Debug.Log("[Tutorial] Tutorial abgeschlossen!");
            CloseTutorial();
            return;
        }
        
        currentStep = tutorialSteps[currentStepIndex];
        
        if (currentStep == null)
        {
            Debug.LogError($"[Tutorial] tutorialSteps[{currentStepIndex}] ist NULL!");
            return;
        }
        
        if (currentStep.events == null)
        {
            Debug.LogWarning($"[Tutorial] Schritt '{currentStep.stepName}' hat null Events-Liste - initialisiere");
            currentStep.events = new List<TutorialEvent>();
        }
        
        currentStep.ResetEvents();
        
        Debug.Log($"[Tutorial] Starte Schritt {currentStepIndex + 1}/{tutorialSteps.Count}: {currentStep.stepName}");
        
        // Setup and play video
        PlayStepVideo();
    }
    
    /// <summary>
    /// Play the video for the current step
    /// </summary>
    private void PlayStepVideo()
    {
        Debug.Log($"[Tutorial] PlayStepVideo aufgerufen - VideoPlayer null: {videoPlayer == null}, VideoClip null: {currentStep?.videoClip == null}");
        
        if (videoPlayer == null || currentStep.videoClip == null)
        {
            Debug.LogWarning("[Tutorial] Kein VideoPlayer oder Clip für diesen Schritt - überspringe zu Fortfahren");
            // Skip to waiting for continue
            StartCoroutine(WaitForContinue(0f));
            return;
        }
        
        // Position video panel relative to tutorial origin and rotation
        if (videoDisplayPanel != null)
        {
            // Transform the video position from local to world space
            Vector3 worldPos = tutorialOrigin + tutorialRotation * currentStep.videoPosition;
            videoDisplayPanel.transform.position = worldPos;
            
            // Face directly at camera (flip to face user)
            if (mainCamera != null)
            {
                videoDisplayPanel.transform.LookAt(mainCamera.transform);
                videoDisplayPanel.transform.Rotate(0, 180, 0);
            }
            else
            {
                videoDisplayPanel.transform.rotation = tutorialRotation * Quaternion.Euler(0, 180, 0);
            }
            
            videoDisplayPanel.transform.localScale = Vector3.one * currentStep.videoScale;
            videoDisplayPanel.SetActive(true);
            Debug.Log($"[Tutorial] Video-Panel AKTIV an Weltposition {worldPos}, Skalierung {currentStep.videoScale}, aktiv={videoDisplayPanel.activeSelf}");
        }
        else
        {
            Debug.LogError("[Tutorial] videoDisplayPanel ist NULL!");
        }
        
        // Setup and play video
        videoPlayer.clip = currentStep.videoClip;
        videoPlayer.Prepare();
    }
    
    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (currentStep == null || currentStep.videoClip == null)
        {
            Debug.LogWarning("[Tutorial] OnVideoPrepared aufgerufen aber currentStep oder videoClip ist null");
            return;
        }
        
        videoTime = 0f;
        vp.Play();
        Debug.Log($"[Tutorial] Video vorbereitet und abspielend: {currentStep.videoClip.name}");
    }
    
    private void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("[Tutorial] Video beendet");
        
        if (currentStep == null)
        {
            Debug.LogWarning("[Tutorial] OnVideoEnd aufgerufen aber currentStep ist null");
            return;
        }
        
        if (currentStep.autoAdvance)
        {
            NextStep();
        }
        else
        {
            StartCoroutine(WaitForContinue(currentStep.continueDelay));
        }
    }
    
    private IEnumerator WaitForContinue(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        isWaitingForContinue = true;
        OnContinueButtonStateChanged?.Invoke(true);
        
        // Broadcast to iPad UI
        var webSocket = FindObjectOfType<WebSocketServer>();
        if (webSocket != null)
        {
            webSocket.BroadcastMessage("{\"type\":\"tutorial\",\"status\":\"waitingContinue\"}");
        }
        
        Debug.Log("[Tutorial] Warte auf Fortfahren-Button-Druck");
    }
    
    /// <summary>
    /// Process timed events for current step
    /// </summary>
    private void ProcessEvents()
    {
        if (currentStep == null) return;
        
        foreach (var evt in currentStep.events)
        {
            if (!evt.hasTriggered && videoTime >= evt.triggerTime)
            {
                ExecuteEvent(evt);
                evt.hasTriggered = true;
            }
        }
    }
    
    /// <summary>
    /// Execute a tutorial event
    /// </summary>
    private void ExecuteEvent(TutorialEvent evt)
    {
        Debug.Log($"[Tutorial] Führe Event aus: {evt.eventType} auf '{evt.targetObjectName}' bei {evt.triggerTime}s");
        
        GameObject target = null;
        if (!string.IsNullOrEmpty(evt.targetObjectName))
        {
            if (!objectPool.TryGetValue(evt.targetObjectName, out target))
            {
                Debug.LogError($"[Tutorial] FEHLER: Objekt '{evt.targetObjectName}' nicht im Pool! Verfügbare Objekte: {string.Join(", ", objectPool.Keys)}");
            }
        }
        
        switch (evt.eventType)
        {
            case TutorialEventType.Show:
                if (target != null)
                {
                    // Transform position from local to world space
                    Vector3 adjustedPos = evt.position;
                    
                    // Move objects closer to center (video position) by reducing X offset
                    // This brings right-positioned objects left, towards the video
                    adjustedPos.x *= 0.5f; // Reduce horizontal offset by half
                    
                    Vector3 worldPos = tutorialOrigin + tutorialRotation * adjustedPos;
                    
                    // Check for overlap with other active objects and adjust position if needed
                    worldPos = GetNonOverlappingPosition(worldPos, target);
                    
                    target.transform.position = worldPos;
                    target.transform.rotation = tutorialRotation * Quaternion.Euler(evt.rotation);
                    target.transform.localScale = evt.scale;
                    target.SetActive(true);
                    
                    // Track this object as currently active
                    if (!currentActiveObjects.Contains(target))
                    {
                        currentActiveObjects.Add(target);
                    }
                    
                    // After spawning, fix any overlapping text labels
                    StartCoroutine(FixOverlappingLabelsDelayed());
                    
                    Debug.Log($"[Tutorial] Zeige {evt.targetObjectName} an Weltpos {worldPos}, Skalierung {evt.scale}");
                }
                else
                {
                    Debug.LogWarning($"[Tutorial] Objekt nicht im Pool gefunden: {evt.targetObjectName}");
                }
                break;
                
            case TutorialEventType.Hide:
                if (target != null)
                {
                    target.SetActive(false);
                    currentActiveObjects.Remove(target); // Remove from active list
                }
                break;
                
            case TutorialEventType.Move:
                if (target != null)
                {
                    // Direct world position
                    activeAnimations.Add(StartCoroutine(AnimatePosition(target.transform, evt.position, evt.duration)));
                }
                break;
                
            case TutorialEventType.Rotate:
                if (target != null)
                {
                    activeAnimations.Add(StartCoroutine(AnimateRotation(target.transform, evt.rotation, evt.duration)));
                }
                break;
                
            case TutorialEventType.Scale:
                if (target != null)
                {
                    activeAnimations.Add(StartCoroutine(AnimateScale(target.transform, evt.scale, evt.duration)));
                }
                break;
                
            case TutorialEventType.ShowText:
                if (textPanel != null)
                {
                    textPanel.SetActive(true);
                    if (tutorialText != null) tutorialText.text = evt.textContent;
                }
                break;
                
            case TutorialEventType.HideText:
                if (textPanel != null)
                {
                    textPanel.SetActive(false);
                }
                break;
                
            case TutorialEventType.LoadMolecule:
                // Find MoleculeLibrary and load molecule
                var library = FindObjectOfType<MoleculeLibrary>();
                if (library != null)
                {
                    library.LoadAndDisplayMolecule(evt.moleculeName);
                }
                break;
        }
    }
    
    // Animation coroutines
    private IEnumerator AnimatePosition(Transform target, Vector3 endPos, float duration)
    {
        Vector3 startPos = target.position;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            target.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        target.position = endPos;
    }
    
    private IEnumerator AnimateRotation(Transform target, Vector3 endRotation, float duration)
    {
        Quaternion startRot = target.rotation;
        Quaternion endRot = Quaternion.Euler(endRotation);
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            target.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        
        target.rotation = endRot;
    }
    
    private IEnumerator AnimateScale(Transform target, Vector3 endScale, float duration)
    {
        Vector3 startScale = target.localScale;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            target.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }
        
        target.localScale = endScale;
    }
    
    /// <summary>
    /// Convert a local position (relative to tutorial origin) to world position
    /// X = right/left, Y = up/down, Z = forward/back from camera
    /// </summary>
    private Vector3 GetWorldPosition(Vector3 localPos)
    {
        // Transform local position by tutorial rotation and add to origin
        return tutorialOrigin + tutorialRotation * localPos;
    }
    
    /// <summary>
    /// Delayed coroutine to fix overlapping labels after objects are fully spawned
    /// </summary>
    private IEnumerator FixOverlappingLabelsDelayed()
    {
        // Wait one frame for all objects to be fully initialized
        yield return null;
        
        FixOverlappingLabels();
    }
    
    /// <summary>
    /// Find all active TextMeshPro labels in the scene and adjust their positions
    /// to prevent overlap. Called after spawning new tutorial objects.
    /// </summary>
    private void FixOverlappingLabels()
    {
        const float minDistance = 0.15f; // Minimum distance between labels (15cm)
        const float pushDistance = 0.08f; // How far to push overlapping labels (8cm)
        
        // Collect all visible text labels with their transforms
        List<TMPro.TextMeshPro> visibleLabels = new List<TMPro.TextMeshPro>();
        
        // Find all 3D text labels (these are the ones in prefabs like "H₂O - Gewinkelt")
        var allTextComponents = FindObjectsOfType<TMPro.TextMeshPro>();
        foreach (var tmp in allTextComponents)
        {
            if (tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
            {
                visibleLabels.Add(tmp);
            }
        }
        
        if (visibleLabels.Count < 2)
        {
            // No overlap possible with less than 2 labels
            return;
        }
        
        // Check each pair of labels for overlap
        bool foundOverlap = false;
        for (int i = 0; i < visibleLabels.Count; i++)
        {
            for (int j = i + 1; j < visibleLabels.Count; j++)
            {
                TMPro.TextMeshPro label1 = visibleLabels[i];
                TMPro.TextMeshPro label2 = visibleLabels[j];
                
                float distance = Vector3.Distance(label1.transform.position, label2.transform.position);
                
                if (distance < minDistance)
                {
                    // Overlap detected! Push them apart
                    foundOverlap = true;
                    
                    // Calculate push direction (away from each other)
                    Vector3 direction = (label2.transform.position - label1.transform.position).normalized;
                    
                    // If they're at exactly the same position, use a default direction
                    if (direction.magnitude < 0.01f)
                    {
                        direction = Vector3.up;
                    }
                    
                    // Push both labels apart
                    label1.transform.position -= direction * (pushDistance * 0.5f);
                    label2.transform.position += direction * (pushDistance * 0.5f);
                    
                    Debug.Log($"[Tutorial] Label-Überlappung behoben: '{label1.text}' und '{label2.text}' auseinander verschoben (Distanz war {distance:F3}m)");
                }
            }
        }
        
        if (foundOverlap)
        {
            Debug.Log($"[Tutorial] {visibleLabels.Count} Labels geprüft, Überlappungen wurden korrigiert");
        }
    }
    
    /// <summary>
    /// Check if the given position would overlap with any currently active objects.
    /// If overlap detected, adjust position to avoid collision.
    /// Checks ALL active objects in the scene, including prefab labels and text panels.
    /// </summary>
    private Vector3 GetNonOverlappingPosition(Vector3 desiredPos, GameObject newObject)
    {
        const float minDistance = 0.2f; // Minimum distance between objects (20cm)
        const float verticalOffset = 0.12f; // Vertical offset when repositioning (12cm)
        const float horizontalOffset = 0.15f; // Horizontal offset for side repositioning (15cm)
        const int maxAttempts = 12; // Maximum repositioning attempts
        
        Vector3 testPos = desiredPos;
        
        // Collect all potential overlapping objects in the scene
        List<Vector3> existingPositions = new List<Vector3>();
        
        // 1. Check tracked tutorial objects
        foreach (var activeObj in currentActiveObjects)
        {
            if (activeObj == null || activeObj == newObject || !activeObj.activeInHierarchy) continue;
            existingPositions.Add(activeObj.transform.position);
        }
        
        // 2. Check all active TextMeshPro components (labels, text panels)
        var allTextComponents = FindObjectsOfType<TMPro.TextMeshPro>(true);
        foreach (var tmp in allTextComponents)
        {
            if (tmp.gameObject == newObject || !tmp.gameObject.activeInHierarchy) continue;
            if (tmp.text.Length > 0) // Only consider visible text
            {
                existingPositions.Add(tmp.transform.position);
            }
        }
        
        // 3. Check all active TextMeshProUGUI components (UI text)
        var allUITextComponents = FindObjectsOfType<TMPro.TextMeshProUGUI>(true);
        foreach (var tmpUI in allUITextComponents)
        {
            if (tmpUI.gameObject == newObject || !tmpUI.gameObject.activeInHierarchy) continue;
            if (tmpUI.text.Length > 0)
            {
                existingPositions.Add(tmpUI.transform.position);
            }
        }
        
        // 4. Check all active Renderers in object pool or tutorial area
        foreach (var kvp in objectPool)
        {
            if (kvp.Value != null && kvp.Value.activeInHierarchy && kvp.Value != newObject)
            {
                existingPositions.Add(kvp.Value.transform.position);
            }
        }
        
        // Try different positions until we find one without overlap
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            bool hasOverlap = false;
            
            foreach (var existingPos in existingPositions)
            {
                float distance = Vector3.Distance(testPos, existingPos);
                
                if (distance < minDistance)
                {
                    hasOverlap = true;
                    break;
                }
            }
            
            if (!hasOverlap)
            {
                // Position is good - no overlap
                if (attempt > 0)
                {
                    Debug.Log($"[Tutorial] Position angepasst nach {attempt} Versuchen um Überlappung zu vermeiden: {testPos}");
                }
                return testPos;
            }
            
            // Try different positions in a more varied pattern
            // Cycle through: up, down, left, right, up-left, up-right, down-left, down-right
            switch (attempt % 8)
            {
                case 0: // Up
                    testPos = desiredPos + tutorialRotation * new Vector3(0, verticalOffset * (attempt / 8 + 1), 0);
                    break;
                case 1: // Down
                    testPos = desiredPos + tutorialRotation * new Vector3(0, -verticalOffset * (attempt / 8 + 1), 0);
                    break;
                case 2: // Left
                    testPos = desiredPos + tutorialRotation * new Vector3(-horizontalOffset * (attempt / 8 + 1), 0, 0);
                    break;
                case 3: // Right
                    testPos = desiredPos + tutorialRotation * new Vector3(horizontalOffset * (attempt / 8 + 1), 0, 0);
                    break;
                case 4: // Up-Left
                    testPos = desiredPos + tutorialRotation * new Vector3(-horizontalOffset * (attempt / 8 + 1), verticalOffset * (attempt / 8 + 1), 0);
                    break;
                case 5: // Up-Right
                    testPos = desiredPos + tutorialRotation * new Vector3(horizontalOffset * (attempt / 8 + 1), verticalOffset * (attempt / 8 + 1), 0);
                    break;
                case 6: // Down-Left
                    testPos = desiredPos + tutorialRotation * new Vector3(-horizontalOffset * (attempt / 8 + 1), -verticalOffset * (attempt / 8 + 1), 0);
                    break;
                case 7: // Down-Right
                    testPos = desiredPos + tutorialRotation * new Vector3(horizontalOffset * (attempt / 8 + 1), -verticalOffset * (attempt / 8 + 1), 0);
                    break;
            }
        }
        
        // If still overlapping after all attempts, use a significant offset
        Debug.LogWarning($"[Tutorial] Konnte nach {maxAttempts} Versuchen keine überlappungsfreie Position finden, verwende finalen Offset");
        return desiredPos + tutorialRotation * new Vector3(0.35f, 0.2f, 0);
    }
    
    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }
    }
}
