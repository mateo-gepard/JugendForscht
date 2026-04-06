using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// Rendert ein Molekül als 3D-Objekt (Ball-and-Stick Model)
/// Mit Unterstützung für stereochemische Darstellung (Keil/Strich)
/// 
/// Quest 3 Performance-Optimierungen:
///   - Low-poly Meshes (Icosphere 42v statt Unity Sphere 515v)
///   - Atom Mesh-Combining (alle Atome gleichen Elements → 1 Draw Call)
///   - Keine Bond-Collider (nur Atoms haben Collider)
///   - Debounced Bond Re-Rendering (max 5×/s statt jedes Frame)
///   - GPU Instancing auf allen Materials
/// </summary>
public class MoleculeRenderer : MonoBehaviour
{
    [Header("References")]
    public ElementDatabase elementDatabase;
    public MoleculePlaneAlignment planeAlignment;

    [Header("Prefabs")]
    public GameObject atomSpherePrefab;
    public GameObject bondCylinderPrefab;

    [Header("Settings")]
    [Tooltip("Skalierungsfaktor: 1 Angström = X Unity-Meter")]
    public float angstromToMeter = 0.1f;

    [Tooltip("Vergrößert die Abstände zwischen Atomen")]
    [Range(1.0f, 3.0f)]
    public float bondLengthMultiplier = 1.5f;

    [Tooltip("Ball-and-Stick: Atoms werden kleiner dargestellt")]
    [Range(0.1f, 1.0f)]
    public float atomScaleFactor = 0.3f;

    [Tooltip("Dicke der Bindungs-Zylinder")]
    [Range(0.005f, 0.05f)]
    public float bondRadius = 0.015f;

    [Header("Stereo Display")]
    [Tooltip("Stereochemische Darstellung aktivieren (Keil/Strich)")]
    public bool enableStereoDisplay = true;

    // Runtime Data
    private MoleculeData currentMolecule;
    private List<GameObject> atomObjects = new List<GameObject>();
    private List<GameObject> bondObjects = new List<GameObject>();
    private List<GameObject> combinedAtomObjects = new List<GameObject>();

    // Cached materials to prevent grey flashes
    private Material cachedBondMaterial;
    private Material cachedDashedMaterial;

    // Bond re-render debounce
    private float lastBondRerenderTime;
    private const float BOND_RERENDER_INTERVAL = 0.2f; // max 5×/s
    private bool bondRerenderPending;

    // Atom data cache for O(1) lookup
    private Dictionary<int, AtomData> atomLookup;

    /// <summary>
    /// Gets the current molecule data
    /// </summary>
    public MoleculeData CurrentMolecule => currentMolecule;

    /// <summary>
    /// Rendert ein Molekül in der Szene
    /// </summary>
    public void RenderMolecule(MoleculeData moleculeData)
    {
        if (moleculeData == null)
        {
            Debug.LogError("[MoleculeRenderer] MoleculeData is null");
            return;
        }

        if (elementDatabase == null)
        {
            Debug.LogError("[MoleculeRenderer] ElementDatabase not assigned!");
            return;
        }

        // Clear previous molecule
        ClearMolecule();

        currentMolecule = moleculeData;

        // Build O(1) atom lookup dictionary
        BuildAtomLookup();

        Debug.Log($"[MoleculeRenderer] Rendering {moleculeData.name}: {moleculeData.atoms.Count} atoms, {moleculeData.bonds.Count} bonds");

        // Calculate scale based on atom count
        float scale = CalculateMoleculeScale(moleculeData.atoms.Count);
        transform.localScale = Vector3.one * scale;

        Debug.Log($"[MoleculeRenderer] Molecule scale: {scale:F2}x for {moleculeData.atoms.Count} atoms");

        // Render Atoms
        foreach (var atom in moleculeData.atoms)
        {
            RenderAtom(atom);
        }

        // Initialize plane alignment if available
        if (enableStereoDisplay && planeAlignment != null)
        {
            planeAlignment.InitializeForMolecule(moleculeData);
        }

        // Render Bonds
        foreach (var bond in moleculeData.bonds)
        {
            RenderBond(bond);
        }

        // ── Performance: Combine atom meshes per material ──
        CombineAtomMeshes();

        Debug.Log($"[MoleculeRenderer] Rendered: {combinedAtomObjects.Count} combined atom batches, {bondObjects.Count} bond objects");
    }

    /// <summary>
    /// Calculates molecule scale based on atom count
    /// 5 atoms = 100%, 50+ atoms = 50%, -5% per 5 atoms
    /// </summary>
    private float CalculateMoleculeScale(int atomCount)
    {
        const int baseAtomCount = 5;
        int atomSteps = (atomCount - baseAtomCount) / 5;
        float scale = 1.0f - (atomSteps * 0.25f);
        scale = Mathf.Clamp(scale, 0.15f, 1.0f);
        scale = Mathf.Round(scale * 20f) / 20f;
        return scale;
    }

    // ════════════════════════════════════════════════════════════
    // ATOM LOOKUP (O(1) instead of O(n))
    // ════════════════════════════════════════════════════════════

