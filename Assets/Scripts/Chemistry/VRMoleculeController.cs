using UnityEngine;

/// <summary>
/// VR Controller für Molekül-Rotation mit Quest-Controllern.
/// Rotiert das Molekül um seinen eigenen geometrischen Schwerpunkt (Centroid),
/// nicht um den Transform-Pivot, um die "Orbit"-Anomalie zu vermeiden.
/// </summary>
public class VRMoleculeController : MonoBehaviour
{
    [Header("References")]
    public MoleculePlaneAlignment planeAlignment;

    [Header("Input Settings")]
    [Tooltip("Rotations-Geschwindigkeit")]
    [Range(10f, 100f)]
    public float rotationSpeed = 50f;

    [Tooltip("Controller-Button für Rotation (z.B. Grip)")]
    public OVRInput.Button rotationButton = OVRInput.Button.PrimaryHandTrigger;

    [Tooltip("Welcher Controller? (None = beide)")]
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

#pragma warning disable 0414
    private bool isRotating = false;
#pragma warning restore 0414
    private Vector2 lastThumbstick;

    void Update()
    {
        if (planeAlignment == null) return;

        // Check if rotation button is pressed
        bool buttonPressed = OVRInput.Get(rotationButton, controller);

        if (buttonPressed)
        {
            // Get thumbstick input
            Vector2 thumbstick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller);

            if (thumbstick.magnitude > 0.1f)
            {
                float deltaX = thumbstick.y * rotationSpeed * Time.deltaTime;
                float deltaY = thumbstick.x * rotationSpeed * Time.deltaTime;

                RotateAroundCentroid(deltaX, deltaY);

                isRotating = true;
            }
            else
            {
                isRotating = false;
            }

            lastThumbstick = thumbstick;
        }
        else
        {
            isRotating = false;
        }
    }

    /// <summary>
    /// Touch-basierte Rotation (für alternative Input-Systeme)
    /// </summary>
    public void RotateWithTouch(Vector2 touchDelta)
    {
        if (planeAlignment == null) return;

        float deltaX = touchDelta.y * rotationSpeed * Time.deltaTime;
        float deltaY = touchDelta.x * rotationSpeed * Time.deltaTime;

        RotateAroundCentroid(deltaX, deltaY);
    }

    /// <summary>
    /// Rotiert das Molekül um seinen geometrischen Schwerpunkt.
    /// Berechnet den Centroid aus allen Kind-Atomen des Renderers.
    /// Nutzt die Kamera-Achsen für intuitive Steuerung.
    /// </summary>
    private void RotateAroundCentroid(float deltaX, float deltaY)
    {
        Transform t = planeAlignment.transform;
        Camera cam = Camera.main;
        if (cam == null) return;

        // Berechne den geometrischen Schwerpunkt aller Kinder (Atome)
        Vector3 centroid = ComputeCentroid(t);

        // Rotiere um den Centroid, entlang der Kameraachsen
        t.RotateAround(centroid, cam.transform.up, -deltaY);
        t.RotateAround(centroid, cam.transform.right, deltaX);

        // Aktualisiere die internen Positionsdaten
        planeAlignment.UpdateAtomWorldPositions();
    }

    /// <summary>
    /// Berechnet den geometrischen Mittelpunkt aller Kind-Renderer (Atome/Bonds).
    /// Fallback: transform.position wenn keine Kinder vorhanden.
    /// </summary>
    private Vector3 ComputeCentroid(Transform root)
    {
        Renderer[] childRenderers = root.GetComponentsInChildren<Renderer>();
        if (childRenderers.Length == 0) return root.position;

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var r in childRenderers)
        {
            sum += r.bounds.center;
            count++;
        }
        return sum / count;
    }
}