using UnityEngine;

/// <summary>
/// Unified color scheme for molecular visualization
/// All colors defined in one place for consistency
/// </summary>
public static class MoleculeColors
{
    // ============ ATOM COLORS ============
    // Carbon: Dark Grey
    public static readonly Color Carbon = new Color(0.3f, 0.3f, 0.3f);
    
    // Hydrogen: Light Grey
    public static readonly Color Hydrogen = new Color(0.85f, 0.85f, 0.85f);
    
    // Oxygen: Red
    public static readonly Color Oxygen = new Color(0.9f, 0.2f, 0.2f);
    
    // Nitrogen: Blue
    public static readonly Color Nitrogen = new Color(0.2f, 0.4f, 0.9f);
    
    // Fluorine: Light Green
    public static readonly Color Fluorine = new Color(0.5f, 0.9f, 0.5f);
    
    // Boron: Pink/Salmon
    public static readonly Color Boron = new Color(1f, 0.7f, 0.7f);
    
    // Sulfur: Yellow
    public static readonly Color Sulfur = new Color(1f, 1f, 0.2f);
    
    // Chlorine: Green
    public static readonly Color Chlorine = new Color(0.2f, 0.9f, 0.2f);
    
    // Unknown: Magenta (error indicator)
    public static readonly Color Unknown = Color.magenta;

    // ============ BOND COLORS ============
    // Normal bonds: Black
    public static readonly Color Bond = Color.black;
    
    // Dashed bonds (behind plane): Dark Grey
    public static readonly Color BondDashed = new Color(0.2f, 0.2f, 0.2f);
    
    // Wedge bonds (towards viewer): Black
    public static readonly Color BondWedge = Color.black;

    // ============ HIGHLIGHT COLORS ============
    // Primary highlight: Green (for bonds)
    public static readonly Color HighlightGreen = new Color(0.2f, 0.9f, 0.2f);
    
    // Secondary highlight: Yellow (for bonds or atoms)
    public static readonly Color HighlightYellow = new Color(1f, 0.9f, 0.2f);
    
    // Electron pair highlight: Red
    public static readonly Color HighlightRed = new Color(0.9f, 0.2f, 0.2f);

    /// <summary>
    /// Get color for an element by its symbol
    /// </summary>
    public static Color GetAtomColor(string element)
    {
        switch (element.ToUpper())
        {
            case "C": return Carbon;
            case "H": return Hydrogen;
            case "O": return Oxygen;
            case "N": return Nitrogen;
            case "F": return Fluorine;
            case "B": return Boron;
            case "S": return Sulfur;
            case "CL": return Chlorine;
            default: return Unknown;
        }
    }

    /// <summary>
    /// Creates a material with the MoleculeUnlit shader (or fallback)
    /// </summary>
    public static Material CreateMaterial(Color color)
    {
        // Try custom shader first, fallback to Standard
        Shader shader = Shader.Find("Custom/MoleculeUnlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        
        Material mat = new Material(shader);
        mat.color = color;
        
        // Set Standard shader properties if applicable
        if (shader.name == "Standard")
        {
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Glossiness", 0.3f);
        }
        
        return mat;
    }

    /// <summary>
    /// Creates material for atoms
    /// </summary>
    public static Material CreateAtomMaterial(string element)
    {
        return CreateMaterial(GetAtomColor(element));
    }

    /// <summary>
    /// Creates material for bonds
    /// </summary>
    public static Material CreateBondMaterial(bool highlighted = false)
    {
        return CreateMaterial(highlighted ? HighlightGreen : Bond);
    }

    /// <summary>
    /// Creates material for dashed bonds
    /// </summary>
    public static Material CreateDashedBondMaterial(bool highlighted = false)
    {
        return CreateMaterial(highlighted ? HighlightYellow : BondDashed);
    }

    /// <summary>
    /// Creates material for wedge bonds
    /// </summary>
    public static Material CreateWedgeBondMaterial(bool highlighted = false)
    {
        return CreateMaterial(highlighted ? HighlightYellow : BondWedge);
    }
}
