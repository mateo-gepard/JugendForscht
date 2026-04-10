using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Verwaltet die Ebenen-Ausrichtung eines Moleküls basierend auf PCA
/// und die Klassifikation von Bonds (Keil/Strich/Normal)
/// </summary>
public class MoleculePlaneAlignment : MonoBehaviour
{
    [Header("References")]
    public new MoleculeRenderer renderer;

    [Header("Plane Settings")]
    [Tooltip("Ebenen-Normale (Richtung zum Betrachter)")]
    public Vector3 planeNormal = Vector3.forward;

    [Header("Classification Settings")]
    [Tooltip("Depth threshold relativ zur Molekülgröße")]
    [Range(0.1f, 2.0f)]
    public float depthThresholdFactor = 1.0f;

    [Header("Debug")]
    public bool showDebugPlane = true;
    public bool showDebugInfo = false;
    [Tooltip("Press P to toggle plane visibility at runtime")]
    public bool enableDebugToggle = true;
    [Tooltip("Log renderer states every frame")]
    public bool logRendererStates = false;

    [Header("Runtime Visualization")]
    [Tooltip("Show plane in VR at runtime")]
    public bool showPlaneInVR = true;
    [Tooltip("Show centroid/rotation center marker")]
    public bool showCentroidMarker = false;
    [Tooltip("Plane material (transparent)")]
    public Material planeMaterial;
    private GameObject planeVisual;
    private GameObject centroidMarker;
    private MeshRenderer planeRenderer;
    private List<GameObject> intersectionMarkers = new List<GameObject>();
    
    [Header("Auto Rotation")]
    [Tooltip("Enable automatic rotation on load")]
    public bool enableAutoRotation = true;
    [Tooltip("Rotation speed (degrees per second)")]
    public float rotationSpeed = 30f;
    [Tooltip("Duration of auto rotation (seconds)")]
    public float rotationDuration = 8f;
    [Tooltip("Pause duration at 180° and 360° (seconds)")]
    public float pauseDuration = 1f;
    private bool isRotating = false;
    private bool isManuallyControlled = false; // Set by external controllers
    private float rotationTimer = 0f;
    private float bondRecalcTimer = 0f;
    private float totalRotationAngle = 0f; // Track cumulative rotation
    private bool isPaused = false;
    private float pauseTimer = 0f;
    private bool paused180 = false;
    private bool paused360 = false;

    // Molekül-Daten
    private MoleculeData currentMolecule;
    private List<Vector3> atomWorldPositions = new List<Vector3>(); // ALL atoms for bond classification
    private List<Vector3> planeFittingPositions = new List<Vector3>(); // C-atoms only (or all if no C)

    // PCA-Ergebnisse
    private Vector3 centroid;
    private Vector3 pcaNormal;
    private int anchorAtomIndex = -1;

    // Fixierte Ebene
    private Vector3 planePoint;  // P0 - geht durch Anchor
    private Vector3 fixedNormal; // N0 - fixierte Normale
    
    // Fixed positions after initialization
    private Vector3 fixedMoleculePosition;
    private Quaternion fixedMoleculeRotation;
    private Vector3 fixedCentroid;
    private Vector3 fixedPlanePoint;
    private bool positionsLocked = false;

    private float moleculeRadius;
    private float depthThreshold;

    /// <summary>
    /// Initialisiert die Ebenen-Ausrichtung für ein neues Molekül
    /// </summary>
    public void InitializeForMolecule(MoleculeData molecule)
    {
        if (molecule == null || molecule.atoms.Count == 0)
        {
            Debug.LogWarning("[PlaneAlignment] Invalid molecule data");
            return;
        }

        currentMolecule = molecule;

        // Check for hardcoded molecule configurations
        string molName = molecule.name.ToLower();
        if (TryInitializeHardcodedMolecule(molName))
        {
            Debug.Log($"[PlaneAlignment] Using hardcoded configuration for {molecule.name}");
            
            // Skip to visualization and alignment steps
            FixPlane();
            CalculateDepthThreshold();
            
            if (showDebugInfo)
            {
                Debug.Log($"[PlaneAlignment] Plane fixiert bei {planePoint}, Normale: {fixedNormal}");
            }
            
            // 7. Create or update runtime plane visualization
            if (showPlaneInVR)
            {
                CreatePlaneVisualization();
            }
            
            // 8. Rotate molecule so plane faces camera
            AlignPlaneToCamera();
            
            // 9. Start auto rotation if enabled
            if (enableAutoRotation)
            {
                StartAutoRotation();
            }
            
            // 10. Disable physics on molecule to prevent movement
            DisableMoleculePhysics();
            
            return;
        }

        // Standard initialization for other molecules
        // 1. Schwerpunkt berechnen
        CalculateCentroid();

        // 2. Anchor-Atom finden (nächstes zum Schwerpunkt)
        FindAnchorAtom();

        // 3. PCA durchführen
        CalculatePCA();

        // 4. Molekül initial ausrichten
        AlignMoleculeToTarget(planeNormal);

        // 5. Ebene fixieren
        FixPlane();

        // 6. Depth threshold berechnen
        CalculateDepthThreshold();

        if (showDebugInfo)
        {
            Debug.Log($"[PlaneAlignment] Initialized for {molecule.name}");
            Debug.Log($"  Centroid: {centroid}");
            Debug.Log($"  Anchor Atom: {anchorAtomIndex} ({currentMolecule.atoms[anchorAtomIndex].element})");
            Debug.Log($"  PCA Normal: {pcaNormal}");
            Debug.Log($"  Molecule Radius: {moleculeRadius:F3}");
            Debug.Log($"  Depth Threshold: {depthThreshold:F3}");
        }
        
        // 7. Create or update runtime plane visualization
        if (showPlaneInVR)
        {
            CreatePlaneVisualization();
        }
        
        // 8. Rotate molecule so plane faces camera
        AlignPlaneToCamera();
        
        // 9. Start auto rotation if enabled
        if (enableAutoRotation)
        {
            StartAutoRotation();
        }
        
        // 10. Disable physics on molecule to prevent movement
        DisableMoleculePhysics();
    }
    
    /// <summary>
    /// Starts the automatic rotation animation
    /// </summary>
    public void StartAutoRotation()
    {
        isRotating = true;
        isManuallyControlled = false; // Auto control active
        rotationTimer = 0f;
        totalRotationAngle = 0f;
        isPaused = false;
        pauseTimer = 0f;
        paused180 = false;
        paused360 = false;
        Debug.Log("[PlaneAlignment] Started auto rotation");
    }
    
    /// <summary>
    /// Stops the automatic rotation (called when user grabs molecule)
    /// </summary>
    public void StopAutoRotation()
    {
        isRotating = false;
        isManuallyControlled = true; // User is now controlling rotation
        Debug.Log("[PlaneAlignment] Stopped auto rotation - manual control active");
    }
    
