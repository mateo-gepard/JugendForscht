using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject containing the full tutorial timeline.
/// One video, multiple units with timed cues.
/// Created via Tutorial > Build Tutorial System in the editor.
/// </summary>
[CreateAssetMenu(fileName = "TutorialTimeline", menuName = "Tutorial/Timeline")]
public class TutorialTimeline : ScriptableObject
{
    [Tooltip("All tutorial units in chronological order")]
    public List<TutorialUnit> units = new List<TutorialUnit>();
}
