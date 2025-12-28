using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Parser für SDF (Structure Data File) Format
/// Konvertiert SDF-String zu MoleculeData
/// </summary>
public static class SDFParser
{
    /// <summary>
    /// Parst SDF-String zu MoleculeData
    /// </summary>
    public static MoleculeData Parse(string sdfContent, string moleculeName = "Unknown")
    {
        if (string.IsNullOrEmpty(sdfContent))
        {
            Debug.LogError("[SDFParser] Empty SDF content");
            return null;
        }

        try
        {
            // Split lines, keep empty lines to maintain indices
            string[] lines = sdfContent.Split(new[] { '\n' }, StringSplitOptions.None);

            // Trim all lines
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim('\r', ' ');
            }

            if (lines.Length < 4)
            {
                Debug.LogError("[SDFParser] SDF too short");
                return null;
            }

            MoleculeData molecule = new MoleculeData();
            molecule.name = string.IsNullOrEmpty(moleculeName) ? lines[0] : moleculeName;

            // Find the counts line (contains "V2000" or "V3000")
            int countsLineIndex = -1;
            for (int i = 0; i < Mathf.Min(10, lines.Length); i++)
            {
                if (lines[i].Contains("V2000") || lines[i].Contains("V3000"))
                {
                    countsLineIndex = i;
                    break;
                }
            }

            if (countsLineIndex == -1)
            {
                Debug.LogError("[SDFParser] Could not find counts line (V2000/V3000)");
                return null;
            }

            string countsLine = lines[countsLineIndex];
            Debug.Log($"[SDFParser] Counts line: '{countsLine}'");

            // Parse atom and bond counts
            // Format: aaabbblllfffcccsssxxxrrrpppiiimmmvvvvvv
            int atomCount = 0;
            int bondCount = 0;

            if (countsLine.Length >= 6)
            {
                atomCount = ParseInt(countsLine.Substring(0, 3));
                bondCount = ParseInt(countsLine.Substring(3, 3));
            }

            Debug.Log($"[SDFParser] Parsing {moleculeName}: {atomCount} atoms, {bondCount} bonds");

            if (atomCount == 0)
            {
                Debug.LogError("[SDFParser] No atoms found in SDF");
                return null;
            }

            // Atom Block starts right after counts line
            int lineIndex = countsLineIndex + 1;

            for (int i = 0; i < atomCount; i++)
            {
                if (lineIndex >= lines.Length)
                {
                    Debug.LogWarning($"[SDFParser] Unexpected end of file at atom {i}");
                    break;
                }

                AtomData atom = ParseAtomLine(lines[lineIndex], i);
                if (atom != null)
                {
                    molecule.atoms.Add(atom);
                }
                else
                {
                    Debug.LogWarning($"[SDFParser] Failed to parse atom {i}: '{lines[lineIndex]}'");
                }

                lineIndex++;
            }

            // Bond Block
            for (int i = 0; i < bondCount; i++)
            {
                if (lineIndex >= lines.Length)
                {
                    Debug.LogWarning($"[SDFParser] Unexpected end of file at bond {i}");
                    break;
                }

                BondData bond = ParseBondLine(lines[lineIndex]);
                if (bond != null)
                {
                    molecule.bonds.Add(bond);
                }
                else
                {
                    Debug.LogWarning($"[SDFParser] Failed to parse bond {i}: '{lines[lineIndex]}'");
                }

                lineIndex++;
            }

            // Center molecule at origin
            if (molecule.atoms.Count > 0)
            {
                molecule.CenterAtOrigin();
            }

            Debug.Log($"[SDFParser] Successfully parsed: {molecule.atoms.Count} atoms, {molecule.bonds.Count} bonds");

            return molecule;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SDFParser] Parse error: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Parst eine Atom-Zeile
    /// Format: xxxxx.xxxxyyyyy.yyyyzzzzz.zzzz aaaddcccssshhhbbbvvvHHHrrriiimmmnnneee
    /// </summary>
    private static AtomData ParseAtomLine(string line, int atomId)
    {
        try
        {
            if (string.IsNullOrEmpty(line) || line.Length < 34)
            {
                Debug.LogWarning($"[SDFParser] Atom line too short: '{line}'");
                return null;
            }

            // Split by whitespace for more robust parsing
            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4)
            {
                Debug.LogWarning($"[SDFParser] Not enough parts in atom line: {parts.Length}");
                return null;
            }

            float x = ParseFloat(parts[0]);
            float y = ParseFloat(parts[1]);
            float z = ParseFloat(parts[2]);
            string element = parts[3].Trim();

            return new AtomData(atomId, element, new Vector3(x, y, z));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SDFParser] Failed to parse atom line: {line}\n{e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parst eine Bond-Zeile
    /// Format: 111222tttsssxxxrrrccc
    /// </summary>
    private static BondData ParseBondLine(string line)
    {
        try
        {
            if (string.IsNullOrEmpty(line))
            {
                return null;
            }

            // Split by whitespace
            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                Debug.LogWarning($"[SDFParser] Not enough parts in bond line: {parts.Length}");
                return null;
            }

            // Atom IDs (1-based in SDF, convert to 0-based)
            int atomA = ParseInt(parts[0]) - 1;
            int atomB = ParseInt(parts[1]) - 1;

            // Bond Type: 1=single, 2=double, 3=triple, 4=aromatic
            int bondTypeValue = ParseInt(parts[2]);
            BondType bondType = (BondType)Mathf.Clamp(bondTypeValue, 1, 4);

            // Stereo (optional, 4th column)
            int stereoValue = parts.Length > 3 ? ParseInt(parts[3]) : 0;
            BondStereo stereo = BondStereo.None;

            if (stereoValue == 1) stereo = BondStereo.Up;
            else if (stereoValue == 6) stereo = BondStereo.Down;

            return new BondData(atomA, atomB, bondType, stereo);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SDFParser] Failed to parse bond line: {line}\n{e.Message}");
            return null;
        }
    }

    // === Helper Methods ===

    private static int ParseInt(string str)
    {
        str = str.Trim();
        if (int.TryParse(str, out int result))
        {
            return result;
        }
        return 0;
    }

    private static float ParseFloat(string str)
    {
        str = str.Trim();
        if (float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }
        return 0f;
    }
}