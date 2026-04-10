using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Einzelnes Atom im Molekül
/// </summary>
[System.Serializable]
public class AtomData
{
    public int id;                  // Unique ID (für Bond-Referenzen)
    public string element;          // Element-Symbol: "C", "H", etc.
    public Vector3 position;        // 3D Position in Angström

    // Optional: individuelle Overrides
    public Color? colorOverride;
    public float? radiusOverride;

    public AtomData(int id, string elem, Vector3 pos)
    {
        this.id = id;
        element = elem;
        position = pos;
    }
}

/// <summary>
/// Bindung zwischen zwei Atomen
/// </summary>
[System.Serializable]
public class BondData
{
    public int atomA_ID;            // Referenz auf Atom.id
    public int atomB_ID;
    public BondType bondType;
    public BondStereo stereo;       // Für Keil/Strich (Phase 4)

    public BondData(int a, int b, BondType type, BondStereo stereo = BondStereo.None)
    {
        atomA_ID = a;
        atomB_ID = b;
        bondType = type;
        this.stereo = stereo;
    }
}

/// <summary>
/// Bond-Typen
/// </summary>
public enum BondType
{
    Single = 1,
    Double = 2,
    Triple = 3,
    Aromatic = 4    // Für Benzol etc.
}

/// <summary>
/// Stereochemie-Info (wichtig für Phase 4)
/// </summary>
public enum BondStereo
{
    None = 0,       // Normale Darstellung (Zylinder)
    Up = 1,         // Wedge (Keil nach vorne)
    Down = -1       // Dashed (gestrichelt nach hinten)
}

/// <summary>
/// Komplettes Molekül mit Metadaten
/// </summary>
[System.Serializable]
public class MoleculeData
{
    [Header("Metadata")]
    public string name;             // "Ethanol"
    public string formula;          // "C2H6O"
    public int pubchemCID;          // PubChem Compound ID

    [Header("Structure")]
    public List<AtomData> atoms = new List<AtomData>();
    public List<BondData> bonds = new List<BondData>();

    [Header("Visual")]
    public Sprite previewIcon;      // Für UI (optional)

    // Helper Methods
    public Vector3 GetCentroid()
    {
        if (atoms.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var atom in atoms)
        {
            sum += atom.position;
        }
        return sum / atoms.Count;
    }

    public void CenterAtOrigin()
    {
        Vector3 centroid = GetCentroid();
        for (int i = 0; i < atoms.Count; i++)
        {
            atoms[i].position -= centroid;
        }
    }

    public AtomData GetAtom(int id)
    {
        return atoms.Find(a => a.id == id);
    }
}