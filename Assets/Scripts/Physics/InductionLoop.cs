using UnityEngine;

/// <summary>
/// Fallende Leiterschleife — Elektromagnetische Induktion & Lenzsche Regel.
///
/// Physik (analytisch, 1D vertikal):
///   EMF   = -dΦ/dt = B · w · |v|       (nur bei Flussänderung)
///   I_ind = EMF / R                      (Spalt → R = ∞ → I = 0)
///   F_L   = B · I · w = B²·w²·|v|/R    (Lenz: bremst entgegen Bewegung)
///
/// Phasen:
///   ABOVE        → kein Fluss, freier Fall
///   ENTERING     → Fluss ↑, Strom CW (Lenz), Bremskraft ↑
///   FULLY_INSIDE → dΦ/dt = 0, freier Fall im Feld
///   EXITING      → Fluss ↓, Strom CCW, Bremskraft ↑
///   BELOW        → kein Fluss
///
/// Spalt-Modus: Stromkreis offen → kein Induktionsstrom → freier Fall
/// </summary>
public class InductionLoop : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Schleife")]
    public float loopWidth  = 0.15f;
    public float loopHeight = 0.12f;
    public float wireRadius = 0.003f;
    public float resistance = 0.5f;
    public float loopMass   = 0.02f;
    public float gravity    = 3f;

    [Header("Spalt (Isolator)")]
    public bool  slitOpen    = false;
    public float slitGapSize = 0.012f;

    [Header("Referenzen")]
    public MagneticFieldVolume fieldVolume;

    [Header("Darstellung")]
    public int   currentArrowCount = 10;
    public float currentArrowScale = 0.006f;
    public float arrowFlowSpeed    = 0.4f;
    public float forceArrowLength  = 0.1f;

    // ════════════════════════════════════════════════════════════
    //  Zustand (Read Only)
    // ════════════════════════════════════════════════════════════

    [Header("Zustand")]
    [SerializeField] private float   velocity;
    [SerializeField] private float   inducedCurrent;
    [SerializeField] private Vector3 brakingForce;
    [SerializeField] private LoopPhase phase = LoopPhase.Idle;

    public enum LoopPhase { Idle, Above, Entering, FullyInside, Exiting, Below }

    public float     Velocity       => velocity;
    public float     InducedCurrent => inducedCurrent;
    public Vector3   BrakingForce   => brakingForce;
    public LoopPhase CurrentPhase   => phase;
    public bool      IsSlitOpen     => slitOpen;

    // ════════════════════════════════════════════════════════════
    //  Intern
    // ════════════════════════════════════════════════════════════

    private Vector3 startLocalPos;
    private bool    falling;
    private int     currentFlowSign; // +1=CW(+Z view), -1=CCW, 0=keiner

    // Visuals
    private Material wireMat, arrowMat, forceArrowMat, slitMat;
    private MeshRenderer[] edgeMRs = new MeshRenderer[4];
    private Transform[]    arrowTFs;
    private Transform      forceArrowTF;
    private Transform      slitMarkerTF;

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    [ContextMenu("▼ Drop")]
    public void Drop()
    {
        if (falling) return;
        falling  = true;
        velocity = 0f;
        phase    = LoopPhase.Above;
    }

    [ContextMenu("↺ Reset")]
    public void ResetLoop()
    {
        falling         = false;
        velocity        = 0f;
        inducedCurrent  = 0f;
        brakingForce    = Vector3.zero;
        currentFlowSign = 0;
        phase           = LoopPhase.Idle;
        transform.localPosition = startLocalPos;
    }

    [ContextMenu("🔌 Spalt Toggle")]
    public void ToggleSlit()      { slitOpen = !slitOpen; UpdateSlitVisual(); }
    public void SetSlit(bool open) { slitOpen = open;      UpdateSlitVisual(); }

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        startLocalPos = transform.localPosition;
        BuildVisuals();
    }

    void FixedUpdate()
    {
        if (!falling) return;
        float dt = Time.fixedDeltaTime;

        // Feld-Grenzen (Welt-Y)
        float fTop, fBot;
        FieldBoundsY(out fTop, out fBot);

        // Schleifen-Grenzen (Welt-Y)
        float ly   = transform.position.y;
        float lTop = ly + loopHeight * 0.5f;
        float lBot = ly - loopHeight * 0.5f;

        // ── Phase bestimmen ──
        if (lBot >= fTop)
        {
            phase = LoopPhase.Above;
            currentFlowSign = 0;
        }
        else if (lBot < fTop && lTop > fTop)
        {
            phase = LoopPhase.Entering;
            currentFlowSign = 1; // CW — Lenz: Fluss ↑ → Gegenstrom
        }
        else if (lTop <= fTop && lBot >= fBot)
        {
            phase = LoopPhase.FullyInside;
            currentFlowSign = 0;
        }
        else if (lBot < fBot && lTop > fBot && lTop <= fTop)
        {
            phase = LoopPhase.Exiting;
            currentFlowSign = -1; // CCW — Lenz: Fluss ↓ → Strom umkehrt
        }
        else
        {
            phase = LoopPhase.Below;
            currentFlowSign = 0;
            falling = false;
        }

        // ── Physik ──
        float B    = fieldVolume != null ? fieldVolume.fieldStrength : 0f;
        float w    = loopWidth;
        bool  edge = (phase == LoopPhase.Entering || phase == LoopPhase.Exiting);

        if (edge && !slitOpen && B > 0.001f)
        {
            float emf    = B * w * Mathf.Abs(velocity);
            inducedCurrent = emf / resistance;
            float Fmag   = B * inducedCurrent * w;          // = B²w²|v|/R
            brakingForce = new Vector3(0f, velocity < 0f ? Fmag : -Fmag, 0f);
        }
        else
        {
            inducedCurrent = 0f;
            brakingForce   = Vector3.zero;
        }

        // Spalt → kein Strom
        if (slitOpen)
        {
            inducedCurrent = 0f;
            brakingForce   = Vector3.zero;
            currentFlowSign = 0;
        }

        // ── Integration (symplektisches Euler) ──
        float a = -gravity + brakingForce.y / loopMass;
        velocity += a * dt;

        Vector3 lp = transform.localPosition;
        lp.y += velocity * dt;
        transform.localPosition = lp;
    }

    void LateUpdate()
    {
        UpdateCurrentArrows();
        UpdateForceArrow();
        UpdateWireColor();
    }

    // ════════════════════════════════════════════════════════════
    //  Feld-Grenzen
    // ════════════════════════════════════════════════════════════

    private void FieldBoundsY(out float top, out float bot)
    {
        if (fieldVolume != null)
        {
            float cy = fieldVolume.transform.position.y;
            float hy = fieldVolume.volumeSize.y * 0.5f;
            top = cy + hy;
            bot = cy - hy;
        }
        else { top = 0f; bot = -1f; }
    }

    // ════════════════════════════════════════════════════════════
    //  Visuals — Aufbau
    // ════════════════════════════════════════════════════════════

    private void BuildVisuals()
    {
        Shader sh = Shader.Find("Custom/MoleculeUnlit")
                 ?? Shader.Find("Unlit/Color")
                 ?? Shader.Find("Standard");

        wireMat       = new Material(sh) { color = new Color(0.7f, 0.7f, 0.75f) };
        arrowMat      = new Material(sh) { color = new Color(1f, 0.85f, 0.1f) };
        forceArrowMat = new Material(sh) { color = new Color(1f, 0.2f, 0.2f) };
        slitMat       = new Material(sh) { color = new Color(0.35f, 0.1f, 0.1f) };

        BuildEdges();
        BuildCurrentArrows();
        BuildForceArrow();
        BuildSlitMarker();
    }

    // ── Kanten-Rahmen ──

    private void BuildEdges()
    {
        float hw = loopWidth  * 0.5f;
        float hh = loopHeight * 0.5f;

        edgeMRs[0] = BuildEdge("Kante_Oben",   new Vector3(0f, hh, 0f),  90f, loopWidth);
        edgeMRs[1] = BuildEdge("Kante_Rechts",  new Vector3(hw, 0f, 0f),   0f, loopHeight);
        edgeMRs[2] = BuildEdge("Kante_Unten",   new Vector3(0f, -hh, 0f), 90f, loopWidth);
        edgeMRs[3] = BuildEdge("Kante_Links",   new Vector3(-hw, 0f, 0f),  0f, loopHeight);
    }

    private MeshRenderer BuildEdge(string name, Vector3 pos, float zRot, float length)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
        go.transform.localScale    = new Vector3(wireRadius * 2f, length * 0.5f, wireRadius * 2f);

        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = wireMat;
        return mr;
    }

    // ── Strom-Pfeile (animiert entlang des Umfangs) ──

    private void BuildCurrentArrows()
    {
        Mesh arrowMesh = VectorArrowDisplay.GetStaticArrowMesh();
        arrowTFs = new Transform[currentArrowCount];

        for (int i = 0; i < currentArrowCount; i++)
        {
            var go = new GameObject("StromPfeil_" + i);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = arrowMat;
            go.transform.localScale = Vector3.one * currentArrowScale;
            go.SetActive(false);
            arrowTFs[i] = go.transform;
        }
    }

    // ── Kraft-Pfeil ──

    private void BuildForceArrow()
    {
        Mesh arrowMesh = VectorArrowDisplay.GetStaticArrowMesh();

        var go = new GameObject("Kraft_FL");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = forceArrowMat;
        go.SetActive(false);
        forceArrowTF = go.transform;
    }

    // ── Spalt-Markierung ──

    private void BuildSlitMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Spalt_Marker";
        go.transform.SetParent(transform, false);

        float hw = loopWidth * 0.5f;
        float hh = loopHeight * 0.5f;
        go.transform.localPosition = new Vector3(-hw, -hh, 0f);
        go.transform.localScale    = Vector3.one * slitGapSize;

        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        go.GetComponent<MeshRenderer>().sharedMaterial = slitMat;
        go.SetActive(false);
        slitMarkerTF = go.transform;
    }

    // ════════════════════════════════════════════════════════════
    //  Visuals — Update
    // ════════════════════════════════════════════════════════════

    private void UpdateCurrentArrows()
    {
        bool show = currentFlowSign != 0;

        for (int i = 0; i < currentArrowCount; i++)
        {
            if (!show)
            {
                arrowTFs[i].gameObject.SetActive(false);
                continue;
            }
            arrowTFs[i].gameObject.SetActive(true);

            // Parameter entlang des Umfangs (scrollend)
            float t = Mathf.Repeat(
                (float)i / currentArrowCount + Time.time * arrowFlowSpeed * currentFlowSign,
                1f
            );

            arrowTFs[i].localPosition = PerimPos(t);

            Vector3 dir = PerimDir(t) * currentFlowSign;
            arrowTFs[i].localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        }
    }

    private void UpdateForceArrow()
    {
        float fMag = brakingForce.magnitude;
        bool show  = fMag > 0.001f;
        forceArrowTF.gameObject.SetActive(show);

        if (show)
        {
            // Pfeil zeigt nach oben (Bremskraft)
            forceArrowTF.localRotation = Quaternion.identity;
            float s = Mathf.Clamp(fMag * 10f, 0.03f, forceArrowLength);
            forceArrowTF.localScale = new Vector3(s * 0.4f, s, s * 0.4f);
            forceArrowTF.localPosition = new Vector3(loopWidth * 0.35f, 0f, 0f);
        }
    }

    private void UpdateWireColor()
    {
        if (wireMat == null) return;
        wireMat.color = (currentFlowSign != 0)
            ? new Color(1f, 0.7f, 0.3f)      // warm orange (Strom fließt)
            : new Color(0.7f, 0.7f, 0.75f);  // silber (kein Strom)
    }

    private void UpdateSlitVisual()
    {
        if (slitMarkerTF != null) slitMarkerTF.gameObject.SetActive(slitOpen);
    }

    // ════════════════════════════════════════════════════════════
    //  Umfangs-Parameterisierung
    // ════════════════════════════════════════════════════════════

    /// <summary>Position auf dem Rahmen-Umfang (t ∈ [0,1)), CW von oben-links.</summary>
    private Vector3 PerimPos(float t)
    {
        float hw = loopWidth * 0.5f;
        float hh = loopHeight * 0.5f;
        float perim = 2f * (loopWidth + loopHeight);
        float d = Mathf.Repeat(t, 1f) * perim;

        if (d < loopWidth)
            return new Vector3(-hw + d, hh, 0f);
        d -= loopWidth;
        if (d < loopHeight)
            return new Vector3(hw, hh - d, 0f);
        d -= loopHeight;
        if (d < loopWidth)
            return new Vector3(hw - d, -hh, 0f);
        d -= loopWidth;
        return new Vector3(-hw, -hh + d, 0f);
    }

    /// <summary>Tangente (CW-Richtung) an Position t.</summary>
    private Vector3 PerimDir(float t)
    {
        float perim = 2f * (loopWidth + loopHeight);
        float d = Mathf.Repeat(t, 1f) * perim;

        if (d < loopWidth)  return Vector3.right;
        d -= loopWidth;
        if (d < loopHeight) return Vector3.down;
        d -= loopHeight;
        if (d < loopWidth)  return Vector3.left;
        return Vector3.up;
    }

    // ════════════════════════════════════════════════════════════
    //  Cleanup
    // ════════════════════════════════════════════════════════════

    void OnDestroy()
    {
        if (wireMat)       Destroy(wireMat);
        if (arrowMat)      Destroy(arrowMat);
        if (forceArrowMat) Destroy(forceArrowMat);
        if (slitMat)       Destroy(slitMat);
    }
}
