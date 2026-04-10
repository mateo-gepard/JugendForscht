using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Optimiert die 3D-Geometrie eines im Baukasten erstellten Moleküls
/// nach den VSEPR-Regeln (Valence Shell Electron Pair Repulsion).
/// 
/// Ansatz: Deterministische BFS-Koordinatengenerierung.
/// 1. Berechne für jedes Atom die Sterische Zahl (SN = BondingPairs + LonePairs)
/// 2. Schlage den idealen Bindungswinkel nach (Lookup-Tabelle)
/// 3. Platziere Atome per BFS mit exakten Bindungslängen und Winkeln
/// 4. Gib das optimierte MoleculeData zurück (Positionen in Ångström)
/// </summary>
public static class MoleculeOptimizer
{
    // ═══════════════════ VSEPR LOOKUP ═══════════════════
    
    private static readonly Dictionary<string, int> valenceElectrons = new Dictionary<string, int>
    {
        {"H", 1}, {"He", 2},
        {"Li", 1}, {"Be", 2}, {"B", 3}, {"C", 4}, {"N", 5}, {"O", 6}, {"F", 7}, {"Ne", 8},
        {"Na", 1}, {"Mg", 2}, {"Al", 3}, {"Si", 4}, {"P", 5}, {"S", 6}, {"Cl", 7}, {"Ar", 8},
        {"K", 1}, {"Ca", 2}, {"Br", 7}, {"I", 7},
    };

    /// <summary>
    /// Standard-Einfachbindungslänge in Ångström.
    /// </summary>
    private static float GetIdealBondLength(string elemA, string elemB)
    {
        string key = (elemA.CompareTo(elemB) < 0) ? elemA + "-" + elemB : elemB + "-" + elemA;
        switch (key)
        {
            case "C-C": return 1.54f;
            case "C-H": return 1.09f;
            case "C-N": return 1.47f;
            case "C-O": return 1.43f;
            case "C-F": return 1.35f;
            case "C-Cl": return 1.77f;
            case "C-Br": return 1.94f;
            case "C-S": return 1.82f;
            case "H-N": return 1.01f;
            case "H-O": return 0.96f;
            case "H-S": return 1.34f;
            case "N-N": return 1.45f;
            case "N-O": return 1.40f;
            case "O-O": return 1.48f;
            case "H-H": return 0.74f;
            default: return 1.50f;
        }
    }

    // ═══════════════════ VSEPR DIRECTION TEMPLATES ═══════════════════

    /// <summary>
    /// Gibt die idealen Richtungsvektoren für eine gegebene Sterische Zahl (SN) zurück.
    /// Dies sind die "Elektronenpaar-Positionen" nach VSEPR.
    /// Die ersten (SN - lonePairs) Vektoren werden für echte Bindungen genutzt,
    /// die restlichen sind Lone-Pair-Platzhalter.
    /// </summary>
    private static Vector3[] GetDirectionTemplate(int stericNumber)
    {
        switch (stericNumber)
        {
            case 1:
                return new Vector3[] { Vector3.forward };

            case 2: // Linear: 180°
                return new Vector3[] {
                    Vector3.forward,
                    Vector3.back
                };

            case 3: // Trigonal planar: 120° im XZ-Plane
                return new Vector3[] {
                    Vector3.forward,
                    new Vector3(Mathf.Sin(120f * Mathf.Deg2Rad), 0, Mathf.Cos(120f * Mathf.Deg2Rad)),
                    new Vector3(Mathf.Sin(240f * Mathf.Deg2Rad), 0, Mathf.Cos(240f * Mathf.Deg2Rad))
                };

            case 4: // Tetraeder: 109.5°
                return new Vector3[] {
                    new Vector3( 1,  1,  1).normalized,
                    new Vector3( 1, -1, -1).normalized,
                    new Vector3(-1,  1, -1).normalized,
                    new Vector3(-1, -1,  1).normalized
                };

            case 5: // Trigonal bipyramidal
                return new Vector3[] {
                    Vector3.up,                          // Axial
                    Vector3.forward,                     // Äquatorial
                    new Vector3(Mathf.Sin(120f * Mathf.Deg2Rad), 0, Mathf.Cos(120f * Mathf.Deg2Rad)),
                    new Vector3(Mathf.Sin(240f * Mathf.Deg2Rad), 0, Mathf.Cos(240f * Mathf.Deg2Rad)),
                    Vector3.down                         // Axial
                };

            case 6: // Oktaedrisch
                return new Vector3[] {
                    Vector3.up,
                    Vector3.down,
                    Vector3.forward,
                    Vector3.back,
                    Vector3.right,
                    Vector3.left
                };

            default:
                // Fallback: Vertices auf einer Sphäre verteilen
                var dirs = new List<Vector3>();
                float goldenRatio = (1 + Mathf.Sqrt(5)) / 2;
                for (int i = 0; i < stericNumber; i++)
                {
                    float theta = Mathf.Acos(1 - 2f * (i + 0.5f) / stericNumber);
                    float phi = 2 * Mathf.PI * i / goldenRatio;
                    dirs.Add(new Vector3(
                        Mathf.Sin(theta) * Mathf.Cos(phi),
                        Mathf.Sin(theta) * Mathf.Sin(phi),
                        Mathf.Cos(theta)
                    ));
                }
                return dirs.ToArray();
        }
    }

