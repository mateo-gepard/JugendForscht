using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Allows user to rotate the molecule using hand tracking via Meta Building Blocks
/// Pinch with thumb and index finger to grab and rotate
/// Requires: Building Blocks "Hand Grab Interaction" or "Interaction SDK"
/// </summary>
public class HandRotationController : MonoBehaviour
{
    [Header("References")]
    public MoleculeRenderer moleculeRenderer;
    public MoleculePlaneAlignment planeAlignment;

    [Header("Hand Tracking (Building Blocks)")]
    [Tooltip("Reference to the Hand component (from Building Blocks)")]
    public Hand rightHand;
    
    [Tooltip("Reference to the Hand component (from Building Blocks)")]
    public Hand leftHand;
    
    [Tooltip("Use right hand, left hand, or both")]
    public HandSelection handSelection = HandSelection.Both;

    [Header("Settings")]
    [Tooltip("Rotation speed multiplier (higher for hand tracking)")]
    [Range(1f, 15f)]
    public float rotationSpeed = 8f;

    [Tooltip("Pinch strength threshold to start rotation (0-1)")]
    [Range(0.5f, 1f)]
    public float pinchThreshold = 0.7f;

    [Tooltip("Time in seconds before auto-rotation resumes")]
    public float autoRotationDelay = 5f;
    
    [Tooltip("Dead zone to prevent jitter (meters)")]
    [Range(0.001f, 0.01f)]
    public float deadZone = 0.005f;

    public enum HandSelection { Right, Left, Both }

    private bool isGrabbingRight = false;
    private bool isGrabbingLeft = false;
    private bool hasEverGrabbed = false; // Track if user has ever grabbed
    private Vector3 lastHandPositionRight;
    private Vector3 lastHandPositionLeft;
    private float timeSinceLastInteraction = 0f;

    void Start()
    {
        // Auto-find hands if not assigned
        if (rightHand == null || leftHand == null)
        {
            Hand[] hands = FindObjectsOfType<Hand>();
            Debug.Log($"[HandRotation] Found {hands.Length} Hand components in scene");
            
            foreach (var hand in hands)
            {
                Debug.Log($"[HandRotation] Found hand: {hand.name}, Handedness: {hand.Handedness}");
                
                if (hand.Handedness == Handedness.Right && rightHand == null)
                {
                    rightHand = hand;
                    Debug.Log("[HandRotation] Assigned right hand");
                }
                else if (hand.Handedness == Handedness.Left && leftHand == null)
                {
                    leftHand = hand;
                    Debug.Log("[HandRotation] Assigned left hand");
                }
            }
        }
        
        if (moleculeRenderer == null)
        {
            moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
        }
        
        if (planeAlignment == null)
        {
            planeAlignment = FindObjectOfType<MoleculePlaneAlignment>();
        }

        if (rightHand == null && leftHand == null)
        {
            Debug.LogWarning("[HandRotation] No Hand components found. Make sure Building Blocks Interaction SDK is set up.");
        }
        else
        {
            Debug.Log($"[HandRotation] Initialized - Right: {rightHand != null}, Left: {leftHand != null}, Selection: {handSelection}");
        }
    }

