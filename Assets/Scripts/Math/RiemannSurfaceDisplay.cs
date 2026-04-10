using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Numerics;
using System.Collections.Generic;
using TMPro;
using Oculus.Interaction.Input;

/// <summary>
/// VR display for Riemann surfaces.
/// Renders the surface mesh inside a bounded 3D box with axes, grid, labels.
/// Handles finger-tap interaction for point probing (vertical intersection lines).
/// </summary>
public class RiemannSurfaceDisplay : MonoBehaviour
{
    [Header("Box Settings")]
    public float boxSize = 0.4f; // Physical box size in meters (40cm cube)
    public float maxVal = 5f;    // Mathematical bounds (symmetric)

    [Header("Visuals")]
    public Color axisColor = new Color(0.7f, 0.7f, 0.8f, 0.6f);
    public Color gridColor = new Color(0.4f, 0.4f, 0.5f, 0.15f);
    public Color probeLineColor = new Color(0f, 0.9f, 1f, 0.8f); // Cyan

    [Header("Interaction")]
    public float tapDistance = 0.05f; // Max finger distance to complex plane for tap

    // Runtime state
    private GameObject surfaceMeshObj;
    private GameObject boxContainer;
    private List<GameObject> axisObjects = new List<GameObject>();
    private List<GameObject> labelObjects = new List<GameObject>();
    private GameObject probeLine;
    private List<GameObject> probeLabels = new List<GameObject>();
    private ParsedFunction currentFunction;
    private string currentFunctionText = "";

    // Label showing the function below the plot
    private TextMeshPro functionLabel;

    // Material cache
    private Material surfaceMaterial;
    private Material lineMaterial;

    // ════════════════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Display a Riemann surface for the given function.
    /// </summary>
    public void ShowSurface(ParsedFunction func, string functionText, float newMaxVal)
    {
        currentFunction = func;
        currentFunctionText = functionText;
        maxVal = newMaxVal;

        ClearSurface();
        CreateBox();
        GenerateAndShowMesh();
        UpdateFunctionLabel();

        Debug.Log($"[RiemannDisplay] Rendered f(z) = {functionText}, sheets={func.Sheets}, maxVal={maxVal}");
    }

    /// <summary>
    /// Clear everything.
    /// </summary>
    public void ClearSurface()
    {
        ClearProbe();

        if (surfaceMeshObj != null) Destroy(surfaceMeshObj);
        foreach (var obj in axisObjects) if (obj != null) Destroy(obj);
        foreach (var obj in labelObjects) if (obj != null) Destroy(obj);
        if (boxContainer != null) Destroy(boxContainer);
        if (functionLabel != null) Destroy(functionLabel.gameObject);

        axisObjects.Clear();
        labelObjects.Clear();
        surfaceMeshObj = null;
        boxContainer = null;
        functionLabel = null;
    }

    /// <summary>
    /// Update the maximum value (rescales axes).
    /// </summary>
    public void SetMaxVal(float newMaxVal)
    {
        if (currentFunction != null)
        {
            ShowSurface(currentFunction, currentFunctionText, newMaxVal);
        }
    }

    // ════════════════════════════════════════════════════════════
    // BOX & AXES
    // ════════════════════════════════════════════════════════════

    void CreateBox()
    {
        boxContainer = new GameObject("RiemannBox");
        boxContainer.transform.SetParent(transform, false);

        float half = boxSize / 2f;

        // Create axes
        CreateAxis(Vector3.right, half, "Re(z)", new Color(1f, 0.4f, 0.4f, 0.8f));
        CreateAxis(Vector3.forward, half, "Im(z)", new Color(0.4f, 1f, 0.4f, 0.8f));
        CreateAxis(Vector3.up, half, "Re(f(z))", new Color(0.4f, 0.6f, 1f, 0.8f));

        // Create grid on complex plane (y=0)
        CreateGrid();

        // Create tick labels
        CreateTickLabels();
    }

