using UnityEngine;
using System;
using System.Collections.Generic;
using Complex = System.Numerics.Complex;
using Vector3 = UnityEngine.Vector3;

/// <summary>
/// Generates a 3D mesh for a Riemann surface from a parsed complex function.
/// Uses numerical analytic continuation along angular paths to follow
/// the correct branch/sheet of multi-valued functions.
/// 
/// Coordinate mapping:
///   Unity X = Re(z)    (real part of input)
///   Unity Z = Im(z)    (imaginary part of input)
///   Unity Y = Re(f(z)) (real part of output = height)
///   Color   = based on Arg(f(z)) + |f(z)| (domain coloring)
/// </summary>
public static class RiemannMeshGenerator
{
    // ════════════════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Generate a Riemann surface mesh for the given function.
    /// </summary>
    /// <param name="func">Parsed complex function</param>
    /// <param name="maxVal">Maximum value for all axes (symmetric bounds)</param>
    /// <param name="boxSize">Physical size of the bounding box in meters</param>
    /// <param name="radialSteps">Number of radial sample points</param>
    /// <param name="angularStepsPerSheet">Number of angular samples per sheet</param>
    public static Mesh Generate(ParsedFunction func, float maxVal, float boxSize,
        int radialSteps = 40, int angularStepsPerSheet = 100)
    {
        int sheets = func.Sheets;
        int totalAngularSteps = angularStepsPerSheet * sheets;
        float inputScale = boxSize / (2f * maxVal); // Scale for Re(z), Im(z) axes
        float rMin = maxVal * 0.01f; // Small ε to avoid branch point at origin

        // ── PASS 1: Sample all function values ──
        int rows = radialSteps;
        int cols = totalAngularSteps + 1;
        var zValues = new Complex[rows * cols];
        var wValues = new Complex[rows * cols];
        var valid = new bool[rows * cols];
        float maxHeight = 0.001f; // Track max |Re(f(z))| for auto-scaling Y axis

        for (int i = 0; i < rows; i++)
        {
            float t = (float)i / (rows - 1);
            double r = rMin + (maxVal - rMin) * t;
            Complex prevW = Complex.Zero;

            for (int j = 0; j < cols; j++)
            {
                double theta = 2.0 * Math.PI * j / angularStepsPerSheet;
                Complex z = Complex.FromPolarCoordinates(r, theta);

                Complex w;
                if (j == 0)
                    w = func.Evaluate(z);
                else
                    w = ContinueValue(func, z, prevW);

                int idx = i * cols + j;
                zValues[idx] = z;
                wValues[idx] = w;

                if (double.IsNaN(w.Real) || double.IsInfinity(w.Real) ||
                    double.IsNaN(w.Imaginary) || double.IsInfinity(w.Imaginary))
                {
                    valid[idx] = false;
                }
                else
                {
                    valid[idx] = true;
                    float absH = Mathf.Abs((float)w.Real);
                    if (absH > maxHeight && absH < 1e6f) maxHeight = absH;
                }
                prevW = w;
            }
        }

        // Height scale: map [-maxHeight, maxHeight] → [-boxSize/2, boxSize/2]
        float heightScale = boxSize / (2f * maxHeight);

        // ── PASS 2: Build vertices with proper scaling ──
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();

        for (int idx = 0; idx < rows * cols; idx++)
        {
            if (!valid[idx])
            {
                vertices.Add(Vector3.zero);
                colors.Add(Color.clear);
            }
            else
            {
                Complex z = zValues[idx];
                Complex w = wValues[idx];

                // Clamp height to box bounds
                float height = Mathf.Clamp((float)w.Real, -maxHeight, maxHeight);

                float vx = (float)z.Real * inputScale;
                float vy = height * heightScale;
                float vz = (float)z.Imaginary * inputScale;

                vertices.Add(new Vector3(vx, vy, vz));

                // Domain coloring: hue from phase, brightness from magnitude
                float hue = (float)(w.Phase / (2.0 * Math.PI));
                if (hue < 0) hue += 1f;
                float saturation = 0.8f;
                float brightness = Mathf.Clamp01(1f - 0.3f * Mathf.Log10(1f + (float)w.Magnitude));

                colors.Add(Color.HSVToRGB(hue, saturation, Mathf.Max(brightness, 0.3f)));
            }
        }

        // ── Build triangles (correct winding: normals point UP) ──
        for (int i = 0; i < rows - 1; i++)
        {
            for (int j = 0; j < cols - 1; j++)
            {
                int a = i * cols + j;
                int b = i * cols + j + 1;
                int c = (i + 1) * cols + j;
                int d = (i + 1) * cols + j + 1;

                if (!valid[a] || !valid[b] || !valid[c] || !valid[d])
                    continue;

                // Skip torn triangles at branch cuts
                float maxEdge = MaxEdgeLength(vertices[a], vertices[b], vertices[c], vertices[d]);
                if (maxEdge > boxSize * 0.3f) continue;

                // Winding: (a, b, c) and (b, d, c) → normals point upward
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(b);
                triangles.Add(d);
                triangles.Add(c);
            }
        }

        // ── Build mesh ──
        Mesh mesh = new Mesh();
        mesh.indexFormat = vertices.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    // ════════════════════════════════════════════════════════════
    // ANALYTIC CONTINUATION
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Given the previous function value, pick the branch at the new z
    /// that is closest to prevW (numerical analytic continuation).
    /// </summary>
    static Complex ContinueValue(ParsedFunction func, Complex z, Complex prevW)
    {
        var candidates = func.GetCandidates(z);

        Complex best = candidates[0];
        double bestDist = (candidates[0] - prevW).Magnitude;

        for (int i = 1; i < candidates.Count; i++)
        {
            double dist = (candidates[i] - prevW).Magnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidates[i];
            }
        }

        return best;
    }

