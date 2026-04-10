using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Visualisiert Chiralitätszentren mit Stern-Markierungen (*) in VR
/// Wird vom WebSocketServer aufgerufen nach der Chiralitätserkennung
/// </summary>
public class ChiralityVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    [Tooltip("Größe des Stern-Markers relativ zum Atom")]
    public float starScale = 0.08f;

    [Tooltip("Höhe über dem Atom")]
    public float starHeight = 0.06f;

    [Header("Colors")]
    public Color rColor = new Color(0.42f, 0.54f, 1f, 1f);   // Blau für R
    public Color sColor = new Color(1f, 0.65f, 0.15f, 1f);    // Orange für S

    // Active markers
    private List<GameObject> activeMarkers = new List<GameObject>();
    private MoleculeRenderer moleculeRenderer;

    void Start()
    {
        moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
    }

    /// <summary>
    /// Zeigt Stern-Marker an den Chiralitätszentren
    /// </summary>
    public void ShowChiralCenters(List<ChiralityDetector.ChiralCenter> centers, MoleculeData molecule)
    {
        // Clear previous markers
        ClearMarkers();

        if (moleculeRenderer == null)
            moleculeRenderer = FindObjectOfType<MoleculeRenderer>();

        if (moleculeRenderer == null || molecule == null)
        {
            Debug.LogWarning("[ChiralityVis] No MoleculeRenderer found");
            return;
        }

        foreach (var center in centers)
        {
            var atom = molecule.GetAtom(center.atomId);
            if (atom == null) continue;

            CreateStarMarker(atom, center);
        }

        // Debug.Log($"[ChiralityVis] Created {activeMarkers.Count} star markers");
    }

    /// <summary>
    /// Erstellt einen Stern-Marker über einem Atom
    /// </summary>
    private void CreateStarMarker(AtomData atom, ChiralityDetector.ChiralCenter center)
    {
        // Create star container
        GameObject starObj = new GameObject($"ChiralStar_{center.element}{center.atomId}_{center.configuration}");
        starObj.transform.SetParent(moleculeRenderer.transform, false);

        // Position: at the atom position + slightly above
        float scale = moleculeRenderer.angstromToMeter * moleculeRenderer.bondLengthMultiplier;
        Vector3 atomWorldPos = atom.position * scale;
        starObj.transform.localPosition = atomWorldPos + Vector3.up * starHeight;

        // Make star always face camera
        var billboard = starObj.AddComponent<BillboardFacer>();

        // Create the star shape
        Color color = center.configuration == "R" ? rColor : sColor;
        CreateStarMesh(starObj, color);

        // Add gentle pulsing animation
        var pulser = starObj.AddComponent<PulseAnimation>();
        pulser.pulseSpeed = 2f;
        pulser.pulseAmount = 0.15f;

        activeMarkers.Add(starObj);
    }

    /// <summary>
    /// Erstellt ein Stern-Mesh aus Dreiecken
    /// </summary>
    private void CreateStarMesh(GameObject parent, Color color)
    {
        // Create a 5-pointed star mesh
        Mesh starMesh = CreateStarShape(5, starScale, starScale * 0.45f);

        GameObject meshObj = new GameObject("StarMesh");
        meshObj.transform.SetParent(parent.transform, false);

        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
        mf.mesh = starMesh;

        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();

        // Create unlit material with the color
        Material mat = new Material(Shader.Find("Unlit/Color"));
        mat.color = color;
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Also create a back-facing copy so star is visible from both sides
        GameObject backObj = new GameObject("StarMeshBack");
        backObj.transform.SetParent(parent.transform, false);
        backObj.transform.localRotation = Quaternion.Euler(0, 180, 0);

        MeshFilter mfBack = backObj.AddComponent<MeshFilter>();
        mfBack.mesh = starMesh;

        MeshRenderer mrBack = backObj.AddComponent<MeshRenderer>();
        mrBack.material = mat;
        mrBack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mrBack.receiveShadows = false;
    }

    /// <summary>
    /// Erstellt ein R/S Label unter dem Stern
    /// </summary>
    private void CreateLabel(GameObject parent, string config, Color color)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent.transform, false);
        labelObj.transform.localPosition = Vector3.down * starScale * 1.5f;

        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = config;
        textMesh.fontSize = 48;
        textMesh.characterSize = starScale * 0.6f;
        textMesh.anchor = TextAnchor.UpperCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = color;
        textMesh.fontStyle = FontStyle.Bold;
    }

    /// <summary>
    /// Erzeugt ein Stern-Mesh (5 Zacken)
    /// </summary>
    private Mesh CreateStarShape(int points, float outerRadius, float innerRadius)
    {
        int numVertices = points * 2 + 1; // outer + inner + center
        Vector3[] vertices = new Vector3[numVertices];
        int[] triangles = new int[points * 2 * 3];

        // Center vertex
        vertices[0] = Vector3.zero;

        // Create outer and inner vertices
        for (int i = 0; i < points * 2; i++)
        {
            float angle = (i * Mathf.PI / points) - Mathf.PI / 2f;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
        }

        // Create triangles (fan from center)
        for (int i = 0; i < points * 2; i++)
        {
            int triIdx = i * 3;
            triangles[triIdx] = 0; // center
            triangles[triIdx + 1] = i + 1;
            triangles[triIdx + 2] = (i + 1) % (points * 2) + 1;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Entfernt alle aktiven Marker
    /// </summary>
    public void ClearMarkers()
    {
        foreach (var marker in activeMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeMarkers.Clear();
    }

    void OnDestroy()
    {
        ClearMarkers();
    }
}

/// <summary>
/// Billboard: Objekt zeigt immer zur Kamera
/// </summary>
public class BillboardFacer : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position, Vector3.up);
        }
    }
}

/// <summary>
/// Sanfte Puls-Animation (Skalierung)
/// </summary>
public class PulseAnimation : MonoBehaviour
{
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.15f;

    private Vector3 baseScale;

    void Start()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }
}