    void CreateAxis(Vector3 dir, float halfLength, string label, Color color)
    {
        // Axis line
        var axisObj = CreateLine(
            -dir * halfLength, dir * halfLength,
            color, 0.001f, boxContainer.transform);
        axisObjects.Add(axisObj);

        // Negative axis (dimmer)
        var negColor = color * 0.5f;
        negColor.a = 0.4f;

        // Arrow head
        var arrowObj = CreateCone(dir * halfLength, dir, color, 0.006f, 0.015f, boxContainer.transform);
        axisObjects.Add(arrowObj);

        // Label at arrow tip
        var labelObj = CreateTextLabel(
            dir * (halfLength + 0.02f), label,
            color, 0.015f, boxContainer.transform);
        labelObjects.Add(labelObj);
    }

    void CreateGrid()
    {
        float half = boxSize / 2f;
        float scale = boxSize / (2f * maxVal);
        float step = CalculateGridStep(maxVal);

        for (float v = -maxVal; v <= maxVal + 0.001f; v += step)
        {
            float pos = v * scale;
            if (Mathf.Abs(v) < 0.001f) continue; // Skip origin (axis covers it)

            // Lines parallel to X axis (at different Z positions)
            var lineX = CreateLine(
                new Vector3(-half, 0, pos), new Vector3(half, 0, pos),
                gridColor, 0.0005f, boxContainer.transform);
            axisObjects.Add(lineX);

            // Lines parallel to Z axis (at different X positions)
            var lineZ = CreateLine(
                new Vector3(pos, 0, -half), new Vector3(pos, 0, half),
                gridColor, 0.0005f, boxContainer.transform);
            axisObjects.Add(lineZ);
        }
    }

    void CreateTickLabels()
    {
        float scale = boxSize / (2f * maxVal);
        float step = CalculateGridStep(maxVal);

        for (float v = -maxVal; v <= maxVal + 0.001f; v += step)
        {
            if (Mathf.Abs(v) < 0.001f) continue;

            string text = FormatNumber(v);

            // Re(z) axis ticks (along X)
            var tickRe = CreateTextLabel(
                new Vector3(v * scale, 0, -boxSize / 2f - 0.012f),
                text, axisColor, 0.008f, boxContainer.transform);
            labelObjects.Add(tickRe);

            // Im(z) axis ticks (along Z)
            var tickIm = CreateTextLabel(
                new Vector3(-boxSize / 2f - 0.012f, 0, v * scale),
                text + "i", axisColor, 0.008f, boxContainer.transform);
            labelObjects.Add(tickIm);

            // Height axis ticks (along Y) – only a few
            if (Mathf.Abs(v) <= maxVal)
            {
                var tickH = CreateTextLabel(
                    new Vector3(-boxSize / 2f - 0.015f, v * scale, 0),
                    text, axisColor, 0.008f, boxContainer.transform);
                labelObjects.Add(tickH);
            }
        }

        // Origin label
        var origin = CreateTextLabel(
            new Vector3(-0.01f, -0.005f, -0.01f),
            "0", axisColor, 0.01f, boxContainer.transform);
        labelObjects.Add(origin);
    }

    float CalculateGridStep(float max)
    {
        if (max <= 2) return 0.5f;
        if (max <= 5) return 1f;
        if (max <= 10) return 2f;
        if (max <= 20) return 5f;
        return 10f;
    }

    string FormatNumber(float v)
    {
        if (Mathf.Abs(v - Mathf.Round(v)) < 0.01f)
            return ((int)Mathf.Round(v)).ToString();
        return v.ToString("F1");
    }

    // ════════════════════════════════════════════════════════════
    // MESH RENDERING
    // ════════════════════════════════════════════════════════════

