using UnityEngine;

/// <summary>
/// Editor-Hilfsskript: Erzeugt das komplette Lorentz-Labor Setup,
/// wenn es zur Szene hinzugefügt wird.
///
/// Nutzung:
///   1. Leeres GameObject erstellen
///   2. Diese Komponente draufziehen
///   3. Play drücken → alles wird automatisch erzeugt
///   4. Rechtsklick → "Lorentz-Labor aufbauen" für manuellen Aufbau
///
/// Kann auch komplett per Code erzeugt werden (z.B. vom WebSocketServer).
/// </summary>
public class LorentzLabSetup : MonoBehaviour
{
    [Header("Soll das Lab beim Start automatisch erzeugt werden?")]
    public bool autoSetup = true;

    [Header("Position des Labors in der Szene")]
    public Vector3 labPosition = new Vector3(0f, 1.3f, 0.6f);

    [Header("Physik-Parameter")]
    public float fieldStrength = 1f;
    public Vector3 fieldDirection = Vector3.forward;
    public Vector3 volumeSize = new Vector3(0.5f, 0.5f, 0.5f);
    public Vector3 startVelocity = new Vector3(0.15f, 0f, 0f);
    public int chargeSign = 1;

    [Header("Referenzen (nach Setup befüllt)")]
    public LorentzLabManager labManager;

    void Start()
    {
        if (autoSetup && labManager == null)
            Setup();
    }

    [ContextMenu("Lorentz-Labor aufbauen")]
    public void Setup()
    {
        // Prüfe ob schon ein Lab existiert
        if (LorentzLabManager.Instance != null)
        {
            labManager = LorentzLabManager.Instance;
            return;
        }

        // Position relativ zur Kamera berechnen (wie alle anderen VR-Objekte)
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            labPosition = cam.transform.position + forward * 0.6f;
            labPosition.y = cam.transform.position.y - 0.15f;
        }

        // Root-Objekt
        GameObject root = new GameObject("═══ LORENTZ-LABOR ═══");
        root.transform.position = labPosition;

        // Manager
        labManager = root.AddComponent<LorentzLabManager>();
        labManager.defaultFieldStrength = fieldStrength;
        labManager.defaultFieldDirection = fieldDirection;
        labManager.defaultVolumeSize = volumeSize;
        labManager.defaultStartVelocity = startVelocity;
        labManager.defaultChargeSign = chargeSign;

        // XR Grab auf das B-Feld-Volumen
        // (wird nach Awake des Managers hinzugefügt, da das Volumen dort erzeugt wird)
        StartCoroutine(AddGrabAfterFrame(root));
    }

    private System.Collections.IEnumerator AddGrabAfterFrame(GameObject root)
    {
        yield return null; // 1 Frame warten bis Awake() durch ist

        if (labManager != null && labManager.fieldVolume != null)
        {
            var grab = labManager.fieldVolume.gameObject.AddComponent<FieldVolumeGrab>();
            // Box-Collider für Raycasts (optional, falls später benötigt)
            var col = labManager.fieldVolume.gameObject.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = labManager.fieldVolume.gameObject.AddComponent<BoxCollider>();
                col.size = labManager.fieldVolume.volumeSize;
                col.isTrigger = true;
            }
        }
    }

    [ContextMenu("Lorentz-Labor entfernen")]
    public void Teardown()
    {
        if (labManager != null)
        {
            if (Application.isPlaying) Destroy(labManager.gameObject);
            else DestroyImmediate(labManager.gameObject);
            labManager = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Zeige wo das Lab erscheinen wird
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
        Gizmos.DrawWireCube(labPosition, volumeSize);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(labPosition + new Vector3(-volumeSize.x * 0.8f, 0f, 0f), 0.025f);
    }
}