    void Update()
    {
        bool wasGrabbing = isGrabbingRight || isGrabbingLeft;

        // Process right hand
        if ((handSelection == HandSelection.Right || handSelection == HandSelection.Both) && rightHand != null)
        {
            ProcessHand(rightHand, ref isGrabbingRight, ref lastHandPositionRight);
        }
        else
        {
            // Reset if hand is disabled
            if (isGrabbingRight)
            {
                isGrabbingRight = false;
            }
        }

        // Process left hand
        if ((handSelection == HandSelection.Left || handSelection == HandSelection.Both) && leftHand != null)
        {
            Debug.Log($"[HandRotation] Left hand exists, IsTrackedDataValid: {leftHand.IsTrackedDataValid}, handSelection: {handSelection}");
            if (leftHand.IsTrackedDataValid)
            {
                float pinchStrength = leftHand.GetFingerPinchStrength(HandFinger.Index);
                Debug.Log($"[HandRotation] Left hand pinch strength: {pinchStrength:F2}, threshold: {pinchThreshold}, currently grabbing: {isGrabbingLeft}");
            }
            ProcessHand(leftHand, ref isGrabbingLeft, ref lastHandPositionLeft);
        }
        else
        {
            Debug.Log($"[HandRotation] Left hand skipped - handSelection: {handSelection}, leftHand null: {leftHand == null}");
            // Reset if hand is disabled
            if (isGrabbingLeft)
            {
                isGrabbingLeft = false;
            }
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
                Debug.Log($"[HandRotation] Restarting auto-rotation after {timeSinceLastInteraction:F1}s of inactivity");
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

    void ProcessHand(Hand hand, ref bool isGrabbing, ref Vector3 lastHandPosition)
    {
        if (hand == null || !hand.IsTrackedDataValid)
        {
            if (isGrabbing)
            {
                StopGrab(ref isGrabbing, hand?.Handedness.ToString() ?? "Unknown");
            }
            return;
        }

        // Get pinch strength from Building Blocks Hand
        float pinchStrength = hand.GetFingerPinchStrength(HandFinger.Index);
        bool shouldGrab = pinchStrength > pinchThreshold;

        // Start grabbing
        if (shouldGrab && !isGrabbing)
        {
            Debug.Log($"[HandRotation] {hand.Handedness} hand pinch detected: {pinchStrength:F2}");
            StartGrab(hand, ref isGrabbing, ref lastHandPosition);
        }
        // Stop grabbing
        else if (!shouldGrab && isGrabbing)
        {
            StopGrab(ref isGrabbing, hand.Handedness.ToString());
        }
        // Continue grabbing
        else if (shouldGrab && isGrabbing)
        {
            UpdateRotation(hand, ref lastHandPosition);
            timeSinceLastInteraction = 0f;
        }
    }

    void StartGrab(Hand hand, ref bool isGrabbing, ref Vector3 lastHandPosition)
    {
        isGrabbing = true;
        timeSinceLastInteraction = 0f;
        
        // Get initial index finger tip position
        if (hand.GetJointPose(HandJointId.HandIndexTip, out Pose indexTipPose))
        {
            lastHandPosition = indexTipPose.position;
        }
        
        // Stop auto-rotation if active
        if (planeAlignment != null)
        {
            planeAlignment.StopAutoRotation();
        }
        
        Debug.Log($"[HandRotation] Started pinch grab ({hand.Handedness})");
    }

    void StopGrab(ref bool isGrabbing, string handName)
    {
        isGrabbing = false;
        Debug.Log($"[HandRotation] Released pinch grab ({handName})");
    }

    void UpdateRotation(Hand hand, ref Vector3 lastHandPosition)
    {
        if (moleculeRenderer == null) return;

        // Get current index finger tip position
        if (!hand.GetJointPose(HandJointId.HandIndexTip, out Pose indexTipPose))
        {
            return;
        }

        Vector3 currentHandPos = indexTipPose.position;
        
        // Calculate movement delta
        Vector3 delta = currentHandPos - lastHandPosition;
        
        // Apply dead zone to reduce jitter
        if (delta.magnitude < deadZone)
        {
            return;
        }

        // Get camera reference for relative rotation
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // Calculate rotation based on hand movement
        float horizontalDelta = delta.x;
        float verticalDelta = delta.y;

        // Create rotation quaternions
        Quaternion yawRotation = Quaternion.AngleAxis(horizontalDelta * rotationSpeed * 200f, Vector3.up);
        Quaternion pitchRotation = Quaternion.AngleAxis(-verticalDelta * rotationSpeed * 200f, mainCam.transform.right);

        // Combine rotations
        Quaternion deltaRotation = yawRotation * pitchRotation;

        // Store original position AND local position to keep molecule in place
        Vector3 originalPosition = moleculeRenderer.transform.position;
        Vector3 originalLocalPosition = moleculeRenderer.transform.localPosition;

        // Only rotate, don't move - rotate around the molecule's own center
        moleculeRenderer.transform.rotation = deltaRotation * moleculeRenderer.transform.rotation;
        
        // Force position to stay exactly the same
        moleculeRenderer.transform.position = originalPosition;
        moleculeRenderer.transform.localPosition = originalLocalPosition;

        // Update last position
        lastHandPosition = currentHandPos;

        // Trigger bond re-rendering with new stereochemistry
        moleculeRenderer.RerenderBondsOnly();
    }

    void OnDrawGizmos()
    {
        // Visualize index finger positions when grabbing
        if (Application.isPlaying)
        {
            if (isGrabbingRight && rightHand != null && rightHand.GetJointPose(HandJointId.HandIndexTip, out Pose rightTip))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(rightTip.position, 0.01f);
            }
            
            if (isGrabbingLeft && leftHand != null && leftHand.GetJointPose(HandJointId.HandIndexTip, out Pose leftTip))
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(leftTip.position, 0.01f);
            }
        }
    }
}