    // ════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════

    static float MaxEdgeLength(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        float e1 = (a - b).sqrMagnitude;
        float e2 = (a - c).sqrMagnitude;
        float e3 = (b - d).sqrMagnitude;
        float e4 = (c - d).sqrMagnitude;
        float e5 = (a - d).sqrMagnitude;
        float e6 = (b - c).sqrMagnitude;
        return Mathf.Sqrt(Mathf.Max(Mathf.Max(Mathf.Max(e1, e2), Mathf.Max(e3, e4)), Mathf.Max(e5, e6)));
    }

    // ════════════════════════════════════════════════════════════
    // INTERSECTION FINDER (for point probing)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Find all intersections of a vertical line at (re, im) with the Riemann surface.
    /// Returns the function values at each intersection.
    /// </summary>
    public static List<Complex> FindIntersections(ParsedFunction func, double re, double im,
        float maxVal, int numSamples = 500)
    {
        var intersections = new List<Complex>();
        Complex z = new Complex(re, im);
        double r = z.Magnitude;
        if (r < 1e-10) r = 1e-10;

        // Evaluate along the angular path, tracking analytic continuation
        double baseTheta = z.Phase;
        Complex prevW = func.Evaluate(z);

        // Collect all distinct values the function takes at this z
        var values = new HashSet<string>();
        AddValue(intersections, values, prevW, maxVal);

        for (int sheet = 0; sheet < func.Sheets; sheet++)
        {
            // For each additional sheet, we go around one more full rotation
            // and evaluate at the same z position
            double theta = baseTheta + 2 * Math.PI * (sheet + 1);
            Complex zWrapped = Complex.FromPolarCoordinates(r, theta);
            // zWrapped has the same position as z, but we continue from prevW
            Complex w = ContinueValue(func, zWrapped, prevW);
            AddValue(intersections, values, w, maxVal);
            prevW = w;
        }

        return intersections;
    }

    static void AddValue(List<Complex> list, HashSet<string> seen, Complex w, float maxVal)
    {
        if (double.IsNaN(w.Real) || double.IsInfinity(w.Real)) return;
        if (Math.Abs(w.Real) > maxVal) return;

        // Round for deduplication
        string key = $"{Math.Round(w.Real, 6)},{Math.Round(w.Imaginary, 6)}";
        if (!seen.Contains(key))
        {
            seen.Add(key);
            list.Add(w);
        }
    }
}