    // ═══════════════════ CORE API ═══════════════════

    /// <summary>
    /// Optimiert die Atom-Positionen im MoleculeData nach VSEPR-Regeln.
    /// Generiert komplett neue 3D-Koordinaten in Ångström basierend auf
    /// der Molekülgraph-Topologie (welches Atom mit welchem verbunden ist).
    /// </summary>
    public static MoleculeData Optimize(MoleculeData mol)
    {
        if (mol == null || mol.atoms.Count < 2) return mol;

        // 1. Baue Adjacency
        var adj = BuildAdjacency(mol);

        // 2. Berechne Lone Pairs pro Atom
        var lonePairs = ComputeLonePairs(mol, adj);

        // 3. Generiere 3D-Koordinaten per BFS
        Vector3[] positions = GenerateCoordinatesBFS(mol, adj, lonePairs);

        // 4. Zentriere am Ursprung
        Vector3 center = Vector3.zero;
        for (int i = 0; i < positions.Length; i++) center += positions[i];
        center /= positions.Length;
        for (int i = 0; i < positions.Length; i++) positions[i] -= center;

        // 5. Schreibe zurück
        for (int i = 0; i < mol.atoms.Count; i++)
            mol.atoms[i].position = positions[i];

        Debug.Log($"[VSEPR] Optimized {mol.atoms.Count} atoms. LP: [{string.Join(",", lonePairs)}]");
        return mol;
    }

    // ═══════════════════ ADJACENCY ═══════════════════

    private static Dictionary<int, List<(int neighborIdx, int bondOrder)>> BuildAdjacency(MoleculeData mol)
    {
        var adj = new Dictionary<int, List<(int, int)>>();
        for (int i = 0; i < mol.atoms.Count; i++)
            adj[i] = new List<(int, int)>();

        foreach (var bond in mol.bonds)
        {
            int a = -1, b = -1;
            for (int i = 0; i < mol.atoms.Count; i++)
            {
                if (mol.atoms[i].id == bond.atomA_ID) a = i;
                if (mol.atoms[i].id == bond.atomB_ID) b = i;
            }
            if (a < 0 || b < 0) continue;

            int order = 1;
            if (bond.bondType == BondType.Double) order = 2;
            else if (bond.bondType == BondType.Triple) order = 3;

            adj[a].Add((b, order));
            adj[b].Add((a, order));
        }
        return adj;
    }

    // ═══════════════════ LONE PAIRS ═══════════════════

    private static int[] ComputeLonePairs(MoleculeData mol, Dictionary<int, List<(int neighborIdx, int bondOrder)>> adj)
    {
        int[] lp = new int[mol.atoms.Count];
        for (int i = 0; i < mol.atoms.Count; i++)
        {
            string elem = mol.atoms[i].element;
            int ve = valenceElectrons.ContainsKey(elem) ? valenceElectrons[elem] : 4;

            int bondOrderSum = 0;
            foreach (var (_, order) in adj[i])
                bondOrderSum += order;

            int nonBonding = ve - bondOrderSum;
            lp[i] = Mathf.Max(0, nonBonding / 2);
        }
        return lp;
    }

    // ═══════════════════ BFS COORDINATE GENERATION ═══════════════════

