using UnityEngine;
using Oculus.Interaction.Input;

/// <summary>
/// Erzeugt einen kleinen Trigger-Collider an der Zeigefingerspitze.
/// Wird für QuizButton-Interaktion per Hand Tracking benötigt,
/// da Oculus Hand Tracking keine automatischen Physik-Collider auf Fingern erzeugt.
/// Bewegt per Rigidbody.MovePosition() in FixedUpdate(), damit OnTriggerEnter korrekt feuert.
/// </summary>
public class FingerTipPoker : MonoBehaviour
{
    [Header("Hand Reference")]
    public Hand hand;

    [Header("Settings")]
    public float colliderRadius = 0.015f;

    private GameObject tipColliderObj;
    private SphereCollider tipCollider;
    private Rigidbody tipRb;
    private Pose latestTipPose;
    private bool hasPose = false;

    void Start()
    {
        // Erstelle ein kleines Objekt mit SphereCollider als Trigger
        tipColliderObj = new GameObject("FingerTip_Poker");
        // Nicht parenten — sonst beeinflusst Eltern-Skalierung den Collider
        // Position wird jeden Frame manuell gesetzt

        tipCollider = tipColliderObj.AddComponent<SphereCollider>();
        tipCollider.radius = colliderRadius;
        tipCollider.isTrigger = true;

        // Kinematischer Rigidbody nötig damit OnTriggerEnter ausgelöst wird
        tipRb = tipColliderObj.AddComponent<Rigidbody>();
        tipRb.isKinematic = true;
        tipRb.useGravity = false;
        tipRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Debug.Log($"[FingerTipPoker] Erstellt für Hand: {(hand != null ? hand.name : "null")}");
    }

    void Update()
    {
        if (hand == null || tipColliderObj == null) return;

        // Pose in Update() lesen (Hand-Tracking-Daten kommen pro Frame)
        if (hand.GetJointPose(HandJointId.HandIndexTip, out Pose tipPose))
        {
            latestTipPose = tipPose;
            hasPose = true;

            if (!tipColliderObj.activeSelf)
            {
                tipColliderObj.SetActive(true);
                // Debug.Log("[FingerTipPoker] Hand erkannt, Collider aktiviert");
            }
        }
        else
        {
            if (tipColliderObj.activeSelf)
            {
                tipColliderObj.SetActive(false);
                hasPose = false;
            }
        }
    }

    void FixedUpdate()
    {
        // Position per MovePosition setzen — nur so feuert die Physik-Engine Trigger-Events
        if (hasPose && tipRb != null && tipColliderObj.activeSelf)
        {
            tipRb.MovePosition(latestTipPose.position);
            tipRb.MoveRotation(latestTipPose.rotation);
        }
    }

    void OnDestroy()
    {
        if (tipColliderObj != null)
            Destroy(tipColliderObj);
    }
}
