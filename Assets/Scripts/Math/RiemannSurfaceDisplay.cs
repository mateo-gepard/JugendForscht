using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using Complex = System.Numerics.Complex;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

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
    public Color axisColor = new Color(0.7f, 0.7f, 0.8f, 0.8f);
    public Color gridColor = new Color(0.5f, 0.5f, 0.6f, 0.35f);
    public Color probeLineColor = new Color(0f, 0.9f, 1f, 0.9f); // Cyan

    [Header("Interaction")]
    public float tapDistance = 0.12f; // Max finger distance to complex plane for tap (12cm — generous)

    // Runtime state
    private GameObject surfaceMeshObj;
    private GameObject boxContainer;
    private List<GameObject> axisObjects = new List<GameObject>();
    private List<GameObject> labelObjects = new List<GameObject>();
    private GameObject probeLine;
    private List<GameObject> probeLabels = new List<GameObject>();
    private ParsedFunction currentFunction;
    private string currentFunctionText = "";

    // Scaling factors — must match those from RiemannMeshGenerator
    private float inputScale;   // boxSize / (2 * maxVal) — for X/Z
    private float heightScale;  // boxSize / (2 * maxHeight) — for Y (set after mesh generation)
    private float maxHeight;    // actual max |Re(f(z))| found during mesh generation

    // Label showing the function below the plot
    private TextMesh functionLabel;

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
        CreateHeightTickLabels(); // Must be after mesh gen (uses heightScale)
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
        if (functionLabel != null && functionLabel.gameObject != null) Destroy(functionLabel.gameObject);

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

        // Semi-transparent quad for the complex plane at y=0
        var planeQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        planeQuad.name = "ComplexPlane";
        planeQuad.transform.SetParent(boxContainer.transform, false);
        planeQuad.transform.localPosition = Vector3.zero;
        planeQuad.transform.localRotation = Quaternion.Euler(90, 0, 0);
        planeQuad.transform.localScale = new Vector3(boxSize, boxSize, 1f);
        Destroy(planeQuad.GetComponent<Collider>());
        var planeMr = planeQuad.GetComponent<MeshRenderer>();
        var planeMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
        planeMat.color = new Color(0.3f, 0.3f, 0.4f, 0.08f);
        planeMr.material = planeMat;
        planeMr.shadowCastingMode = ShadowCastingMode.Off;
        axisObjects.Add(planeQuad);

        // Grid lines on the complex plane
        for (float v = -maxVal; v <= maxVal + 0.001f; v += step)
        {
            float pos = v * scale;
            if (Mathf.Abs(v) < 0.001f) continue; // Skip origin (axis covers it)

            // Lines parallel to X axis (at different Z positions)
            var lineX = CreateLine(
                new Vector3(-half, 0, pos), new Vector3(half, 0, pos),
                gridColor, 0.001f, boxContainer.transform);
            axisObjects.Add(lineX);

            // Lines parallel to Z axis (at different X positions)
            var lineZ = CreateLine(
                new Vector3(pos, 0, -half), new Vector3(pos, 0, half),
                gridColor, 0.001f, boxContainer.transform);
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
        }

        // Origin label
        var origin = CreateTextLabel(
            new Vector3(-0.01f, -0.005f, -0.01f),
            "0", axisColor, 0.01f, boxContainer.transform);
        labelObjects.Add(origin);
    }

    /// <summary>
    /// Create Y-axis (height) tick labels. Called AFTER mesh generation
    /// so we can use the correct heightScale.
    /// </summary>
    void CreateHeightTickLabels()
    {
        if (boxContainer == null || maxHeight < 0.001f) return;

        float half = boxSize / 2f;
        float step = CalculateGridStep(maxHeight);

        for (float v = -maxHeight; v <= maxHeight + 0.001f; v += step)
        {
            if (Mathf.Abs(v) < 0.001f) continue;

            float yPos = v * heightScale;
            if (Mathf.Abs(yPos) > half) continue; // Skip if outside box

            string text = FormatNumber(v);
            var tickH = CreateTextLabel(
                new Vector3(-half - 0.015f, yPos, 0),
                text, axisColor, 0.008f, boxContainer.transform);
            labelObjects.Add(tickH);
        }
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
        Mesh mesh = RiemannMeshGenerator.Generate(currentFunction, maxVal, boxSize, out float mh);
        maxHeight = mh;
        inputScale = boxSize / (2f * maxVal);
        heightScale = boxSize / (2f * maxHeight);

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

        // Force double-sided rendering (Cull Off) on any shader
        if (surfaceMaterial.HasProperty("_Cull"))
            surfaceMaterial.SetFloat("_Cull", 0); // 0 = Off
        else
            surfaceMaterial.SetInt("_Cull", 0);

        // Enable vertex colors for Particles shader
        if (surfaceMaterial.HasProperty("_ColorMode"))
            surfaceMaterial.SetFloat("_ColorMode", 1); // Multiply

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
            labelObj.transform.localPosition = new Vector3(0, -boxSize / 2f - 0.035f, 0);

            functionLabel = labelObj.AddComponent<TextMesh>();
            functionLabel.anchor = TextAnchor.MiddleCenter;
            functionLabel.alignment = TextAlignment.Center;
            functionLabel.characterSize = 0.012f;
            functionLabel.fontSize = 64;
            functionLabel.color = new Color(0.9f, 0.9f, 1f, 0.95f);

            // Billboard: face camera
            labelObj.AddComponent<BillboardLabel>();
        }

        functionLabel.text = $"f(z) = {currentFunctionText}";
    }

    // ════════════════════════════════════════════════════════════
    // POINT PROBING (finger tap interaction)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when a finger taps a point on the complex plane.
    /// localPos is in the local space of boxContainer.
    /// </summary>
    public void ProbePoint(Vector3 localPos)
    {
        if (currentFunction == null || boxContainer == null) return;

        ClearProbe();

        float half = boxSize / 2f;

        // Convert local position to complex coordinates using inputScale
        double re = localPos.x / inputScale;
        double im = localPos.z / inputScale;

        // Clamp to bounds
        re = System.Math.Max(-maxVal, System.Math.Min(maxVal, re));
        im = System.Math.Max(-maxVal, System.Math.Min(maxVal, im));

        // X/Z position in box-local space
        float probeX = (float)re * inputScale;
        float probeZ = (float)im * inputScale;

        // Draw vertical probe line (full height of box)
        Vector3 lineStart = new Vector3(probeX, -half, probeZ);
        Vector3 lineEnd = new Vector3(probeX, half, probeZ);
        probeLine = CreateLine(lineStart, lineEnd, probeLineColor, 0.002f, boxContainer.transform);

        // Find intersections (function values at this z for each sheet)
        var intersections = RiemannMeshGenerator.FindIntersections(
            currentFunction, re, im, maxVal);

        // Draw intersection dots and labels
        Complex z = new Complex(re, im);
        for (int i = 0; i < intersections.Count; i++)
        {
            var w = intersections[i];
            // Y position uses the SAME heightScale as the mesh generator
            float yPos = Mathf.Clamp((float)w.Real * heightScale, -half, half);

            // Intersection dot (bright, visible)
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.SetParent(boxContainer.transform, false);
            dot.transform.localPosition = new Vector3(probeX, yPos, probeZ);
            dot.transform.localScale = Vector3.one * 0.012f; // 1.2cm — visible in VR
            var dotMr = dot.GetComponent<MeshRenderer>();
            dotMr.material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
            dotMr.material.color = Color.yellow;
            dotMr.shadowCastingMode = ShadowCastingMode.Off;
            Destroy(dot.GetComponent<Collider>());
            probeLabels.Add(dot);

            // Label: show z and f(z) values
            string zText = FormatComplex(z);
            string wText = FormatComplex(w);
            string labelText = $"z = {zText}\nf(z) = {wText}";

            // Offset label to the right so it doesn't overlap the line
            var label = CreateProbeLabel(
                new Vector3(probeX + 0.035f, yPos + 0.005f, probeZ),
                labelText, Color.white, 2.5f, boxContainer.transform);
            probeLabels.Add(label);
        }

        // Base label at the bottom showing tapped z-coordinate
        string baseText = $"z = {FormatComplex(z)}";
        var baseLabel = CreateProbeLabel(
            new Vector3(probeX, -half - 0.025f, probeZ),
            baseText, new Color(1f, 1f, 0.5f), 2f, boxContainer.transform);
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
    /// Called every frame to check for finger pokes on the complex plane.
    /// Triggers when index finger tip is near the y=0 plane within the box bounds.
    /// </summary>
    private float lastProbeTime = -10f;
    private const float probeCooldown = 1.5f;
    private HandRotationController cachedHRC;

    public void CheckFingerTap()
    {
        if (boxContainer == null) return;
        if (Time.time - lastProbeTime < probeCooldown) return;

        // Cache the HandRotationController
        if (cachedHRC == null)
            cachedHRC = UnityEngine.Object.FindObjectOfType<HandRotationController>();
        if (cachedHRC == null) return;

        if (TryFingerPoke(cachedHRC.rightHand)) return;
        TryFingerPoke(cachedHRC.leftHand);
    }

    private bool TryFingerPoke(Hand hand)
    {
        if (hand == null || !hand.IsTrackedDataValid) return false;
        if (!hand.GetJointPose(HandJointId.HandIndexTip, out Pose tipPose)) return false;

        // Convert to local space of the box
        Vector3 localTip = boxContainer.transform.InverseTransformPoint(tipPose.position);
        float half = boxSize / 2f;

        // Check if finger is within the box's X/Z bounds
        bool inXZ = Mathf.Abs(localTip.x) < half && Mathf.Abs(localTip.z) < half;

        // Check if finger is near the complex plane (y ≈ 0, within ~8cm)
        bool nearPlane = Mathf.Abs(localTip.y) < 0.08f;

        if (inXZ && nearPlane)
        {
            // Also check that index finger is extended (not pinching for rotation)
            float pinch = hand.GetFingerPinchStrength(HandFinger.Index);
            if (pinch < 0.4f) // Finger is extended, not pinching
            {
                lastProbeTime = Time.time;
                Vector3 probePos = new Vector3(localTip.x, 0f, localTip.z);
                ProbePoint(probePos);
                return true;
            }
        }
        return false;
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
        obj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);

        var mr = obj.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
        mr.material.color = color;
        mr.shadowCastingMode = ShadowCastingMode.Off;

        Destroy(obj.GetComponent<Collider>());

        return obj;
    }

    /// <summary>
    /// Create a small text label (for axis ticks, origin marker etc.)
    /// Uses Unity TextMesh (legacy) – guaranteed to work without font assets.
    /// </summary>
    static GameObject CreateTextLabel(Vector3 position, string text, Color color,
        float charSize, Transform parent)
    {
        var obj = new GameObject("Label");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;

        var tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = charSize;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        // Billboard
        obj.AddComponent<BillboardLabel>();

        return obj;
    }

    /// <summary>
    /// Create a probe label – larger, bold, always faces camera.
    /// </summary>
    static GameObject CreateProbeLabel(Vector3 position, string text, Color color,
        float charSize, Transform parent)
    {
        var obj = new GameObject("ProbeLabel");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;

        var tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = charSize;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleLeft;
        tm.alignment = TextAlignment.Left;
        tm.color = color;
        tm.fontStyle = FontStyle.Bold;

        // Billboard: always face camera
        obj.AddComponent<BillboardLabel>();

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

/// <summary>
/// Simple component that makes a label always face the VR camera.
/// </summary>
public class BillboardLabel : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position, Vector3.up);
    }
}
