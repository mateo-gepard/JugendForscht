using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;

/// <summary>
/// Macht jedes VR-Panel greifbar und schließbar.
/// 
/// Greifen: Pinch mit einer Hand in der Nähe des Panels → Panel folgt der Hand
/// Schließen: Pinch mit BEIDEN Händen gleichzeitig in der Nähe → Panel wird geschlossen
/// 
/// Wird automatisch zu Panels hinzugefügt, die es brauchen.
/// Nutzt dieselbe Hand-Tracking-API wie FieldVolumeGrab.
/// </summary>
public class VRPanelGrab : MonoBehaviour
{
    // ── Static tracking: lets other scripts know if ANY panel is being grabbed ──
    private static HashSet<VRPanelGrab> activeGrabs = new HashSet<VRPanelGrab>();

    /// <summary>
    /// True if any VRPanelGrab instance is currently being grabbed.
    /// Used by HandRotationController, FieldVolumeGrab, etc. to suppress
    /// their interactions while a panel is being moved.
    /// </summary>
    public static bool IsAnyPanelBeingGrabbed => activeGrabs.Count > 0;

    [Header("Hand Tracking")]
    public Hand rightHand;
    public Hand leftHand;

    [Header("Grab Settings")]
    [Tooltip("Maximale Greif-Entfernung (Meter)")]
    public float grabDistance = 0.5f;

    [Tooltip("Pinch-Schwelle (0-1)")]
    [Range(0.5f, 1f)]
    public float pinchThreshold = 0.7f;

    [Header("Close Settings")]
    [Tooltip("Doppel-Pinch mit beiden Händen schließt das Panel")]
    public bool enableClose = true;

    [Tooltip("Minimale Zeit beider Hände pinchend für Close (Sekunden)")]
    public float closeHoldTime = 0.8f;

    /// <summary>
    /// Callback wenn das Panel geschlossen werden soll.
    /// Wer VRPanelGrab hinzufügt, registriert hier seine Close-Logik.
    /// </summary>
    public System.Action OnCloseRequested;

    // Grab state
    private bool isGrabbing;
    private bool grabbingWithRight;
    private Vector3 grabOffset;
    private Quaternion grabRotOffset;

    // Close state
    private float bothPinchTimer = 0f;
    private bool closeTriggered = false;

    // Visual feedback
    private GameObject closeIndicator;
    private TextMesh closeText;

    void Start()
    {
        FindHands();
    }

    void Update()
    {
        if (rightHand == null && leftHand == null)
        {
            FindHands();
            if (rightHand == null && leftHand == null) return;
        }

        // ── Close detection: both hands pinching near panel ──
        if (enableClose && !isGrabbing)
        {
            CheckBothHandClose();
        }

        // ── Grab logic ──
        if (isGrabbing)
        {
            Hand activeHand = grabbingWithRight ? rightHand : leftHand;
            if (activeHand == null || !activeHand.IsTrackedDataValid)
            {
                SetGrabbing(false);
                return;
            }

            float pinch = activeHand.GetFingerPinchStrength(HandFinger.Index);
            if (pinch < pinchThreshold * 0.8f)
            {
                SetGrabbing(false);
                return;
            }

            if (!activeHand.GetRootPose(out Pose handPose)) return;

            // Panel folgt der Hand
            transform.position = handPose.position + handPose.rotation * grabOffset;
            transform.rotation = handPose.rotation * grabRotOffset;
        }
        else
        {
            TryStartGrab(rightHand, true);
            if (!isGrabbing) TryStartGrab(leftHand, false);
        }
    }

    private void TryStartGrab(Hand hand, bool isRight)
    {
        if (hand == null || !hand.IsTrackedDataValid) return;

        float pinch = hand.GetFingerPinchStrength(HandFinger.Index);
        if (pinch < pinchThreshold) return;

        if (!hand.GetRootPose(out Pose handPose)) return;

        float dist = Vector3.Distance(handPose.position, transform.position);
        if (dist > grabDistance) return;

        // Don't grab if both hands are pinching (close gesture)
        Hand otherHand = isRight ? leftHand : rightHand;
        if (otherHand != null && otherHand.IsTrackedDataValid)
        {
            float otherPinch = otherHand.GetFingerPinchStrength(HandFinger.Index);
            if (otherPinch > pinchThreshold) return; // Both pinching → close, not grab
        }

        grabbingWithRight = isRight;
        SetGrabbing(true);

        Quaternion invHandRot = Quaternion.Inverse(handPose.rotation);
        grabOffset = invHandRot * (transform.position - handPose.position);
        grabRotOffset = invHandRot * transform.rotation;
    }

