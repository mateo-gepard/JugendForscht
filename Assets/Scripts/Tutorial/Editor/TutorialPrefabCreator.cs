using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to create tutorial bond prefabs
/// </summary>
public class TutorialPrefabCreator : EditorWindow
{
    [MenuItem("Tutorial/Create Bond Prefabs")]
    public static void CreateBondPrefabs()
    {
        // Create prefab folder if needed - must be in Resources for runtime loading
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        }
        
        // Create Normal Bond
        CreateNormalBondPrefab();
        
        // Create Dashed Bond
        CreateDashedBondPrefab();
        
        // Create Wedge Bond
        CreateWedgeBondPrefab();
        
        // Create Ammonia with Dashed Highlight
        CreateAmmoniaDashedPrefab();
        
        // Create Ammonia with Wedge Highlight
        CreateAmmoniaWedgePrefab();
        
        // Create Methan 3D (räumlicher Bau)
        CreateMethan3DPrefab();
        
        // Create Methan Keilstrichformel
        CreateMethanKeilstrichPrefab();
        
        // Step 3 - Molekülgeometrie Prefabs
        CreateCO2LinearPrefab();
        CreateH2OGewinkeltPrefab();
        CreateBF3TrigonalPlanarPrefab();
        CreateNH3TrigonalPyramidalPrefab();
        CreateCH4TetraedrischPrefab();
        
        // Step 4 - Linearer Bau Highlight
        CreateCO2LinearHighlightPrefab();
        
        // Step 5 - Gewinkelter Bau Highlights
        CreateH2OGewinkeltHighlightPrefab();
        CreateH2OGewinkeltEPHighlightPrefab();
        
        // Step 6 - Trigonal Planar Highlight
        CreateBF3TrigonalPlanarHighlightPrefab();
        
        // Step 7 - Trigonal Pyramidal Highlights
        CreateNH3TrigonalPyramidalHighlightPrefab();
        CreateNH3TrigonalPyramidalEPHighlightPrefab();
        