    /// <summary>
    /// Rotates the molecule so the calculated plane is parallel to the camera view
    /// </summary>
    void AlignPlaneToCamera()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning("[PlaneAlignment] No main camera found for alignment");
            return;
        }
        
        // Get camera forward direction (this is the direction we want the plane normal to point)
        Vector3 cameraForward = Camera.main.transform.forward;
        
        // Calculate rotation needed to align plane normal with camera forward
        // The plane normal in world space is currently fixedNormal
        Quaternion alignmentRotation = Quaternion.FromToRotation(fixedNormal, cameraForward);
        
        // Apply rotation to the molecule renderer
        renderer.transform.rotation = alignmentRotation * renderer.transform.rotation;
        
        Debug.Log($"[PlaneAlignment] Aligned plane to camera. Plane normal: {fixedNormal}, Camera forward: {cameraForward}");
    }
    
    void Update()
    {
        // Debug controls
        if (enableDebugToggle && Input.GetKeyDown(KeyCode.P))
        {
            TogglePlaneVisibility();
        }
        
        // Log renderer states for debugging
        if (logRendererStates && Time.frameCount % 30 == 0) // Every 30 frames
        {
            LogRendererStates();
        }
        
        if (isRotating && currentMolecule != null)
        {
            rotationTimer += Time.deltaTime;
            bondRecalcTimer += Time.deltaTime;
            
            // Handle pause state
            if (isPaused)
            {
                pauseTimer += Time.deltaTime;
                if (pauseTimer >= pauseDuration)
                {
                    isPaused = false;
                    pauseTimer = 0f;
                    Debug.Log("[PlaneAlignment] Resuming rotation after pause");
                }
                // Don't rotate while paused
                return;
            }
            
            // Rotate continuously (no duration limit)
            
            // Rotate molecule around height axis (Y-axis) at constant speed
            // The plane (planePoint and fixedNormal) stays fixed in world space
            float rotationAngle = rotationSpeed * Time.deltaTime;
            totalRotationAngle += rotationAngle;
            
            // Check for pause points (180° and 360°)
            // Normalize angle to 0-360 range
            float normalizedAngle = totalRotationAngle % 360f;
            
            // Pause at 180° (once per cycle)
            if (!paused180 && normalizedAngle >= 180f && normalizedAngle - rotationAngle < 180f)
            {
                isPaused = true;
                paused180 = true;
                Debug.Log("[PlaneAlignment] Pausing at 180°");
            }
            
            // Pause at 360° (once per cycle, then reset)
            if (!paused360 && normalizedAngle >= 359.9f)
            {
                isPaused = true;
                paused360 = true;
                paused180 = false; // Reset for next cycle
                paused360 = false; // Reset immediately so next cycle can pause again
                totalRotationAngle = 0f; // Reset rotation counter
                Debug.Log("[PlaneAlignment] Pausing at 360° - cycle complete");
            }
            
            // Use RotateAround to spin molecule around the fixed centroid point
            // This keeps the centroid at the same world position while the molecule rotates
            renderer.transform.RotateAround(fixedCentroid, Vector3.up, rotationAngle);
            
            // Update atom world positions after rotation
            UpdateAtomWorldPositions();
            
            // Re-render bonds with updated stereo classification (real-time)
            // Bonds are recalculated based on new atom positions relative to fixed plane
            if (bondRecalcTimer >= 0.1f && renderer != null)
            {
                renderer.RerenderBondsOnly();
                bondRecalcTimer = 0f;
            }
        }
    }
    
    /// <summary>
    /// Physics-based position locking - runs before physics updates
    /// </summary>
    void FixedUpdate()
    {
        if (!positionsLocked || renderer == null) return;
        
        // Don't lock during auto-rotation or manual control
        if (!isRotating && !isManuallyControlled)
        {
            // Aggressively lock position in FixedUpdate (before physics)
            renderer.transform.position = fixedMoleculePosition;
            
            // Also lock rotation
            renderer.transform.rotation = fixedMoleculeRotation;
            
            // Force rigidbody velocities to zero
            Rigidbody rb = renderer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
    
    /// <summary>
    /// Ensures positions stay locked even if something tries to move them
    /// </summary>
    void LateUpdate()
    {
        if (!positionsLocked || renderer == null) return;
        
        // Don't enforce molecule position/rotation during auto-rotation or manual control
        if (!isRotating && !isManuallyControlled)
        {
            // Enforce fixed molecule position and rotation when idle
            renderer.transform.position = fixedMoleculePosition;
            renderer.transform.rotation = fixedMoleculeRotation;
        }
        
        // Enforce fixed plane position
        if (planeVisual != null)
        {
            planeVisual.transform.position = fixedPlanePoint;
        }
    }
    
    /// <summary>
    /// Creates a visible plane mesh for VR runtime visualization
    /// </summary>
    private void CreatePlaneVisualization()
    {
        // Destroy old plane if exists
        if (planeVisual != null)
        {
            Destroy(planeVisual);
        }
        
        // Destroy old centroid marker if exists
        if (centroidMarker != null)
        {
            Destroy(centroidMarker);
        }
        
        // Create centroid marker (rotation center)
        if (showCentroidMarker)
        {
            CreateCentroidMarker();
        }
        
        // Create new plane GameObject in world space (independent, doesn't rotate with molecule)
        planeVisual = new GameObject("MoleculePlane_Visual");
        // DO NOT parent - keep independent so plane stays fixed while molecule rotates through it
        
        // Add mesh components
        MeshFilter meshFilter = planeVisual.AddComponent<MeshFilter>();
        planeRenderer = planeVisual.AddComponent<MeshRenderer>();
        
        // Create plane mesh (sized to molecule)
        float size = moleculeRadius * 1.2f;
        meshFilter.mesh = CreatePlaneMesh(size);
        
        // No collider needed - this is just a visual guide, not for physics interactions
        
        // Configure renderer to prevent camera clipping issues
        planeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        planeRenderer.receiveShadows = false;
        planeVisual.layer = LayerMask.NameToLayer("Default");
        
        // Assign material (create default if none assigned)
        if (planeMaterial == null)
        {
            // Use Mobile/Particles/Alpha Blended - best for transparent objects on Quest
            Shader mobileShader = Shader.Find("Mobile/Particles/Alpha Blended");
            if (mobileShader == null)
            {
                // Fallback to Legacy Shaders/Particles/Alpha Blended
                mobileShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            }
            if (mobileShader == null)
            {
                // Try Unlit/Transparent
                mobileShader = Shader.Find("Unlit/Transparent");
            }
            if (mobileShader == null)
            {
                Debug.LogError("[PlaneAlignment] No transparent shader found!");
                mobileShader = Shader.Find("Unlit/Color");
            }
            
            planeMaterial = new Material(mobileShader);
            planeMaterial.color = new Color(0.0f, 1.0f, 0.0f, 0.5f); // More visible transparent green
            
            // Set texture to white for particle shaders
            if (mobileShader.name.Contains("Particle"))
            {
                Texture2D whiteTex = Texture2D.whiteTexture;
                planeMaterial.mainTexture = whiteTex;
            }
            
            planeMaterial.renderQueue = 3000;
            
            Debug.Log($"[PlaneAlignment] Created material with shader: {mobileShader.name}");
        }
        else
        {
            // Ensure assigned material uses mobile-compatible settings
            planeMaterial.renderQueue = 3000;
        }
        planeRenderer.material = planeMaterial;
        
        // Hide the filled plane, only show outline
        planeRenderer.enabled = false;
        
        Debug.Log($"[PlaneAlignment] Plane mesh disabled (outline only)");
        
        // Add edge outline
        CreatePlaneOutline(planeVisual, size);
        
        // Ensure planeVisual is active so outline is visible
        planeVisual.SetActive(true);
        
        Debug.Log($"[PlaneAlignment] ✨✨✨ PLANE VISUAL CREATED: active={planeVisual.activeSelf}, position={planeVisual.transform.position}, children={planeVisual.transform.childCount}");
        
        // Position and orient the plane in world space - stays fixed while molecule rotates through it
        planeVisual.transform.position = planePoint;
        planeVisual.transform.rotation = Quaternion.LookRotation(fixedNormal);
        
        // Lock all positions - these are now fixed forever
        fixedMoleculePosition = renderer.transform.position;
        fixedMoleculeRotation = renderer.transform.rotation;
        fixedCentroid = centroid;
        fixedPlanePoint = planePoint;
        positionsLocked = true;
        
        Debug.Log($"[PlaneAlignment] Positionen fixiert - Molekül: {fixedMoleculePosition}, Centroid: {fixedCentroid}, Plane: {fixedPlanePoint}");
        
        Debug.Log($"[PlaneAlignment] Plane position: {planePoint}, rotation: {fixedNormal}, size: {size}");
        Debug.Log("[PlaneAlignment] Created plane visualization in world space (stays fixed while molecule rotates)");
    }
    
    /// <summary>
    /// Creates a simple quad mesh for the plane
    /// </summary>
    private Mesh CreatePlaneMesh(float size)
    {
        Mesh mesh = new Mesh();
        mesh.name = "PlaneMesh";
        
        // Create a simple double-sided quad to avoid camera clipping issues
        // Thin plane is better for VR - no thick geometry that can cause grey flashes
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-size, -size, 0),
            new Vector3(size, -size, 0),
            new Vector3(-size, size, 0),
            new Vector3(size, size, 0)
        };
        
        // Double-sided triangles (both front and back facing)
        int[] triangles = new int[12]
        {
            // Front face
            0, 2, 1,
            2, 3, 1,
            // Back face (reversed winding for double-sided)
            0, 1, 2,
            2, 1, 3
        };
        
        // UVs
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Creates an outline around the plane edges
    /// </summary>
    private void CreatePlaneOutline(GameObject parent, float size)
    {
        GameObject outlineObj = new GameObject("PlaneOutline");
        outlineObj.transform.SetParent(parent.transform, false);
        outlineObj.SetActive(true); // Ensure outline is active
        
        LineRenderer lineRenderer = outlineObj.AddComponent<LineRenderer>();
        // Use mobile-compatible shader for Quest
        Shader outlineShader = Shader.Find("Unlit/Color");
        if (outlineShader == null) outlineShader = Shader.Find("Sprites/Default");
        lineRenderer.material = new Material(outlineShader);
        
        // White thin line
        lineRenderer.material.color = new Color(1.0f, 1.0f, 1.0f, 1.0f); // White
        lineRenderer.startColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        lineRenderer.endColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        lineRenderer.startWidth = 0.008f; // Thin line (0.8cm)
        lineRenderer.endWidth = 0.008f;
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = 4;
        lineRenderer.enabled = true; // Ensure LineRenderer is enabled
        
        // Force outline to always render
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        outlineObj.layer = LayerMask.NameToLayer("Default");
        
        // Slight offset to prevent camera near-plane clipping (move line 1cm back from plane surface)
        float zOffset = -0.01f;
        
        // Draw simple rectangle around plane edge (4 corners)
        lineRenderer.SetPosition(0, new Vector3(-size, -size, zOffset));
        lineRenderer.SetPosition(1, new Vector3(size, -size, zOffset));
        lineRenderer.SetPosition(2, new Vector3(size, size, zOffset));
        lineRenderer.SetPosition(3, new Vector3(-size, size, zOffset));
        
        Debug.Log($"[PlaneAlignment] ✨ Plane outline created: size={size}, color=white, width=0.008, enabled={lineRenderer.enabled}, parent active={parent.activeSelf}");
    }
    
    /// <summary>
    /// Creates a visible marker at the centroid (rotation center)
    /// </summary>
    private void CreateCentroidMarker()
    {
        // Create small sphere at centroid
        centroidMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        centroidMarker.name = "CentroidMarker";
        centroidMarker.transform.position = centroid;
        centroidMarker.transform.localScale = Vector3.one * 0.02f; // Small sphere (2cm)
        
        // Remove collider
        Collider col = centroidMarker.GetComponent<Collider>();
        if (col != null) Destroy(col);
        
        // Set material - bright color for visibility
        MeshRenderer markerRenderer = centroidMarker.GetComponent<MeshRenderer>();
        Shader markerShader = Shader.Find("Unlit/Color");
        if (markerShader == null) markerShader = Shader.Find("Standard");
        
        Material markerMat = new Material(markerShader);
        markerMat.color = new Color(1f, 0.3f, 0.3f, 1f); // Bright red
        markerRenderer.material = markerMat;
        
        // Disable shadows
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;
        
        Debug.Log($"[PlaneAlignment] Centroid marker erstellt bei: {centroid}");
    }
    
    /// <summary>
    /// Updates visual markers showing where bonds intersect the plane
    /// </summary>
    private void UpdateIntersectionMarkers()
    {
        // Clear old markers
        foreach (var marker in intersectionMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        intersectionMarkers.Clear();
        
        if (currentMolecule == null || planeVisual == null) return;
        
        // Check each bond for intersection
        foreach (var bond in currentMolecule.bonds)
        {
            Vector3 posA = GetAtomWorldPosition(bond.atomA_ID);
            Vector3 posB = GetAtomWorldPosition(bond.atomB_ID);
            
            float dA = Vector3.Dot(posA - planePoint, fixedNormal);
            float dB = Vector3.Dot(posB - planePoint, fixedNormal);
            
            // Check if bond crosses the plane (atoms on opposite sides)
            bool crosses = (dA > depthThreshold && dB < -depthThreshold) || 
                          (dA < -depthThreshold && dB > depthThreshold);
            
            if (crosses)
            {
                // Calculate intersection point using parametric line equation
                // Line: P(t) = A + t(B - A)
                // Plane: (P - P0) · N = 0
                // Solve for t: t = [(P0 - A) · N] / [(B - A) · N]
                
                Vector3 lineDir = posB - posA;
                float denominator = Vector3.Dot(lineDir, fixedNormal);
                
                if (Mathf.Abs(denominator) > 0.0001f)
                {
                    float t = Vector3.Dot(planePoint - posA, fixedNormal) / denominator;
                    Vector3 intersectionPoint = posA + t * lineDir;
                    
                    // Create marker at intersection
                    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.name = $"Intersection_{bond.atomA_ID}_{bond.atomB_ID}";
                    marker.transform.position = intersectionPoint;
                    marker.transform.localScale = Vector3.one * 0.03f; // Small sphere
                    
                    // Bright color for visibility
                    Renderer markerRenderer = marker.GetComponent<Renderer>();
                    Material markerMat = new Material(Shader.Find("Standard"));
                    markerMat.color = new Color(1f, 0.8f, 0.2f, 1f); // Bright yellow-orange
                    markerMat.SetFloat("_Metallic", 0.5f);
                    markerMat.SetFloat("_Glossiness", 0.8f);
                    markerRenderer.material = markerMat;
                    
                    intersectionMarkers.Add(marker);
                }
            }
        }
        
        Debug.Log($"[PlaneAlignment] Created {intersectionMarkers.Count} intersection markers");
    }
    
    /// <summary>
    /// Hides or shows the plane visualization
    /// </summary>
    public void SetPlaneVisibility(bool visible)
    {
        if (planeVisual != null)
        {
            planeVisual.SetActive(visible);
        }
    }
    
    private void OnDestroy()
    {
        if (planeVisual != null)
        {
            Destroy(planeVisual);
        }
        
        if (centroidMarker != null)
        {
            Destroy(centroidMarker);
        }
        
        // Clean up intersection markers
        foreach (var marker in intersectionMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        intersectionMarkers.Clear();
    }
    
    /// <summary>
    /// Disables all physics on the molecule to prevent it from being moved
    /// </summary>
    private void DisableMoleculePhysics()
    {
        if (renderer == null) return;
        
        // Remove or disable Rigidbody
        Rigidbody rb = renderer.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll; // Freeze all movement
            rb.detectCollisions = false; // Disable collision detection entirely
            Debug.Log("[PlaneAlignment] Rigidbody frozen with all constraints");
        }
        
        // Also check children
        Rigidbody[] childRbs = renderer.GetComponentsInChildren<Rigidbody>();
        foreach (var childRb in childRbs)
        {
            childRb.isKinematic = true;
            childRb.useGravity = false;
            childRb.velocity = Vector3.zero;
            childRb.angularVelocity = Vector3.zero;
            childRb.constraints = RigidbodyConstraints.FreezeAll;
            childRb.detectCollisions = false;
        }
        
        // Disable colliders that might cause pushing
        Collider[] colliders = renderer.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            // Don't destroy, just disable physics interaction
            col.isTrigger = true; // Make triggers so they don't push
        }
        
        if (colliders.Length > 0)
        {
            Debug.Log($"[PlaneAlignment] Set {colliders.Length} colliders to triggers (no physics push)");
        }
    }

    /// <summary>
    /// Clears and destroys the plane visualization
    /// </summary>
    public void ClearPlane()
    {
        if (planeVisual != null)
        {
            Destroy(planeVisual);
            planeVisual = null;
            Debug.Log("[PlaneAlignment] Plane visualization destroyed");
        }
        
        if (centroidMarker != null)
        {
            Destroy(centroidMarker);
            centroidMarker = null;
        }
    }

    /// <summary>
    /// Hardcoded configurations for specific molecules
    /// Returns true if molecule was configured, false otherwise
    /// </summary>
    private bool TryInitializeHardcodedMolecule(string moleculeName)
    {
        // Collect all atom world positions first
        atomWorldPositions.Clear();
        foreach (var atom in currentMolecule.atoms)
        {
            Vector3 worldPos = renderer.transform.TransformPoint(atom.position * renderer.angstromToMeter * renderer.bondLengthMultiplier);
            atomWorldPositions.Add(worldPos);
        }

        switch (moleculeName)
        {
            case "water":
                return InitializeWater();
            case "ethanol":
                return InitializeEthanol();
            case "benzene":
                return InitializeBenzene();
            case "methane":
                return InitializeMethane();
            case "propanon":
            case "propanone":
            case "acetone":
                return InitializePropanon();
            case "ammonia":
                return InitializeAmmonia();
            default:
                return false;
        }
    }

    private bool InitializeWater()
    {
        // Water: Plane defined by O + 2H (3 points), rotation center = O atom
        int oIndex = -1;
        List<int> hIndices = new List<int>();
        
        for (int i = 0; i < currentMolecule.atoms.Count; i++)
        {
            if (currentMolecule.atoms[i].element == "O")
                oIndex = i;
            else if (currentMolecule.atoms[i].element == "H")
                hIndices.Add(i);
        }

        if (oIndex < 0 || hIndices.Count < 2)
        {
            Debug.LogWarning("[PlaneAlignment] Water: Invalid structure");
            return false;
        }

        // Rotation center = O atom
        centroid = atomWorldPositions[oIndex];
        anchorAtomIndex = oIndex;
        
        // Calculate molecule radius for plane sizing
        float maxDist = 0f;
        foreach (var pos in atomWorldPositions)
        {
            float dist = Vector3.Distance(pos, centroid);
            maxDist = Mathf.Max(maxDist, dist);
        }
        moleculeRadius = maxDist;
        
        // Plane parallel to molecular plane (contains all 3 atoms: O + 2H)
        Vector3 v1 = atomWorldPositions[hIndices[0]] - atomWorldPositions[oIndex];
        Vector3 v2 = atomWorldPositions[hIndices[1]] - atomWorldPositions[oIndex];
        planeNormal = Vector3.Cross(v1, v2).normalized;
        
        Debug.Log($"[PlaneAlignment] Water: Plane parallel to molecule (contains O+2H), rotation at O {centroid}, Normal {planeNormal}, radius {moleculeRadius}");
        return true;
    }

    private bool InitializeEthanol()
    {
        // Ethanol: Plane defined by 2C + O (3 points), rotation center = middle of 2C
        List<int> cIndices = new List<int>();
        int oIndex = -1;

        for (int i = 0; i < currentMolecule.atoms.Count; i++)
        {
            string elem = currentMolecule.atoms[i].element;
            if (elem == "C") cIndices.Add(i);
            else if (elem == "O") oIndex = i;
        }

        if (cIndices.Count != 2 || oIndex < 0)
        {
            Debug.LogWarning("[PlaneAlignment] Ethanol: Invalid structure");
            return false;
        }

        // Rotation center = middle of 2 carbons
        centroid = (atomWorldPositions[cIndices[0]] + atomWorldPositions[cIndices[1]]) / 2f;
        anchorAtomIndex = cIndices[0];

        // Calculate molecule radius for plane sizing
        float maxDist = 0f;
        foreach (var pos in atomWorldPositions)
        {
            float dist = Vector3.Distance(pos, centroid);
            maxDist = Mathf.Max(maxDist, dist);
        }
        moleculeRadius = maxDist;

        // Plane defined by 3 points: C1, C2, O
        Vector3 v1 = atomWorldPositions[cIndices[1]] - atomWorldPositions[cIndices[0]];
        Vector3 v2 = atomWorldPositions[oIndex] - atomWorldPositions[cIndices[0]];
        planeNormal = Vector3.Cross(v1, v2).normalized;

        Debug.Log($"[PlaneAlignment] Ethanol: Plane through 2C+O, rotation at C-C middle {centroid}, Normal {planeNormal}, radius {moleculeRadius}");
        return true;
    }

    private bool InitializeBenzene()
    {
        // Benzene: Plane defined by first 3 carbons (3 points), rotation center = hexagon center
        List<int> cIndices = new List<int>();
        for (int i = 0; i < currentMolecule.atoms.Count; i++)
        {
            if (currentMolecule.atoms[i].element == "C")
                cIndices.Add(i);
        }

        if (cIndices.Count != 6)
        {
            Debug.LogWarning("[PlaneAlignment] Benzene: Invalid structure");
            return false;
        }

        // Rotation center = average of all 6 carbons
        centroid = Vector3.zero;
        foreach (int idx in cIndices)
            centroid += atomWorldPositions[idx];
        centroid /= 6f;
        
        anchorAtomIndex = cIndices[0];

        // Calculate molecule radius for plane sizing
        float maxDist = 0f;
        foreach (var pos in atomWorldPositions)
        {
            float dist = Vector3.Distance(pos, centroid);
            maxDist = Mathf.Max(maxDist, dist);
        }
        moleculeRadius = maxDist;

        // Plane defined by 3 points: first 3 carbons
        Vector3 v1 = atomWorldPositions[cIndices[1]] - atomWorldPositions[cIndices[0]];
        Vector3 v2 = atomWorldPositions[cIndices[2]] - atomWorldPositions[cIndices[0]];
        planeNormal = Vector3.Cross(v1, v2).normalized;

        Debug.Log($"[PlaneAlignment] Benzene: Plane through 3C, rotation at hexagon center {centroid}, Normal {planeNormal}, radius {moleculeRadius}");
        return true;
    }

    private bool InitializeMethane()
    {
        // Methane: Plane defined by C + 2H (3 points), rotation center = C
        int cIndex = -1;
        List<int> hIndices = new List<int>();

        for (int i = 0; i < currentMolecule.atoms.Count; i++)
        {
            string elem = currentMolecule.atoms[i].element;
            if (elem == "C") cIndex = i;
            else if (elem == "H") hIndices.Add(i);
        }

        if (cIndex < 0 || hIndices.Count < 2)
        {
            Debug.LogWarning("[PlaneAlignment] Methane: Invalid structure");
            return false;
        }

        // Rotation center = C atom
        centroid = atomWorldPositions[cIndex];
        anchorAtomIndex = cIndex;

        // Calculate molecule radius for plane sizing
        float maxDist = 0f;
        foreach (var pos in atomWorldPositions)
        {
            float dist = Vector3.Distance(pos, centroid);
            maxDist = Mathf.Max(maxDist, dist);
        }
        moleculeRadius = maxDist;

        // Plane defined by 3 points: C, H1, H2
        Vector3 v1 = atomWorldPositions[hIndices[0]] - atomWorldPositions[cIndex];
        Vector3 v2 = atomWorldPositions[hIndices[1]] - atomWorldPositions[cIndex];
        planeNormal = Vector3.Cross(v1, v2).normalized;

        Debug.Log($"[PlaneAlignment] Methane: Plane through C+2H, rotation at C {centroid}, Normal {planeNormal}, radius {moleculeRadius}");
        return true;
    }

    private bool InitializePropanon()
    {
        // Propanon: Plane defined by 3C + O (use first 3 for plane), rotation center = middle C
        List<int> cIndices = new List<int>();
        int oIndex = -1;

        for (int i = 0; i < currentMolecule.atoms.Count; i++)
        {
            string elem = currentMolecule.atoms[i].element;
            if (elem == "C") cIndices.Add(i);
            else if (elem == "O") oIndex = i;
        }

        if (cIndices.Count != 3 || oIndex < 0)
        {
            Debug.LogWarning("[PlaneAlignment] Propanon: Invalid structure");
            return false;
        }

        // Find middle carbon (the one bonded to O)
        int middleCIndex = -1;
        foreach (var bond in currentMolecule.bonds)
        {
            int a = bond.atomA_ID;
            int b = bond.atomB_ID;
            if ((a == oIndex && cIndices.Contains(b)))
            {
                middleCIndex = b;
                break;
            }
            if ((b == oIndex && cIndices.Contains(a)))
            {
                middleCIndex = a;
                break;
            }
        }

        if (middleCIndex < 0) middleCIndex = cIndices[1]; // Fallback to middle index

        // Rotation center = middle C
        centroid = atomWorldPositions[middleCIndex];
        anchorAtomIndex = middleCIndex;

        // Calculate molecule radius for plane sizing
        float maxDist = 0f;
        foreach (var pos in atomWorldPositions)
        {
            float dist = Vector3.Distance(pos, centroid);
            maxDist = Mathf.Max(maxDist, dist);
        }
        moleculeRadius = maxDist;

        // Propanone is planar - plane should be parallel to molecular plane (contains all 3C + O)
        // Get the other two carbons (not the middle one)
        List<int> otherCIndices = cIndices.Where(idx => idx != middleCIndex).ToList();
        
        // Use C=O vector and one of the C-C vectors to define the molecular plane
        Vector3 v1 = atomWorldPositions[oIndex] - atomWorldPositions[middleCIndex];
        Vector3 v2 = atomWorldPositions[otherCIndices[0]] - atomWorldPositions[middleCIndex];
        planeNormal = Vector3.Cross(v1, v2).normalized;

        Debug.Log($"[PlaneAlignment] Propanon: Plane through 3C+O, rotation at middle C {centroid}, Normal {planeNormal}, radius {moleculeRadius}");
        return true;
    }

    private bool InitializeAmmonia()
    {
        // Ammonia: Plane defined by N + 2H (3 points), rotation center = N
        int nIndex = -1;
        List<int> hIndices = new List<int>();

        for (int i = 0; i < currentMolecule.atoms.Count; i++)
        {
            string elem = currentMolecule.atoms[i].element;
            if (elem == "N") nIndex = i;
            else if (elem == "H") hIndices.Add(i);
        }

        if (nIndex < 0 || hIndices.Count < 2)
        {
            Debug.LogWarning("[PlaneAlignment] Ammonia: Invalid structure");
            return false;
        }

        // Rotation center = N atom
        centroid = atomWorldPositions[nIndex];
        anchorAtomIndex = nIndex;

        // Calculate molecule radius for plane sizing
        float maxDist = 0f;
        foreach (var pos in atomWorldPositions)
        {
            float dist = Vector3.Distance(pos, centroid);
            maxDist = Mathf.Max(maxDist, dist);
        }
        moleculeRadius = maxDist;

        // Plane defined by 3 points: N, H1, H2
        Vector3 v1 = atomWorldPositions[hIndices[0]] - atomWorldPositions[nIndex];
        Vector3 v2 = atomWorldPositions[hIndices[1]] - atomWorldPositions[nIndex];
        planeNormal = Vector3.Cross(v1, v2).normalized;

        Debug.Log($"[PlaneAlignment] Ammonia: Plane through N+2H, rotation at N {centroid}, Normal {planeNormal}, radius {moleculeRadius}");
        return true;
    }

    /// <summary>
    /// Berechnet den Schwerpunkt - NUR von C-Atomen wenn vorhanden, sonst alle Atome
    /// CRITICAL: atomWorldPositions contains ALL atoms for bond classification
    /// CRITICAL: centroid is calculated ONLY from C-atoms (if present)
    /// </summary>
    private void CalculateCentroid()
    {
        centroid = Vector3.zero;
        atomWorldPositions.Clear();
        planeFittingPositions.Clear();

        // Sammle ALLE Atompositionen (für Bond-Klassifikation und Distanzberechnungen)
        foreach (var atom in currentMolecule.atoms)
        {
            Vector3 worldPos = renderer.transform.TransformPoint(atom.position * renderer.angstromToMeter * renderer.bondLengthMultiplier);
            atomWorldPositions.Add(worldPos);
        }
        
        // Sammle C-Atom Positionen mit Debug-Info
        List<Vector3> carbonPositions = new List<Vector3>();
        List<string> carbonDebugInfo = new List<string>();
        
        for (int i = 0; i < currentMolecule.atoms.Count; i++)
        {
            var atom = currentMolecule.atoms[i];
            if (atom.element == "C")
            {
                carbonPositions.Add(atomWorldPositions[i]);
                carbonDebugInfo.Add($"C[{i}] local={atom.position} world={atomWorldPositions[i]}");
            }
        }
        
        // CRITICAL: Berechne Mittelpunkt IMMER von C-Atomen wenn vorhanden
        // Dies ist der Rotationsmittelpunkt und der Punkt durch den die Ebene geht
        if (carbonPositions.Count > 0)
        {
            // Mittelpunkt = Durchschnitt aller C-Atome
            foreach (var pos in carbonPositions)
            {
                centroid += pos;
            }
            centroid /= carbonPositions.Count;
            
            Debug.Log($"[PlaneAlignment] === C-ATOM CENTROID BERECHNUNG ===");
            Debug.Log($"[PlaneAlignment] Renderer Position: {renderer.transform.position}");
            Debug.Log($"[PlaneAlignment] Renderer Scale: {renderer.transform.localScale}");
            Debug.Log($"[PlaneAlignment] angstromToMeter: {renderer.angstromToMeter}, bondLengthMultiplier: {renderer.bondLengthMultiplier}");
            Debug.Log($"[PlaneAlignment] Gefundene C-Atome: {carbonPositions.Count}");
            foreach (var info in carbonDebugInfo)
            {
                Debug.Log($"[PlaneAlignment] {info}");
            }
            Debug.Log($"[PlaneAlignment] Berechneter C-Centroid (Rotationsmittelpunkt): {centroid}");
            
            // CRITICAL: Plane fitting uses ONLY C-atoms when available
            planeFittingPositions.AddRange(carbonPositions);
            Debug.Log($"[PlaneAlignment] Plane fitting wird NUR mit {planeFittingPositions.Count} C-Atomen berechnet");
            
            // Berechne auch den Mittelpunkt in local space zur Verifizierung
            Vector3 localCentroid = Vector3.zero;
            foreach (var atom in currentMolecule.atoms)
            {
                if (atom.element == "C")
                {
                    localCentroid += atom.position;
                }
            }
            localCentroid /= carbonPositions.Count;
            Vector3 expectedWorldCentroid = renderer.transform.TransformPoint(localCentroid * renderer.angstromToMeter * renderer.bondLengthMultiplier);
            Debug.Log($"[PlaneAlignment] Local C-Centroid: {localCentroid}");
            Debug.Log($"[PlaneAlignment] Expected World Centroid: {expectedWorldCentroid}");
        }
        else
        {
            // Fallback: Wenn keine C-Atome, dann Durchschnitt aller Atome
            foreach (var pos in atomWorldPositions)
            {
                centroid += pos;
            }
            centroid /= atomWorldPositions.Count;
            
            // Use all atoms for plane fitting as fallback
            planeFittingPositions.AddRange(atomWorldPositions);
            Debug.Log($"[PlaneAlignment] Keine C-Atome gefunden, Mittelpunkt aus {atomWorldPositions.Count} Atomen: {centroid}");
            Debug.Log($"[PlaneAlignment] Plane fitting verwendet alle {planeFittingPositions.Count} Atome als Fallback");
        }
    }

    /// <summary>
    /// Findet das Atom, das am nächsten zum Schwerpunkt liegt (Anchor)
    /// </summary>
    private void FindAnchorAtom()
    {
        float minDistance = float.MaxValue;
        anchorAtomIndex = 0;

        for (int i = 0; i < atomWorldPositions.Count; i++)
        {
            float distance = Vector3.Distance(atomWorldPositions[i], centroid);
            if (distance < minDistance)
            {
                minDistance = distance;
                anchorAtomIndex = i;
            }
        }
    }

    /// <summary>
    /// Finds the plane that:
    /// 1. Goes through the centroid (average of C-atoms or all atoms)
    /// 2. Has the maximum number of atoms ON the plane (within threshold)
    /// This is the stereochemistry plane for bond classification
    /// </summary>
    private void CalculatePCA()
    {
        // Calculate molecule size for thresholds (use ALL atoms for this)
        float maxDist = 0f;
        foreach (var pos in atomWorldPositions)
        {
            Vector3 q = pos - centroid;
            maxDist = Mathf.Max(maxDist, q.magnitude);
        }
        moleculeRadius = maxDist;

        // Build covariance matrix for PCA-based candidates
        // CRITICAL: Use ONLY planeFittingPositions (C-atoms when available)
        Matrix3x3 M = new Matrix3x3();
        foreach (var pos in planeFittingPositions)
        {
            Vector3 q = pos - centroid;
            M.m00 += q.x * q.x;
            M.m01 += q.x * q.y;
            M.m02 += q.x * q.z;
            M.m10 += q.y * q.x;
            M.m11 += q.y * q.y;
            M.m12 += q.y * q.z;
            M.m20 += q.z * q.x;
            M.m21 += q.z * q.y;
            M.m22 += q.z * q.z;
        }

        // Generate candidate normals from multiple approaches
        Vector3 candidate1 = FindSmallestEigenVector(M);
        Vector3 largest1 = FindLargestEigenVector(M);
        Matrix3x3 M2 = DeflateMatrix(M, largest1);
        Vector3 largest2 = FindLargestEigenVector(M2);
        Vector3 candidate2 = Vector3.Cross(largest1, largest2).normalized;
        
        List<Vector3> candidates = new List<Vector3>
        {
            candidate1, -candidate1,
            candidate2, -candidate2,
            Vector3.right, Vector3.left,
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back
        };
        
        // CRITICAL: Find the plane orientation that passes through the MOST atoms
        // The plane ALWAYS goes through the centroid, we're just finding the best rotation
        pcaNormal = FindOptimalPlaneNormal(candidates);
        
        Debug.Log($"[PlaneAlignment] Selected plane normal: {pcaNormal}, with best in-plane atom/bond count");
    }
    
    /// <summary>
    /// Finds eigenvector with largest eigenvalue using power iteration
    /// </summary>
    private Vector3 FindLargestEigenVector(Matrix3x3 M)
    {
        Vector3 v = new Vector3(1, 1, 1).normalized;
        for (int iter = 0; iter < 30; iter++)
        {
            Vector3 Mv = M.MultiplyVector(v);
            v = Mv.normalized;
        }
        return v;
    }
    
    /// <summary>
    /// Finds eigenvector with smallest eigenvalue using inverse power iteration
    /// </summary>
    private Vector3 FindSmallestEigenVector(Matrix3x3 M)
    {
        // Add small identity to make invertible
        M.m00 += 0.0001f;
        M.m11 += 0.0001f;
        M.m22 += 0.0001f;
        
        Vector3 v = new Vector3(1, 1, 1).normalized;
        
        for (int iter = 0; iter < 30; iter++)
        {
            // Solve M*v_next = v (inverse iteration)
            // Using simplified Gauss-Seidel for 3x3
            Vector3 vNext = SolveLinearSystem(M, v);
            v = vNext.normalized;
        }
        return v;
    }
    
    /// <summary>
    /// Deflates matrix by removing component along given vector
    /// </summary>
    private Matrix3x3 DeflateMatrix(Matrix3x3 M, Vector3 v)
    {
        Vector3 Mv = M.MultiplyVector(v);
        float lambda = Vector3.Dot(v, Mv);
        
        Matrix3x3 result = new Matrix3x3();
        result.m00 = M.m00 - lambda * v.x * v.x;
        result.m01 = M.m01 - lambda * v.x * v.y;
        result.m02 = M.m02 - lambda * v.x * v.z;
        result.m10 = M.m10 - lambda * v.y * v.x;
        result.m11 = M.m11 - lambda * v.y * v.y;
        result.m12 = M.m12 - lambda * v.y * v.z;
        result.m20 = M.m20 - lambda * v.z * v.x;
        result.m21 = M.m21 - lambda * v.z * v.y;
        result.m22 = M.m22 - lambda * v.z * v.z;
        
        return result;
    }
    
    /// <summary>
    /// Simple iterative solver for M*x = b
    /// </summary>
    private Vector3 SolveLinearSystem(Matrix3x3 M, Vector3 b)
    {
        Vector3 x = b;
        
        // Gauss-Seidel iterations
        for (int i = 0; i < 10; i++)
        {
            float x0 = (b.x - M.m01 * x.y - M.m02 * x.z) / (M.m00 + 0.001f);
            float x1 = (b.y - M.m10 * x0 - M.m12 * x.z) / (M.m11 + 0.001f);
            float x2 = (b.z - M.m20 * x0 - M.m21 * x1) / (M.m22 + 0.001f);
            x = new Vector3(x0, x1, x2);
        }
        
        return x;
    }
    
    /// <summary>
    /// Tests candidate normals and returns the one that maximizes atoms ON the plane
    /// The plane is positioned through the centroid for all tests
    /// Priority: 1) Maximize atoms on plane, 2) Maximize bonds on plane
    /// </summary>
    private Vector3 FindOptimalPlaneNormal(List<Vector3> candidates)
    {
        float tempThreshold = moleculeRadius * depthThresholdFactor;
        
        Vector3 bestNormal = candidates[0];
        int bestAtomCount = -1;
        int bestBondCount = -1;
        
        foreach (var candidate in candidates)
        {
            int atomCount = 0;
            int bondCount = 0;
            
            // Count atoms ON the plane (within threshold from plane)
            // The plane goes through centroid with this normal
            // CRITICAL: Only count planeFittingPositions (C-atoms when available)
            foreach (var pos in planeFittingPositions)
            {
                float distanceToPlane = Mathf.Abs(Vector3.Dot(pos - centroid, candidate));
                if (distanceToPlane < tempThreshold)
                {
                    atomCount++;
                }
            }
            
            // Count bonds where BOTH atoms are on the plane
            foreach (var bond in currentMolecule.bonds)
            {
                Vector3 posA = atomWorldPositions[bond.atomA_ID];
                Vector3 posB = atomWorldPositions[bond.atomB_ID];
                
                float distA = Mathf.Abs(Vector3.Dot(posA - centroid, candidate));
                float distB = Mathf.Abs(Vector3.Dot(posB - centroid, candidate));
                
                if (distA < tempThreshold && distB < tempThreshold)
                {
                    bondCount++;
                }
            }
            
            // Select plane with MOST atoms on it (primary criterion)
            // If tied, use most bonds on plane (secondary criterion)
            if (atomCount > bestAtomCount || (atomCount == bestAtomCount && bondCount > bestBondCount))
            {
                bestAtomCount = atomCount;
                bestBondCount = bondCount;
                bestNormal = candidate;
            }
        }
        
        Debug.Log($"[PlaneAlignment] Best plane: {bestNormal} with {bestAtomCount} atoms and {bestBondCount} bonds on plane (threshold: {tempThreshold:F4})");
        return bestNormal;
    }
    
    /// <summary>
    /// Zählt wie viele Bindungen in der Ebene liegen (beide Atome nahe der Ebene)
    /// Improved: Also considers atom count and weighted scoring
    /// </summary>
    private int CountInPlaneBonds(Vector3 normal, float threshold)
    {
        int bondScore = 0;
        int atomScore = 0;
        Vector3 testPlanePoint = centroid;
        
        // Count atoms in plane
        for (int i = 0; i < atomWorldPositions.Count; i++)
        {
            Vector3 pos = atomWorldPositions[i];
            float d = Vector3.Dot(pos - testPlanePoint, normal);
            
            if (Mathf.Abs(d) < threshold)
            {
                atomScore++;
            }
        }
        
        // Count bonds with both atoms in plane
        foreach (var bond in currentMolecule.bonds)
        {
            Vector3 posA = atomWorldPositions[bond.atomA_ID];
            Vector3 posB = atomWorldPositions[bond.atomB_ID];
            
            float dA = Vector3.Dot(posA - testPlanePoint, normal);
            float dB = Vector3.Dot(posB - testPlanePoint, normal);
            
            // Both atoms close to plane = in-plane bond
            if (Mathf.Abs(dA) < threshold && Mathf.Abs(dB) < threshold)
            {
                bondScore++;
            }
        }
        
        // Weighted score: bonds are more important than isolated atoms
        // Priority: maximize in-plane bonds, then maximize in-plane atoms
        return bondScore * 100 + atomScore;
    }

    /// <summary>
    /// Richtet das Molekül so aus, dass die PCA-Normale zur Ziel-Normale zeigt
    /// Der Anchor bleibt dabei an derselben Position
    /// </summary>
    private void AlignMoleculeToTarget(Vector3 targetNormal)
    {
        // Anchor-Position vor Rotation merken
        Vector3 anchorBefore = GetAtomWorldPosition(anchorAtomIndex);

        // Rotation berechnen: PCA-Normal → Target-Normal
        Quaternion alignRotation = Quaternion.FromToRotation(pcaNormal, targetNormal);

        // Molekül rotieren
        renderer.transform.rotation = alignRotation * renderer.transform.rotation;

        // Anchor-Position nach Rotation
        Vector3 anchorAfter = GetAtomWorldPosition(anchorAtomIndex);

        // Molekül verschieben damit Anchor wieder an ursprünglicher Position ist
        renderer.transform.position += (anchorBefore - anchorAfter);

        // World-Positionen aktualisieren
        UpdateAtomWorldPositions();
    }

    /// <summary>
    /// Fixes the plane in world space at the centroid with the calculated normal
    /// IMPORTANT: Plane ALWAYS goes through centroid (average of C-atoms)
    /// </summary>
    private void FixPlane()
    {
        // CRITICAL: Plane is positioned at centroid (rotation center)
        // This ensures the plane goes through the average position of carbon atoms
        planePoint = centroid;
        fixedNormal = planeNormal.normalized;
        
        Debug.Log($"[PlaneAlignment] Plane fixed through centroid: {planePoint}, Normal: {fixedNormal}");
        Debug.Log($"[PlaneAlignment] Rotation center = Centroid = Average of C-atoms: {centroid}");
    }

    /// <summary>
    /// Berechnet den Depth-Threshold basierend auf Molekülgröße
    /// </summary>
    private void CalculateDepthThreshold()
    {
        depthThreshold = moleculeRadius * depthThresholdFactor;
    }

    /// <summary>
    /// Aktualisiert die Welt-Positionen aller Atome
    /// </summary>
    private void UpdateAtomWorldPositions()
    {
        atomWorldPositions.Clear();
        foreach (var atom in currentMolecule.atoms)
        {
            Vector3 worldPos = renderer.transform.TransformPoint(atom.position * renderer.angstromToMeter * renderer.bondLengthMultiplier);
            atomWorldPositions.Add(worldPos);
        }
    }

    /// <summary>
    /// Gibt die Welt-Position eines Atoms zurück
    /// </summary>
    private Vector3 GetAtomWorldPosition(int atomIndex)
    {
        var atom = currentMolecule.atoms[atomIndex];
        return renderer.transform.TransformPoint(atom.position * renderer.angstromToMeter * renderer.bondLengthMultiplier);
    }

    /// <summary>
    /// Rotiert das Molekül um den Anchor-Punkt
    /// </summary>
    public void RotateAroundAnchor(Quaternion deltaRotation)
    {
        if (anchorAtomIndex < 0) return;

        Vector3 anchorPos = GetAtomWorldPosition(anchorAtomIndex);

        // Rotation um Anchor-Punkt anwenden
        Vector3 toMolecule = renderer.transform.position - anchorPos;
        toMolecule = deltaRotation * toMolecule;

        renderer.transform.position = anchorPos + toMolecule;
        renderer.transform.rotation = deltaRotation * renderer.transform.rotation;

        UpdateAtomWorldPositions();
    }

    /// <summary>
    /// Rotiert das Molekül um den Anchor mit Euler-Winkeln
    /// </summary>
    public void RotateAroundAnchorEuler(float deltaX, float deltaY)
    {
        if (anchorAtomIndex < 0) return;

        Vector3 anchorPos = GetAtomWorldPosition(anchorAtomIndex);

        // Rotationen um Anchor
        renderer.transform.RotateAround(anchorPos, Vector3.up, deltaY);
        renderer.transform.RotateAround(anchorPos, Vector3.right, deltaX);

        UpdateAtomWorldPositions();
    }

    /// <summary>
    /// Klassifiziert einen Bond basierend auf Position relativ zur fixierten Ebene
    /// </summary>
    public BondStereo ClassifyBond(BondData bond)
    {
        if (anchorAtomIndex < 0)
        {
            Debug.LogWarning($"[PlaneAlignment] ClassifyBond called but anchorAtomIndex < 0");
            return BondStereo.None;
        }

        // Atom-Positionen holen
        Vector3 posA = GetAtomWorldPosition(bond.atomA_ID);
        Vector3 posB = GetAtomWorldPosition(bond.atomB_ID);

        // Calculate bond vector and length
        Vector3 bondVector = posB - posA;
        float bondLength = bondVector.magnitude;
        
        // Calculate signed distances to plane
        // Positive = behind plane, Negative = in front of plane
        float dA = Vector3.Dot(posA - planePoint, fixedNormal);
        float dB = Vector3.Dot(posB - planePoint, fixedNormal);

        // CRITICAL: Check if plane intersects middle 20% of bond
        // Calculate where plane intersects the bond (parametric t along bond)
        // If dA and dB have opposite signs, bond crosses plane
        bool bondCrossesPlane = (dA * dB) < 0;
        
        if (bondCrossesPlane)
        {
            // Calculate intersection point: t = dA / (dA - dB)
            // t=0 is posA, t=1 is posB, t=0.5 is middle
            float t = Mathf.Abs(dA) / (Mathf.Abs(dA) + Mathf.Abs(dB));
            
            // Check if intersection is in middle 50% (0.25 to 0.75)
            bool intersectsMiddle50 = (t >= 0.25f && t <= 0.75f);
            
            if (intersectsMiddle50)
            {
                // Plane cuts through middle 50% → Normal bond
                Debug.Log($"[PlaneAlignment] Bond {bond.atomA_ID}->{bond.atomB_ID}: dA={dA:F4}, dB={dB:F4}, t={t:F2} → None (plane cuts middle 50%)");
                return BondStereo.None;
            }
            else if (t < 0.25f)
            {
                // Plane cuts near atomA → Use atomB's side
                if (dB < 0)
                {
                    Debug.Log($"[PlaneAlignment] Bond {bond.atomA_ID}->{bond.atomB_ID}: dA={dA:F4}, dB={dB:F4}, t={t:F2} → UP (cuts near A, B in front)");
                    return BondStereo.Up;
                }
                else
                {
                    Debug.Log($"[PlaneAlignment] Bond {bond.atomA_ID}->{bond.atomB_ID}: dA={dA:F4}, dB={dB:F4}, t={t:F2} → DOWN (cuts near A, B behind)");
                    return BondStereo.Down;
                }
            }
            else // t > 0.75
            {
                // Plane cuts near atomB → Use atomA's side
                if (dA < 0)
                {
                    Debug.Log($"[PlaneAlignment] Bond {bond.atomA_ID}->{bond.atomB_ID}: dA={dA:F4}, dB={dB:F4}, t={t:F2} → UP (cuts near B, A in front)");
                    return BondStereo.Up;
                }
                else
                {
                    Debug.Log($"[PlaneAlignment] Bond {bond.atomA_ID}->{bond.atomB_ID}: dA={dA:F4}, dB={dB:F4}, t={t:F2} → DOWN (cuts near B, A behind)");
                    return BondStereo.Down;
                }
            }
        }
        
        // Bond doesn't cross plane - both atoms on same side
        // Use average depth to classify
        float avgDepth = (dA + dB) / 2f;
        
        if (avgDepth < 0)
        {
            // Both in front → Wedge
            Debug.Log($"[PlaneAlignment] Bond {bond.atomA_ID}->{bond.atomB_ID}: dA={dA:F4}, dB={dB:F4} → UP (both in front)");
            return BondStereo.Up;
        }
        else
        {
            // Both behind → Dashed
            Debug.Log($"[PlaneAlignment] Bond {bond.atomA_ID}->{bond.atomB_ID}: dA={dA:F4}, dB={dB:F4} → DOWN (both behind)");
            return BondStereo.Down;
        }
    }

    /// <summary>
    /// Berechnet den Abstand eines Atoms zur Ebene
    /// </summary>
    public float GetAtomDepth(int atomIndex)
    {
        Vector3 pos = GetAtomWorldPosition(atomIndex);
        return Vector3.Dot(pos - planePoint, fixedNormal);
    }

    void OnDrawGizmos()
    {
        if (!showDebugPlane || anchorAtomIndex < 0) return;

        // Ebene visualisieren
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

        Vector3 right = Vector3.Cross(fixedNormal, Vector3.up).normalized;
        if (right.magnitude < 0.1f) right = Vector3.Cross(fixedNormal, Vector3.forward).normalized;
        Vector3 up = Vector3.Cross(right, fixedNormal).normalized;

        float size = moleculeRadius * 2f;

        Vector3 p1 = planePoint + right * size + up * size;
        Vector3 p2 = planePoint - right * size + up * size;
        Vector3 p3 = planePoint - right * size - up * size;
        Vector3 p4 = planePoint + right * size - up * size;

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);

        // Normale visualisieren
        Gizmos.color = Color.green;
        Gizmos.DrawRay(planePoint, fixedNormal * moleculeRadius);

        // Anchor-Atom markieren
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAtomWorldPosition(anchorAtomIndex), 0.05f);
    }
    
    /// <summary>
    /// Toggle plane visibility at runtime for debugging
    /// </summary>
    private void TogglePlaneVisibility()
    {
        if (planeVisual != null && planeRenderer != null)
        {
            planeRenderer.enabled = !planeRenderer.enabled;
            Debug.Log($"[PlaneAlignment] Plane visibility toggled: {planeRenderer.enabled}");
        }
    }
    
    /// <summary>
    /// Log all renderer states in the scene for debugging grey flashes
    /// </summary>
    private void LogRendererStates()
    {
        Debug.Log("=== RENDERER DEBUG INFO ===");
        
        // Plane renderer
        if (planeRenderer != null)
        {
            Debug.Log($"[PlaneRenderer] enabled={planeRenderer.enabled}, " +
                     $"visible={planeRenderer.isVisible}, " +
                     $"material={planeRenderer.sharedMaterial?.name}, " +
                     $"shader={planeRenderer.sharedMaterial?.shader.name}, " +
                     $"color={planeRenderer.sharedMaterial?.color}, " +
                     $"renderQueue={planeRenderer.sharedMaterial?.renderQueue}");
        }
        
        // Molecule renderers
        if (renderer != null)
        {
            Renderer[] moleculeRenderers = renderer.GetComponentsInChildren<Renderer>();
            Debug.Log($"[MoleculeRenderers] Found {moleculeRenderers.Length} renderers");
            
            int visibleCount = 0;
            int greyCount = 0;
            foreach (Renderer r in moleculeRenderers)
            {
                if (r.enabled && r.isVisible)
                {
                    visibleCount++;
                    
                    // Check for grey materials
                    Material mat = r.sharedMaterial;
                    if (mat != null)
                    {
                        Color c = mat.color;
                        if (Mathf.Abs(c.r - c.g) < 0.1f && Mathf.Abs(c.g - c.b) < 0.1f && c.r < 0.6f && c.r > 0.4f)
                        {
                            greyCount++;
                            Debug.LogWarning($"[GreyMaterial] {r.gameObject.name}: shader={mat.shader.name}, color={c}");
                        }
                    }
                }
            }
            Debug.Log($"[MoleculeRenderers] Visible: {visibleCount}, Grey materials: {greyCount}");
        }
        
        // All renderers in scene
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        Debug.Log($"[AllRenderers] Total in scene: {allRenderers.Length}");
    }
}

/// <summary>
/// Einfache 3x3 Matrix für Kovarianz-Berechnung
/// </summary>
public struct Matrix3x3
{
    public float m00, m01, m02;
    public float m10, m11, m12;
    public float m20, m21, m22;
    
    /// <summary>
    /// Multiplies this matrix by a vector
    /// </summary>
    public Vector3 MultiplyVector(Vector3 v)
    {
        return new Vector3(
            m00 * v.x + m01 * v.y + m02 * v.z,
            m10 * v.x + m11 * v.y + m12 * v.z,
            m20 * v.x + m21 * v.y + m22 * v.z
        );
    }
}