    private void CheckBothHandClose()
    {
        if (rightHand == null || leftHand == null) return;
        if (!rightHand.IsTrackedDataValid || !leftHand.IsTrackedDataValid) return;

        float rightPinch = rightHand.GetFingerPinchStrength(HandFinger.Index);
        float leftPinch = leftHand.GetFingerPinchStrength(HandFinger.Index);

        bool bothPinching = rightPinch > pinchThreshold && leftPinch > pinchThreshold;

        // Check if at least one hand is near the panel
        bool nearPanel = false;
        if (rightHand.GetRootPose(out Pose rightPose))
            nearPanel |= Vector3.Distance(rightPose.position, transform.position) < grabDistance;
        if (leftHand.GetRootPose(out Pose leftPose))
            nearPanel |= Vector3.Distance(leftPose.position, transform.position) < grabDistance;

        if (bothPinching && nearPanel)
        {
            bothPinchTimer += Time.deltaTime;
            UpdateCloseIndicator(bothPinchTimer / closeHoldTime);

            if (bothPinchTimer >= closeHoldTime && !closeTriggered)
            {
                closeTriggered = true;
                Debug.Log($"[VRPanelGrab] Close triggered for {gameObject.name}");
                HideCloseIndicator();
                OnCloseRequested?.Invoke();
            }
        }
        else
        {
            if (bothPinchTimer > 0f)
            {
                bothPinchTimer = 0f;
                closeTriggered = false;
                HideCloseIndicator();
            }
        }
    }

    private void UpdateCloseIndicator(float progress)
    {
        if (closeIndicator == null)
        {
            closeIndicator = new GameObject("CloseIndicator");
            closeIndicator.transform.SetParent(transform, false);
            closeIndicator.transform.localPosition = Vector3.up * 0.12f;

            closeText = closeIndicator.AddComponent<TextMesh>();
            closeText.anchor = TextAnchor.MiddleCenter;
            closeText.alignment = TextAlignment.Center;
            closeText.characterSize = 0.008f;
            closeText.fontSize = 48;
            closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeText.color = Color.white;

            var mr = closeIndicator.GetComponent<MeshRenderer>();
            if (mr != null && closeText.font != null)
            {
                var mat = new Material(Shader.Find("GUI/Text Shader"));
                mat.mainTexture = closeText.font.material.mainTexture;
                mat.color = Color.white;
                mr.material = mat;
            }

            closeIndicator.AddComponent<BillboardLabel>();
        }

        closeIndicator.SetActive(true);
        progress = Mathf.Clamp01(progress);

        // Show a closing progress bar: [████░░░░] Schließen
        int filled = Mathf.RoundToInt(progress * 8);
        string bar = new string('█', filled) + new string('░', 8 - filled);
        closeText.text = $"[{bar}] Schließen";
        closeText.color = Color.Lerp(Color.white, Color.red, progress);
    }

    private void HideCloseIndicator()
    {
        if (closeIndicator != null)
            closeIndicator.SetActive(false);
    }

    private void FindHands()
    {
        if (rightHand != null && leftHand != null) return;

        // Try HandRotationController first
        var hrc = FindObjectOfType<HandRotationController>();
        if (hrc != null)
        {
            if (rightHand == null) rightHand = hrc.rightHand;
            if (leftHand == null) leftHand = hrc.leftHand;
        }

        // Fallback: search directly
        if (rightHand == null || leftHand == null)
        {
            foreach (var h in FindObjectsOfType<Hand>())
            {
                if (h.Handedness == Handedness.Right && rightHand == null) rightHand = h;
                else if (h.Handedness == Handedness.Left && leftHand == null) leftHand = h;
            }
        }
    }

    /// <summary>
    /// Centralised grab-state setter that also maintains the static tracking set.
    /// </summary>
    private void SetGrabbing(bool value)
    {
        isGrabbing = value;
        if (value)
            activeGrabs.Add(this);
        else
            activeGrabs.Remove(this);
    }

    void OnDestroy()
    {
        activeGrabs.Remove(this);
        if (closeIndicator != null)
            Destroy(closeIndicator);
    }
}
