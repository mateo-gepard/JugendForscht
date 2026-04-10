using UnityEngine;

/// <summary>
/// Repräsentiert ein chemisches Element mit visuellen Eigenschaften
/// </summary>
[System.Serializable]
public class ElementData
{
    [Header("Basic Info")]
    public string symbol;           // "C", "H", "O", "N"
    public string name;             // "Carbon", "Hydrogen"
    public int atomicNumber;        // 6, 1, 8, 7

    [Header("Visual Properties")]
    public Color cpkColor;          // Standard CPK Coloring
    public float vdwRadius;         // Van der Waals Radius in Angström
    public float covalentRadius;    // Covalent Radius in Angström

    [Header("Display Settings")]
    [Range(0.3f, 1.5f)]
    public float displayScale = 0.7f; // Ball-and-Stick Style (70% of vdW)

    public ElementData(string sym, string n, int num, Color color, float vdw, float cov)
    {
        symbol = sym;
        name = n;
        atomicNumber = num;
        cpkColor = color;
        vdwRadius = vdw;
        covalentRadius = cov;
    }
}