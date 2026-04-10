using UnityEngine;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Controls the tutorial: single video with timed pauses between units.
/// Objects animate in/out at specified timestamps. iPad "Weiter" resumes playback.
/// 
/// PUBLIC API (used by WebSocketServer):
///   - StartTutorial()
///   - CloseTutorial()
///   - ContinueToNextStep()
///   - GoToPreviousStep()
/// </summary>
public class TutorialManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    // SINGLETON
    // ════════════════════════════════════════════════════════════
    public static TutorialManager Instance { get; private set; }

    // ════════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ════════════════════════════════════════════════════════════

    [Header("Timeline")]
    [Tooltip("The tutorial timeline asset (create via Tutorial > Build Tutorial System)")]
    public TutorialTimeline timeline;

    [Header("Video")]
    [Tooltip("VideoPlayer component (auto-finds on this GameObject if null)")]
    public VideoPlayer videoPlayer;

    [Tooltip("RenderTexture the video renders into")]
    public RenderTexture videoRenderTexture;

    [Tooltip("Quad that displays the video (with chroma key material)")]
    public GameObject videoDisplayPanel;

    [Tooltip("ChromaKey material applied to the video panel")]
    public Material chromaKeyMaterial;

    [Header("Layout")]
    [Tooltip("Main camera / headset (auto-finds if null)")]
    public Camera mainCamera;

    [Tooltip("Distance from camera to tutorial content (meters)")]
    public float spawnDistance = 0.45f;

    [Tooltip("Video panel height in meters (width auto-calculated for 16:9)")]
    public float videoScale = 0.35f;

    [Tooltip("Horizontal offset: video shifts RIGHT by this amount, objects go LEFT")]
    public float videoOffsetX = 0f;

    [Header("Object Pool")]
    [Tooltip("Parent transform whose children are reusable display objects")]
    public Transform objectPoolParent;

    [Header("State (Read Only)")]
    public bool isTutorialActive = false;
    public int currentUnitIndex = -1;
    public bool isWaitingForContinue = false;

    // ════════════════════════════════════════════════════════════
    // EVENTS (for UI / iPad)
    // ════════════════════════════════════════════════════════════
    public System.Action<bool> OnTutorialStateChanged;
    public System.Action<bool> OnContinueButtonStateChanged;

    // ════════════════════════════════════════════════════════════
    // INTERNAL STATE
    // ════════════════════════════════════════════════════════════
    private Dictionary<string, GameObject> objectPool = new Dictionary<string, GameObject>();
    private List<GameObject> activeObjects = new List<GameObject>();
    private Dictionary<GameObject, Vector3> basePositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Vector3> targetScales = new Dictionary<GameObject, Vector3>();
    private List<Coroutine> runningAnimations = new List<Coroutine>();
    private Coroutine transitionCoroutine;

    private Vector3 tutorialOrigin;
    private Quaternion tutorialRotation;
    private bool videoReady = false;
    private float floatTimer = 0f;

    private WebSocketServer cachedWebSocket;

    // VR buttons shown during pause
    private GameObject vrButtonContainer;
    private QuizButton vrWeiterButton;
    private QuizButton vrNochmalButton;

    // ════════════════════════════════════════════════════════════
    // ANIMATION CONSTANTS
    // ════════════════════════════════════════════════════════════
    private const float ANIM_IN_DURATION = 0.4f;
    private const float ANIM_OUT_DURATION = 0.25f;
    private const float ANIM_REPLACE_GAP = 0.05f;
    private const float FLOAT_AMPLITUDE = 0.006f;
    private const float FLOAT_FREQUENCY = 0.35f;
    private const float HIGHLIGHT_DURATION = 0.5f;

    // ════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        FindCamera();
        BuildObjectPool();
    }

    void Start()
    {
        SetupVideoPlayer();

        if (videoDisplayPanel != null)
            videoDisplayPanel.SetActive(false);

        cachedWebSocket = FindObjectOfType<WebSocketServer>();
    }

    void Update()
    {
        if (!isTutorialActive || !videoReady) return;

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            float t = (float)videoPlayer.time;

            // Pause at next unit boundary
            CheckForUnitPause(t);

            // Fire cues for current unit
            ProcessCues(t);
        }

        // Gentle hover animation on active objects
        ApplyFloatAnimation();
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }
    }

    // ════════════════════════════════════════════════════════════
    // PUBLIC API (called by WebSocketServer, iPad, VR buttons)
    // ════════════════════════════════════════════════════════════

    /// <summary>Start the tutorial from the beginning.</summary>
    public void StartTutorial()
    {
        if (timeline == null || timeline.units.Count == 0)
        {
            Debug.LogWarning("[Tutorial] No timeline assigned or no units defined!");
            return;
        }

        // Clear any loaded molecule
        var moleculeLibrary = FindObjectOfType<MoleculeLibrary>();
        if (moleculeLibrary != null)
            moleculeLibrary.ClearCurrentMolecule();

        FindCamera();
        CalculateAnchorPoint();

        currentUnitIndex = 0;
        isTutorialActive = true;
        isWaitingForContinue = false;

        // Reset all cue triggers
        foreach (var unit in timeline.units)
            foreach (var cue in unit.cues)
                cue.triggered = false;

        // Position and show video panel
        PositionVideoPanel();

        // Start video from the beginning
        PlayVideoFromStart();

        OnTutorialStateChanged?.Invoke(true);
        // Debug.Log($"[Tutorial] Started – {timeline.units.Count} units loaded");
    }

    /// <summary>Stop and close the tutorial.</summary>
    public void CloseTutorial()
    {
        // Debug.Log("[Tutorial] Closing");

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        if (videoPlayer != null)
            videoPlayer.Stop();

        HideAllObjectsInstant();
        StopAllRunningAnimations();

        if (videoDisplayPanel != null)
            videoDisplayPanel.SetActive(false);

        isTutorialActive = false;
        isWaitingForContinue = false;
        currentUnitIndex = -1;
        videoReady = false;

        HideVRPauseButtons();

        OnTutorialStateChanged?.Invoke(false);
        OnContinueButtonStateChanged?.Invoke(false);
    }

    /// <summary>Advance to the next unit (called when iPad "Weiter" is pressed or VR button).</summary>
    public void ContinueToNextStep()
    {
        if (!isTutorialActive || !isWaitingForContinue) return;

        // Debug.Log("[Tutorial] Continue pressed");
        isWaitingForContinue = false;
        OnContinueButtonStateChanged?.Invoke(false);
        HideVRPauseButtons();

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(TransitionToNextUnit());
    }

    /// <summary>Go back to the previous unit.</summary>
    public void GoToPreviousStep()
    {
        if (!isTutorialActive || currentUnitIndex <= 0) return;

        // Debug.Log("[Tutorial] Going back");
        isWaitingForContinue = false;
        OnContinueButtonStateChanged?.Invoke(false);
        HideVRPauseButtons();

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        StopAllRunningAnimations();
        HideAllObjectsInstant();

        currentUnitIndex--;
        ResetUnitCues(currentUnitIndex);

        // Also reset the unit we just left so it replays correctly if we go forward again
        if (currentUnitIndex + 1 < timeline.units.Count)
            ResetUnitCues(currentUnitIndex + 1);

        // Seek video and resume
        SeekVideoTo(timeline.units[currentUnitIndex].startTime);
        videoPlayer.Play();
    }

    // ════════════════════════════════════════════════════════════
    // VIDEO SETUP & CONTROL
    // ════════════════════════════════════════════════════════════

    private void SetupVideoPlayer()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
            if (videoPlayer == null)
            {
                Debug.LogError("[Tutorial] No VideoPlayer component found!");
                return;
            }
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.prepareCompleted += OnVideoPrepared;

        // Connect RenderTexture
        if (videoRenderTexture != null)
        {
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRenderTexture;
        }

        // Audio setup
        if (videoPlayer.audioOutputMode == VideoAudioOutputMode.None)
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

        if (videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource)
        {
            AudioSource audioSrc = videoPlayer.GetTargetAudioSource(0);
            if (audioSrc == null)
            {
                audioSrc = GetComponent<AudioSource>();
                if (audioSrc == null)
                    audioSrc = gameObject.AddComponent<AudioSource>();
                videoPlayer.SetTargetAudioSource(0, audioSrc);
            }
            audioSrc.playOnAwake = false;
            audioSrc.volume = 1f;
        }

        // Assign chroma key material to display panel
        if (videoDisplayPanel != null && chromaKeyMaterial != null)
        {
            Renderer r = videoDisplayPanel.GetComponent<Renderer>();
            if (r != null)
            {
                if (videoRenderTexture != null)
                    chromaKeyMaterial.mainTexture = videoRenderTexture;
                r.sharedMaterial = chromaKeyMaterial;
            }
        }

        // Debug.Log("[Tutorial] VideoPlayer configured");
    }

    private void PlayVideoFromStart()
    {
        if (videoPlayer == null || videoPlayer.clip == null)
        {
            Debug.LogError("[Tutorial] No video clip assigned to VideoPlayer!");
            return;
        }

        videoReady = false;
        videoPlayer.time = 0;
        videoPlayer.Prepare();
    }

    private void SeekVideoTo(float seconds)
    {
        if (videoPlayer == null) return;
        videoPlayer.time = seconds;
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        videoReady = true;
        vp.Play();
        // Debug.Log("[Tutorial] Video prepared – playing");
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        // Debug.Log("[Tutorial] Video ended");
        CloseTutorial();
    }

    // ════════════════════════════════════════════════════════════
    // UNIT PAUSE / RESUME
    // ════════════════════════════════════════════════════════════

    private void CheckForUnitPause(float videoTime)
    {
        if (isWaitingForContinue) return;

        int nextUnit = currentUnitIndex + 1;
        if (nextUnit >= timeline.units.Count) return; // Last unit plays to end

        float nextStart = timeline.units[nextUnit].startTime;

        if (videoTime >= nextStart)
        {
            videoPlayer.Pause();
            isWaitingForContinue = true;
            OnContinueButtonStateChanged?.Invoke(true);

            // Notify iPad
            string unitName = timeline.units[currentUnitIndex].name;
            NotifyIPad($"{{\"type\":\"tutorial\",\"status\":\"waitingContinue\",\"unit\":{currentUnitIndex},\"unitName\":\"{unitName}\"}}");

            // Show VR buttons so user can continue/replay from within the headset
            ShowVRPauseButtons();

            // Debug.Log($"[Tutorial] Paused – completed unit: {unitName}");
        }
    }

    private IEnumerator TransitionToNextUnit()
    {
        // 1. Animate out all visible objects
        yield return StartCoroutine(AnimateOutAll());

        // 2. Advance unit
        currentUnitIndex++;
        if (currentUnitIndex >= timeline.units.Count)
        {
            CloseTutorial();
            yield break;
        }

        // 3. Reset cues for the new unit
        ResetUnitCues(currentUnitIndex);

        // 4. Resume video (it's already at the right position from the pause)
        videoPlayer.Play();

        string unitName = timeline.units[currentUnitIndex].name;
        // Debug.Log($"[Tutorial] Now playing: {unitName}");
        transitionCoroutine = null;
    }

    private void ResetUnitCues(int index)
    {
        if (index < 0 || index >= timeline.units.Count) return;
        foreach (var cue in timeline.units[index].cues)
            cue.triggered = false;
    }

    // ════════════════════════════════════════════════════════════
    // CUE PROCESSING
    // ════════════════════════════════════════════════════════════

    private void ProcessCues(float videoTime)
    {
        if (currentUnitIndex < 0 || currentUnitIndex >= timeline.units.Count) return;

        var unit = timeline.units[currentUnitIndex];
        foreach (var cue in unit.cues)
        {
            if (!cue.triggered && videoTime >= cue.time)
            {
                cue.triggered = true;
                ExecuteCue(cue);
            }
        }
    }

    private void ExecuteCue(TutorialCue cue)
    {
        // Debug.Log($"[Tutorial] Cue @{cue.time:F1}s: {cue.action} '{cue.objectName}'");

        switch (cue.action)
        {
            case CueAction.Show:
                ShowObject(cue);
                break;

            case CueAction.Hide:
                HideObject(cue.objectName);
                break;

            case CueAction.Replace:
                ReplaceWithObject(cue);
                break;

            case CueAction.HideAll:
                StartCoroutine(AnimateOutAll());
                break;

            case CueAction.Highlight:
                HighlightObject(cue.objectName);
                break;
        }
    }

    // ════════════════════════════════════════════════════════════
    // SHOW / HIDE / REPLACE
    // ════════════════════════════════════════════════════════════

    private void ShowObject(TutorialCue cue)
    {
        GameObject obj = GetPoolObject(cue.objectName);
        if (obj == null)
        {
            Debug.LogWarning($"[Tutorial] Object not in pool: '{cue.objectName}'");
            return;
        }

        // Position relative to tutorial anchor
        Vector3 worldPos = tutorialOrigin + tutorialRotation * cue.position;
        obj.transform.position = worldPos;
        obj.transform.rotation = tutorialRotation * Quaternion.Euler(cue.rotation);
        obj.transform.localScale = Vector3.zero; // Start invisible

        // Face the camera
        if (mainCamera != null)
        {
            Vector3 toCam = mainCamera.transform.position - worldPos;
            toCam.y = 0; // Keep upright
            if (toCam.sqrMagnitude > 0.001f)
                obj.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
        }

        obj.SetActive(true);

        if (!activeObjects.Contains(obj))
            activeObjects.Add(obj);

        basePositions[obj] = worldPos;
        targetScales[obj] = cue.scale;

        // Animate in
        var anim = StartCoroutine(AnimateScaleIn(obj, cue.scale));
        runningAnimations.Add(anim);
    }

    private void HideObject(string objectName)
    {
        GameObject obj = GetPoolObject(objectName);
        if (obj == null || !obj.activeSelf) return;

        var anim = StartCoroutine(AnimateScaleOut(obj));
        runningAnimations.Add(anim);
    }

    private void ReplaceWithObject(TutorialCue cue)
    {
        // Snap-hide all active objects (instant, no animation – snappy carousel feel)
        StopAllRunningAnimations();
        foreach (var obj in activeObjects)
        {
            if (obj != null)
            {
                obj.transform.localScale = Vector3.zero;
                obj.SetActive(false);
            }
        }
        activeObjects.Clear();
        basePositions.Clear();
        targetScales.Clear();

        // Brief pause, then animate in the new object
        var anim = StartCoroutine(DelayedShow(cue, ANIM_REPLACE_GAP));
        runningAnimations.Add(anim);
    }

    private IEnumerator DelayedShow(TutorialCue cue, float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowObject(cue);
    }

    private void HighlightObject(string objectName)
    {
        GameObject obj = GetPoolObject(objectName);
        if (obj == null || !obj.activeSelf) return;

        var anim = StartCoroutine(AnimatePulse(obj));
        runningAnimations.Add(anim);
    }

    private void HideAllObjectsInstant()
    {
        foreach (var obj in activeObjects)
        {
            if (obj != null)
            {
                obj.transform.localScale = Vector3.zero;
                obj.SetActive(false);
            }
        }
        activeObjects.Clear();
        basePositions.Clear();
        targetScales.Clear();
    }

    private IEnumerator AnimateOutAll()
    {
        if (activeObjects.Count == 0) yield break;

        StopAllRunningAnimations();

        // Launch all shrink animations in parallel
        var snapshot = new List<GameObject>(activeObjects);
        foreach (var obj in snapshot)
        {
            if (obj != null && obj.activeSelf)
            {
                var c = StartCoroutine(AnimateScaleOut(obj));
                runningAnimations.Add(c);
            }
        }

        // Wait for them to finish
        yield return new WaitForSeconds(ANIM_OUT_DURATION + 0.05f);

        // Ensure cleanup
        foreach (var obj in snapshot)
        {
            if (obj != null)
            {
                obj.transform.localScale = Vector3.zero;
                obj.SetActive(false);
            }
        }
        activeObjects.Clear();
        basePositions.Clear();
        targetScales.Clear();
    }

    // ════════════════════════════════════════════════════════════
    // ANIMATION COROUTINES
    // ════════════════════════════════════════════════════════════

    /// <summary>Scale from 0 to target with elastic overshoot.</summary>
    private IEnumerator AnimateScaleIn(GameObject obj, Vector3 targetScale)
    {
        float elapsed = 0f;
        while (elapsed < ANIM_IN_DURATION)
        {
            if (obj == null || !obj.activeSelf) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / ANIM_IN_DURATION);
            float ease = EaseOutBack(t);
            obj.transform.localScale = targetScale * ease;
            yield return null;
        }
        if (obj != null)
            obj.transform.localScale = targetScale;
    }

    /// <summary>Anticipation (slight grow) then rapid shrink to zero.</summary>
    private IEnumerator AnimateScaleOut(GameObject obj)
    {
        Vector3 startScale = obj.transform.localScale;
        if (startScale.sqrMagnitude < 0.0001f)
        {
            // Already invisible
            obj.SetActive(false);
            activeObjects.Remove(obj);
            basePositions.Remove(obj);
            targetScales.Remove(obj);
            yield break;
        }

        // Phase 1: anticipation – slight scale up (20% of duration)
        float phase1 = ANIM_OUT_DURATION * 0.2f;
        Vector3 peakScale = startScale * 1.06f;
        float elapsed = 0f;
        while (elapsed < phase1)
        {
            if (obj == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / phase1);
            obj.transform.localScale = Vector3.Lerp(startScale, peakScale, t);
            yield return null;
        }

        // Phase 2: rapid shrink (80% of duration)
        float phase2 = ANIM_OUT_DURATION * 0.8f;
        elapsed = 0f;
        while (elapsed < phase2)
        {
            if (obj == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / phase2);
            float ease = t * t; // ease-in quadratic
            obj.transform.localScale = Vector3.Lerp(peakScale, Vector3.zero, ease);
            yield return null;
        }

        if (obj != null)
        {
            obj.transform.localScale = Vector3.zero;
            obj.SetActive(false);
        }
        activeObjects.Remove(obj);
        basePositions.Remove(obj);
        targetScales.Remove(obj);
    }

    /// <summary>Pulse: scale up 25% then back down.</summary>
    private IEnumerator AnimatePulse(GameObject obj)
    {
        if (!targetScales.ContainsKey(obj)) yield break;

        Vector3 baseScale = targetScales[obj];
        Vector3 peakScale = baseScale * 1.25f;
        float half = HIGHLIGHT_DURATION * 0.5f;

        // Scale up
        float elapsed = 0f;
        while (elapsed < half)
        {
            if (obj == null || !obj.activeSelf) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            obj.transform.localScale = Vector3.Lerp(baseScale, peakScale, EaseOutQuad(t));
            yield return null;
        }

        // Scale down
        elapsed = 0f;
        while (elapsed < half)
        {
            if (obj == null || !obj.activeSelf) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            obj.transform.localScale = Vector3.Lerp(peakScale, baseScale, EaseInQuad(t));
            yield return null;
        }

        if (obj != null)
            obj.transform.localScale = baseScale;
    }

    // ════════════════════════════════════════════════════════════
    // FLOAT (HOVER) ANIMATION
    // ════════════════════════════════════════════════════════════

    private void ApplyFloatAnimation()
    {
        if (activeObjects.Count == 0) return;

        floatTimer += Time.deltaTime;
        float yOffset = Mathf.Sin(floatTimer * Mathf.PI * 2f * FLOAT_FREQUENCY) * FLOAT_AMPLITUDE;

        foreach (var obj in activeObjects)
        {
            if (obj != null && obj.activeSelf && basePositions.ContainsKey(obj))
            {
                obj.transform.position = basePositions[obj] + new Vector3(0, yOffset, 0);
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    // EASING FUNCTIONS
    // ════════════════════════════════════════════════════════════

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseInQuad(float t) => t * t;

    // ════════════════════════════════════════════════════════════
    // OBJECT POOL
    // ════════════════════════════════════════════════════════════

    private void BuildObjectPool()
    {
        objectPool.Clear();

        // 1. Add all children of the pool parent
        if (objectPoolParent != null)
        {
            foreach (Transform child in objectPoolParent)
            {
                objectPool[child.name] = child.gameObject;
                child.gameObject.SetActive(false);
            }
        }

        // 2. Load any referenced prefabs from Resources that aren't already in the pool
        LoadMissingFromResources();

        // 3. Create special built-in objects
        EnsureBuiltInObjects();

        // Debug.Log($"[Tutorial] Object pool ready: {objectPool.Count} objects");
    }

    private void LoadMissingFromResources()
    {
        if (timeline == null) return;

        HashSet<string> needed = new HashSet<string>();
        foreach (var unit in timeline.units)
            foreach (var cue in unit.cues)
                if (!string.IsNullOrEmpty(cue.objectName))
                    needed.Add(cue.objectName);

        foreach (string name in needed)
        {
            if (objectPool.ContainsKey(name)) continue;

            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{name}");
            if (prefab != null)
            {
                Transform parent = objectPoolParent != null ? objectPoolParent : transform;
                GameObject instance = Instantiate(prefab, parent);
                instance.name = name;
                instance.SetActive(false);
                objectPool[name] = instance;
                // Debug.Log($"[Tutorial] Pool: loaded '{name}' from Resources");
            }
        }
    }

    private void EnsureBuiltInObjects()
    {
        if (!objectPool.ContainsKey("Arrow3D"))
            objectPool["Arrow3D"] = CreateArrowPrefab();

        if (!objectPool.ContainsKey("KeilstrichBild"))
            objectPool["KeilstrichBild"] = CreateKeilstrichBildPrefab();
    }

    private GameObject GetPoolObject(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (objectPool.TryGetValue(name, out GameObject obj))
            return obj;

        // Last resort: try Resources
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/{name}");
        if (prefab != null)
        {
            Transform parent = objectPoolParent != null ? objectPoolParent : transform;
            GameObject instance = Instantiate(prefab, parent);
            instance.name = name;
            instance.SetActive(false);
            objectPool[name] = instance;
            // Debug.Log($"[Tutorial] Pool: late-loaded '{name}'");
            return instance;
        }

        Debug.LogError($"[Tutorial] Object not found anywhere: '{name}'");
        return null;
    }

    // ════════════════════════════════════════════════════════════
    // BUILT-IN PREFAB CREATION
    // ════════════════════════════════════════════════════════════

    private static Shader FindBestShader()
    {
        Shader s = Shader.Find("Custom/MoleculeUnlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        if (s == null) s = Shader.Find("Mobile/Diffuse");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    private GameObject CreateArrowPrefab()
    {
        Transform parent = objectPoolParent != null ? objectPoolParent : transform;
        GameObject arrow = new GameObject("Arrow3D");
        arrow.transform.SetParent(parent);

        Material mat = new Material(FindBestShader());
        mat.color = new Color(0.9f, 0.9f, 0.9f);

        // Shaft (elongated cube)
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        shaft.transform.SetParent(arrow.transform);
        shaft.transform.localPosition = new Vector3(-0.1f, 0, 0);
        shaft.transform.localScale = new Vector3(0.5f, 0.06f, 0.06f);
        Object.Destroy(shaft.GetComponent<Collider>());
        shaft.GetComponent<Renderer>().sharedMaterial = mat;

        // Head (rotated cube → diamond shape)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.transform.SetParent(arrow.transform);
        head.transform.localPosition = new Vector3(0.2f, 0, 0);
        head.transform.localScale = new Vector3(0.18f, 0.18f, 0.06f);
        head.transform.localRotation = Quaternion.Euler(0, 0, 45);
        Object.Destroy(head.GetComponent<Collider>());
        head.GetComponent<Renderer>().sharedMaterial = mat;

        arrow.SetActive(false);
        return arrow;
    }

    private GameObject CreatePaperSheetPrefab()
    {
        Transform parent = objectPoolParent != null ? objectPoolParent : transform;
        GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
        paper.name = "PaperSheet";
        paper.transform.SetParent(parent);

        Object.Destroy(paper.GetComponent<Collider>());

        Material mat = new Material(FindBestShader());
        mat.color = new Color(0.97f, 0.97f, 0.97f);
        paper.GetComponent<Renderer>().sharedMaterial = mat;

        paper.SetActive(false);
        return paper;
    }

    private GameObject CreateKeilstrichBildPrefab()
    {
        Transform parent = objectPoolParent != null ? objectPoolParent : transform;
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "KeilstrichBild";
        quad.transform.SetParent(parent);

        Object.Destroy(quad.GetComponent<Collider>());

        // Load the Keilstrich image from Assets/Tutorial/
        Texture2D tex = Resources.Load<Texture2D>("KeilstrichBild");
        if (tex == null)
        {
            // Fallback: try loading from any common location
            tex = Resources.Load<Texture2D>("Methan_Keilstrich.svg_");
        }

        Shader unlitTransparent = Shader.Find("Unlit/Transparent");
        if (unlitTransparent == null) unlitTransparent = Shader.Find("UI/Default");
        if (unlitTransparent == null) unlitTransparent = FindBestShader();

        Material mat = new Material(unlitTransparent);
        if (tex != null)
        {
            mat.mainTexture = tex;
            // Adjust quad aspect ratio to match image
            float aspect = (float)tex.width / tex.height;
            quad.transform.localScale = new Vector3(aspect, 1f, 1f);
            // Debug.Log($"[Tutorial] KeilstrichBild loaded: {tex.width}x{tex.height}");
        }
        else
        {
            mat.color = Color.white;
            Debug.LogWarning("[Tutorial] Keilstrich image not found! Place it in Resources/ as KeilstrichBild.png");
        }
        quad.GetComponent<Renderer>().sharedMaterial = mat;

        quad.SetActive(false);
        return quad;
    }

    // ════════════════════════════════════════════════════════════
    // POSITIONING
    // ════════════════════════════════════════════════════════════

    private void FindCamera()
    {
        if (mainCamera != null) return;

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null)
                mainCamera = centerEye.GetComponent<Camera>();
        }
    }

    private void CalculateAnchorPoint()
    {
        if (mainCamera != null)
        {
            Vector3 fwd = mainCamera.transform.forward;
            tutorialOrigin = mainCamera.transform.position + fwd * spawnDistance;
            tutorialRotation = Quaternion.LookRotation(fwd);
        }
        else
        {
            tutorialOrigin = new Vector3(0, 1.5f, spawnDistance);
            tutorialRotation = Quaternion.identity;
        }
    }

    private void PositionVideoPanel()
    {
        if (videoDisplayPanel == null) return;

        // Shift video to the RIGHT so 3D objects appear to the LEFT
        Vector3 rightShift = tutorialRotation * new Vector3(videoOffsetX, 0f, 0f);
        videoDisplayPanel.transform.position = tutorialOrigin + rightShift;

        // Face camera
        if (mainCamera != null)
        {
            videoDisplayPanel.transform.LookAt(mainCamera.transform);
            videoDisplayPanel.transform.Rotate(0, 180, 0);
        }
        else
        {
            videoDisplayPanel.transform.rotation = tutorialRotation * Quaternion.Euler(0, 180, 0);
        }

        // 16:9 aspect ratio
        float w = videoScale * (16f / 9f);
        float h = videoScale;
        videoDisplayPanel.transform.localScale = new Vector3(w, h, 1f);
        videoDisplayPanel.SetActive(true);
    }

    // ════════════════════════════════════════════════════════════
    // VR PAUSE BUTTONS (Weiter / Nochmal)
    // ════════════════════════════════════════════════════════════

    private void ShowVRPauseButtons()
    {
        if (vrButtonContainer == null)
            CreateVRPauseButtons();

        if (vrButtonContainer == null) return;

        // Position below the video panel
        if (videoDisplayPanel != null)
        {
            Vector3 videoPos = videoDisplayPanel.transform.position;
            Vector3 down = -videoDisplayPanel.transform.up;
            Vector3 buttonPos = videoPos + down * (videoScale * 0.6f);
            vrButtonContainer.transform.position = buttonPos;
            vrButtonContainer.transform.rotation = videoDisplayPanel.transform.rotation;
        }

        vrButtonContainer.SetActive(true);

        // Reset button states
        if (vrWeiterButton != null) vrWeiterButton.ResetButton();
        if (vrNochmalButton != null) vrNochmalButton.ResetButton();
    }

    private void HideVRPauseButtons()
    {
        if (vrButtonContainer != null)
            vrButtonContainer.SetActive(false);
    }

    private void CreateVRPauseButtons()
    {
        vrButtonContainer = new GameObject("TutorialVRButtons");

        float btnWidth = 0.12f;
        float btnHeight = 0.04f;
        float btnDepth = 0.015f;
        float spacing = 0.14f;

        // ── "Weiter" Button (right, green) ──
        GameObject weiterObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        weiterObj.name = "WeiterButton";
        weiterObj.transform.SetParent(vrButtonContainer.transform, false);
        weiterObj.transform.localPosition = new Vector3(spacing / 2f, 0, 0);
        weiterObj.transform.localScale = new Vector3(btnWidth, btnHeight, btnDepth);

        var weiterBtn = weiterObj.AddComponent<QuizButton>();
        weiterBtn.answerIndex = 0;
        weiterBtn.normalColor = new Color(0.1f, 0.45f, 0.2f, 1f);
        weiterBtn.hoverColor = new Color(0.15f, 0.65f, 0.3f, 1f);
        weiterBtn.cooldownTime = 0.8f;
        weiterBtn.OnPressed += (idx) => ContinueToNextStep();

        // Label
        var weiterLabel = new GameObject("Label");
        weiterLabel.transform.SetParent(weiterObj.transform, false);
        weiterLabel.transform.localPosition = new Vector3(0, 0, -0.6f);
        weiterLabel.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var weiterTM = weiterLabel.AddComponent<TextMesh>();
        weiterTM.text = "▶ Weiter";
        weiterTM.fontSize = 32;
        weiterTM.characterSize = 0.025f;
        weiterTM.anchor = TextAnchor.MiddleCenter;
        weiterTM.alignment = TextAlignment.Center;
        weiterTM.color = Color.white;

        vrWeiterButton = weiterBtn;

        // ── "Nochmal" Button (left, blue) ──
        GameObject nochmalObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nochmalObj.name = "NochmalButton";
        nochmalObj.transform.SetParent(vrButtonContainer.transform, false);
        nochmalObj.transform.localPosition = new Vector3(-spacing / 2f, 0, 0);
        nochmalObj.transform.localScale = new Vector3(btnWidth, btnHeight, btnDepth);

        var nochmalBtn = nochmalObj.AddComponent<QuizButton>();
        nochmalBtn.answerIndex = 1;
        nochmalBtn.normalColor = new Color(0.15f, 0.25f, 0.5f, 1f);
        nochmalBtn.hoverColor = new Color(0.2f, 0.35f, 0.7f, 1f);
        nochmalBtn.cooldownTime = 0.8f;
        nochmalBtn.OnPressed += (idx) => GoToPreviousStep();

        // Label
        var nochmalLabel = new GameObject("Label");
        nochmalLabel.transform.SetParent(nochmalObj.transform, false);
        nochmalLabel.transform.localPosition = new Vector3(0, 0, -0.6f);
        nochmalLabel.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var nochmalTM = nochmalLabel.AddComponent<TextMesh>();
        nochmalTM.text = "↺ Nochmal";
        nochmalTM.fontSize = 32;
        nochmalTM.characterSize = 0.025f;
        nochmalTM.anchor = TextAnchor.MiddleCenter;
        nochmalTM.alignment = TextAlignment.Center;
        nochmalTM.color = Color.white;

        vrNochmalButton = nochmalBtn;

        vrButtonContainer.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════

    private void StopAllRunningAnimations()
    {
        foreach (var c in runningAnimations)
            if (c != null) StopCoroutine(c);
        runningAnimations.Clear();
    }

    private void NotifyIPad(string jsonMessage)
    {
        if (cachedWebSocket == null)
            cachedWebSocket = FindObjectOfType<WebSocketServer>();

        if (cachedWebSocket != null)
            cachedWebSocket.BroadcastMessage(jsonMessage);
    }
}
