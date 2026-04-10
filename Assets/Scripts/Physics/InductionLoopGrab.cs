using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Erlaubt es, die gesamte Induktions-Aufstellung
/// per Hand-Tracking zu greifen und im Raum zu verschieben/drehen.
/// Gleiche Pinch-Geste wie FieldVolumeGrab / SwingFieldGrab.
/// </summary>
public class InductionLoopGrab : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Hand Tracking")]
    public Hand rightHand;
    public Hand leftHand;

    [Range(0.5f, 1f)]
    public float pinchThreshold = 0.7f;

    [Header("Greifdistanz")]
    public float grabDistance = 0.8f;

    // ════════════════════════════════════════════════════════════
    //  Intern
    // ════════════════════════════════════════════════════════════

    private bool isGrabbing;
    private bool grabbingWithRight;
    private Vector3 grabOffset;
    private Quaternion grabRotOffset;

    // ════════════════════════════════════════════════════════════
    //  Unity Lifecycle
    // ════════════════════════════════════════════════════════════

    void Start()
    {
        if (rightHand == null || leftHand == null)
        {
            Hand[] hands = FindObjectsOfType<Hand>();
            foreach (var hand in hands)
            {
                if (hand.Handedness == Handedness.Right && rightHand == null)
                    rightHand = hand;
                else if (hand.Handedness == Handedness.Left && leftHand == null)
                    leftHand = hand;
            }
        }
    }

    void Update()
    {
        if (isGrabbing)
        {
            Hand activeHand = grabbingWithRight ? rightHand : leftHand;
            if (activeHand == null || !activeHand.IsTrackedDataValid)
            {
                isGrabbing = false;
                return;
            }

            float pinch = activeHand.GetFingerPinchStrength(HandFinger.Index);
            if (pinch < pinchThreshold * 0.8f)
            {
                isGrabbing = false;
                return;
            }

            if (!activeHand.GetRootPose(out Pose handPose)) return;

            transform.position = handPose.position + handPose.rotation * grabOffset;
            transform.rotation = handPose.rotation * grabRotOffset;
        }
        else
        {
            TryStartGrab(rightHand, true);
            if (!isGrabbing) TryStartGrab(leftHand, false);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Grab-Logik
    // ════════════════════════════════════════════════════════════

    private void TryStartGrab(Hand hand, bool isRight)
    {
        if (hand == null || !hand.IsTrackedDataValid) return;

        float pinch = hand.GetFingerPinchStrength(HandFinger.Index);
        if (pinch < pinchThreshold) return;

        if (!hand.GetRootPose(out Pose handPose)) return;

        float dist = Vector3.Distance(handPose.position, transform.position);
        if (dist > grabDistance) return;

        isGrabbing = true;
        grabbingWithRight = isRight;

        Quaternion invHandRot = Quaternion.Inverse(handPose.rotation);
        grabOffset    = invHandRot * (transform.position - handPose.position);
        grabRotOffset = invHandRot * transform.rotation;
    }
}
