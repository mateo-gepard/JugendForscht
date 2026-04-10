using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Visualisiert Chiralitätszentren mit Stern-Markierungen in VR
/// Positioniert Sterne direkt über den gerenderten Atomen
/// </summary>
public class ChiralityVisualizer : MonoBehaviour
{
    [Header("Visual Settings")]
    public float starScale = 0.06f;
    public float starHeight = 0.04f;

    [Header("Colors")]
    public Color rColor = new Color(0.42f, 0.54f, 1f, 1f);
    public Color sColor = new Color(1f, 0.65f, 0.15f, 1f);

    private List<GameObject> activeMarkers = new List<GameObject>();
    private MoleculeRenderer moleculeRenderer;

    /// <summary>
    /// Zeigt Stern-Marker an den Chiralitätszentren
    /// </summary>
    public void ShowChiralCenters(List<ChiralityDetector.ChiralCenter> centers, MoleculeData molecule)
    {
        ClearMarkers();

        moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
        if (moleculeRenderer == null || molecule == null)
        {
            Debug.LogWarning("[ChiralityVis] No MoleculeRenderer found");
            return;
        }

        float scale = moleculeRenderer.angstromToMeter * moleculeRenderer.bondLengthMultiplier;

        foreach (var center in centers)
        {
            var atom = molecule.GetAtom(center.atomId);
            if (atom == null) continue;

            // Calculate position in renderer's local space (same as RenderAtom)
            Vector3 localPos = atom.position * scale;

            CreateStarMarker(localPos, center);
        }

        Debug.Log($"[ChiralityVis] Created {activeMarkers.Count} star markers");
    }

    /// <summary>
    /// Erstellt einen Stern-Marker
    /// </summary>
    private void CreateStarMarker(Vector3 localPos, ChiralityDetector.ChiralCenter center)
    {
        GameObject starObj = new GameObject($"ChiralStar_{center.atomId}_{center.configuration}");

        // Parent to the molecule renderer so it moves/scales with the molecule
        starObj.transform.SetParent(moleculeRenderer.transform, false);

        // Position directly above the atom in local space
        starObj.transform.localPosition = localPos + Vector3.up * starHeight;

        // Billboard
        starObj.AddComponent<BillboardFacer>();

        // Star mesh
        Color color = center.configuration == "R" ? rColor : sColor;
        CreateStarMesh(starObj, color);

        // Pulse animation
        var pulser = starObj.AddComponent<PulseAnimation>();
        pulser.pulseSpeed = 2f;
        pulser.pulseAmount = 0.15f;

        activeMarkers.Add(starObj);
    }

    private void CreateStarMesh(GameObject parent, Color color)
    {
        Mesh starMesh = CreateStarShape(5, starScale, starScale * 0.45f);

        // Front face
        GameObject meshObj = new GameObject("StarMesh");
        meshObj.transform.SetParent(parent.transform, false);
        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
        mf.mesh = starMesh;
        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();

        // Use Sprites/Default for reliable unlit rendering
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Back face
        GameObject backObj = new GameObject("StarMeshBack");
        backObj.transform.SetParent(parent.transform, false);
        backObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        MeshFilter mfBack = backObj.AddComponent<MeshFilter>();
        mfBack.mesh = starMesh;
        MeshRenderer mrBack = backObj.AddComponent<MeshRenderer>();
        mrBack.material = mat;
        mrBack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private Mesh CreateStarShape(int points, float outerRadius, float innerRadius)
    {
        int numVertices = points * 2 + 1;
        Vector3[] vertices = new Vector3[numVertices];
        int[] triangles = new int[points * 2 * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < points * 2; i++)
        {
            float angle = (i * Mathf.PI / points) - Mathf.PI / 2f;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
        }

        for (int i = 0; i < points * 2; i++)
        {
            int triIdx = i * 3;
            triangles[triIdx] = 0;
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
