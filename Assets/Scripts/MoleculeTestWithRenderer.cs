using UnityEngine;

/// <summary>
/// Test-Script: Lädt Molekül von PubChem und zeigt es im Raum an
/// </summary>
public class MoleculeTestWithRenderer : MonoBehaviour
{
    [Header("References")]
    public ElementDatabase elementDB;
    public new MoleculeRenderer renderer;

    [Header("Settings")]
    [Tooltip("Welches Molekül laden? (z.B. 'ethanol', 'caffeine', 'aspirin')")]
    public string moleculeName = "ethanol";

    [Tooltip("Distance from camera where molecule appears")]
    public float spawnDistance = 1.0f;

    private PubChemAPI api;

    async void Start()
    {
        // Setup API
        api = gameObject.AddComponent<PubChemAPI>();

        // Validate references
        if (elementDB == null)
        {
            Debug.LogError("❌ ElementDatabase not assigned!");
            return;
        }

        if (renderer == null)
        {
            Debug.LogError("❌ MoleculeRenderer not assigned!");
            return;
        }

        Debug.Log($"=== Loading {moleculeName} from PubChem ===");

        // Download & Parse
        string sdf = await api.GetMoleculeSDF(moleculeName);

        if (sdf == null)
        {
            Debug.LogError($"❌ Failed to load molecule '{moleculeName}'");
            return;
        }

        MoleculeData molecule = SDFParser.Parse(sdf, moleculeName);

        if (molecule == null)
        {
            Debug.LogError("❌ Failed to parse SDF");
            return;
        }

        Debug.Log($"✅ Loaded: {molecule.atoms.Count} atoms, {molecule.bonds.Count} bonds");

        // Position molecule in front of camera
        PositionInFrontOfCamera();

        // Render
        renderer.RenderMolecule(molecule);

        Debug.Log("🎉 Molecule rendered successfully!");
    }

    private void PositionInFrontOfCamera()
    {
        // Find main camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("No main camera found, using default position");
            return;
        }

        // Position molecule in front of camera
        Vector3 forward = cam.transform.forward;
        Vector3 targetPos = cam.transform.position + forward * spawnDistance;

        transform.position = targetPos;
        transform.rotation = Quaternion.LookRotation(forward);
    }
}