    private void BuildAtomLookup()
    {
        atomLookup = new Dictionary<int, AtomData>(currentMolecule.atoms.Count);
        foreach (var atom in currentMolecule.atoms)
        {
            atomLookup[atom.id] = atom;
        }
    }

    private AtomData GetAtomFast(int id)
    {
        if (atomLookup != null && atomLookup.TryGetValue(id, out AtomData atom))
            return atom;
        // Fallback to linear search
        return currentMolecule.GetAtom(id);
    }

    // ════════════════════════════════════════════════════════════
    // BOND RE-RENDER DEBOUNCE
    // ════════════════════════════════════════════════════════════

    private void Update()
    {
        if (bondRerenderPending && Time.time - lastBondRerenderTime >= BOND_RERENDER_INTERVAL)
        {
            bondRerenderPending = false;
            DoRerenderBonds();
        }
    }

    /// <summary>
    /// Re-renders only the bonds without reinitializing the plane.
    /// Debounced to max 5×/second to avoid GC pressure on Quest 3.
    /// </summary>
    public void RerenderBondsOnly()
    {
        if (currentMolecule == null) return;

        // Debounce: only re-render at most every BOND_RERENDER_INTERVAL seconds
        if (Time.time - lastBondRerenderTime < BOND_RERENDER_INTERVAL)
        {
            bondRerenderPending = true;
            return;
        }

        DoRerenderBonds();
    }

    private void DoRerenderBonds()
    {
        lastBondRerenderTime = Time.time;

        // Destroy existing bonds
        for (int i = bondObjects.Count - 1; i >= 0; i--)
        {
            if (bondObjects[i] != null)
            {
                var r = bondObjects[i].GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                Destroy(bondObjects[i]);
            }
        }
        bondObjects.Clear();

        // Re-render bonds with current plane alignment
        foreach (var bond in currentMolecule.bonds)
        {
            RenderBond(bond);
        }
    }

    // ════════════════════════════════════════════════════════════
    // ATOM RENDERING (low-poly + mesh combining)
    // ════════════════════════════════════════════════════════════

    private void RenderAtom(AtomData atom)
    {
        // Get element data
        ElementData element = elementDatabase.GetElement(atom.element);
        Material mat = ShaderIncluder.GetMaterialForElement(atom.element);

        // Create lightweight GameObject with low-poly mesh (no prefab needed)
        GameObject atomObj = new GameObject($"Atom_{atom.id}_{atom.element}");
        atomObj.transform.SetParent(transform, false);

        MeshFilter mf = atomObj.AddComponent<MeshFilter>();
        mf.sharedMesh = LowPolyMeshes.GetSphere();

        MeshRenderer mr = atomObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // Position (Angström → Meter)
        atomObj.transform.localPosition = atom.position * angstromToMeter * bondLengthMultiplier;

        // Size (Van der Waals radius)
        float displayRadius = element.vdwRadius * atomScaleFactor * angstromToMeter;
        atomObj.transform.localScale = Vector3.one * displayRadius * 2f;

        // Add a small sphere collider for interaction (optional)
        SphereCollider collider = atomObj.AddComponent<SphereCollider>();
        collider.radius = 0.5f;

        atomObjects.Add(atomObj);
    }

    // ════════════════════════════════════════════════════════════
    // ATOM MESH COMBINING (reduces draw calls dramatically)
    // ════════════════════════════════════════════════════════════

    private void CombineAtomMeshes()
    {
        if (atomObjects.Count == 0) return;

        // Group atoms by material
        var groups = new Dictionary<Material, List<CombineInstance>>();

        foreach (var atomObj in atomObjects)
        {
            if (atomObj == null) continue;
            MeshFilter mf = atomObj.GetComponent<MeshFilter>();
            MeshRenderer mr = atomObj.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null || mr == null) continue;

            Material mat = mr.sharedMaterial;
            if (!groups.ContainsKey(mat))
                groups[mat] = new List<CombineInstance>();

            groups[mat].Add(new CombineInstance
            {
                mesh = mf.sharedMesh,
                transform = transform.worldToLocalMatrix * atomObj.transform.localToWorldMatrix
            });
        }

        // Create one combined mesh per material
        foreach (var kvp in groups)
        {
            GameObject combined = new GameObject("CombinedAtoms_" + kvp.Key.name);
            combined.transform.SetParent(transform, false);
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;

            MeshFilter mf = combined.AddComponent<MeshFilter>();
            MeshRenderer mr = combined.AddComponent<MeshRenderer>();
            mr.sharedMaterial = kvp.Key;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Mesh mesh = new Mesh();
            // Use 32-bit indices for large molecules (>65k vertices)
            if (kvp.Value.Count * 42 > 65000)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.CombineMeshes(kvp.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            combinedAtomObjects.Add(combined);
        }

        // Destroy individual atom GameObjects (no longer needed)
        foreach (var obj in atomObjects)
        {
            if (obj != null) Destroy(obj);
        }
        atomObjects.Clear();

        Debug.Log($"[MoleculeRenderer] Combined atoms into {combinedAtomObjects.Count} draw calls");
    }

