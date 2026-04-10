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
