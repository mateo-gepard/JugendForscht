using UnityEngine;

/// <summary>
/// VR Controller f�r Molek�l-Rotation mit Quest-Controllern
/// Rotiert das Molek�l um den Anchor-Punkt
/// </summary>
public class VRMoleculeController : MonoBehaviour
{
    [Header("References")]
    public MoleculePlaneAlignment planeAlignment;

    [Header("Input Settings")]
    [Tooltip("Rotations-Geschwindigkeit")]
    [Range(10f, 100f)]
    public float rotationSpeed = 50f;

    [Tooltip("Controller-Button f�r Rotation (z.B. Grip)")]
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
                // Rotate around anchor
                float deltaX = thumbstick.y * rotationSpeed * Time.deltaTime;
                float deltaY = thumbstick.x * rotationSpeed * Time.deltaTime;

                planeAlignment.RotateAroundAnchorEuler(deltaX, deltaY);

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
    /// Alternative: Touch-basierte Rotation (f�r alternative Input-Systeme)
    /// </summary>
    public void RotateWithTouch(Vector2 touchDelta)
    {
        if (planeAlignment == null) return;

        float deltaX = touchDelta.y * rotationSpeed * Time.deltaTime;
        float deltaY = touchDelta.x * rotationSpeed * Time.deltaTime;

        planeAlignment.RotateAroundAnchorEuler(deltaX, deltaY);
    }
}