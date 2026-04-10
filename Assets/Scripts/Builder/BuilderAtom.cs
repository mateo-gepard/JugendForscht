using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Atom im VR-Molekülbaukasten.
/// Tracked: Ladung, Bonds, Bond-Orders.
/// </summary>
public class BuilderAtom : MonoBehaviour
{
    [Header("State")]
    public string elementSymbol;
    public bool isDragging = false;

    [Header("Visual")]
    public float atomRadius = 0.025f;

    // Bond constraint
    private BuilderAtom bondedTo = null;
    private float bondRadius = 0f;
    public BuilderAtom BondedTo => bondedTo;
    public float BondRadius => bondRadius;

    // Charge
    private int formalCharge = 0;
    public int FormalCharge => formalCharge;

    // Valence
    private int maxBonds = 4;
    public int MaxBonds => maxBonds;

    // Bond tracking (sum of bond orders)
    private int totalBondOrderSum = 0;
    public int TotalBondOrderSum => totalBondOrderSum;

    // Internal visuals
    private GameObject sphere;
    private GameObject label;
    private GameObject chargeLabel;
    private Material atomMaterial;
    private Color elementColor;
    private bool isHighlighted = false;

    // Valence lookup
    private static readonly Dictionary<string, int> valenceLookup = new Dictionary<string, int>
    {
        {"H", 1}, {"He", 0}, {"Li", 1}, {"Be", 2},
        {"B", 3}, {"C", 4}, {"N", 3}, {"O", 2},
        {"F", 1}, {"Ne", 0}, {"Na", 1}, {"Mg", 2},
        {"Al", 3}, {"Si", 4}, {"P", 3}, {"S", 2},
        {"Cl", 1}, {"Ar", 0}, {"K", 1}, {"Ca", 2},
        {"Fe", 3}, {"Cu", 2}, {"Zn", 2}, {"Br", 1}, {"I", 1},
    };

    private static readonly Dictionary<string, int> expandedValence = new Dictionary<string, int>
    {
        {"S", 6}, {"P", 5},  // Can expand beyond normal valence
    };

    public void Initialize(string symbol, ElementDatabase elementDB)
    {
        elementSymbol = symbol;
        maxBonds = valenceLookup.ContainsKey(symbol) ? valenceLookup[symbol] : 4;

        if (elementDB != null && elementDB.HasElement(symbol))
        {
            var data = elementDB.GetElement(symbol);
            elementColor = data.cpkColor;
            atomRadius = Mathf.Clamp(data.covalentRadius * 0.02f, 0.012f, 0.035f);
        }
        else
        {
            elementColor = Color.gray;
        }
        CreateVisual();
    }

    private void CreateVisual()
    {
        // Sphere
        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "AtomSphere";
        sphere.transform.SetParent(transform, false);
        sphere.transform.localScale = Vector3.one * atomRadius * 2f;

        atomMaterial = new Material(Shader.Find("Standard"));
        Color displayColor = elementColor;
        if (displayColor.r > 0.7f && displayColor.g > 0.7f && displayColor.b > 0.7f)
            displayColor = new Color(displayColor.r * 0.82f, displayColor.g * 0.82f, displayColor.b * 0.87f);
        atomMaterial.color = displayColor;
        atomMaterial.EnableKeyword("_EMISSION");
        atomMaterial.SetColor("_EmissionColor", displayColor * 0.2f);
        atomMaterial.SetFloat("_Glossiness", 0.7f);
        atomMaterial.SetFloat("_Metallic", 0.1f);
        sphere.GetComponent<Renderer>().material = atomMaterial;

        var sphereCol = sphere.GetComponent<Collider>();
        if (sphereCol != null) Object.Destroy(sphereCol);

        // Dark outline for very light elements
        if (elementColor.r > 0.7f && elementColor.g > 0.7f && elementColor.b > 0.7f)
        {
            var outline = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            outline.name = "Outline";
            outline.transform.SetParent(transform, false);
            outline.transform.localScale = Vector3.one * atomRadius * 2.15f;
            var oc = outline.GetComponent<Collider>(); if (oc) Object.Destroy(oc);
            var om = new Material(Shader.Find("Standard"));
            om.color = new Color(0.3f, 0.3f, 0.35f);
            om.SetFloat("_Glossiness", 0.2f);
            outline.GetComponent<Renderer>().material = om;
        }

        // Element label
        label = new GameObject("Label");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = Vector3.up * (atomRadius + 0.01f);
        var tm = label.AddComponent<TextMesh>();
        tm.text = elementSymbol;
        tm.fontSize = 28;
        tm.characterSize = 0.004f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.fontStyle = FontStyle.Bold;

        // Charge label (initially empty)
        chargeLabel = new GameObject("ChargeLabel");
        chargeLabel.transform.SetParent(transform, false);
        chargeLabel.transform.localPosition = new Vector3(atomRadius * 0.8f, atomRadius + 0.015f, 0);
        var cl = chargeLabel.AddComponent<TextMesh>();
        cl.text = "";
        cl.fontSize = 22;
        cl.characterSize = 0.003f;
        cl.anchor = TextAnchor.MiddleCenter;
        cl.color = Color.yellow;
        cl.fontStyle = FontStyle.Bold;
    }

