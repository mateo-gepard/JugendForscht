using UnityEngine;

/// <summary>
/// Zeigt drei farbige 3D-Pfeile direkt am fliegenden Teilchen:
///   • Geschwindigkeit v  (grün)
///   • Magnetfeld B       (blau/cyan)
///   • Lorentzkraft F_L   (gelb/orange)
///
/// Die Pfeile drehen und skalieren sich in Echtzeit physikalisch
/// korrekt mit dem Teilchen mit. Der F_L-Pfeil kann für den
/// Quiz-Modus ausgeblendet werden.
///
/// Alle Geometrie wird prozedural erzeugt (kein Prefab nötig).
/// </summary>
public class VectorArrowDisplay : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Referenzen")]
    [Tooltip("Wird automatisch am selben GameObject oder Parent gesucht")]
    public ChargedParticle particle;

    [Tooltip("Wird automatisch gesucht, wenn leer")]
    public MagneticFieldVolume fieldVolume;

    [Header("Darstellung")]
    [Tooltip("Basis-Länge der Pfeile (in Metern)")]
    public float arrowLength = 0.1f;

    [Tooltip("Dicke der Pfeile (Skalierung)")]
    public float arrowThickness = 0.012f;

    [Tooltip("Mindestlänge: Pfeile unter diesem Wert werden ausgeblendet")]
    public float minVisibleLength = 0.01f;

    [Header("Farben")]
    public Color velocityColor = new Color(0.2f, 0.9f, 0.3f, 1f);     // Grün
    public Color magneticFieldColor = new Color(0.3f, 0.7f, 1f, 1f);   // Cyan
    public Color forceColor = new Color(1f, 0.75f, 0.1f, 1f);          // Gelb/Orange

    // ════════════════════════════════════════════════════════════
    //  Zustand
    // ════════════════════════════════════════════════════════════

    private bool showForceArrow = true;

    /// <summary>Ist der F_L-Pfeil sichtbar? (Quiz-Modus = false)</summary>
    public bool ShowForceArrow
    {
        get => showForceArrow;
        set
        {
            showForceArrow = value;
            if (forceArrow != null) forceArrow.SetActive(value);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private GameObject velocityArrow;
    private GameObject fieldArrow;
    private GameObject forceArrow;

    private Material matVelocity;
    private Material matField;
    private Material matForce;

    // Tipp-Indikatoren für Drei-Finger-Regel
    private GameObject tipV, tipB, tipF;
    private Material tipMaterial;
    private float tipRadius = 0.015f;

    private static Mesh s_SharedArrowMesh;
    private static Mesh s_SharedSphereMesh;

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        if (particle == null)
            particle = GetComponent<ChargedParticle>() ?? GetComponentInParent<ChargedParticle>();
        if (fieldVolume == null)
            fieldVolume = FindObjectOfType<MagneticFieldVolume>();

        BuildArrows();
    }

    void LateUpdate()
    {
        if (particle == null) return;

        // Position: immer am Teilchen
        transform.position = particle.transform.position;

        UpdateArrow(velocityArrow, particle.Velocity, velocityColor);
        UpdateArrow(fieldArrow, fieldVolume != null ? fieldVolume.GetWorldFieldVector() : Vector3.zero, magneticFieldColor);

        if (showForceArrow)
            UpdateArrow(forceArrow, particle.CurrentForce, forceColor);

        // Tipp-Indikatoren an Pfeilspitzen positionieren
        PositionTipAt(tipV, velocityArrow);
        PositionTipAt(tipB, fieldArrow);
        PositionTipAt(tipF, forceArrow);
    }

    void OnDestroy()
    {
        if (matVelocity != null) Destroy(matVelocity);
        if (matField != null) Destroy(matField);
        if (matForce != null) Destroy(matForce);
        if (tipMaterial != null) Destroy(tipMaterial);
    }

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Toggle für den Quiz-Modus: blendet den Kraftpfeil ein/aus.
    /// </summary>
    [ContextMenu("Toggle Kraft-Pfeil (Quiz)")]
    public void ToggleForceArrow()
    {
        ShowForceArrow = !ShowForceArrow;
    }

    /// <summary>
    /// Alle Pfeile ein-/ausblenden.
    /// </summary>
    public void SetAllVisible(bool visible)
    {
        if (velocityArrow != null) velocityArrow.SetActive(visible);
        if (fieldArrow != null) fieldArrow.SetActive(visible);
        if (forceArrow != null) forceArrow.SetActive(visible && showForceArrow);
    }

    /// <summary>
    /// Zeigt/verbirgt farbige Kugeln an den Pfeilspitzen.
    /// Für die Drei-Finger-Regel: grün = richtig, rot = falsch.
    /// </summary>
    public void SetTipIndicators(bool show, bool allCorrect)
    {
        EnsureTipIndicators();

        Color c = allCorrect
            ? new Color(0.1f, 1f, 0.2f, 1f)
            : new Color(1f, 0.15f, 0.15f, 1f);

        if (tipMaterial != null)
            tipMaterial.color = c;

        if (tipV != null) tipV.SetActive(show);
        if (tipB != null) tipB.SetActive(show);
        if (tipF != null) tipF.SetActive(show && showForceArrow);
    }

    // ════════════════════════════════════════════════════════════
    //  Pfeil-Erzeugung
    // ════════════════════════════════════════════════════════════

    private void BuildArrows()
    {
        matVelocity = CreateArrowMaterial(velocityColor);
        matField = CreateArrowMaterial(magneticFieldColor);
        matForce = CreateArrowMaterial(forceColor);

        velocityArrow = CreateArrowObject("Pfeil_v (Geschwindigkeit)", matVelocity);
        fieldArrow = CreateArrowObject("Pfeil_B (Magnetfeld)", matField);
        forceArrow = CreateArrowObject("Pfeil_F (Lorentzkraft)", matForce);
    }

    private GameObject CreateArrowObject(string name, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = GetSharedArrowMesh();

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return obj;
    }

    private Material CreateArrowMaterial(Color color)
    {
        Shader shader = Shader.Find("Custom/MoleculeUnlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        mat.enableInstancing = true;
        return mat;
    }

    // ════════════════════════════════════════════════════════════
    //  Pfeil-Update
    // ════════════════════════════════════════════════════════════

    private void UpdateArrow(GameObject arrow, Vector3 worldVector, Color color)
    {
        if (arrow == null) return;

        float magnitude = worldVector.magnitude;
        if (magnitude < minVisibleLength)
        {
            arrow.SetActive(false);
            return;
        }

        arrow.SetActive(true);

        // Richtung → Rotation (Pfeil-Mesh zeigt entlang +Z)
        arrow.transform.rotation = Quaternion.LookRotation(worldVector.normalized);

        // Skalierung: Länge proportional zum Betrag, Dicke konstant
        float length = arrowLength * Mathf.Clamp(magnitude, 0.1f, 10f);
        arrow.transform.localScale = new Vector3(arrowThickness, arrowThickness, length);
    }

    // ════════════════════════════════════════════════════════════
    //  Tipp-Indikatoren (Drei-Finger-Regel)
    // ════════════════════════════════════════════════════════════

    private void EnsureTipIndicators()
    {
        if (tipV != null) return;

        tipMaterial = CreateArrowMaterial(Color.red);

        tipV = CreateTipSphere("TipIndicator_v");
        tipB = CreateTipSphere("TipIndicator_B");
        tipF = CreateTipSphere("TipIndicator_F");

        tipV.SetActive(false);
        tipB.SetActive(false);
        tipF.SetActive(false);
    }

    private GameObject CreateTipSphere(string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = GetSharedSphereMesh();

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = tipMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        float d = tipRadius * 2f;
        obj.transform.localScale = new Vector3(d, d, d);
        return obj;
    }

    private void PositionTipAt(GameObject tip, GameObject arrow)
    {
        if (tip == null || arrow == null || !tip.activeSelf) return;
        if (!arrow.activeSelf) { tip.SetActive(false); return; }

        // Pfeilspitze ist bei z=1 im lokalen Mesh, skaliert durch localScale.z
        tip.transform.position = arrow.transform.position
            + arrow.transform.forward * arrow.transform.localScale.z;
    }

    // ════════════════════════════════════════════════════════════
    //  Shared Mesh (entlang +Z, Einheitslänge 1)
    // ════════════════════════════════════════════════════════════

    private static Mesh GetSharedArrowMesh()
    {
        if (s_SharedArrowMesh != null) return s_SharedArrowMesh;

        // Gleiche Geometrie wie MagneticFieldVolume, aber eigene
        // statische Referenz um Abhängigkeiten zu vermeiden.
        int sides = 6;
        float shaftR = 0.3f;   // relativ, wird über localScale skaliert
        float shaftLen = 0.6f;
        float headR = 0.7f;
        float headLen = 0.4f;

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        // Schaft
        for (int i = 0; i < sides; i++)
        {
            float a = (2f * Mathf.PI * i) / sides;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts.Add(new Vector3(c * shaftR, s * shaftR, 0f));
            verts.Add(new Vector3(c * shaftR, s * shaftR, shaftLen));
        }
        for (int i = 0; i < sides; i++)
        {
            int b = i * 2, n = ((i + 1) % sides) * 2;
            tris.Add(b); tris.Add(n); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(n); tris.Add(n + 1);
        }

        // Kopf
        int baseIdx = verts.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = (2f * Mathf.PI * i) / sides;
            verts.Add(new Vector3(Mathf.Cos(a) * headR, Mathf.Sin(a) * headR, shaftLen));
        }
        int tip = verts.Count;
        verts.Add(new Vector3(0f, 0f, 1f)); // Spitze bei z=1 (Einheitslänge)

        for (int i = 0; i < sides; i++)
        {
            tris.Add(baseIdx + i);
            tris.Add(baseIdx + (i + 1) % sides);
            tris.Add(tip);
        }

        s_SharedArrowMesh = new Mesh();
        s_SharedArrowMesh.name = "VectorArrowMesh";
        s_SharedArrowMesh.SetVertices(verts);
        s_SharedArrowMesh.SetTriangles(tris, 0);
        s_SharedArrowMesh.RecalculateNormals();
        s_SharedArrowMesh.RecalculateBounds();
        return s_SharedArrowMesh;
    }

    private static Mesh GetSharedSphereMesh()
    {
        if (s_SharedSphereMesh != null) return s_SharedSphereMesh;

        // Einfache Icosphere (42 Vertices) für Tipp-Indikatoren
        // Identisch zum Projekt-Standard (LowPolyMeshes)
        int rec = 1;
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] iv = {
            new Vector3(-1, t,0).normalized, new Vector3(1, t,0).normalized,
            new Vector3(-1,-t,0).normalized, new Vector3(1,-t,0).normalized,
            new Vector3(0,-1, t).normalized, new Vector3(0, 1, t).normalized,
            new Vector3(0,-1,-t).normalized, new Vector3(0, 1,-t).normalized,
            new Vector3( t,0,-1).normalized, new Vector3( t,0, 1).normalized,
            new Vector3(-t,0,-1).normalized, new Vector3(-t,0, 1).normalized
        };
        verts.AddRange(iv);
        int[][] faces = {
            new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
            new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
            new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
        };
        var triList = new System.Collections.Generic.List<int[]>();
        foreach (var f in faces) triList.Add(f);

        for (int r = 0; r < rec; r++)
        {
            var next = new System.Collections.Generic.List<int[]>();
            var midCache = new System.Collections.Generic.Dictionary<long, int>();
            foreach (var tri in triList)
            {
                int a = GetMidpoint(verts, midCache, tri[0], tri[1]);
                int b = GetMidpoint(verts, midCache, tri[1], tri[2]);
                int c = GetMidpoint(verts, midCache, tri[2], tri[0]);
                next.Add(new[]{tri[0],a,c});
                next.Add(new[]{tri[1],b,a});
                next.Add(new[]{tri[2],c,b});
                next.Add(new[]{a,b,c});
            }
            triList = next;
        }
        foreach (var tri in triList) { tris.Add(tri[0]); tris.Add(tri[1]); tris.Add(tri[2]); }

        s_SharedSphereMesh = new Mesh();
        s_SharedSphereMesh.name = "TipSphereMesh";
        s_SharedSphereMesh.SetVertices(verts);
        s_SharedSphereMesh.SetTriangles(tris, 0);
        s_SharedSphereMesh.RecalculateNormals();
        s_SharedSphereMesh.RecalculateBounds();
        return s_SharedSphereMesh;
    }

    private static int GetMidpoint(System.Collections.Generic.List<Vector3> verts,
        System.Collections.Generic.Dictionary<long, int> cache, int i1, int i2)
    {
        long key = ((long)Mathf.Min(i1,i2) << 32) + Mathf.Max(i1,i2);
        if (cache.TryGetValue(key, out int idx)) return idx;
        Vector3 mid = ((verts[i1] + verts[i2]) * 0.5f).normalized;
        idx = verts.Count;
        verts.Add(mid);
        cache[key] = idx;
        return idx;
    }
}