    // ════════════════════════════════════════════════════════════
    // BOND RENDERING (low-poly, no colliders)
    // ════════════════════════════════════════════════════════════

    private void RenderBond(BondData bond)
    {
        AtomData atomA = GetAtomFast(bond.atomA_ID);
        AtomData atomB = GetAtomFast(bond.atomB_ID);

        if (atomA == null || atomB == null) return;

        // Stereo classification
        BondStereo stereoType = bond.stereo;
        if (enableStereoDisplay && planeAlignment != null)
        {
            stereoType = planeAlignment.ClassifyBond(bond);
        }

        ElementData elementA = elementDatabase.GetElement(atomA.element);
        ElementData elementB = elementDatabase.GetElement(atomB.element);

        Vector3 posA = atomA.position * angstromToMeter * bondLengthMultiplier;
        Vector3 posB = atomB.position * angstromToMeter * bondLengthMultiplier;

        float radiusA = elementA.vdwRadius * atomScaleFactor * angstromToMeter;
        float radiusB = elementB.vdwRadius * atomScaleFactor * angstromToMeter;

        Vector3 direction = posB - posA;
        float fullDistance = direction.magnitude;
        Vector3 dirNorm = direction.normalized;

        Vector3 bondStart = posA + dirNorm * radiusA;
        Vector3 bondEnd = posB - dirNorm * radiusB;
        float bondLength = (bondEnd - bondStart).magnitude;

        if (bondLength <= 0) return;

        switch (stereoType)
        {
            case BondStereo.Up:
                RenderWedgeBond(bondStart, bondEnd, dirNorm, bondLength);
                break;
            case BondStereo.Down:
                RenderDashedBond(bondStart, bondEnd, dirNorm, bondLength);
                break;
            default:
                RenderNormalBond(bondStart, bondEnd, dirNorm, bondLength);
                break;
        }
    }

    /// <summary>
    /// Creates a bond cylinder using low-poly mesh (no collider).
    /// </summary>
    private GameObject CreateBondCylinder(string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = LowPolyMeshes.GetCylinder();

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (cachedBondMaterial == null)
            cachedBondMaterial = ShaderIncluder.GetBondMaterial();
        mr.sharedMaterial = cachedBondMaterial;

        return obj;
    }

    private void RenderNormalBond(Vector3 start, Vector3 end, Vector3 direction, float length)
    {
        GameObject bondObj = CreateBondCylinder("Bond_Normal");

        Vector3 midpoint = (start + end) / 2f;
        bondObj.transform.localPosition = midpoint;
        bondObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        bondObj.transform.localScale = new Vector3(bondRadius, length / 2f, bondRadius);

        bondObjects.Add(bondObj);
    }

    private void RenderWedgeBond(Vector3 start, Vector3 end, Vector3 direction, float length)
    {
        int segments = 5;
        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / (segments - 1);
            Vector3 pos = Vector3.Lerp(start, end, t);
            float radius = Mathf.Lerp(bondRadius * 0.5f, bondRadius * 2f, t);

            GameObject segment = CreateBondCylinder("Bond_Wedge");
            segment.transform.localPosition = pos;
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            segment.transform.localScale = new Vector3(radius, length / (segments * 2), radius);

            bondObjects.Add(segment);
        }
    }

    private void RenderDashedBond(Vector3 start, Vector3 end, Vector3 direction, float length)
    {
        int dashes = 6;
        float dashLength = length / (dashes * 2);

        if (cachedDashedMaterial == null)
            cachedDashedMaterial = ShaderIncluder.GetBondMaterial();

        for (int i = 0; i < dashes; i++)
        {
            float t1 = (float)(i * 2) / (dashes * 2);
            float t2 = (float)(i * 2 + 1) / (dashes * 2);

            Vector3 dashMid = (Vector3.Lerp(start, end, t1) + Vector3.Lerp(start, end, t2)) / 2f;

            GameObject dash = CreateBondCylinder("Bond_Dash");
            dash.transform.localPosition = dashMid;
            dash.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            dash.transform.localScale = new Vector3(bondRadius * 0.7f, dashLength / 2f, bondRadius * 0.7f);

            MeshRenderer mr = dash.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = cachedDashedMaterial;

            bondObjects.Add(dash);
        }
    }

    // ════════════════════════════════════════════════════════════
    // CLEANUP
    // ════════════════════════════════════════════════════════════

    public void ClearMolecule()
    {
        foreach (var obj in atomObjects)
            if (obj != null) Destroy(obj);

        foreach (var obj in bondObjects)
            if (obj != null) Destroy(obj);

        foreach (var obj in combinedAtomObjects)
        {
            if (obj != null)
            {
                // Destroy combined meshes to free memory
                MeshFilter mf = obj.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Destroy(mf.sharedMesh);
                Destroy(obj);
            }
        }

        atomObjects.Clear();
        bondObjects.Clear();
        combinedAtomObjects.Clear();
        currentMolecule = null;
        atomLookup = null;
        bondRerenderPending = false;

        if (planeAlignment != null)
            planeAlignment.ClearPlane();
    }

    private void OnDestroy()
    {
        ClearMolecule();
    }
}