using UnityEngine;
using UnityEditor;

/// <summary>
/// [DEPRECATED] Old step-based tutorial setup.
/// Use "Tutorial > Build Tutorial System" (TutorialBuilder) instead.
/// </summary>
public class TutorialSetup : MonoBehaviour
{
    [MenuItem("Tutorial/Setup Tutorial System (OLD - Deprecated)")]
    public static void SetupTutorialSystem()
    {
        EditorUtility.DisplayDialog(
            "Deprecated",
            "This setup is deprecated.\n\nUse 'Tutorial > Build Tutorial System' instead.",
            "OK");
    }
}
