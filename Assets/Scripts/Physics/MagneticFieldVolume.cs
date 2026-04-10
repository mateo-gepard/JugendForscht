using UnityEngine;

/// <summary>
/// Visualisiert ein homogenes Magnetfeld als Box mit einem statischen
/// Raster aus Pfeilen. Die Feldrichtung ist in lokalen Koordinaten
/// definiert, sodass die gesamte Box beliebig im Raum gedreht werden
/// kann, ohne die Physik zu brechen.
///
/// Erzeugung komplett prozedural – keine Prefabs nötig.
/// </summary>
public class MagneticFieldVolume : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Feld-Parameter")]
    [Tooltip("Magnetische Flussdichte in Tesla (lokale Z-Achse)")]
    public float fieldStrength = 1f;

    [Tooltip("Lokale Feldrichtung (wird normalisiert)")]
    public Vector3 localFieldDirection = Vector3.forward;

    [Header("Volumen")]
    [Tooltip("Abmessungen der Box in Metern")]
    public Vector3 volumeSize = new Vector3(0.5f, 0.5f, 0.5f);

    [Header("Visualisierung")]
    [Tooltip("Anzahl Pfeile pro Achse")]
    public int arrowsPerAxis = 6;

    [Tooltip("Skalierung der Pfeile")]
    public float arrowScale = 0.04f;

    [Tooltip("Farbe der Feldpfeile")]
    public Color arrowColor = new Color(0.3f, 0.7f, 1f, 0.6f);

    [Tooltip("Farbe der Box-Kanten")]
    public Color wireColor = new Color(0.3f, 0.7f, 1f, 0.15f);

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private GameObject arrowContainer;
    private Material arrowMaterial;
    private static Mesh s_ArrowMesh;

    // ════════════════════════════════════════════════════════════
    //  Öffentliche API
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// B-Vektor in Weltkoordinaten (fieldStrength * normalisierte Richtung).
    /// </summary>
    public Vector3 GetWorldFieldVector()
    {
        return transform.TransformDirection(localFieldDirection.normalized) * fieldStrength;
    }

    /// <summary>
    /// Prüft, ob ein Weltpunkt innerhalb des Volumens liegt.
    /// </summary>
    public bool ContainsPoint(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        Vector3 half = volumeSize * 0.5f;
        return Mathf.Abs(local.x) <= half.x
            && Mathf.Abs(local.y) <= half.y
            && Mathf.Abs(local.z) <= half.z;
    }

    /// <summary>
    /// Baut das Pfeil-Raster neu auf (z.B. nach Parameteränderung).
    /// </summary>
    [ContextMenu("Pfeile neu generieren")]
    public void RebuildArrows()
    {
        ClearArrows();
        BuildArrowGrid();
    }

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Awake()
    {
        EnsureMaterial();
        BuildArrowGrid();
    }

    void OnDrawGizmos()
    {
        // Box-Umriss im Editor anzeigen
        Gizmos.color = wireColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, volumeSize);

        // Feldrichtung als Pfeil in der Mitte
        Gizmos.color = arrowColor;
        Vector3 dir = localFieldDirection.normalized * volumeSize.magnitude * 0.2f;
        Gizmos.DrawRay(Vector3.zero, dir);
    }

    void OnDestroy()
    {
        if (arrowMaterial != null) Destroy(arrowMaterial);
    }

    // ════════════════════════════════════════════════════════════
    //  Pfeil-Erzeugung
    // ════════════════════════════════════════════════════════════

    private void BuildArrowGrid()
    {
        arrowContainer = new GameObject("B-Feld Pfeile");
        arrowContainer.transform.SetParent(transform, false);

        Vector3 half = volumeSize * 0.5f;
        int n = Mathf.Max(1, arrowsPerAxis);

        Vector3 step = new Vector3(
            n > 1 ? volumeSize.x / (n - 1) : 0f,
            n > 1 ? volumeSize.y / (n - 1) : 0f,
            n > 1 ? volumeSize.z / (n - 1) : 0f
        );

        Quaternion rot = Quaternion.LookRotation(localFieldDirection.normalized);
        Mesh mesh = GetArrowMesh();

        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        for (int z = 0; z < n; z++)
        {
            Vector3 pos = new Vector3(
                -half.x + (n > 1 ? x * step.x : 0f),
                -half.y + (n > 1 ? y * step.y : 0f),
                -half.z + (n > 1 ? z * step.z : 0f)
            );

            GameObject arrow = new GameObject($"BArrow_{x}_{y}_{z}");
            arrow.transform.SetParent(arrowContainer.transform, false);
            arrow.transform.localPosition = pos;
            arrow.transform.localRotation = rot;
            arrow.transform.localScale = Vector3.one * arrowScale;

            MeshFilter mf = arrow.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            MeshRenderer mr = arrow.AddComponent<MeshRenderer>();
            mr.sharedMaterial = arrowMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }

    private void ClearArrows()
    {
        if (arrowContainer != null)
        {
            if (Application.isPlaying) Destroy(arrowContainer);
            else DestroyImmediate(arrowContainer);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Material & Mesh
    // ════════════════════════════════════════════════════════════

    private void EnsureMaterial()
    {
        if (arrowMaterial != null) return;

        Shader shader = Shader.Find("Custom/MoleculeUnlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");

        arrowMaterial = new Material(shader);
        arrowMaterial.color = arrowColor;
        arrowMaterial.enableInstancing = true;
    }

    /// <summary>
    /// Erzeugt ein einfaches Pfeil-Mesh (Schaft + Kopf) entlang +Z.
    /// Gecacht – wird nur einmal erzeugt.
    /// </summary>
    private static Mesh GetArrowMesh()
    {
        if (s_ArrowMesh != null) return s_ArrowMesh;

        // Schaft: dünner Zylinder (6 Seiten)
        // Kopf: Kegel (6 Seiten)
        int sides = 6;
        float shaftRadius = 0.08f;
        float shaftLength = 0.6f;
        float headRadius = 0.2f;
        float headLength = 0.4f;
        float totalLength = shaftLength + headLength;

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        // Schaft
        for (int i = 0; i < sides; i++)
        {
            float a = (2f * Mathf.PI * i) / sides;
            float cos = Mathf.Cos(a) * shaftRadius;
            float sin = Mathf.Sin(a) * shaftRadius;
            verts.Add(new Vector3(cos, sin, 0f));                  // bottom ring
            verts.Add(new Vector3(cos, sin, shaftLength));          // top ring
        }
        for (int i = 0; i < sides; i++)
        {
            int b = i * 2;
            int n = ((i + 1) % sides) * 2;
            tris.Add(b); tris.Add(n); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(n); tris.Add(n + 1);
        }

        // Kopf (Kegel)
        int baseStart = verts.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = (2f * Mathf.PI * i) / sides;
            verts.Add(new Vector3(Mathf.Cos(a) * headRadius, Mathf.Sin(a) * headRadius, shaftLength));
        }
        int tipIdx = verts.Count;
        verts.Add(new Vector3(0f, 0f, totalLength)); // Spitze

        for (int i = 0; i < sides; i++)
        {
            int curr = baseStart + i;
            int next = baseStart + (i + 1) % sides;
            tris.Add(curr); tris.Add(next); tris.Add(tipIdx);
        }

        // Zentriere um den Mittelpunkt der Gesamtlänge
        float offset = totalLength * 0.5f;
        for (int i = 0; i < verts.Count; i++)
            verts[i] = new Vector3(verts[i].x, verts[i].y, verts[i].z - offset);

        s_ArrowMesh = new Mesh();
        s_ArrowMesh.name = "ArrowMesh";
        s_ArrowMesh.SetVertices(verts);
        s_ArrowMesh.SetTriangles(tris, 0);
        s_ArrowMesh.RecalculateNormals();
        s_ArrowMesh.RecalculateBounds();
        return s_ArrowMesh;
    }
}