    /// <summary>
    /// Generiert 3D-Koordinaten per BFS vom Zentralatom aus.
    /// Jedes Atom wird deterministisch platziert anhand der VSEPR-Geometrie.
    /// </summary>
    private static Vector3[] GenerateCoordinatesBFS(
        MoleculeData mol,
        Dictionary<int, List<(int neighborIdx, int bondOrder)>> adj,
        int[] lonePairs)
    {
        int n = mol.atoms.Count;
        Vector3[] pos = new Vector3[n];
        bool[] placed = new bool[n];

        // Finde das "zentralste" Atom (meiste Bindungen) als Startpunkt
        int startAtom = 0;
        int maxBonds = 0;
        for (int i = 0; i < n; i++)
        {
            if (adj[i].Count > maxBonds)
            {
                maxBonds = adj[i].Count;
                startAtom = i;
            }
        }

        // Starte BFS
        pos[startAtom] = Vector3.zero;
        placed[startAtom] = true;

        Queue<int> queue = new Queue<int>();
        queue.Enqueue(startAtom);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            var neighbors = adj[current];

            int bondingPairs = neighbors.Count;
            int lp = lonePairs[current];
            int stericNumber = bondingPairs + lp;

            // Hole die VSEPR-Richtungs-Template
            Vector3[] template = GetDirectionTemplate(Mathf.Max(stericNumber, 1));

            // Bestimme die Referenzrichtung (= Richtung zum Parent-Atom, falls vorhanden)
            Vector3 parentDir = Vector3.zero;
            int parentIdx = -1;
            foreach (var (ni, _) in neighbors)
            {
                if (placed[ni])
                {
                    parentDir = (pos[current] - pos[ni]).normalized;
                    parentIdx = ni;
                    break;
                }
            }

            // Rotiere das Template so, dass der erste Vektor auf den Parent zeigt
            Quaternion alignRotation = Quaternion.identity;
            if (parentDir != Vector3.zero && template.Length > 0)
            {
                alignRotation = Quaternion.FromToRotation(template[0], parentDir);
            }
            else
            {
                // Kein Parent → leichte Zufalls-Rotation um Determinismus zu wahren
                // aber nicht alle Atome auf einer Linie zu haben
                alignRotation = Quaternion.identity;
            }

            Vector3[] alignedDirs = new Vector3[template.Length];
            for (int d = 0; d < template.Length; d++)
                alignedDirs[d] = alignRotation * template[d];

            // Verteile Nachbarn auf die Richtungen:
            // - Index 0 ist für den Parent reserviert (falls vorhanden)
            // - Die letzten LP Slots sind für Lone Pairs (bleiben leer)
            // - Die mittleren Slots sind für neue Nachbarn
            int dirIdx = (parentIdx >= 0) ? 1 : 0; // Überspringe Slot 0 wenn Parent da

            foreach (var (ni, bondOrder) in neighbors)
            {
                if (placed[ni]) continue; // Parent oder bereits platziert
                if (dirIdx >= alignedDirs.Length) break; // Sicherheitscheck

                // Überspringe Lone-Pair-Positionen am Ende
                // Bei SN=4, LP=2: Dirs 0=parent, 1=bond, 2=LP, 3=LP → nutze 1
                // Bei SN=4, LP=1: Dirs 0=parent, 1=bond, 2=bond, 3=LP → nutze 1,2
                // Genauer: Die letzten LP Positionen sind Lone Pairs
                int maxBondSlots = template.Length - lp;
                if (dirIdx >= maxBondSlots)
                {
                    // Fallback: Nimm die nächste verfügbare Richtung
                    // (besser als gar nicht platzieren)
                    dirIdx = maxBondSlots > 0 ? maxBondSlots - 1 : 0;
                }

                Vector3 dir = alignedDirs[dirIdx];
                float bondLen = GetIdealBondLength(mol.atoms[current].element, mol.atoms[ni].element);

                pos[ni] = pos[current] + dir * bondLen;
                placed[ni] = true;
                queue.Enqueue(ni);

                dirIdx++;
            }
        }

        // Handle unplaced atoms (disconnected fragments)
        float offset = 3.0f;
        for (int i = 0; i < n; i++)
        {
            if (!placed[i])
            {
                pos[i] = new Vector3(offset, 0, 0);
                offset += 2.0f;
                placed[i] = true;
            }
        }

        return pos;
    }
}
