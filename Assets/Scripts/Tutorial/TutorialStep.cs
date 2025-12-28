using UnityEngine;
using UnityEngine.Video;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject defining a single tutorial step/snippet
/// </summary>
[CreateAssetMenu(fileName = "TutorialStep", menuName = "Tutorial/Tutorial Step")]
public class TutorialStep : ScriptableObject
{
    [Header("Step Info")]
    [Tooltip("Name/ID of this step")]
    public string stepName;
    
    [Tooltip("Description for editor reference")]
    [TextArea(2, 4)]
    public string description;
    
    [Header("Video")]
    [Tooltip("Video clip with alpha channel (WebM VP9 or ProRes 4444)")]
    public VideoClip videoClip;
    
    [Tooltip("Position of video panel in world space")]
    public Vector3 videoPosition = new Vector3(-0.3f, 1.5f, 1f);
    
    [Tooltip("Scale of video panel")]
    public float videoScale = 0.5f;
    
    [Header("Timed Events")]
    [Tooltip("Events triggered at specific times during video playback")]
    public List<TutorialEvent> events = new List<TutorialEvent>();
    
    [Header("Flow Control")]
    [Tooltip("Auto-advance to next step when video ends (otherwise wait for Continue)")]
    public bool autoAdvance = false;
    
    [Tooltip("Delay after video before showing Continue button")]
    public float continueDelay = 0.5f;
    
    /// <summary>
    /// Reset all event triggers (call when starting this step)
    /// </summary>
    public void ResetEvents()
    {
        foreach (var evt in events)
        {
            evt.hasTriggered = false;
        }
    }
}
