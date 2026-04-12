using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Erlaubt es, das Lorentz-Labor (MagneticFieldVolume) per Hand-Tracking
/// zu greifen und im Raum zu verschieben/drehen.
///
/// Nutzt dieselbe Pinch-Geste wie HandRotationController.
/// Nur eine Hand greift gleichzeitig (First-Come). Physik bleibt korrekt,
/// da alle Berechnungen in lokalen Koordinaten des Volumens stattfinden.
/// </summary>
public class FieldVolumeGrab : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════
    //  Einstellungen
    // ════════════════════════════════════════════════════════════

    [Header("Hand Tracking")]
    [Tooltip("Wird automatisch gesucht")]
    public Hand rightHand;

    [Tooltip("Wird automatisch gesucht")]
    public Hand leftHand;

    [Tooltip("Pinch-Schwelle (0-1)")]
    [Range(0.5f, 1f)]
    public float pinchThreshold = 0.7f;

    [Header("Greifdistanz")]
    [Tooltip("Maximale Entfernung zum Volumen-Zentrum, um es zu greifen (Meter)")]
    public float grabDistance = 0.8f;

    // ════════════════════════════════════════════════════════════
    //  Interne Felder
    // ════════════════════════════════════════════════════════════

    private bool isGrabbing;
    private bool grabbingWithRight;
    private Vector3 grabOffset;
    private Quaternion grabRotOffset;
    private Vector3 lastHandPos;
    private Quaternion lastHandRot;

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
            if (pinch < pinchThreshold * 0.8f) // Hysterese
            {
                isGrabbing = false;
                return;
            }

            // Hand-Position & Rotation holen
            if (!activeHand.GetRootPose(out Pose handPose)) return;

            // Objekt folgt der Hand mit Offset
            transform.position = handPose.position + handPose.rotation * grabOffset;
            transform.rotation = handPose.rotation * grabRotOffset;
        }
        else
        {
            // Prüfe ob eine Hand greift
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

        // Don't grab field volume while a VR panel is being moved
        if (VRPanelGrab.IsAnyPanelBeingGrabbed) return;

        float pinch = hand.GetFingerPinchStrength(HandFinger.Index);
        if (pinch < pinchThreshold) return;

        if (!hand.GetRootPose(out Pose handPose)) return;

        // Distanz-Check
        float dist = Vector3.Distance(handPose.position, transform.position);
        if (dist > grabDistance) return;

        // Grab starten
        isGrabbing = true;
        grabbingWithRight = isRight;

        // Offset berechnen, damit das Objekt nicht springt
        Quaternion invHandRot = Quaternion.Inverse(handPose.rotation);
        grabOffset = invHandRot * (transform.position - handPose.position);
        grabRotOffset = invHandRot * transform.rotation;
    }
}
