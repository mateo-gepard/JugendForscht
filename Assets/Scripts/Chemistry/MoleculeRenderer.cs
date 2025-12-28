using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Rendert ein Molekül als 3D-Objekt (Ball-and-Stick Model)
/// Mit Unterstützung für stereochemische Darstellung (Keil/Strich)
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
    
    // Cached materials to prevent grey flashes
    private Material cachedBondMaterial;
    private Material cachedDashedMaterial;
    
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

        Debug.Log($"[MoleculeRenderer] Rendering {moleculeData.name}: {moleculeData.atoms.Count} atoms, {moleculeData.bonds.Count} bonds");

        // Calculate scale based on atom count
        // 5 atoms = 100%, 50 atoms = 50%, decrease 5% per 5 atoms
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

        // Render Bonds (mit Stereo-Klassifikation falls aktiviert)
        foreach (var bond in moleculeData.bonds)
        {
            RenderBond(bond);
        }

        Debug.Log($"[MoleculeRenderer] Successfully rendered molecule");
    }

    /// <summary>
    /// Calculates molecule scale based on atom count
    /// 5 atoms = 100%, 50+ atoms = 50%, -5% per 5 atoms
    /// </summary>
    private float CalculateMoleculeScale(int atomCount)
    {
        // Base atom count (no scaling)
        const int baseAtomCount = 5;
        
        // Scale reduction per 5 atoms: 25% reduction per 5-atom step
        // Every 5 atoms reduces scale by 25% (more aggressive)
        int atomSteps = (atomCount - baseAtomCount) / 5;
        
        // Calculate scale: reduce by 25% for each 5-atom step
        float scale = 1.0f - (atomSteps * 0.25f);
        
        // Clamp to reasonable range (minimum 15%, maximum 100%)
        scale = Mathf.Clamp(scale, 0.15f, 1.0f);
        
        // Round to nearest 5% step for cleaner values
        scale = Mathf.Round(scale * 20f) / 20f; // Round to 0.05 increments
        
        Debug.Log($"[MoleculeScale] Atoms: {atomCount}, Steps: {atomSteps}, Scale: {scale:P0}");
        
        return scale;
    }

    /// <summary>
    /// Re-renders only the bonds without reinitializing the plane
    /// Used during rotation to update bond stereochemistry
    /// </summary>
    public void RerenderBondsOnly()
    {
        if (currentMolecule == null) return;

        // Disable renderers first to prevent flashing, then destroy immediately
        foreach (var bondObj in bondObjects)
        {
            if (bondObj != null)
            {
                // Disable renderer before destroying to prevent grey flash
                Renderer renderer = bondObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
                DestroyImmediate(bondObj);
            }
        }
        bondObjects.Clear();

        // Re-render bonds with current plane alignment
        foreach (var bond in currentMolecule.bonds)
        {
            RenderBond(bond);
        }
    }

    /// <summary>
    /// Rendert ein einzelnes Atom
    /// </summary>
    private void RenderAtom(AtomData atom)
    {
        if (atomSpherePrefab == null)
        {
            atomSpherePrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            atomSpherePrefab.SetActive(false);
        }

        // Get element data
        ElementData element = elementDatabase.GetElement(atom.element);

        // Instantiate sphere
        GameObject atomObj = Instantiate(atomSpherePrefab, transform);
        atomObj.name = $"Atom_{atom.id}_{atom.element}";
        atomObj.SetActive(true);

        // Position (Angström → Meter, mit Bond-Length-Multiplikator)
        atomObj.transform.localPosition = atom.position * angstromToMeter * bondLengthMultiplier;

        // Size (Van der Waals radius)
        float displayRadius = element.vdwRadius * atomScaleFactor * angstromToMeter;
        atomObj.transform.localScale = Vector3.one * displayRadius * 2f; // Diameter

        // Color (use ShaderIncluder for standalone-safe materials)
        Renderer renderer = atomObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Use ShaderIncluder for guaranteed shader availability in standalone builds
            renderer.sharedMaterial = ShaderIncluder.GetMaterialForElement(atom.element);
        }

        atomObjects.Add(atomObj);
    }

    /// <summary>
    /// Rendert eine Bindung zwischen zwei Atomen
    /// Mit optionaler stereochemischer Darstellung
    /// </summary>
    private void RenderBond(BondData bond)
    {
        // Get atom positions
        AtomData atomA = currentMolecule.GetAtom(bond.atomA_ID);
        AtomData atomB = currentMolecule.GetAtom(bond.atomB_ID);

        if (atomA == null || atomB == null)
        {
            Debug.LogWarning($"[MoleculeRenderer] Bond references invalid atoms: {bond.atomA_ID} -> {bond.atomB_ID}");
            return;
        }

        // Stereo-Klassifikation falls aktiviert
        BondStereo stereoType = bond.stereo;
        
        // Debug: Check stereo display conditions
        Debug.Log($"[MoleculeRenderer] Bond {bond.atomA_ID}->{bond.atomB_ID}: enableStereoDisplay={enableStereoDisplay}, planeAlignment={(planeAlignment != null ? "present" : "NULL")}");
        
        if (enableStereoDisplay && planeAlignment != null)
        {
            stereoType = planeAlignment.ClassifyBond(bond);
        }
        else
        {
            Debug.LogWarning($"[MoleculeRenderer] Skipping stereo classification - enableStereoDisplay={enableStereoDisplay}, planeAlignment={(planeAlignment != null ? "present" : "NULL")}");
        }

        // Get element data for radius calculation
        ElementData elementA = elementDatabase.GetElement(atomA.element);
        ElementData elementB = elementDatabase.GetElement(atomB.element);

        Vector3 posA = atomA.position * angstromToMeter * bondLengthMultiplier;
        Vector3 posB = atomB.position * angstromToMeter * bondLengthMultiplier;

        // Calculate display radii
        float radiusA = elementA.vdwRadius * atomScaleFactor * angstromToMeter;
        float radiusB = elementB.vdwRadius * atomScaleFactor * angstromToMeter;

        // Direction and distance
        Vector3 direction = posB - posA;
        float fullDistance = direction.magnitude;
        Vector3 directionNormalized = direction.normalized;

        // Shorten bond to stop at atom surfaces
        Vector3 bondStart = posA + directionNormalized * radiusA;
        Vector3 bondEnd = posB - directionNormalized * radiusB;
        float bondLength = (bondEnd - bondStart).magnitude;

        // Safety check: if atoms overlap, don't render bond
        if (bondLength <= 0)
        {
            return;
        }

        // Render basierend auf Stereo-Typ
        switch (stereoType)
        {
            case BondStereo.Up:
                RenderWedgeBond(bondStart, bondEnd, directionNormalized, bondLength);
                break;
            case BondStereo.Down:
                RenderDashedBond(bondStart, bondEnd, directionNormalized, bondLength);
                break;
            default:
                RenderNormalBond(bondStart, bondEnd, directionNormalized, bondLength);
                break;
        }
    }

    /// <summary>
    /// Normal Bond (Zylinder)
    /// </summary>
    private void RenderNormalBond(Vector3 start, Vector3 end, Vector3 direction, float length)
    {
        if (bondCylinderPrefab == null)
        {
            bondCylinderPrefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bondCylinderPrefab.SetActive(false);
        }

        GameObject bondObj = Instantiate(bondCylinderPrefab, transform);
        bondObj.name = $"Bond_Normal";
        bondObj.SetActive(true);

        Vector3 midpoint = (start + end) / 2f;
        bondObj.transform.localPosition = midpoint;
        bondObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        bondObj.transform.localScale = new Vector3(bondRadius, length / 2f, bondRadius);

        Renderer renderer = bondObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (cachedBondMaterial == null)
            {
                cachedBondMaterial = ShaderIncluder.GetBondMaterial();
            }
            renderer.sharedMaterial = cachedBondMaterial;
        }
        
        // Make collider only cover middle 10% of bond
        CapsuleCollider collider = bondObj.GetComponent<CapsuleCollider>();
        if (collider != null)
        {
            collider.height = 0.1f; // 10% of original height (which is 1.0 in local space)
            // Collider stays centered, so middle 10% is covered
        }

        bondObjects.Add(bondObj);
    }

    /// <summary>
    /// Wedge Bond (Keil - vorne)
    /// Wird breiter zum Ende hin
    /// </summary>
    private void RenderWedgeBond(Vector3 start, Vector3 end, Vector3 direction, float length)
    {
        // Erstelle einen Keil mit mehreren gestaffelten Zylindern
        int segments = 5;
        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / (segments - 1);
            Vector3 pos = Vector3.Lerp(start, end, t);
            float radius = Mathf.Lerp(bondRadius * 0.5f, bondRadius * 2f, t);

            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.transform.parent = transform;
            segment.transform.localPosition = pos;
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            segment.transform.localScale = new Vector3(radius, length / (segments * 2), radius);

            Renderer renderer = segment.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (cachedBondMaterial == null)
                {
                    cachedBondMaterial = ShaderIncluder.GetBondMaterial();
                }
                renderer.sharedMaterial = cachedBondMaterial;
            }
            
            // Make collider only cover middle 10% of segment
            CapsuleCollider collider = segment.GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                collider.height = 0.1f; // 10% of original
            }

            bondObjects.Add(segment);
        }
    }

    /// <summary>
    /// Dashed Bond (Gestrichelt - hinten)
    /// </summary>
    private void RenderDashedBond(Vector3 start, Vector3 end, Vector3 direction, float length)
    {
        // Erstelle gestrichelte Linie mit kleinen Segmenten
        int dashes = 6;
        float dashLength = length / (dashes * 2);

        for (int i = 0; i < dashes; i++)
        {
            float t1 = (float)(i * 2) / (dashes * 2);
            float t2 = (float)(i * 2 + 1) / (dashes * 2);

            Vector3 dashStart = Vector3.Lerp(start, end, t1);
            Vector3 dashEnd = Vector3.Lerp(start, end, t2);
            Vector3 dashMid = (dashStart + dashEnd) / 2f;

            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dash.transform.parent = transform;
            dash.transform.localPosition = dashMid;
            dash.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            dash.transform.localScale = new Vector3(bondRadius * 0.7f, dashLength / 2f, bondRadius * 0.7f);

            Renderer renderer = dash.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (cachedDashedMaterial == null)
                {
                    cachedDashedMaterial = ShaderIncluder.GetBondMaterial();
                }
                renderer.sharedMaterial = cachedDashedMaterial;
            }
            
            // Make collider only cover middle 30% of dash
            CapsuleCollider collider = dash.GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                collider.height = 0.1f; // 10% of original
            }

            bondObjects.Add(dash);
        }
    }

    /// <summary>
    /// Entfernt aktuelles Molekül
    /// </summary>
    public void ClearMolecule()
    {
        foreach (var obj in atomObjects)
        {
            if (obj != null) Destroy(obj);
        }

        foreach (var obj in bondObjects)
        {
            if (obj != null) Destroy(obj);
        }

        atomObjects.Clear();
        bondObjects.Clear();
        currentMolecule = null;
        
        // Also clear the plane visualization
        if (planeAlignment != null)
        {
            planeAlignment.ClearPlane();
        }
    }

    private void OnDestroy()
    {
        ClearMolecule();
    }
}