using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Actions a tutorial cue can perform
/// </summary>
public enum CueAction
{
    Show,       // Animate object in (scale-up with overshoot)
    Hide,       // Animate object out (shrink + anticipation)
    Replace,    // Instantly hide ALL active objects, then animate in new one
    HideAll,    // Animate out all active objects
    Highlight   // Pulse scale on an already-visible object
}

/// <summary>
/// A single timed event in the tutorial video.
/// Fires when videoTime >= time.
/// </summary>
[System.Serializable]
public class TutorialCue
{
    [Tooltip("Absolute time in video (seconds)")]
    public float time;

    [Tooltip("What to do")]
    public CueAction action;

    [Tooltip("Pool object name (must match prefab or pool child name)")]
    public string objectName;

    [Tooltip("Position relative to tutorial anchor point")]
    public Vector3 position = new Vector3(0.35f, 0f, 0f);

    [Tooltip("Euler rotation relative to tutorial anchor")]
    public Vector3 rotation = Vector3.zero;

    [Tooltip("Target scale of the object")]
    public Vector3 scale = Vector3.one * 0.25f;

    [HideInInspector]
    public bool triggered;
}

/// <summary>
/// A unit (Einheit) in the tutorial. The video pauses at the start of
/// the NEXT unit, giving the student time to study before pressing Continue.
/// </summary>
[System.Serializable]
public class TutorialUnit
{
    [Tooltip("Display name (shown on iPad)")]
    public string name;

    [Tooltip("Video timestamp where this unit begins (seconds)")]
    public float startTime;

    [Tooltip("Timed cues during this unit's video segment")]
    public List<TutorialCue> cues = new List<TutorialCue>();
}
