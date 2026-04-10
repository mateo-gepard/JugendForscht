using UnityEngine;

/// <summary>
/// Helper Script: Richtet Stereo-Display automatisch ein
/// Füge dieses Script auf dein MoleculeSystem GameObject
/// und drücke im Inspector den "Setup Stereo Display" Button
/// </summary>
public class StereoDisplaySetup : MonoBehaviour
{
    [Header("References")]
    public new MoleculeRenderer renderer;
    public MoleculePlaneAlignment planeAlignment;

    [ContextMenu("Setup Stereo Display")]
    public void SetupStereoDisplay()
    {
        Debug.Log("=== Setting up Stereo Display ===");

        // 1. Get or add MoleculeRenderer
        if (renderer == null)
        {
            renderer = GetComponent<MoleculeRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<MoleculeRenderer>();
                Debug.Log("✅ Added MoleculeRenderer");
            }
        }

        // 2. Get or add MoleculePlaneAlignment
        if (planeAlignment == null)
        {
            planeAlignment = GetComponent<MoleculePlaneAlignment>();
            if (planeAlignment == null)
            {
                planeAlignment = gameObject.AddComponent<MoleculePlaneAlignment>();
                Debug.Log("✅ Added MoleculePlaneAlignment");
            }
        }

        // 3. Configure MoleculeRenderer
        renderer.enableStereoDisplay = true;
        renderer.planeAlignment = planeAlignment;
        Debug.Log("✅ Enabled Stereo Display on Renderer");

        // 4. Configure MoleculePlaneAlignment
        planeAlignment.renderer = renderer;
        planeAlignment.planeNormal = Vector3.forward; // Zur Kamera
        planeAlignment.depthThresholdFactor = 0.02f;
        planeAlignment.showDebugPlane = true;
        planeAlignment.showDebugInfo = true;
        Debug.Log("✅ Configured Plane Alignment");

        Debug.Log("=== Setup Complete! ===");
        Debug.Log("Now load a molecule and check if you see wedges/dashes!");
    }

    [ContextMenu("Test with Alanine")]
    public async void TestWithAlanine()
    {
        Debug.Log("=== Testing with Alanine (has stereo centers) ===");

        var api = gameObject.GetComponent<PubChemAPI>();
        if (api == null)
        {
            api = gameObject.AddComponent<PubChemAPI>();
        }

        string sdf = await api.GetMoleculeSDF("alanine");

        if (sdf != null)
        {
            MoleculeData molecule = SDFParser.Parse(sdf, "Alanine");

            if (molecule != null)
            {
                Debug.Log($"✅ Loaded Alanine: {molecule.atoms.Count} atoms, {molecule.bonds.Count} bonds");
                renderer.RenderMolecule(molecule);
                Debug.Log("Check the Scene view - you should see wedges and dashes!");
            }
        }
    }

    void OnValidate()
    {
        // Auto-assign if missing
        if (renderer == null) renderer = GetComponent<MoleculeRenderer>();
        if (planeAlignment == null) planeAlignment = GetComponent<MoleculePlaneAlignment>();
    }
}