using UnityEngine;

/// <summary>
/// Zentraler Manager der VRPanelGrab automatisch zu allen aktiven Panels hinzufügt.
/// Bietet auch eine universelle Close-Funktion für alle aktiven Module.
/// 
/// Wird als Komponente auf dem MoleculeSystem-GameObject platziert.
/// </summary>
public class VRPanelManager : MonoBehaviour
{
    public static VRPanelManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Automatisch VRPanelGrab zu neuen Panels hinzufügen")]
    public bool autoAttachGrab = true;

    private float scanInterval = 1.0f;
    private float lastScan = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Update()
    {
        if (!autoAttachGrab) return;
        if (Time.time - lastScan < scanInterval) return;
        lastScan = Time.time;
        ScanAndAttach();
    }

    /// <summary>
    /// Scannt nach VR-Panels die noch keinen VRPanelGrab haben und fügt einen hinzu.
    /// </summary>
    void ScanAndAttach()
    {
        // Quiz Display
        var quizDisplay = FindObjectOfType<QuizDisplay>();
        if (quizDisplay != null)
            EnsureGrab(quizDisplay.gameObject, () =>
            {
                quizDisplay.Hide();
                var quiz = QuizManager.Instance;
                if (quiz != null) quiz.EndQuiz();
            });

        // Builder Manager
        var builder = BuilderManager.Instance;
        if (builder != null && builder.gameObject.activeInHierarchy)
            EnsureGrab(builder.gameObject, () => builder.StopBuilder());

        // Lorentz Lab
        if (LorentzLabManager.Instance != null)
            EnsureGrab(LorentzLabManager.Instance.gameObject,
                () => Destroy(LorentzLabManager.Instance.gameObject));

        // Conductor Swing
        if (ConductorSwingManager.Instance != null)
            EnsureGrab(ConductorSwingManager.Instance.gameObject,
                () => Destroy(ConductorSwingManager.Instance.gameObject));

        // Induction Loop
        if (InductionLoopManager.Instance != null)
            EnsureGrab(InductionLoopManager.Instance.gameObject,
                () => Destroy(InductionLoopManager.Instance.gameObject));

        // Riemann Surface
        var riemann = FindObjectOfType<RiemannSurfaceDisplay>();
        if (riemann != null && riemann.gameObject.activeInHierarchy)
            EnsureGrab(riemann.gameObject, () =>
            {
                riemann.ClearSurface();
                var mgr = FindObjectOfType<RiemannSurfaceManager>();
                if (mgr != null) mgr.Deactivate();
            });

        // Chirality Panel
        var chiralPanel = FindObjectOfType<ChiralityPanelDisplay>();
        if (chiralPanel != null && chiralPanel.gameObject.activeInHierarchy)
            EnsureGrab(chiralPanel.gameObject,
                () => chiralPanel.gameObject.SetActive(false));

        // Tutorial Video Panel
        var tutorial = TutorialManager.Instance;
        if (tutorial != null && tutorial.videoDisplayPanel != null &&
            tutorial.videoDisplayPanel.activeInHierarchy)
            EnsureGrab(tutorial.videoDisplayPanel, () => tutorial.CloseTutorial());

        // Molecule Renderer (the actual molecule display)
        var molRenderer = FindObjectOfType<MoleculeRenderer>();
        if (molRenderer != null && molRenderer.gameObject.activeInHierarchy &&
            molRenderer.CurrentMolecule != null)
        {
            EnsureGrab(molRenderer.gameObject, () =>
            {
                var lib = FindObjectOfType<MoleculeLibrary>();
                if (lib != null) lib.ClearCurrentMolecule();
                var planeAlign = molRenderer.GetComponent<MoleculePlaneAlignment>();
                if (planeAlign != null) planeAlign.SetPlaneVisibility(false);
            }, enableClose: true, grabDist: 0.6f);
        }
    }

    /// <summary>
    /// Stellt sicher dass ein GameObject VRPanelGrab hat, mit Close-Callback.
    /// </summary>
    void EnsureGrab(GameObject obj, System.Action onClose,
        bool enableClose = true, float grabDist = 0.5f)
    {
        if (obj == null) return;

        var grab = obj.GetComponent<VRPanelGrab>();
        if (grab == null)
        {
            grab = obj.AddComponent<VRPanelGrab>();
            grab.grabDistance = grabDist;
            grab.enableClose = enableClose;
            grab.OnCloseRequested = onClose;
            Debug.Log($"[VRPanelManager] VRPanelGrab attached to {obj.name}");
        }
    }

    /// <summary>
    /// Schließt alle aktiven Panels (universeller Clear).
    /// Kann vom WebSocket aufgerufen werden.
    /// </summary>
    public void CloseAllPanels()
    {
        Debug.Log("[VRPanelManager] Closing all panels");

        // Quiz
        var quiz = QuizManager.Instance;
        if (quiz != null) quiz.EndQuiz();
        var quizDisplay = FindObjectOfType<QuizDisplay>();
        if (quizDisplay != null) quizDisplay.Hide();

        // Builder
        var builder = BuilderManager.Instance;
        if (builder != null) builder.StopBuilder();

        // Physics
        if (LorentzLabManager.Instance != null) Destroy(LorentzLabManager.Instance.gameObject);
        if (ConductorSwingManager.Instance != null) Destroy(ConductorSwingManager.Instance.gameObject);
        if (InductionLoopManager.Instance != null) Destroy(InductionLoopManager.Instance.gameObject);

        // Riemann
        var riemann = FindObjectOfType<RiemannSurfaceDisplay>();
        if (riemann != null) riemann.ClearSurface();
        var riemannMgr = FindObjectOfType<RiemannSurfaceManager>();
        if (riemannMgr != null) riemannMgr.Deactivate();

        // Chirality
        var chiralPanel = FindObjectOfType<ChiralityPanelDisplay>();
        if (chiralPanel != null) chiralPanel.gameObject.SetActive(false);
        var chiralVis = FindObjectOfType<ChiralityVisualizer>();
        if (chiralVis != null) chiralVis.ClearMarkers();
        var isomerAnim = FindObjectOfType<IsomerAnimator>();
        if (isomerAnim != null) isomerAnim.ClearEnantiomer();

        // Tutorial
        var tutorial = TutorialManager.Instance;
        if (tutorial != null) tutorial.CloseTutorial();

        // Molecule
        var lib = FindObjectOfType<MoleculeLibrary>();
        if (lib != null) lib.ClearCurrentMolecule();
        var molRenderer = FindObjectOfType<MoleculeRenderer>();
        if (molRenderer != null)
        {
            var planeAlign = molRenderer.GetComponent<MoleculePlaneAlignment>();
            if (planeAlign != null) planeAlign.SetPlaneVisibility(false);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