    void GenerateAndShowMesh()
    {
        Mesh mesh = RiemannMeshGenerator.Generate(currentFunction, maxVal, boxSize);

        surfaceMeshObj = new GameObject("RiemannSurface");
        surfaceMeshObj.transform.SetParent(boxContainer.transform, false);

        var mf = surfaceMeshObj.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        var mr = surfaceMeshObj.AddComponent<MeshRenderer>();
        mr.material = GetSurfaceMaterial();
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Add MeshCollider for point probing raycasts
        var mc = surfaceMeshObj.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    Material GetSurfaceMaterial()
    {
        if (surfaceMaterial != null) return surfaceMaterial;

        // Use our custom Riemann surface shader (double-sided, vertex colors, lit)
        var shader = Shader.Find("Custom/RiemannSurface");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Standard");

        surfaceMaterial = new Material(shader);
        surfaceMaterial.enableInstancing = true;

        return surfaceMaterial;
    }

    // ════════════════════════════════════════════════════════════
    // FUNCTION LABEL
    // ════════════════════════════════════════════════════════════

    void UpdateFunctionLabel()
    {
        if (functionLabel == null)
        {
            var labelObj = new GameObject("FunctionLabel");
            labelObj.transform.SetParent(boxContainer.transform, false);
            labelObj.transform.localPosition = new Vector3(0, -boxSize / 2f - 0.03f, 0);

            functionLabel = labelObj.AddComponent<TextMeshPro>();
            functionLabel.alignment = TextAlignmentOptions.Center;
            functionLabel.fontSize = 0.8f;
            functionLabel.color = new Color(0.9f, 0.9f, 1f, 0.9f);
            functionLabel.enableWordWrapping = false;

            // Set rect size
            var rt = functionLabel.GetComponent<RectTransform>();
            rt.sizeDelta = new UnityEngine.Vector2(0.5f, 0.05f);
        }

        functionLabel.text = $"f(z) = {currentFunctionText}";
    }

    // ════════════════════════════════════════════════════════════
    // POINT PROBING (finger tap interaction)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when a finger taps a point on the complex plane.
    /// worldPos should be in the local space of this display.
    /// </summary>
    public void ProbePoint(Vector3 localPos)
    {
        if (currentFunction == null || boxContainer == null) return;

        ClearProbe();

        float scale = boxSize / (2f * maxVal);
        float half = boxSize / 2f;

        // Convert local position to complex coordinates
        double re = localPos.x / scale;
        double im = localPos.z / scale;

        // Clamp to bounds
        re = System.Math.Max(-maxVal, System.Math.Min(maxVal, re));
        im = System.Math.Max(-maxVal, System.Math.Min(maxVal, im));

        // Draw vertical probe line
        Vector3 lineStart = new Vector3((float)re * scale, -half, (float)im * scale);
        Vector3 lineEnd = new Vector3((float)re * scale, half, (float)im * scale);
        probeLine = CreateLine(lineStart, lineEnd, probeLineColor, 0.0015f, boxContainer.transform);

        // Find intersections
        var intersections = RiemannMeshGenerator.FindIntersections(
            currentFunction, re, im, maxVal);

        // Display intersection labels
        Complex z = new Complex(re, im);
        for (int i = 0; i < intersections.Count; i++)
        {
            var w = intersections[i];
            float yPos = Mathf.Clamp((float)w.Real * scale, -half, half);

            // Intersection dot
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.SetParent(boxContainer.transform, false);
            dot.transform.localPosition = new Vector3((float)re * scale, yPos, (float)im * scale);
            dot.transform.localScale = Vector3.one * 0.008f;
            var dotMr = dot.GetComponent<MeshRenderer>();
            dotMr.material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            dotMr.material.color = Color.cyan;
            dotMr.shadowCastingMode = ShadowCastingMode.Off;
            Destroy(dot.GetComponent<Collider>()); // Remove collider
            probeLabels.Add(dot);

            // Label: "z = a+bi → f(z) = c+di"
            string zText = FormatComplex(z);
            string wText = FormatComplex(w);
            string labelText = $"z={zText}\nf(z)={wText}";

            var label = CreateTextLabel(
                new Vector3((float)re * scale + 0.02f, yPos + 0.01f, (float)im * scale),
                labelText, Color.cyan, 0.012f, boxContainer.transform);
            probeLabels.Add(label);
        }

        // Label at base showing the tapped coordinates
        string baseText = $"z = {FormatComplex(z)}";
        var baseLabel = CreateTextLabel(
            new Vector3((float)re * scale, -half - 0.015f, (float)im * scale),
            baseText, new Color(1f, 1f, 0.5f), 0.01f, boxContainer.transform);
        probeLabels.Add(baseLabel);
    }

    public void ClearProbe()
    {
        if (probeLine != null) Destroy(probeLine);
        foreach (var obj in probeLabels) if (obj != null) Destroy(obj);
        probeLabels.Clear();
        probeLine = null;
    }

    /// <summary>
    /// Called every frame to check for finger taps on the complex plane.
    /// </summary>
    public void CheckFingerTap()
    {
        // Find hands
        Hand[] hands = FindObjectsOfType<Hand>();
        foreach (var hand in hands)
        {
            if (hand == null || !hand.IsTrackedDataValid) continue;

            if (hand.GetJointPose(HandJointId.HandIndexTip, out Pose tipPose))
            {
                // Convert to local space
                Vector3 localTip = boxContainer != null
                    ? boxContainer.transform.InverseTransformPoint(tipPose.position)
                    : transform.InverseTransformPoint(tipPose.position);

                float half = boxSize / 2f;

                // Check if finger is near the complex plane (y ≈ 0) and within bounds
                if (Mathf.Abs(localTip.y) < tapDistance &&
                    Mathf.Abs(localTip.x) < half &&
                    Mathf.Abs(localTip.z) < half)
                {
                    // Check for pinch gesture (tap)
                    float pinch = hand.GetFingerPinchStrength(HandFinger.Index);
                    if (pinch > 0.7f)
                    {
                        ProbePoint(localTip);
                    }
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════════
    // HELPER: PRIMITIVES
    // ════════════════════════════════════════════════════════════

    static GameObject CreateLine(Vector3 from, Vector3 to, Color color, float width, Transform parent)
    {
        var obj = new GameObject("Line");
        obj.transform.SetParent(parent, false);

        var lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = width;
        lr.endWidth = width;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;

        var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        mat.color = color;
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;

        return obj;
    }

    static GameObject CreateCone(Vector3 position, Vector3 direction, Color color,
        float radius, float height, Transform parent)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = "ArrowHead";
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = new Vector3(radius, height / 2f, radius);
        obj.transform.localRotation = UnityEngine.Quaternion.FromToRotation(Vector3.up, direction);

        var mr = obj.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
        mr.material.color = color;
        mr.shadowCastingMode = ShadowCastingMode.Off;

        Destroy(obj.GetComponent<Collider>());

        return obj;
    }

    static GameObject CreateTextLabel(Vector3 position, string text, Color color,
        float fontSize, Transform parent)
    {
        var obj = new GameObject("Label");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;

        var tmp = obj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        var rt = tmp.GetComponent<RectTransform>();
        rt.sizeDelta = new UnityEngine.Vector2(0.15f, 0.04f);

        return obj;
    }

    static string FormatComplex(Complex c)
    {
        double re = System.Math.Round(c.Real, 2);
        double im = System.Math.Round(c.Imaginary, 2);

        if (System.Math.Abs(im) < 0.005)
            return re.ToString("F2");
        if (System.Math.Abs(re) < 0.005)
            return $"{im:F2}i";

        string sign = im >= 0 ? "+" : "-";
        return $"{re:F2}{sign}{System.Math.Abs(im):F2}i";
    }

    // ════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════

    void OnDestroy()
    {
        ClearSurface();
        if (surfaceMaterial != null) Destroy(surfaceMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