    // ═══════ STATE ═══════

    public void SetDragState(bool dragging)
    {
        isDragging = dragging;
        if (atomMaterial != null)
            atomMaterial.SetColor("_EmissionColor",
                dragging ? elementColor * 0.6f : (isHighlighted ? elementColor * 0.5f : elementColor * 0.2f));
        if (sphere != null)
            sphere.transform.localScale = Vector3.one * atomRadius * (dragging ? 2.4f : 2f);
    }

    public void SetHighlight(bool hl)
    {
        isHighlighted = hl;
        if (atomMaterial != null)
            atomMaterial.SetColor("_EmissionColor", hl ? elementColor * 0.5f : elementColor * 0.2f);
        if (sphere != null)
            sphere.transform.localScale = Vector3.one * atomRadius * (hl ? 2.3f : 2f);
    }

    public void SetBondConstraint(BuilderAtom partner, float radius)
    {
        bondedTo = partner;
        bondRadius = radius;
    }

    // ═══════ BONDS ═══════

    public void AddBondOrder(int order) { totalBondOrderSum += order; }
    public void RemoveBondOrder(int order) { totalBondOrderSum = Mathf.Max(0, totalBondOrderSum - order); }

    public bool CanAcceptBondOrder(int additionalOrder)
    {
        // Positive charge = lost electron = MORE bonding capacity (NH4+ = 4 bonds)
        // Negative charge = gained electron = FEWER bonds (OH- = 1 bond)
        int effective = maxBonds + formalCharge;
        // Check expanded valence (S, P)
        if (expandedValence.ContainsKey(elementSymbol))
            effective = Mathf.Max(effective, expandedValence[elementSymbol] + formalCharge);
        if (effective < 0) effective = 0;
        return (totalBondOrderSum + additionalOrder) <= effective;
    }

    /// <summary>
    /// Prüft: Ist die Oktettregel für dieses Atom erfüllt?
    /// </summary>
    public bool IsOctetSatisfied()
    {
        // Expected bonds = maxBonds + formalCharge
        // NH4+: expected = 3 + 1 = 4 bonds ✓
        // OH-:  expected = 2 + (-1) = 1 bond ✓
        // H:    expected = 1 + 0 = 1 bond ✓
        int expected = maxBonds + formalCharge;
        if (expected < 0) expected = 0;
        return totalBondOrderSum == expected;
    }

    // ═══════ CHARGE ═══════

    public void ModifyCharge(int delta)
    {
        formalCharge += delta;
        formalCharge = Mathf.Clamp(formalCharge, -3, 3);
        UpdateChargeLabel();
    }

    private void UpdateChargeLabel()
    {
        if (chargeLabel == null) return;
        var tm = chargeLabel.GetComponent<TextMesh>();
        if (tm == null) return;

        if (formalCharge == 0)
        {
            tm.text = "";
        }
        else
        {
            string sign = formalCharge > 0 ? "+" : "-";
            int abs = Mathf.Abs(formalCharge);
            tm.text = abs > 1 ? $"{abs}{sign}" : sign;
            tm.color = formalCharge > 0 ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);
        }
    }

    // ═══════ UPDATE ═══════

    void Update()
    {
        if (label != null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Quaternion rot = Quaternion.LookRotation(label.transform.position - cam.transform.position);
                label.transform.rotation = rot;
                if (chargeLabel != null) chargeLabel.transform.rotation = rot;
            }
        }
        // Enforce bond constraint
        if (!isDragging && bondedTo != null)
        {
            Vector3 dir = (transform.position - bondedTo.transform.position).normalized;
            if (dir.magnitude < 0.001f) dir = Vector3.right;
            transform.position = bondedTo.transform.position + dir * bondRadius;
        }
    }

    void OnDestroy()
    {
        if (BuilderManager.Instance != null)
            BuilderManager.Instance.UnregisterAtom(this);
        if (atomMaterial != null) Destroy(atomMaterial);
    }
}
