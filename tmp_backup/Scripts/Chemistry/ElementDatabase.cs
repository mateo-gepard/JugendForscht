using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Zentrale Datenbank f�r chemische Elemente
/// ScriptableObject f�r einfache Inspector-Verwaltung
/// </summary>
[CreateAssetMenu(fileName = "ElementDatabase", menuName = "Chemistry/Element Database")]
public class ElementDatabase : ScriptableObject
{
    [SerializeField]
    private List<ElementData> elements = new List<ElementData>();

    private Dictionary<string, ElementData> _elementLookup;

    public void Initialize()
    {
        if (_elementLookup != null) return;

        _elementLookup = new Dictionary<string, ElementData>();
        foreach (var element in elements)
        {
            _elementLookup[element.symbol] = element;
        }
    }

    public ElementData GetElement(string symbol)
    {
        Initialize();

        if (_elementLookup.TryGetValue(symbol, out ElementData element))
        {
            return element;
        }

        Debug.LogWarning($"Element '{symbol}' not found in database. Using fallback.");
        return GetFallbackElement();
    }

    public bool HasElement(string symbol)
    {
        Initialize();
        return _elementLookup.ContainsKey(symbol);
    }

    private ElementData GetFallbackElement()
    {
        // Fallback f�r unbekannte Elemente
        return new ElementData("X", "Unknown", 0, Color.magenta, 1.5f, 1.0f);
    }

    /// <summary>
    /// Erstellt Standard-Elemente (CPK Coloring)
    /// Im Inspector �ber Context Menu aufrufbar
    /// </summary>
    [ContextMenu("Create Default Elements")]
    public void CreateDefaultElements()
    {
        elements.Clear();

        // Unified color scheme for molecular visualization
        // H: Light Grey, C: Dark Grey, N: Blue, O: Red, Bonds: Black

        elements.Add(new ElementData("H", "Hydrogen", 1,
            new Color(0.85f, 0.85f, 0.85f), 1.20f, 0.31f)); // Light Grey

        elements.Add(new ElementData("C", "Carbon", 6,
            new Color(0.3f, 0.3f, 0.3f), 1.70f, 0.76f)); // Dark Grey

        elements.Add(new ElementData("N", "Nitrogen", 7,
            new Color(0.2f, 0.4f, 0.9f), 1.55f, 0.71f)); // Blue

        elements.Add(new ElementData("O", "Oxygen", 8,
            new Color(0.9f, 0.2f, 0.2f), 1.52f, 0.66f)); // Red

        elements.Add(new ElementData("F", "Fluorine", 9,
            new Color(0.5f, 0.9f, 0.5f), 1.47f, 0.57f)); // Light Green

        elements.Add(new ElementData("B", "Boron", 5,
            new Color(1f, 0.7f, 0.7f), 1.92f, 0.84f)); // Pink

        elements.Add(new ElementData("P", "Phosphorus", 15,
            new Color(1f, 0.5f, 0f), 1.80f, 1.07f)); // Orange

        elements.Add(new ElementData("S", "Sulfur", 16,
            new Color(1f, 1f, 0.2f), 1.80f, 1.05f)); // Yellow

        elements.Add(new ElementData("Cl", "Chlorine", 17,
            new Color(0.2f, 0.9f, 0.2f), 1.75f, 1.02f)); // Green

        elements.Add(new ElementData("Br", "Bromine", 35,
            new Color(0.65f, 0.16f, 0.16f), 1.85f, 1.20f)); // Dark Red

        elements.Add(new ElementData("I", "Iodine", 53,
            new Color(0.58f, 0f, 0.58f), 1.98f, 1.39f)); // Purple

        // Metals
        elements.Add(new ElementData("Fe", "Iron", 26,
            new Color(0.88f, 0.4f, 0.2f), 2.00f, 1.32f));

        elements.Add(new ElementData("Cu", "Copper", 29,
            new Color(0.78f, 0.5f, 0.2f), 1.96f, 1.22f));

        elements.Add(new ElementData("Zn", "Zinc", 30,
            new Color(0.49f, 0.5f, 0.69f), 2.01f, 1.22f));

        Debug.Log($"Created {elements.Count} default elements with unified color scheme");
    }

    public List<ElementData> GetAllElements()
    {
        return new List<ElementData>(elements);
    }
}