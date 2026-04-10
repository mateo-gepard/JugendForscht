using UnityEngine;
using System;

/// <summary>
/// Manages the Riemann surface visualization.
/// Orchestrates function parsing, mesh generation, and VR display.
/// Called by WebSocketServer when receiving 'riemann' messages.
/// 
/// Placed on the MoleculeRenderer GameObject (reuses its position management).
/// </summary>
public class RiemannSurfaceManager : MonoBehaviour
{
    [Header("Display Settings")]
    public float defaultMaxVal = 5f;
    public float defaultBoxSize = 0.4f; // 40cm cube

    // Runtime
    private RiemannSurfaceDisplay display;
    private ParsedFunction currentFunction;
    private string currentFunctionText;
    private float currentMaxVal;
    private bool isActive = false;

    // ════════════════════════════════════════════════════════════
    // PUBLIC API (called by WebSocketServer)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Parse and display a Riemann surface for the given function string.
    /// </summary>
    public void PlotFunction(string functionText, float maxVal = 0)
    {
        if (maxVal <= 0) maxVal = defaultMaxVal;
        currentMaxVal = maxVal;
        currentFunctionText = functionText;

        try
        {
            currentFunction = ComplexFunctionParser.Parse(functionText);
            Debug.Log($"[RiemannManager] Parsed: f(z) = {functionText}, detected {currentFunction.Sheets} sheets");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RiemannManager] Parse error: {e.Message}");
            return;
        }

        EnsureDisplay();
        
        // Position in front of camera
        PositionInFrontOfCamera();

        display.ShowSurface(currentFunction, functionText, currentMaxVal);
        isActive = true;
    }

    /// <summary>
    /// Update the maximum value and re-render.
    /// </summary>
    public void SetBounds(float maxVal)
    {
        currentMaxVal = maxVal;
        if (display != null && currentFunction != null)
        {
            display.SetMaxVal(maxVal);
        }
    }

    /// <summary>
    /// Clear the Riemann surface.
    /// </summary>
    public void Clear()
    {
        if (display != null)
        {
            display.ClearSurface();
        }
        currentFunction = null;
        currentFunctionText = null;
        isActive = false;
    }

    /// <summary>
    /// Activate the Riemann surface mode.
    /// </summary>
    public void Activate()
    {
        EnsureDisplay();
        display.gameObject.SetActive(true);
        isActive = true;
    }

    /// <summary>
    /// Deactivate the Riemann surface mode.
    /// </summary>
    public void Deactivate()
    {
        if (display != null)
        {
            display.ClearSurface();
            display.gameObject.SetActive(false);
        }
        isActive = false;
    }

    // ════════════════════════════════════════════════════════════
    // POSITIONING
    // ════════════════════════════════════════════════════════════

    void PositionInFrontOfCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Position: slightly in front and below eye level
        Vector3 forward = cam.transform.forward;
        forward.y = 0; // Keep horizontal
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 pos = cam.transform.position + forward * 0.6f + Vector3.down * 0.15f;
        display.transform.position = pos;
        display.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    // ════════════════════════════════════════════════════════════
    // INTERNALS
    // ════════════════════════════════════════════════════════════

    void EnsureDisplay()
    {
        if (display != null) return;

        var displayObj = new GameObject("RiemannSurfaceDisplay");
        display = displayObj.AddComponent<RiemannSurfaceDisplay>();
        display.boxSize = defaultBoxSize;
        display.maxVal = defaultMaxVal;
    }

    void Update()
    {
        if (!isActive || display == null) return;

        // Check for finger taps on the complex plane
        display.CheckFingerTap();
    }
}
