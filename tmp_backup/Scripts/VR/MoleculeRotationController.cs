using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Allows user to rotate the molecule using VR controllers
/// Grab with trigger to rotate, molecule stays in place but rotates
/// </summary>
public class MoleculeRotationController : MonoBehaviour
{
    [Header("References")]
    public MoleculeRenderer moleculeRenderer;
    public MoleculePlaneAlignment planeAlignment;

    [Header("Settings")]
    [Tooltip("Which controller(s) to use for rotation")]
    public ControllerSelection controllerSelection = ControllerSelection.Both;
    
    [Tooltip("Rotation speed multiplier")]
    [Range(0.1f, 15f)]
    public float rotationSpeed = 8f;

    [Tooltip("Trigger threshold to start rotation")]
    [Range(0.1f, 1f)]
    public float triggerThreshold = 0.3f;

    [Tooltip("Time in seconds before auto-rotation resumes")]
    public float autoRotationDelay = 5f;

    public enum ControllerSelection { Right, Left, Both }

    private bool isGrabbingRight = false;
    private bool isGrabbingLeft = false;
    private bool hasEverGrabbed = false; // Track if user has ever grabbed
    private Vector3 lastControllerPositionRight;
    private Vector3 lastControllerPositionLeft;
    private InputDevice rightDevice;
    private InputDevice leftDevice;
    private float timeSinceLastInteraction = 0f;

    void Start()
    {
        // Find both controller devices
        rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        
        if (moleculeRenderer == null)
        {
            moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
        }
        
        if (planeAlignment == null)
        {
            planeAlignment = FindObjectOfType<MoleculePlaneAlignment>();
        }
        
        Debug.Log($"[MoleculeRotation] Controllers initialized: Right={rightDevice.isValid}, Left={leftDevice.isValid}");
    }

    void Update()
    {
        // Refresh device connections if invalid
        if (!rightDevice.isValid)
        {
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }
        if (!leftDevice.isValid)
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        }

        bool wasGrabbing = isGrabbingRight || isGrabbingLeft;

        // Process right controller
        if ((controllerSelection == ControllerSelection.Right || controllerSelection == ControllerSelection.Both) && rightDevice.isValid)
        {
            ProcessController(rightDevice, ref isGrabbingRight, ref lastControllerPositionRight, "Right");
        }
        else if (isGrabbingRight)
        {
            isGrabbingRight = false;
        }

        // Process left controller
        if ((controllerSelection == ControllerSelection.Left || controllerSelection == ControllerSelection.Both) && leftDevice.isValid)
        {
            ProcessController(leftDevice, ref isGrabbingLeft, ref lastControllerPositionLeft, "Left");
        }
        else if (isGrabbingLeft)
        {
            isGrabbingLeft = false;
        }

        bool isGrabbing = isGrabbingRight || isGrabbingLeft;
        
        // Track if we've ever grabbed
        if (isGrabbing)
        {
            hasEverGrabbed = true;
        }
        
        // Restart auto-rotation after inactivity
        if (!isGrabbing && hasEverGrabbed)
        {
            timeSinceLastInteraction += Time.deltaTime;
            
            if (timeSinceLastInteraction >= autoRotationDelay && planeAlignment != null)
            {
                Debug.Log($"[MoleculeRotation] Restarting auto-rotation after {timeSinceLastInteraction:F1}s of inactivity");
                planeAlignment.StartAutoRotation();
                hasEverGrabbed = false; // Reset so we don't spam StartAutoRotation every frame
                timeSinceLastInteraction = 0f;
            }
        }
        else if (isGrabbing)
        {
            // Reset timer only when actively grabbing
            timeSinceLastInteraction = 0f;
        }
    }

    void ProcessController(InputDevice device, ref bool isGrabbing, ref Vector3 lastControllerPosition, string handName)
    {
        // Get trigger value
        float triggerValue;
        if (!device.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
        {
            return;
        }

        bool shouldGrab = triggerValue > triggerThreshold;

        // Start grabbing
        if (shouldGrab && !isGrabbing)
        {
            StartGrab(device, ref isGrabbing, ref lastControllerPosition, handName);
        }
        // Stop grabbing
        else if (!shouldGrab && isGrabbing)
        {
            StopGrab(ref isGrabbing, handName);
        }
        // Continue grabbing
        else if (shouldGrab && isGrabbing)
        {
            UpdateRotation(device, ref lastControllerPosition);
            timeSinceLastInteraction = 0f;
        }
    }

    void StartGrab(InputDevice device, ref bool isGrabbing, ref Vector3 lastControllerPosition, string handName)
    {
        isGrabbing = true;
        timeSinceLastInteraction = 0f;
        
        // Get initial controller position
        Vector3 controllerPos;
        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out controllerPos))
        {
            lastControllerPosition = controllerPos;
        }
        
        // Stop auto-rotation if active
        if (planeAlignment != null)
        {
            planeAlignment.StopAutoRotation();
        }
        
        Debug.Log($"[MoleculeRotation] Started grab ({handName})");
    }

    void StopGrab(ref bool isGrabbing, string handName)
    {
        isGrabbing = false;
        Debug.Log($"[MoleculeRotation] Released grab ({handName})");
    }

    void UpdateRotation(InputDevice device, ref Vector3 lastControllerPosition)
    {
        if (moleculeRenderer == null) return;

        // Get current controller position
        Vector3 controllerPos;
        if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out controllerPos))
        {
            return;
        }

        // Calculate movement delta
        Vector3 deltaPos = controllerPos - lastControllerPosition;
        
        if (deltaPos.magnitude > 0.001f)
        {
            // Store original position AND parent position to keep molecule in place
            Vector3 originalPosition = moleculeRenderer.transform.position;
            Vector3 originalLocalPosition = moleculeRenderer.transform.localPosition;
            
            // Create rotation based on controller movement
            // Horizontal movement = Y-axis rotation
            // Vertical movement = X-axis rotation (relative to camera)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 camRight = mainCam.transform.right;
                Vector3 camUp = Vector3.up;
                
                float horizontalDelta = Vector3.Dot(deltaPos, camRight);
                float verticalDelta = Vector3.Dot(deltaPos, camUp);
                
                // Create rotation
                Quaternion yRot = Quaternion.AngleAxis(-horizontalDelta * rotationSpeed * 200f, Vector3.up);
                Quaternion xRot = Quaternion.AngleAxis(-verticalDelta * rotationSpeed * 200f, camRight);
                Quaternion deltaRotation = yRot * xRot;
                
                // Apply rotation only, keep position fixed
                moleculeRenderer.transform.rotation = deltaRotation * moleculeRenderer.transform.rotation;
                moleculeRenderer.transform.position = originalPosition;
                moleculeRenderer.transform.localPosition = originalLocalPosition;
                
                // Trigger bond re-render to update stereochemistry
                moleculeRenderer.RerenderBondsOnly();
            }
        }

        lastControllerPosition = controllerPos;
    }

    void OnDisable()
    {
        if (isGrabbingRight)
        {
            isGrabbingRight = false;
        }
        if (isGrabbingLeft)
        {
            isGrabbingLeft = false;
        }
    }
}
