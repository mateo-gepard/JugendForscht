using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Erkennt Chiralitätszentren in Molekülen
/// Verwendet vereinfachten Morgan-Algorithmus für Substituenten-Vergleich
/// und Determinanten-Methode für R/S-Bestimmung
/// </summary>
public static class ChiralityDetector
{
    /// <summary>
    /// Ergebnis: Ein Chiralitätszentrum
    /// </summary>
    [System.Serializable]
    public class ChiralCenter
    {
        public int atomId;              // ID des Zentralatoms
        public string element;          // Element (meist "C")
        public string configuration;    // "R" oder "S"
        public int[] neighborIds;       // IDs der 4 Substituenten
        public string[] neighborLabels; // Beschreibungen der 4 Substituenten

        public override string ToString()
        {
            return $"{element}{atomId} ({configuration})";
        }
    }

    /// <summary>
    /// Analysiert ein Molekül und gibt alle Chiralitätszentren zurück
    /// </summary>
    public static List<ChiralCenter> DetectChiralCenters(MoleculeData molecule)
    {
        if (molecule == null || molecule.atoms.Count == 0)
            return new List<ChiralCenter>();

        // Step 1: Build adjacency graph
        var adjacency = BuildAdjacencyGraph(molecule);

        // Step 2: Find potential stereocenters (atoms with 4 different neighbors)
        var centers = new List<ChiralCenter>();

        foreach (var atom in molecule.atoms)
        {
            // Only consider carbon (and optionally nitrogen, phosphorus)
            if (atom.element != "C") continue;

            // Must have exactly 4 bonds
            if (!adjacency.ContainsKey(atom.id)) continue;
            var neighbors = adjacency[atom.id];
            if (neighbors.Count != 4) continue;

            // Step 3: Generate fingerprints for each substituent
            var fingerprints = new List<string>();
            var neighborIds = new int[4];
            var neighborLabels = new string[4];

            for (int i = 0; i < 4; i++)
            {
                neighborIds[i] = neighbors[i].id;
                string fp = GenerateSubstituentFingerprint(
                    molecule, adjacency, neighbors[i].id, atom.id, maxDepth: 6);
                fingerprints.Add(fp);
                neighborLabels[i] = GetSubstituentLabel(molecule, adjacency, neighbors[i].id, atom.id);
            }

            // Step 4: Check if all 4 fingerprints are different
            if (AllDifferent(fingerprints))
            {
                // Step 5: Determine R/S configuration
                string config = DetermineConfiguration(
                    molecule, atom, neighbors, adjacency);

                centers.Add(new ChiralCenter
                {
                    atomId = atom.id,
                    element = atom.element,
                    configuration = config,
                    neighborIds = neighborIds,
                    neighborLabels = neighborLabels
                });

                Debug.Log($"[Chirality] Found stereocenter: {atom.element}{atom.id} ({config}) " +
                          $"Neighbors: [{string.Join(", ", neighborLabels)}]");
            }
        }

        Debug.Log($"[Chirality] Total stereocenters found: {centers.Count} in {molecule.name}");
        return centers;
    }

    // ==================== ADJACENCY GRAPH ====================

    /// <summary>
    /// Baut einen Adjazenzgraphen: atomId → Liste der verbundenen Atome
    /// </summary>
    public static Dictionary<int, List<AtomData>> BuildAdjacencyGraph(MoleculeData molecule)
    {
        var adj = new Dictionary<int, List<AtomData>>();

        // Initialize empty lists
        foreach (var atom in molecule.atoms)
        {
            adj[atom.id] = new List<AtomData>();
        }

        // Fill from bonds
        foreach (var bond in molecule.bonds)
        {
            var atomA = molecule.GetAtom(bond.atomA_ID);
            var atomB = molecule.GetAtom(bond.atomB_ID);

            if (atomA != null && atomB != null)
            {
                adj[bond.atomA_ID].Add(atomB);
                adj[bond.atomB_ID].Add(atomA);
            }
        }

        return adj;
    }

    // ==================== SUBSTITUENT FINGERPRINT ====================

