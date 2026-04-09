using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Erzeugt Enantiomere (Spiegelbilder) von Molekülen
/// Spiegelt alle Atomkoordinaten an einer Ebene durch den Schwerpunkt
/// </summary>
public static class IsomerGenerator
{
    /// <summary>
    /// Prüft ob zwei Moleküle stereochemisch identisch sind.
    /// Vergleicht die R/S-Konfigurationen aller Chiralitätszentren.
    /// Identifiziert korrekt: achirale Moleküle, Meso-Verbindungen.
    /// </summary>
    public static bool AreMoleculesIdentical(MoleculeData a, MoleculeData b)
    {
        if (a == null || b == null) return false;

        var centersA = ChiralityDetector.DetectChiralCenters(a);
        var centersB = ChiralityDetector.DetectChiralCenters(b);

        // Different number of centers → different
        if (centersA.Count != centersB.Count) return false;

        // No centers at all → both achiral → identical
        if (centersA.Count == 0) return true;

        // Create sorted (neighborFingerprint, config) pairs for comparison
        // Two molecules are identical if their sorted stereocenter signatures match
        var sigA = centersA
            .Select(c => string.Join(",", c.neighborLabels.OrderBy(x => x)) + ":" + c.configuration)
            .OrderBy(s => s)
            .ToList();

        var sigB = centersB
            .Select(c => string.Join(",", c.neighborLabels.OrderBy(x => x)) + ":" + c.configuration)
            .OrderBy(s => s)
            .ToList();

        for (int i = 0; i < sigA.Count; i++)
        {
            if (sigA[i] != sigB[i]) return false;
        }

        Debug.Log($"[IsomerGen] Molecules are stereochemically IDENTICAL " +
                  $"({centersA.Count} centers, all configs match)");
        return true;
    }

    /// <summary>
    /// Prüft ob Molekül B das Enantiomer von A ist (alle R/S invertiert).
    /// Gibt false zurück wenn sie identisch sind.
    /// </summary>
    public static bool IsEnantiomer(MoleculeData a, MoleculeData b)
    {
        if (a == null || b == null) return false;

        var centersA = ChiralityDetector.DetectChiralCenters(a);
        var centersB = ChiralityDetector.DetectChiralCenters(b);

        if (centersA.Count == 0 || centersA.Count != centersB.Count) return false;

        // If identical → not an enantiomer
        if (AreMoleculesIdentical(a, b)) return false;

        // Check that ALL configurations are flipped
        string FlipConfig(string c) => c == "R" ? "S" : (c == "S" ? "R" : c);

        var sigA = centersA
            .Select(c => string.Join(",", c.neighborLabels.OrderBy(x => x)) + ":" + FlipConfig(c.configuration))
            .OrderBy(s => s)
            .ToList();

        var sigB = centersB
            .Select(c => string.Join(",", c.neighborLabels.OrderBy(x => x)) + ":" + c.configuration)
            .OrderBy(s => s)
            .ToList();

        for (int i = 0; i < sigA.Count; i++)
        {
            if (sigA[i] != sigB[i]) return false;
        }

        Debug.Log($"[IsomerGen] Molecule B is the ENANTIOMER of A ({centersA.Count} centers, all flipped)");
        return true;
    }

    /// <summary>
    /// Erzeugt das Enantiomer eines Moleküls durch Spiegelung an der YZ-Ebene
    /// </summary>
    public static MoleculeData GenerateEnantiomer(MoleculeData original)
    {
        if (original == null) return null;

        // Deep copy the molecule
        var enantiomer = DeepCopy(original);
        enantiomer.name = original.name + " (Enantiomer)";

        // Calculate center of mass
        Vector3 center = Vector3.zero;
        foreach (var atom in enantiomer.atoms)
        {
            center += atom.position;
        }
        center /= enantiomer.atoms.Count;

        // Mirror all atoms across YZ plane (invert X coordinate relative to center)
        foreach (var atom in enantiomer.atoms)
        {
            Vector3 pos = atom.position;
            pos.x = 2 * center.x - pos.x; // Reflect X around center
            atom.position = pos;
        }

        Debug.Log($"[IsomerGen] Generated enantiomer of {original.name}: " +
                  $"{enantiomer.atoms.Count} atoms mirrored");

        return enantiomer;
    }

    /// <summary>
    /// Erzeugt ein Diastereomer: invertiert nur bestimmte Chiralitätszentren
    /// </summary>
    public static MoleculeData GenerateDiastereomer(
        MoleculeData original,
        int chiralCenterAtomId,
        Dictionary<int, List<AtomData>> adjacency)
    {
        if (original == null) return null;

        var diastereomer = DeepCopy(original);
        diastereomer.name = original.name + " (Diastereomer)";

        var centerAtom = diastereomer.GetAtom(chiralCenterAtomId);
        if (centerAtom == null)
        {
            Debug.LogWarning($"[IsomerGen] Atom {chiralCenterAtomId} not found!");
            return null;
        }

        // Invert only this center by swapping two substituent positions
        if (adjacency.ContainsKey(chiralCenterAtomId))
        {
            var neighbors = adjacency[chiralCenterAtomId];
            if (neighbors.Count >= 2)
            {
                // Get the corresponding atoms in the diastereomer
                var atomA = diastereomer.GetAtom(neighbors[0].id);
                var atomB = diastereomer.GetAtom(neighbors[1].id);

                if (atomA != null && atomB != null)
                {
                    // Swap positions of first two substituents
                    Vector3 tempPos = atomA.position;
                    atomA.position = atomB.position;
                    atomB.position = tempPos;

                    Debug.Log($"[IsomerGen] Inverted center {chiralCenterAtomId}: " +
                              $"swapped {neighbors[0].element}{neighbors[0].id} <-> " +
                              $"{neighbors[1].element}{neighbors[1].id}");
                }
            }
        }

        return diastereomer;
    }

    /// <summary>
    /// Deep copy of a MoleculeData
    /// </summary>
    private static MoleculeData DeepCopy(MoleculeData original)
    {
        var copy = new MoleculeData();
        copy.name = original.name;

        // Deep copy atoms
        foreach (var atom in original.atoms)
        {
            var newAtom = new AtomData(atom.id, atom.element, atom.position);
            copy.atoms.Add(newAtom);
        }

        // Deep copy bonds
        foreach (var bond in original.bonds)
        {
            var newBond = new BondData(bond.atomA_ID, bond.atomB_ID, bond.bondType, bond.stereo);
            copy.bonds.Add(newBond);
        }

        return copy;
    }
}
