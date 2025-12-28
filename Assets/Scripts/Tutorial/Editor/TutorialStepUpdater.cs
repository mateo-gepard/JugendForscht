using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor utility to update existing tutorial step events
/// </summary>
public class TutorialStepUpdater : Editor
{
    [MenuItem("Tutorial/Update Step 3 Events (Force)")]
    public static void UpdateStep3Events()
    {
        string path = "Assets/Tutorial/Steps/Step03_Molekuelgeometrie.asset";
        TutorialStep step = AssetDatabase.LoadAssetAtPath<TutorialStep>(path);
        
        if (step == null)
        {
            EditorUtility.DisplayDialog("Fehler", "Step03 nicht gefunden!\n\nErstelle ihn zuerst mit 'Tutorial → Schritt 3 erstellen'", "OK");
            return;
        }
        
        // Speichere das Video falls vorhanden
        var savedVideoClip = step.videoClip;
        
        // Aktualisiere Events
        step.events = new List<TutorialEvent>
        {
            // 0:21 - Linearer Bau (CO2)
            new TutorialEvent {
                triggerTime = 21f,
                eventType = TutorialEventType.Show,
                targetObjectName = "CO2Linear",
                position = new Vector3(0.35f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.3f, 0.3f, 0.3f)
            },
            
            // 0:23 - Linear verschwindet, Gewinkelter Bau erscheint
            new TutorialEvent {
                triggerTime = 23f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "CO2Linear"
            },
            new TutorialEvent {
                triggerTime = 23f,
                eventType = TutorialEventType.Show,
                targetObjectName = "H2OGewinkelt",
                position = new Vector3(0.35f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.3f, 0.3f, 0.3f)
            },
            
            // 0:25 - Gewinkelt verschwindet, Trigonal-planar erscheint
            new TutorialEvent {
                triggerTime = 25f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "H2OGewinkelt"
            },
            new TutorialEvent {
                triggerTime = 25f,
                eventType = TutorialEventType.Show,
                targetObjectName = "BF3TrigonalPlanar",
                position = new Vector3(0.35f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.3f, 0.3f, 0.3f)
            },
            
            // 0:27 - Trigonal-planar verschwindet, Trigonal-pyramidal erscheint
            new TutorialEvent {
                triggerTime = 27f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "BF3TrigonalPlanar"
            },
            new TutorialEvent {
                triggerTime = 27f,
                eventType = TutorialEventType.Show,
                targetObjectName = "NH3TrigonalPyramidal",
                position = new Vector3(0.35f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.3f, 0.3f, 0.3f)
            },
            
            // 0:29 - Trigonal-pyramidal verschwindet, Tetraedrisch erscheint
            new TutorialEvent {
                triggerTime = 29f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "NH3TrigonalPyramidal"
            },
            new TutorialEvent {
                triggerTime = 29f,
                eventType = TutorialEventType.Show,
                targetObjectName = "CH4Tetraedrisch",
                position = new Vector3(0.35f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.3f, 0.3f, 0.3f)
            }
        };
        
        // Stelle Video wieder her
        step.videoClip = savedVideoClip;
        
        EditorUtility.SetDirty(step);
        AssetDatabase.SaveAssets();
        
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[Tutorial] Step 3 Events aktualisiert: {step.events.Count} Events");
        EditorUtility.DisplayDialog("Step 3 aktualisiert", 
            $"Events aktualisiert!\n\n" +
            $"- {step.events.Count} Events definiert\n" +
            $"- Position: rechts mittig (0.35, 0, 0)\n" +
            $"- Scale: 0.3\n" +
            $"- Video: {(savedVideoClip != null ? savedVideoClip.name : "NICHT ZUGEWIESEN!")}", 
            "OK");
    }
}