    /// <summary>
    /// Erzeugt einen kanonischen Fingerprint für einen Substituenten via BFS
    /// Der Fingerprint kodiert: Element + Ordnungszahl + Konnektivität pro Schicht
    /// </summary>
    private static string GenerateSubstituentFingerprint(
        MoleculeData molecule,
        Dictionary<int, List<AtomData>> adjacency,
        int startAtomId,
        int excludeAtomId,
        int maxDepth)
    {
        var visited = new HashSet<int> { excludeAtomId };
        var queue = new Queue<(int atomId, int depth)>();
        queue.Enqueue((startAtomId, 0));
        visited.Add(startAtomId);

        // Collect atoms per depth layer for canonical ordering
        var layers = new Dictionary<int, List<(string element, int atomicNum, int bondOrder)>>();

        while (queue.Count > 0)
        {
            var (currentId, depth) = queue.Dequeue();
            if (depth > maxDepth) continue;

            var atom = molecule.GetAtom(currentId);
            if (atom == null) continue;

            int atomicNum = GetAtomicNumber(atom.element);

            // Get bond type to parent (if at depth 0, from center)
            int bondOrder = 1;
            if (depth == 0)
            {
                var bond = FindBond(molecule, excludeAtomId, currentId, true);
                if (bond != null) bondOrder = (int)bond.bondType;
            }

            if (!layers.ContainsKey(depth))
                layers[depth] = new List<(string, int, int)>();
            layers[depth].Add((atom.element, atomicNum, bondOrder));

            // BFS: add neighbors
            if (adjacency.ContainsKey(currentId))
            {
                var sortedNeighbors = adjacency[currentId]
                    .Where(n => !visited.Contains(n.id))
                    .OrderByDescending(n => GetAtomicNumber(n.element))
                    .ToList();

                foreach (var neighbor in sortedNeighbors)
                {
                    visited.Add(neighbor.id);
                    queue.Enqueue((neighbor.id, depth + 1));
                }
            }
        }

        // Build canonical fingerprint from layers
        var sb = new StringBuilder();
        for (int d = 0; d <= maxDepth; d++)
        {
            if (!layers.ContainsKey(d)) break;
            var layer = layers[d];
            // Sort by atomic number descending, then by element name
            layer.Sort((a, b) => {
                int cmp = b.atomicNum.CompareTo(a.atomicNum);
                return cmp != 0 ? cmp : string.Compare(a.element, b.element);
            });
            sb.Append($"D{d}:");
            foreach (var (elem, anum, bord) in layer)
            {
                sb.Append($"{elem}{anum}b{bord},");
            }
            sb.Append("|");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Erzeugt ein lesbares Label für einen Substituenten (z.B. "CH3", "OH", "NH2")
    /// </summary>
    private static string GetSubstituentLabel(
        MoleculeData molecule,
        Dictionary<int, List<AtomData>> adjacency,
        int startAtomId,
        int excludeAtomId)
    {
        var atom = molecule.GetAtom(startAtomId);
        if (atom == null) return "?";

        // Simple case: hydrogen
        if (atom.element == "H") return "H";

        // Count immediate neighbors (excluding the central atom)
        if (!adjacency.ContainsKey(startAtomId))
            return atom.element;

        var neighbors = adjacency[startAtomId]
            .Where(n => n.id != excludeAtomId)
            .ToList();

        // Count hydrogens attached to this atom
        int hCount = neighbors.Count(n => n.element == "H");
        int otherCount = neighbors.Count(n => n.element != "H");

        string label = atom.element;
        if (hCount > 0)
        {
            label += "H";
            if (hCount > 1) label += hCount.ToString();
        }
        if (otherCount > 0)
        {
            // Add info about non-H neighbors
            var otherElements = neighbors
                .Where(n => n.element != "H")
                .Select(n => n.element)
                .OrderBy(e => e);
            label += "(" + string.Join("", otherElements) + ")";
        }

        return label;
    }

    // ==================== R/S DETERMINATION ====================

    /// <summary>
    /// Bestimmt R/S-Konfiguration mit der Determinanten-Methode
    /// 
    /// 1. Ordne die 4 Substituenten nach CIP-Priorität (Ordnungszahl)
    /// 2. Berechne den Skalartripelprodukt der Vektoren zu Priorität 1,2,3
    ///    (vom Zentralatom aus, wobei Priorität 4 "weg" zeigt)
    /// 3. Positiv = R, Negativ = S
    /// </summary>
    private static string DetermineConfiguration(
        MoleculeData molecule,
        AtomData center,
        List<AtomData> neighbors,
        Dictionary<int, List<AtomData>> adjacency)
    {
        // Assign CIP priorities (simplified: by atomic number, then by neighbors)
        var prioritized = AssignCIPPriorities(molecule, adjacency, center, neighbors);

        if (prioritized.Count != 4) return "?";

        // Vectors from center to each substituent
        Vector3 centerPos = center.position;
        Vector3 v1 = prioritized[0].position - centerPos; // Highest priority
        Vector3 v2 = prioritized[1].position - centerPos;
        Vector3 v3 = prioritized[2].position - centerPos;
        Vector3 v4 = prioritized[3].position - centerPos; // Lowest priority

        // The determinant method:
        // Place lowest priority (4) pointing away from viewer
        // Check if 1→2→3 goes clockwise (R) or counterclockwise (S)
        
        // Scalar triple product of (v1-v4, v2-v4, v3-v4)
        // This accounts for the perspective from opposite v4
        Vector3 a = v1 - v4;
        Vector3 b = v2 - v4;
        Vector3 c = v3 - v4;

        float det = Vector3.Dot(a, Vector3.Cross(b, c));

        // Positive determinant = R, Negative = S
        if (Mathf.Abs(det) < 0.001f)
        {
            Debug.LogWarning($"[Chirality] Near-zero determinant for {center.element}{center.id}, " +
                           $"configuration ambiguous");
            return "?";
        }

        return det > 0 ? "R" : "S";
    }

    /// <summary>
    /// Vereinfachte CIP-Prioritäts-Zuweisung
    /// Sortiert nach: Ordnungszahl des ersten Atoms → dann rekursiv Nachbarn
    /// </summary>
    private static List<AtomData> AssignCIPPriorities(
        MoleculeData molecule,
        Dictionary<int, List<AtomData>> adjacency,
        AtomData center,
        List<AtomData> neighbors)
    {
        // Calculate extended priority scores for each neighbor
        var scored = new List<(AtomData atom, List<int> priorityPath)>();

        foreach (var neighbor in neighbors)
        {
            var path = CalculatePriorityPath(molecule, adjacency, neighbor.id, center.id, maxDepth: 4);
            scored.Add((neighbor, path));
        }

        // Sort by priority path (higher atomic number = higher priority)
        scored.Sort((a, b) =>
        {
            int maxLen = Mathf.Max(a.priorityPath.Count, b.priorityPath.Count);
            for (int i = 0; i < maxLen; i++)
            {
                int valA = i < a.priorityPath.Count ? a.priorityPath[i] : 0;
                int valB = i < b.priorityPath.Count ? b.priorityPath[i] : 0;
                if (valA != valB) return valB.CompareTo(valA); // Descending (higher = higher priority)
            }
            return 0;
        });

        return scored.Select(s => s.atom).ToList();
    }

    /// <summary>
    /// Berechnet den Prioritätspfad für CIP-Vergleich
    /// Jede Schicht enthält die höchste Ordnungszahl der Nachbarn
    /// </summary>
    private static List<int> CalculatePriorityPath(
        MoleculeData molecule,
        Dictionary<int, List<AtomData>> adjacency,
        int startId,
        int excludeId,
        int maxDepth)
    {
        var path = new List<int>();
        var visited = new HashSet<int> { excludeId };
        var currentLayer = new List<int> { startId };
        visited.Add(startId);

        for (int depth = 0; depth <= maxDepth && currentLayer.Count > 0; depth++)
        {
            // Sort current layer by atomic number (descending) and take highest
            var layerAtomicNumbers = currentLayer
                .Select(id => molecule.GetAtom(id))
                .Where(a => a != null)
                .Select(a => GetAtomicNumber(a.element))
                .OrderByDescending(n => n)
                .ToList();

            if (layerAtomicNumbers.Count > 0)
            {
                // Encode the sum of atomic numbers at this depth for differentiation
                path.Add(layerAtomicNumbers[0]); // Highest at this level
                // Also add sum for tie-breaking
                path.Add(layerAtomicNumbers.Sum());
            }

            // Next layer
            var nextLayer = new List<int>();
            foreach (int id in currentLayer)
            {
                if (adjacency.ContainsKey(id))
                {
                    foreach (var neighbor in adjacency[id])
                    {
                        if (!visited.Contains(neighbor.id))
                        {
                            visited.Add(neighbor.id);
                            nextLayer.Add(neighbor.id);
                        }
                    }
                }
            }
            currentLayer = nextLayer;
        }

        return path;
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Prüft ob alle Strings in einer Liste verschieden sind
    /// </summary>
    private static bool AllDifferent(List<string> items)
    {
        return items.Distinct().Count() == items.Count;
    }

    /// <summary>
    /// Findet eine Bindung zwischen zwei Atomen
    /// </summary>
    private static BondData FindBond(MoleculeData molecule, int atomA, int atomB, bool directOnly)
    {
        if (!directOnly) return null;

        return molecule.bonds.Find(b =>
            (b.atomA_ID == atomA && b.atomB_ID == atomB) ||
            (b.atomA_ID == atomB && b.atomB_ID == atomA));
    }

    /// <summary>
    /// Gibt die Ordnungszahl eines Elements zurück (für CIP-Priorität)
    /// </summary>
    private static int GetAtomicNumber(string element)
    {
        switch (element)
        {
            case "H":  return 1;
            case "He": return 2;
            case "Li": return 3;
            case "Be": return 4;
            case "B":  return 5;
            case "C":  return 6;
            case "N":  return 7;
            case "O":  return 8;
            case "F":  return 9;
            case "Ne": return 10;
            case "Na": return 11;
            case "Mg": return 12;
            case "Al": return 13;
            case "Si": return 14;
            case "P":  return 15;
            case "S":  return 16;
            case "Cl": return 17;
            case "Ar": return 18;
            case "K":  return 19;
            case "Ca": return 20;
            case "Br": return 35;
            case "I":  return 53;
            case "Fe": return 26;
            case "Cu": return 29;
            case "Zn": return 30;
            default:
                Debug.LogWarning($"[Chirality] Unknown element: {element}");
                return 0;
        }
    }
}
