using UnityEngine;

/// <summary>
/// Types of events that can occur during a tutorial step
/// </summary>
public enum TutorialEventType
{
    Show,           // Show an object
    Hide,           // Hide an object
    Move,           // Move an object to position
    Rotate,         // Rotate an object
    Scale,          // Scale an object
    ShowText,       // Show text panel
    HideText,       // Hide text panel
    PlayAnimation,  // Play an animation
    LoadMolecule    // Load a specific molecule
}

/// <summary>
/// A single timed event in a tutorial step
/// </summary>
[System.Serializable]
public class TutorialEvent
{
    [Tooltip("Time in seconds from start of video")]
    public float triggerTime;
    
    [Tooltip("Type of event")]
    public TutorialEventType eventType;
    
    [Tooltip("Target object name (must exist in scene or prefab pool)")]
    public string targetObjectName;
    
    [Tooltip("Position for Show/Move events")]
    public Vector3 position;
    
    [Tooltip("Rotation for Show/Rotate events")]
    public Vector3 rotation;
    
    [Tooltip("Scale for Show/Scale events")]
    public Vector3 scale = Vector3.one;
    
    [Tooltip("Duration for animated events (Move/Rotate/Scale)")]
    public float duration = 1f;
    
    [Tooltip("Text content for ShowText event")]
    [TextArea(2, 5)]
    public string textContent;
    
    [Tooltip("Molecule name for LoadMolecule event")]
    public string moleculeName;
    
    [Tooltip("Has this event been triggered?")]
    [HideInInspector]
    public bool hasTriggered;
}