        // Step 8 - Tetraedrisch Highlight
        CreateCH4TetraedrischHighlightPrefab();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Prefabs erstellt", 
            "Prefabs erstellt in Assets/Resources/Prefabs/\n\n" +
            "Schritt 1-2: Bonds + Methan\n" +
            "Schritt 3: Molekülgeometrie\n" +
            "Schritt 4: CO2LinearHighlight\n" +
            "Schritt 5: H2OGewinkelt + Highlights\n" +
            "Schritt 6: BF3TrigonalPlanar + Highlight\n" +
            "Schritt 7: NH3TrigonalPyramidal + Highlights\n" +
            "Schritt 8: CH4Tetraedrisch + Highlight", 
            "OK");
    }
    
    private static void CreateNormalBondPrefab()
    {
        GameObject bond = new GameObject("NormalBond");
        
        // Create cylinder for bond
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "BondCylinder";
        cylinder.transform.SetParent(bond.transform);
        cylinder.transform.localPosition = Vector3.zero;
        cylinder.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
        
        // Set material - BLACK for all bonds
        var renderer = cylinder.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Custom/MoleculeUnlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = Color.black;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
        renderer.sharedMaterial = mat;
        
        // Remove collider
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
        
        // Add label
        CreateLabel(bond, "Normale Bindung", new Vector3(0, 0.4f, 0));
        
        // Save prefab
        SavePrefab(bond, "Assets/Resources/Prefabs/NormalBond.prefab");
        Object.DestroyImmediate(bond);
    }
    
    private static void CreateDashedBondPrefab()
    {
        GameObject bond = new GameObject("DashedBond");
        
        // Create dashed segments
        int segments = 5;
        float totalHeight = 0.6f;
        float segmentHeight = totalHeight / (segments * 2 - 1);
        float startY = -totalHeight / 2 + segmentHeight / 2;
        
        for (int i = 0; i < segments; i++)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = $"Segment_{i}";
            segment.transform.SetParent(bond.transform);
            segment.transform.localPosition = new Vector3(0, startY + i * segmentHeight * 2, 0);
            segment.transform.localScale = new Vector3(0.08f, segmentHeight * 0.4f, 0.08f);
            
            // Set material - BLACK for all bonds
            var renderer = segment.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Custom/MoleculeUnlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = Color.black;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
            renderer.sharedMaterial = mat;
            
            Object.DestroyImmediate(segment.GetComponent<Collider>());
        }
        
        // Add label
        CreateLabel(bond, "Gestrichelte Bindung\n(Hinter der Ebene)", new Vector3(0, 0.45f, 0));
        
        SavePrefab(bond, "Assets/Resources/Prefabs/DashedBond.prefab");
        Object.DestroyImmediate(bond);
    }
    
    private static void CreateWedgeBondPrefab()
    {
        GameObject bond = new GameObject("WedgeBond");
        
        // Create wedge shape using a cone-like structure
        // We'll use multiple cylinders with increasing radius
        int segments = 8;
        float totalHeight = 0.6f;
        float segmentHeight = totalHeight / segments;
        float startY = -totalHeight / 2 + segmentHeight / 2;
        
        for (int i = 0; i < segments; i++)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = $"WedgeSegment_{i}";
            segment.transform.SetParent(bond.transform);
            segment.transform.localPosition = new Vector3(0, startY + i * segmentHeight, 0);
            
            // Increase width along wedge
            float width = 0.02f + (i / (float)segments) * 0.15f;
            segment.transform.localScale = new Vector3(width, segmentHeight * 0.5f, width);
            
            // Set material - BLACK for all bonds
            var renderer = segment.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Custom/MoleculeUnlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = Color.black;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
            renderer.sharedMaterial = mat;
            
            Object.DestroyImmediate(segment.GetComponent<Collider>());
        }
        
        // Add label
        CreateLabel(bond, "Keil-Bindung\n(Zum Betrachter)", new Vector3(0, 0.45f, 0));
        
        SavePrefab(bond, "Assets/Resources/Prefabs/WedgeBond.prefab");
        Object.DestroyImmediate(bond);
    }
    
    private static void CreateAmmoniaDashedPrefab()
    {
        GameObject molecule = new GameObject("AmmoniaDashedHighlight");
        
        // Unified colors: Nitrogen = blue, Hydrogen = light grey
        Color nitrogenColor = new Color(0.2f, 0.4f, 0.9f);
        Color hydrogenColor = new Color(0.85f, 0.85f, 0.85f);
        
        // Create Nitrogen atom (center)
        GameObject nitrogen = CreateAtom(molecule, "N", new Vector3(0, 0, 0), 0.15f, nitrogenColor);
        
        // Create 3 Hydrogen atoms in trigonal pyramidal arrangement
        float hDist = 0.25f;
        float hHeight = -0.15f;
        GameObject h1 = CreateAtom(molecule, "H1", new Vector3(hDist, hHeight, 0), 0.08f, hydrogenColor);
        GameObject h2 = CreateAtom(molecule, "H2", new Vector3(-hDist * 0.5f, hHeight, hDist * 0.866f), 0.08f, hydrogenColor);
        GameObject h3 = CreateAtom(molecule, "H3", new Vector3(-hDist * 0.5f, hHeight, -hDist * 0.866f), 0.08f, hydrogenColor);
        
        // Create bonds - one dashed (highlighted), two normal
        CreateBondBetween(molecule, nitrogen.transform.position, h1.transform.position, true, true); // Dashed + highlighted
        CreateBondBetween(molecule, nitrogen.transform.position, h2.transform.position, false, false);
        CreateBondBetween(molecule, nitrogen.transform.position, h3.transform.position, false, false);
        
        // Add highlight indicator
        CreateLabel(molecule, "← Gestrichelte Bindung\n   (hinter der Ebene)", new Vector3(0.5f, -0.15f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/AmmoniaDashedHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    private static void CreateAmmoniaWedgePrefab()
    {
        GameObject molecule = new GameObject("AmmoniaWedgeHighlight");
        
        // Unified colors: Nitrogen = blue, Hydrogen = light grey
        Color nitrogenColor = new Color(0.2f, 0.4f, 0.9f);
        Color hydrogenColor = new Color(0.85f, 0.85f, 0.85f);
        
        // Create Nitrogen atom (center)
        GameObject nitrogen = CreateAtom(molecule, "N", new Vector3(0, 0, 0), 0.15f, nitrogenColor);
        
        // Create 3 Hydrogen atoms
        float hDist = 0.25f;
        float hHeight = -0.15f;
        GameObject h1 = CreateAtom(molecule, "H1", new Vector3(hDist, hHeight, 0), 0.08f, hydrogenColor);
        GameObject h2 = CreateAtom(molecule, "H2", new Vector3(-hDist * 0.5f, hHeight, hDist * 0.866f), 0.08f, hydrogenColor);
        GameObject h3 = CreateAtom(molecule, "H3", new Vector3(-hDist * 0.5f, hHeight, -hDist * 0.866f), 0.08f, hydrogenColor);
        
        // Create bonds - one wedge (highlighted), two normal
        CreateWedgeBondBetween(molecule, nitrogen.transform.position, h1.transform.position, true); // Wedge + highlighted
        CreateBondBetween(molecule, nitrogen.transform.position, h2.transform.position, false, false);
        CreateBondBetween(molecule, nitrogen.transform.position, h3.transform.position, false, false);
        
        // Add highlight indicator
        CreateLabel(molecule, "← Keil-Bindung\n   (zum Betrachter)", new Vector3(0.5f, -0.15f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/AmmoniaWedgeHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    private static GameObject CreateAtom(GameObject parent, string name, Vector3 position, float size, Color color)
    {
        GameObject atom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atom.name = name;
        atom.transform.SetParent(parent.transform);
        atom.transform.localPosition = position;
        atom.transform.localScale = Vector3.one * size;
        
        var renderer = atom.GetComponent<MeshRenderer>();
        // Use Unlit shader for standalone VR compatibility
        Shader shader = Shader.Find("Custom/MoleculeUnlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.3f);
        
        // Speichere Material als Asset um pink textures zu vermeiden
        string matPath = $"Assets/Tutorial/Materials/{parent.name}_{name}_Mat.mat";
        System.IO.Directory.CreateDirectory("Assets/Tutorial/Materials");
        AssetDatabase.CreateAsset(mat, matPath);
        
        renderer.sharedMaterial = mat;
        
        Object.DestroyImmediate(atom.GetComponent<Collider>());
        
        return atom;
    }
    
    private static void CreateBondBetween(GameObject parent, Vector3 start, Vector3 end, bool dashed, bool highlighted)
    {
        GameObject bond = new GameObject(dashed ? "DashedBond" : "NormalBond");
        bond.transform.SetParent(parent.transform);
        
        Vector3 midpoint = (start + end) / 2;
        Vector3 direction = end - start;
        float length = direction.magnitude;
        
        bond.transform.localPosition = midpoint;
        bond.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        
        // Unified color scheme: black bonds, green/yellow highlights
        Color bondColor = Color.black;
        Color highlightColor = new Color(0.2f, 0.9f, 0.2f); // Green highlight
        Color dashedHighlightColor = new Color(1f, 0.9f, 0.2f); // Yellow highlight
        
        if (dashed)
        {
            // Create dashed segments
            int segments = 4;
            float segmentLength = length / (segments * 2 - 1);
            
            for (int i = 0; i < segments; i++)
            {
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"Dash_{i}";
                seg.transform.SetParent(bond.transform);
                float offset = -length / 2 + segmentLength / 2 + i * segmentLength * 2;
                seg.transform.localPosition = new Vector3(0, offset, 0);
                seg.transform.localScale = new Vector3(0.03f, segmentLength * 0.4f, 0.03f);
                seg.transform.localRotation = Quaternion.identity;
                
                var rend = seg.GetComponent<MeshRenderer>();
                Shader shader = Shader.Find("Custom/MoleculeUnlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Standard");
                Material m = new Material(shader);
                m.color = highlighted ? dashedHighlightColor : bondColor;
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.3f);
                
                // Speichere Material als Asset
                string matPath = $"Assets/Tutorial/Materials/{parent.name}_DashSeg{i}_Mat.mat";
                System.IO.Directory.CreateDirectory("Assets/Tutorial/Materials");
                AssetDatabase.CreateAsset(m, matPath);
                
                rend.sharedMaterial = m;
                
                Object.DestroyImmediate(seg.GetComponent<Collider>());
            }
        }
        else
        {
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = "BondCylinder";
            cyl.transform.SetParent(bond.transform);
            cyl.transform.localPosition = Vector3.zero;
            cyl.transform.localScale = new Vector3(0.03f, length * 0.45f, 0.03f);
            cyl.transform.localRotation = Quaternion.identity;
            
            var rend = cyl.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Custom/MoleculeUnlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.color = highlighted ? highlightColor : bondColor;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.3f);
            
            // Speichere Material als Asset
            string matPath = $"Assets/Tutorial/Materials/{parent.name}_Bond_Mat.mat";
            System.IO.Directory.CreateDirectory("Assets/Tutorial/Materials");
            AssetDatabase.CreateAsset(m, matPath);
            
            rend.sharedMaterial = m;
            
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
        }
    }
    
    private static void CreateWedgeBondBetween(GameObject parent, Vector3 start, Vector3 end, bool highlighted)
    {
        GameObject bond = new GameObject("WedgeBond");
        bond.transform.SetParent(parent.transform);
        
        Vector3 midpoint = (start + end) / 2;
        Vector3 direction = end - start;
        float length = direction.magnitude;
        
        bond.transform.localPosition = midpoint;
        bond.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        
        // Unified color scheme: black bonds, yellow highlights for wedge
        Color bondColor = Color.black;
        Color highlightColor = new Color(1f, 0.9f, 0.2f); // Yellow highlight
        
        // Create wedge segments
        int segments = 6;
        float segmentLength = length / segments;
        
        for (int i = 0; i < segments; i++)
        {
            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = $"Wedge_{i}";
            seg.transform.SetParent(bond.transform);
            float offset = -length / 2 + segmentLength / 2 + i * segmentLength;
            seg.transform.localPosition = new Vector3(0, offset, 0);
            
            float width = 0.01f + (i / (float)segments) * 0.06f;
            seg.transform.localScale = new Vector3(width, segmentLength * 0.5f, width);
            seg.transform.localRotation = Quaternion.identity;
            
            var rend = seg.GetComponent<MeshRenderer>();
            Shader shader = Shader.Find("Custom/MoleculeUnlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.color = highlighted ? highlightColor : bondColor;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.3f);
            
            // Speichere Material als Asset
            string matPath = $"Assets/Tutorial/Materials/{parent.name}_WedgeSeg{i}_Mat.mat";
            System.IO.Directory.CreateDirectory("Assets/Tutorial/Materials");
            AssetDatabase.CreateAsset(m, matPath);
            
            rend.sharedMaterial = m;
            
            Object.DestroyImmediate(seg.GetComponent<Collider>());
        }
    }
    
    /// <summary>
    /// Creates a label with LegacyRuntime font (same as debug display)
    /// </summary>
    private static void CreateLabel(GameObject parent, string text, Vector3 localPosition)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent.transform);
        labelObj.transform.localPosition = localPosition;
        
        // Add TextMesh for 3D text (works in VR)
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 24;
        textMesh.characterSize = 0.015f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
        textMesh.fontStyle = FontStyle.Normal;
        textMesh.richText = false;
        
        // Use LegacyRuntime font (same as TabletURLDisplay debug)
        textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        // Create material with GUI/Text Shader (supports font atlases properly)
        MeshRenderer renderer = labelObj.GetComponent<MeshRenderer>();
        if (renderer != null && textMesh.font != null)
        {
            // Use Unity's GUI/Text Shader which is designed for TextMesh
            Material textMat = new Material(Shader.Find("GUI/Text Shader"));
            textMat.mainTexture = textMesh.font.material.mainTexture; // Font atlas texture
            textMat.color = Color.white;
            
            // Clean up material name for file system (remove ALL invalid characters)
            string cleanName = text
                .Replace(" ", "")
                .Replace("(", "")
                .Replace(")", "")
                .Replace("/", "")
                .Replace("\\", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(":", "")
                .Replace("*", "")
                .Replace("?", "")
                .Replace("\"", "")
                .Replace("<", "")
                .Replace(">", "")
                .Replace("|", "");
            textMat.name = $"{cleanName}_Label_Mat";
            
            // Save material as asset so it persists in prefab
            string matPath = $"Assets/Resources/Materials/{textMat.name}.mat";
            AssetDatabase.CreateAsset(textMat, matPath);
            
            // Load the saved asset and assign
            Material savedMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            renderer.sharedMaterial = savedMat;
        }
    }
    
    /// <summary>
    /// Methan 3D - räumlicher tetraedrischer Bau
    /// </summary>
    private static void CreateMethan3DPrefab()
    {
        GameObject molecule = new GameObject("Methan3D");
        
        // Unified colors: Carbon = dark grey, Hydrogen = light grey
        Color carbonColor = new Color(0.3f, 0.3f, 0.3f);
        Color hydrogenColor = new Color(0.85f, 0.85f, 0.85f);
        
        // Create Carbon atom (center)
        GameObject carbon = CreateAtom(molecule, "C", Vector3.zero, 0.15f, carbonColor);
        
        // Tetraeder-Geometrie: 109.5° Bindungswinkel
        float bondLength = 0.25f;
        float tetAngle = 109.47f * Mathf.Deg2Rad;
        
        // H1 - oben
        Vector3 h1Pos = new Vector3(0, bondLength, 0);
        
        // H2, H3, H4 - unten im gleichseitigen Dreieck
        float downY = -bondLength * Mathf.Cos(tetAngle / 2);
        float radius = bondLength * Mathf.Sin(tetAngle / 2);
        
        Vector3 h2Pos = new Vector3(radius, downY, 0);
        Vector3 h3Pos = new Vector3(-radius * 0.5f, downY, radius * 0.866f);
        Vector3 h4Pos = new Vector3(-radius * 0.5f, downY, -radius * 0.866f);
        
        // Create 4 Hydrogen atoms (light grey)
        GameObject h1 = CreateAtom(molecule, "H1", h1Pos, 0.08f, hydrogenColor);
        GameObject h2 = CreateAtom(molecule, "H2", h2Pos, 0.08f, hydrogenColor);
        GameObject h3 = CreateAtom(molecule, "H3", h3Pos, 0.08f, hydrogenColor);
        GameObject h4 = CreateAtom(molecule, "H4", h4Pos, 0.08f, hydrogenColor);
        
        // Create normal bonds (alle gleich)
        CreateBondBetween(molecule, Vector3.zero, h1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h2Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h3Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h4Pos, false, false);
        
        // Add label
        CreateLabel(molecule, "Methan (CH₄)\nRäumlicher Bau", new Vector3(0, 0.4f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/Methan3D.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// Methan Keilstrichformel - NUR Bindungen für räumlichen Bau (ohne Atome)
    /// Wird über Methan3D gelegt, um Keilstrich-Notation zu zeigen
    /// </summary>
    private static void CreateMethanKeilstrichPrefab()
    {
        GameObject molecule = new GameObject("MethanKeilstrich");
        
        // KEINE Atome - nur Bindungen in Keilstrich-Form
        // Diese legen sich über die Bindungen von Methan3D
        
        float bondLength = 0.25f;
        
        // Tetraeder-Winkel: 109.5° (gleiche Geometrie wie Methan3D)
        float tetAngle = 109.5f * Mathf.Deg2Rad;
        
        // H1 - oben (normale Bindung)
        Vector3 h1Pos = new Vector3(0, bondLength, 0);
        
        // H2, H3, H4 - unten im gleichseitigen Dreieck
        float downY = -bondLength * Mathf.Cos(tetAngle / 2);
        float radius = bondLength * Mathf.Sin(tetAngle / 2);
        
        // H2 - rechts vorne (normale Bindung)
        Vector3 h2Pos = new Vector3(radius, downY, 0);
        
        // H3 - links hinten, weiter weg vom Betrachter (gestrichelte Bindung)
        Vector3 h3Pos = new Vector3(-radius * 0.5f, downY, radius * 0.866f);
        
        // H4 - links vorne, näher zum Betrachter (Keil-Bindung)
        Vector3 h4Pos = new Vector3(-radius * 0.5f, downY, -radius * 0.866f);
        
        // Create bonds in Keilstrich-Notation:
        // - 2 normale Bindungen (h1 oben, h2 rechts)
        CreateBondBetween(molecule, Vector3.zero, h1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h2Pos, false, false);
        
        // Keil nach vorne zum Betrachter (h4, negative Z) - highlighted
        CreateWedgeBondBetween(molecule, Vector3.zero, h4Pos, true);
        
        // Gestrichelt nach hinten vom Betrachter weg (h3, positive Z) - highlighted
        CreateBondBetween(molecule, Vector3.zero, h3Pos, true, true);
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/MethanKeilstrich.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// CO2 - Linearer Bau (180°)
    /// </summary>
    private static void CreateCO2LinearPrefab()
    {
        GameObject molecule = new GameObject("CO2Linear");
        
        // Unified colors: Carbon = dark grey, Oxygen = red
        Color carbonColor = new Color(0.3f, 0.3f, 0.3f);
        Color oxygenColor = new Color(0.9f, 0.2f, 0.2f);
        
        float bondLength = 0.2f;
        
        // Carbon in der Mitte
        CreateAtom(molecule, "C", Vector3.zero, 0.12f, carbonColor);
        
        // Sauerstoff links und rechts - rot
        Vector3 o1Pos = new Vector3(-bondLength, 0, 0);
        Vector3 o2Pos = new Vector3(bondLength, 0, 0);
        CreateAtom(molecule, "O1", o1Pos, 0.1f, oxygenColor);
        CreateAtom(molecule, "O2", o2Pos, 0.1f, oxygenColor);
        
        // Doppelbindungen (als dickere Zylinder dargestellt)
        CreateDoubleBond(molecule, Vector3.zero, o1Pos);
        CreateDoubleBond(molecule, Vector3.zero, o2Pos);
        
        // Label
        CreateLabel(molecule, "CO₂ - Linear (180°)", new Vector3(0, 0.3f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/CO2Linear.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// CO2 Linear mit grün hervorgehobenen Bindungen (für Step 4)
    /// </summary>
    private static void CreateCO2LinearHighlightPrefab()
    {
        GameObject molecule = new GameObject("CO2LinearHighlight");
        
        // Unified colors
        Color carbonColor = new Color(0.3f, 0.3f, 0.3f);
        Color oxygenColor = new Color(0.9f, 0.2f, 0.2f);
        
        float bondLength = 0.2f;
        
        // Carbon in der Mitte
        CreateAtom(molecule, "C", Vector3.zero, 0.12f, carbonColor);
        
        // Sauerstoff links und rechts - rot
        Vector3 o1Pos = new Vector3(-bondLength, 0, 0);
        Vector3 o2Pos = new Vector3(bondLength, 0, 0);
        CreateAtom(molecule, "O1", o1Pos, 0.1f, oxygenColor);
        CreateAtom(molecule, "O2", o2Pos, 0.1f, oxygenColor);
        
        // Grün hervorgehobene Doppelbindungen
        CreateHighlightedDoubleBond(molecule, Vector3.zero, o1Pos);
        CreateHighlightedDoubleBond(molecule, Vector3.zero, o2Pos);
        
        // Label
        CreateLabel(molecule, "CO₂ - Bindungen", new Vector3(0, 0.3f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/CO2LinearHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// H2O - Gewinkelter Bau (~104.5°)
    /// </summary>
    private static void CreateH2OGewinkeltPrefab()
    {
        GameObject molecule = new GameObject("H2OGewinkelt");
        
        // Unified colors: Oxygen = red, Hydrogen = light grey
        Color oxygenColor = new Color(0.9f, 0.2f, 0.2f);
        Color hydrogenColor = new Color(0.85f, 0.85f, 0.85f);
        
        float bondLength = 0.2f;
        float angle = 104.5f * Mathf.Deg2Rad / 2; // Halber Winkel
        
        // Sauerstoff in der Mitte - rot
        CreateAtom(molecule, "O", Vector3.zero, 0.12f, oxygenColor);
        
        // Wasserstoff oben links und rechts
        Vector3 h1Pos = new Vector3(-bondLength * Mathf.Sin(angle), bondLength * Mathf.Cos(angle), 0);
        Vector3 h2Pos = new Vector3(bondLength * Mathf.Sin(angle), bondLength * Mathf.Cos(angle), 0);
        CreateAtom(molecule, "H1", h1Pos, 0.08f, hydrogenColor);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, hydrogenColor);
        
        // Bindungen
        CreateBondBetween(molecule, Vector3.zero, h1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h2Pos, false, false);
        
        // Winkelanzeige (Bogen)
        CreateAngleIndicator(molecule, Vector3.zero, h1Pos, h2Pos, "104.5°");
        
        // Label
        CreateLabel(molecule, "H₂O - Gewinkelt (104.5°)", new Vector3(0, 0.35f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/H2OGewinkelt.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// BF3 - Trigonal-planar (120°)
    /// </summary>
    private static void CreateBF3TrigonalPlanarPrefab()
    {
        GameObject molecule = new GameObject("BF3TrigonalPlanar");
        
        // Unified colors: Boron = pink, Fluorine = light green
        Color boronColor = new Color(1f, 0.7f, 0.7f);
        Color fluorineColor = new Color(0.5f, 0.9f, 0.5f);
        
        float bondLength = 0.2f;
        
        // Bor in der Mitte - rosa/pink
        CreateAtom(molecule, "B", Vector3.zero, 0.11f, boronColor);
        
        // Fluor in 120° Abständen in der XY-Ebene
        Vector3 f1Pos = new Vector3(0, bondLength, 0); // oben
        Vector3 f2Pos = new Vector3(-bondLength * 0.866f, -bondLength * 0.5f, 0); // unten links
        Vector3 f3Pos = new Vector3(bondLength * 0.866f, -bondLength * 0.5f, 0); // unten rechts
        
        CreateAtom(molecule, "F1", f1Pos, 0.09f, fluorineColor);
        CreateAtom(molecule, "F2", f2Pos, 0.09f, fluorineColor);
        CreateAtom(molecule, "F3", f3Pos, 0.09f, fluorineColor);
        
        // Bindungen
        CreateBondBetween(molecule, Vector3.zero, f1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, f2Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, f3Pos, false, false);
        
        // Label
        CreateLabel(molecule, "BF₃ - Trigonal-planar (120°)", new Vector3(0, 0.35f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/BF3TrigonalPlanar.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// NH3 - Trigonal-pyramidal (~107°)
    /// </summary>
    private static void CreateNH3TrigonalPyramidalPrefab()
    {
        GameObject molecule = new GameObject("NH3TrigonalPyramidal");
        
        // Unified colors: Nitrogen = blue, Hydrogen = light grey
        Color nitrogenColor = new Color(0.2f, 0.4f, 0.9f);
        Color hydrogenColor = new Color(0.85f, 0.85f, 0.85f);
        Color electronPairColor = new Color(0.5f, 0.6f, 1f, 0.5f);
        
        float bondLength = 0.2f;
        float angle = 107f * Mathf.Deg2Rad;
        
        // Stickstoff oben - blau
        CreateAtom(molecule, "N", Vector3.zero, 0.11f, nitrogenColor);
        
        // Wasserstoff unten im Dreieck (pyramidal)
        float downY = -bondLength * Mathf.Cos(angle / 2);
        float radius = bondLength * Mathf.Sin(angle / 2);
        
        Vector3 h1Pos = new Vector3(radius, downY, 0);
        Vector3 h2Pos = new Vector3(-radius * 0.5f, downY, radius * 0.866f);
        Vector3 h3Pos = new Vector3(-radius * 0.5f, downY, -radius * 0.866f);
        
        CreateAtom(molecule, "H1", h1Pos, 0.08f, hydrogenColor);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, hydrogenColor);
        CreateAtom(molecule, "H3", h3Pos, 0.08f, hydrogenColor);
        
        // Bindungen
        CreateBondBetween(molecule, Vector3.zero, h1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h2Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h3Pos, false, false);
        
        // Freies Elektronenpaar andeuten (kleine Kugel oben)
        CreateAtom(molecule, "LP", new Vector3(0, bondLength * 0.6f, 0), 0.04f, electronPairColor);
        
        // Label
        CreateLabel(molecule, "NH₃ - Trigonal-pyramidal (107°)", new Vector3(0, 0.4f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/NH3TrigonalPyramidal.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// CH4 - Tetraedrisch (109.5°)
    /// </summary>
    private static void CreateCH4TetraedrischPrefab()
    {
        GameObject molecule = new GameObject("CH4Tetraedrisch");
        
        // Unified colors: Carbon = dark grey, Hydrogen = light grey
        Color carbonColor = new Color(0.3f, 0.3f, 0.3f);
        Color hydrogenColor = new Color(0.85f, 0.85f, 0.85f);
        
        float bondLength = 0.2f;
        float tetAngle = 109.5f * Mathf.Deg2Rad;
        
        // Kohlenstoff in der Mitte - dunkelgrau
        CreateAtom(molecule, "C", Vector3.zero, 0.12f, carbonColor);
        
        // Tetraeder-Geometrie
        Vector3 h1Pos = new Vector3(0, bondLength, 0); // oben
        
        float downY = -bondLength * Mathf.Cos(tetAngle / 2);
        float radius = bondLength * Mathf.Sin(tetAngle / 2);
        
        Vector3 h2Pos = new Vector3(radius, downY, 0);
        Vector3 h3Pos = new Vector3(-radius * 0.5f, downY, radius * 0.866f);
        Vector3 h4Pos = new Vector3(-radius * 0.5f, downY, -radius * 0.866f);
        
        CreateAtom(molecule, "H1", h1Pos, 0.08f, hydrogenColor);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, hydrogenColor);
        CreateAtom(molecule, "H3", h3Pos, 0.08f, hydrogenColor);
        CreateAtom(molecule, "H4", h4Pos, 0.08f, hydrogenColor);
        
        // Bindungen
        CreateBondBetween(molecule, Vector3.zero, h1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h2Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h3Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h4Pos, false, false);
        
        // Label
        CreateLabel(molecule, "CH₄ - Tetraedrisch (109.5°)", new Vector3(0, 0.35f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/CH4Tetraedrisch.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// Erstellt eine Doppelbindung (zwei parallele Zylinder)
    /// </summary>
    private static void CreateDoubleBond(GameObject parent, Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float length = direction.magnitude;
        Vector3 midpoint = (from + to) / 2f;
        
        float offset = 0.02f; // Abstand zwischen den Bindungen
        Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.forward).normalized * offset;
        if (perpendicular.magnitude < 0.01f)
        {
            perpendicular = Vector3.Cross(direction.normalized, Vector3.up).normalized * offset;
        }
        
        // Erste Bindung
        CreateSingleBondCylinder(parent, midpoint + perpendicular, direction, length, 0.03f, new Color(0.5f, 0.5f, 0.5f));
        // Zweite Bindung
        CreateSingleBondCylinder(parent, midpoint - perpendicular, direction, length, 0.03f, new Color(0.5f, 0.5f, 0.5f));
    }
    
    /// <summary>
    /// Erstellt eine grün hervorgehobene Doppelbindung (für Step 4)
    /// </summary>
    private static void CreateHighlightedDoubleBond(GameObject parent, Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float length = direction.magnitude;
        Vector3 midpoint = (from + to) / 2f;
        
        float offset = 0.02f;
        Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.forward).normalized * offset;
        if (perpendicular.magnitude < 0.01f)
        {
            perpendicular = Vector3.Cross(direction.normalized, Vector3.up).normalized * offset;
        }
        
        // Grüne Bindungen (hervorgehoben)
        Color highlightGreen = new Color(0.2f, 0.9f, 0.2f); // Helles Grün
        CreateSingleBondCylinder(parent, midpoint + perpendicular, direction, length, 0.035f, highlightGreen);
        CreateSingleBondCylinder(parent, midpoint - perpendicular, direction, length, 0.035f, highlightGreen);
    }
    
    private static void CreateSingleBondCylinder(GameObject parent, Vector3 position, Vector3 direction, float length, float radius, Color color)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "BondPart";
        cylinder.transform.SetParent(parent.transform);
        cylinder.transform.position = position;
        cylinder.transform.localScale = new Vector3(radius * 2, length / 2f, radius * 2);
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        
        var rend = cylinder.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
        mat.color = color;
        rend.sharedMaterial = mat;
        
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
    }
    
    /// <summary>
    /// Erstellt einen Winkelindikator (Bogen zwischen zwei Bindungen)
    /// </summary>
    private static void CreateAngleIndicator(GameObject parent, Vector3 center, Vector3 to1, Vector3 to2, string angleText)
    {
        // Einfacher Bogen mit LineRenderer
        GameObject arcObj = new GameObject("AngleIndicator");
        arcObj.transform.SetParent(parent.transform);
        arcObj.transform.localPosition = center;
        
        LineRenderer lr = arcObj.AddComponent<LineRenderer>();
        
        // Verwende Quest-kompatiblen Shader und speichere Material
        Shader shader = Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        Material arcMat = new Material(shader);
        arcMat.color = new Color(1f, 1f, 0f, 1f); // Gelb
        
        // Speichere Material als Asset um pink textures zu vermeiden
        string materialPath = "Assets/Resources/Materials/TutorialAngleMaterial.mat";
        string materialDir = System.IO.Path.GetDirectoryName(materialPath);
        if (!System.IO.Directory.Exists(materialDir))
        {
            System.IO.Directory.CreateDirectory(materialDir);
        }
        
        Material savedMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (savedMat == null)
        {
            AssetDatabase.CreateAsset(arcMat, materialPath);
            savedMat = arcMat;
            Debug.Log($"[Tutorial] Angle Material erstellt: {materialPath}");
        }
        
        lr.sharedMaterial = savedMat;
        lr.startWidth = 0.008f;
        lr.endWidth = 0.008f;
        lr.useWorldSpace = false;
        
        // Bogen zeichnen
        Vector3 dir1 = (to1 - center).normalized;
        Vector3 dir2 = (to2 - center).normalized;
        float arcRadius = 0.08f;
        
        int segments = 10;
        lr.positionCount = segments + 1;
        
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 dir = Vector3.Slerp(dir1, dir2, t);
            lr.SetPosition(i, dir * arcRadius);
        }
    }
    
    // ============================================
    // Step 5: H2O Gewinkelter Bau Highlights
    // ============================================
    
    /// <summary>
    /// H2O mit grün hervorgehobenen Bindungen
    /// </summary>
    private static void CreateH2OGewinkeltHighlightPrefab()
    {
        GameObject molecule = new GameObject("H2OGewinkeltHighlight");
        
        float bondLength = 0.2f;
        float angle = 104.5f * Mathf.Deg2Rad / 2;
        
        // Sauerstoff in der Mitte - rot
        CreateAtom(molecule, "O", Vector3.zero, 0.12f, new Color(1f, 0.2f, 0.2f));
        
        // Wasserstoff
        Vector3 h1Pos = new Vector3(-bondLength * Mathf.Sin(angle), bondLength * Mathf.Cos(angle), 0);
        Vector3 h2Pos = new Vector3(bondLength * Mathf.Sin(angle), bondLength * Mathf.Cos(angle), 0);
        CreateAtom(molecule, "H1", h1Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, Color.white);
        
        // Grüne Bindungen
        Color highlightGreen = new Color(0.2f, 0.9f, 0.2f);
        CreateHighlightedBond(molecule, Vector3.zero, h1Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, h2Pos, highlightGreen);
        
        CreateLabel(molecule, "H₂O - Bindungen", new Vector3(0, 0.35f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/H2OGewinkeltHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// H2O mit rot hervorgehobenem Elektronenpaar
    /// </summary>
    private static void CreateH2OGewinkeltEPHighlightPrefab()
    {
        GameObject molecule = new GameObject("H2OGewinkeltEPHighlight");
        
        float bondLength = 0.2f;
        float angle = 104.5f * Mathf.Deg2Rad / 2;
        
        // Sauerstoff in der Mitte - rot
        CreateAtom(molecule, "O", Vector3.zero, 0.12f, new Color(1f, 0.2f, 0.2f));
        
        // Wasserstoff
        Vector3 h1Pos = new Vector3(-bondLength * Mathf.Sin(angle), bondLength * Mathf.Cos(angle), 0);
        Vector3 h2Pos = new Vector3(bondLength * Mathf.Sin(angle), bondLength * Mathf.Cos(angle), 0);
        CreateAtom(molecule, "H1", h1Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, Color.white);
        
        // Normale Bindungen
        CreateBondBetween(molecule, Vector3.zero, h1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h2Pos, false, false);
        
        // 2 freie Elektronenpaare in rot (unten, nach hinten versetzt)
        Color highlightRed = new Color(0.9f, 0.2f, 0.2f);
        CreateAtom(molecule, "LP1", new Vector3(-0.08f, -bondLength * 0.5f, 0.05f), 0.05f, highlightRed);
        CreateAtom(molecule, "LP2", new Vector3(0.08f, -bondLength * 0.5f, 0.05f), 0.05f, highlightRed);
        
        CreateLabel(molecule, "H₂O - freie EP", new Vector3(0, 0.35f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/H2OGewinkeltEPHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    // ============================================
    // Step 6: BF3 Trigonal Planar Highlight
    // ============================================
    
    /// <summary>
    /// BF3 mit grün hervorgehobenen Bindungen
    /// </summary>
    private static void CreateBF3TrigonalPlanarHighlightPrefab()
    {
        GameObject molecule = new GameObject("BF3TrigonalPlanarHighlight");
        
        float bondLength = 0.2f;
        
        // Bor in der Mitte - rosa
        CreateAtom(molecule, "B", Vector3.zero, 0.11f, new Color(1f, 0.7f, 0.7f));
        
        // Fluor in 120° Abständen
        Vector3 f1Pos = new Vector3(0, bondLength, 0);
        Vector3 f2Pos = new Vector3(-bondLength * 0.866f, -bondLength * 0.5f, 0);
        Vector3 f3Pos = new Vector3(bondLength * 0.866f, -bondLength * 0.5f, 0);
        
        CreateAtom(molecule, "F1", f1Pos, 0.09f, new Color(0.5f, 1f, 0.5f));
        CreateAtom(molecule, "F2", f2Pos, 0.09f, new Color(0.5f, 1f, 0.5f));
        CreateAtom(molecule, "F3", f3Pos, 0.09f, new Color(0.5f, 1f, 0.5f));
        
        // Grüne Bindungen
        Color highlightGreen = new Color(0.2f, 0.9f, 0.2f);
        CreateHighlightedBond(molecule, Vector3.zero, f1Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, f2Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, f3Pos, highlightGreen);
        
        CreateLabel(molecule, "BF₃ - Bindungen", new Vector3(0, 0.35f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/BF3TrigonalPlanarHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    // ============================================
    // Step 7: NH3 Trigonal Pyramidal Highlights
    // ============================================
    
    /// <summary>
    /// NH3 mit grün hervorgehobenen Bindungen
    /// </summary>
    private static void CreateNH3TrigonalPyramidalHighlightPrefab()
    {
        GameObject molecule = new GameObject("NH3TrigonalPyramidalHighlight");
        
        float bondLength = 0.2f;
        float angle = 107f * Mathf.Deg2Rad;
        
        // Stickstoff - blau
        CreateAtom(molecule, "N", Vector3.zero, 0.11f, new Color(0.2f, 0.2f, 1f));
        
        // Wasserstoff unten im Dreieck
        float downY = -bondLength * Mathf.Cos(angle / 2);
        float radius = bondLength * Mathf.Sin(angle / 2);
        
        Vector3 h1Pos = new Vector3(radius, downY, 0);
        Vector3 h2Pos = new Vector3(-radius * 0.5f, downY, radius * 0.866f);
        Vector3 h3Pos = new Vector3(-radius * 0.5f, downY, -radius * 0.866f);
        
        CreateAtom(molecule, "H1", h1Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H3", h3Pos, 0.08f, Color.white);
        
        // Grüne Bindungen
        Color highlightGreen = new Color(0.2f, 0.9f, 0.2f);
        CreateHighlightedBond(molecule, Vector3.zero, h1Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, h2Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, h3Pos, highlightGreen);
        
        // Freies Elektronenpaar (normal)
        CreateAtom(molecule, "LP", new Vector3(0, bondLength * 0.6f, 0), 0.04f, new Color(0.5f, 0.5f, 1f, 0.5f));
        
        CreateLabel(molecule, "NH₃ - Bindungen", new Vector3(0, 0.4f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/NH3TrigonalPyramidalHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// NH3 mit rot hervorgehobenem Elektronenpaar
    /// </summary>
    private static void CreateNH3TrigonalPyramidalEPHighlightPrefab()
    {
        GameObject molecule = new GameObject("NH3TrigonalPyramidalEPHighlight");
        
        float bondLength = 0.2f;
        float angle = 107f * Mathf.Deg2Rad;
        
        // Stickstoff - blau
        CreateAtom(molecule, "N", Vector3.zero, 0.11f, new Color(0.2f, 0.2f, 1f));
        
        // Wasserstoff unten im Dreieck
        float downY = -bondLength * Mathf.Cos(angle / 2);
        float radius = bondLength * Mathf.Sin(angle / 2);
        
        Vector3 h1Pos = new Vector3(radius, downY, 0);
        Vector3 h2Pos = new Vector3(-radius * 0.5f, downY, radius * 0.866f);
        Vector3 h3Pos = new Vector3(-radius * 0.5f, downY, -radius * 0.866f);
        
        CreateAtom(molecule, "H1", h1Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H3", h3Pos, 0.08f, Color.white);
        
        // Normale Bindungen
        CreateBondBetween(molecule, Vector3.zero, h1Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h2Pos, false, false);
        CreateBondBetween(molecule, Vector3.zero, h3Pos, false, false);
        
        // Rot hervorgehobenes freies Elektronenpaar
        Color highlightRed = new Color(0.9f, 0.2f, 0.2f);
        CreateAtom(molecule, "LP", new Vector3(0, bondLength * 0.6f, 0), 0.06f, highlightRed);
        
        CreateLabel(molecule, "NH₃ - freies EP", new Vector3(0, 0.4f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/NH3TrigonalPyramidalEPHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    // ============================================
    // Step 8: CH4 Tetraedrisch Highlight
    // ============================================
    
    /// <summary>
    /// CH4 mit grün hervorgehobenen Bindungen
    /// </summary>
    private static void CreateCH4TetraedrischHighlightPrefab()
    {
        GameObject molecule = new GameObject("CH4TetraedrischHighlight");
        
        float bondLength = 0.2f;
        float tetAngle = 109.5f * Mathf.Deg2Rad;
        
        // Kohlenstoff - schwarz
        CreateAtom(molecule, "C", Vector3.zero, 0.12f, new Color(0.2f, 0.2f, 0.2f));
        
        // Tetraeder-Geometrie
        Vector3 h1Pos = new Vector3(0, bondLength, 0);
        float downY = -bondLength * Mathf.Cos(tetAngle / 2);
        float radius = bondLength * Mathf.Sin(tetAngle / 2);
        
        Vector3 h2Pos = new Vector3(radius, downY, 0);
        Vector3 h3Pos = new Vector3(-radius * 0.5f, downY, radius * 0.866f);
        Vector3 h4Pos = new Vector3(-radius * 0.5f, downY, -radius * 0.866f);
        
        CreateAtom(molecule, "H1", h1Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H2", h2Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H3", h3Pos, 0.08f, Color.white);
        CreateAtom(molecule, "H4", h4Pos, 0.08f, Color.white);
        
        // Grüne Bindungen
        Color highlightGreen = new Color(0.2f, 0.9f, 0.2f);
        CreateHighlightedBond(molecule, Vector3.zero, h1Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, h2Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, h3Pos, highlightGreen);
        CreateHighlightedBond(molecule, Vector3.zero, h4Pos, highlightGreen);
        
        CreateLabel(molecule, "CH₄ - Bindungen", new Vector3(0, 0.35f, 0));
        
        SavePrefab(molecule, "Assets/Resources/Prefabs/CH4TetraedrischHighlight.prefab");
        Object.DestroyImmediate(molecule);
    }
    
    /// <summary>
    /// Helper: Erstellt eine farbige hervorgehobene Einzelbindung
    /// </summary>
    private static void CreateHighlightedBond(GameObject parent, Vector3 from, Vector3 to, Color color)
    {
        Vector3 direction = to - from;
        float length = direction.magnitude;
        Vector3 midpoint = (from + to) / 2f;
        
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "HighlightBond";
        cylinder.transform.SetParent(parent.transform);
        cylinder.transform.localPosition = midpoint;
        cylinder.transform.localScale = new Vector3(0.04f, length / 2f, 0.04f); // Etwas dicker
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        
        var rend = cylinder.GetComponent<MeshRenderer>();
        Material mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
        mat.color = color;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.8f);
        rend.sharedMaterial = mat;
        
        Object.DestroyImmediate(cylinder.GetComponent<Collider>());
    }
    
    private static void SavePrefab(GameObject obj, string path)
    {
        // Check if prefab already exists
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existingPrefab != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
        
        PrefabUtility.SaveAsPrefabAsset(obj, path);
        Debug.Log($"[TutorialPrefabs] Created prefab: {path}");
    }
}
