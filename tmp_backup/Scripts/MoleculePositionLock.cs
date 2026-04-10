using UnityEngine;

/// <summary>
/// Locks the molecule and plane positions after loading.
/// Prevents any physics, XR, or other systems from moving them.
/// </summary>
public class MoleculePositionLock : MonoBehaviour
{
    [Header("References")]
    public MoleculeRenderer moleculeRenderer;
    public MoleculePlaneAlignment planeAlignment;
    
    [Header("Settings")]
    [Tooltip("Lock position after this delay (seconds)")]
    public float lockDelay = 0.5f;
    
    [Tooltip("Allow rotation but lock position")]
    public bool allowRotation = true;
    
    [Header("Status")]
    public bool isLocked = false;
    
    private Vector3 lockedPosition;
    private Quaternion lockedRotation;
    private float timer = 0f;
    
    void Start()
    {
        if (moleculeRenderer == null)
        {
            moleculeRenderer = FindObjectOfType<MoleculeRenderer>();
        }
        
        if (planeAlignment == null)
        {
            planeAlignment = FindObjectOfType<MoleculePlaneAlignment>();
        }
    }
    
    void Update()
    {
        if (moleculeRenderer == null) return;
        
        // Wait for lock delay
        if (!isLocked)
        {
            timer += Time.deltaTime;
            
            if (timer >= lockDelay)
            {
                LockPosition();
            }
        }
    }
    
    void LateUpdate()
    {
        if (!isLocked || moleculeRenderer == null) return;
        
        // Enforce locked position
        if (moleculeRenderer.transform.position != lockedPosition)
        {
            Debug.LogWarning($"[PositionLock] Position was changed from {lockedPosition} to {moleculeRenderer.transform.position} - restoring!");
            moleculeRenderer.transform.position = lockedPosition;
        }
        
        // Optionally lock rotation too
        if (!allowRotation && moleculeRenderer.transform.rotation != lockedRotation)
        {
            moleculeRenderer.transform.rotation = lockedRotation;
        }
    }
    
    public void LockPosition()
    {
        if (moleculeRenderer == null) return;
        
        lockedPosition = moleculeRenderer.transform.position;
        lockedRotation = moleculeRenderer.transform.rotation;
        isLocked = true;
        
        Debug.Log($"[PositionLock] Locked at position: {lockedPosition}");
        
        // Disable physics if present
        Rigidbody rb = moleculeRenderer.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            Debug.Log("[PositionLock] Disabled Rigidbody physics");
        }
    }
    
    public void UnlockPosition()
    {
        isLocked = false;
        timer = 0f;
        Debug.Log("[PositionLock] Position unlocked");
    }
    
    /// <summary>
    /// Call this when a new molecule is loaded to reset the lock
    /// </summary>
    public void OnMoleculeLoaded()
    {
        isLocked = false;
        timer = 0f;
    }
}
