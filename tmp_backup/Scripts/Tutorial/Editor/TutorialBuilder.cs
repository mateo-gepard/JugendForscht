using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor tool to generate the TutorialTimeline asset with all units and cues.
/// Menu: Tutorial > Build Tutorial System
/// 
/// This hardcodes the timeline from the merged video.
/// After running, assign the generated TutorialTimeline asset to TutorialManager in the scene.
/// </summary>
public class TutorialBuilder : Editor
{
    [MenuItem("Tutorial/Build Tutorial System")]
    public static void BuildTutorialSystem()
    {
        // 1. Create the TutorialTimeline asset
        TutorialTimeline timeline = CreateTimeline();

        // 2. Auto-assign to scene TutorialManager if possible
        AutoAssignTimeline(timeline);

        Debug.Log("[TutorialBuilder] Tutorial system built successfully!");
        EditorUtility.DisplayDialog(
            "Tutorial Builder",
            $"TutorialTimeline created with {timeline.units.Count} units.\n\n" +
            "Next steps:\n" +
            "1. Assign your merged video to the VideoPlayer\n" +
            "2. Assign the TutorialTimeline to TutorialManager\n" +
            "3. Make sure your prefabs exist in Resources/Prefabs/",
            "OK");
    }

    [MenuItem("Tutorial/Validate Prefabs")]
    public static void ValidatePrefabs()
    {
        string[] required = new string[]
        {
            "CH4Tetraedrisch", "NormalBond", "DashedBond", "WedgeBond",
            "Methan3D", "MethanKeilstrich",
            "CO2Linear", "H2OGewinkelt", "BF3TrigonalPlanar",
            "NH3TrigonalPyramidal", "CO2LinearHighlight",
            "H2OGewinkeltEPHighlight", "NH3TrigonalPyramidalEPHighlight"
        };

        List<string> missing = new List<string>();
        foreach (string name in required)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{name}");
            if (prefab == null) missing.Add(name);
        }

        if (missing.Count == 0)
        {
            Debug.Log("[TutorialBuilder] All prefabs found!");
            EditorUtility.DisplayDialog("Prefab Validation", "All required prefabs found in Resources/Prefabs/.", "OK");
        }
        else
        {
            string list = string.Join("\n  - ", missing);
            Debug.LogWarning($"[TutorialBuilder] Missing prefabs:\n  - {list}");
            EditorUtility.DisplayDialog("Prefab Validation",
                $"Missing {missing.Count} prefabs:\n  - {list}\n\n" +
                "Run TutorialPrefabCreator to generate them, or check Resources/Prefabs/.", "OK");
        }
    }

    // ════════════════════════════════════════════════════════════
    // TIMELINE DEFINITION (hardcoded from merged video)
    // ════════════════════════════════════════════════════════════

    private static TutorialTimeline CreateTimeline()
    {
        TutorialTimeline timeline = ScriptableObject.CreateInstance<TutorialTimeline>();
        timeline.units = new List<TutorialUnit>();

        // ─── Object positions (relative to tutorial origin) ──────
        // Video is shifted RIGHT by videoOffsetX. Objects go clearly LEFT (negative X).
        Vector3 center      = new Vector3(-0.35f, 0f, 0f);
        Vector3 leftSide    = new Vector3(-0.45f, 0f, 0f);
        Vector3 rightSide   = new Vector3(-0.25f, 0f, 0f);
        Vector3 nearVideo   = new Vector3(-0.28f, 0f, 0f);
        Vector3 midRight    = new Vector3(-0.38f, 0f, 0f);
        Vector3 farRight    = new Vector3(-0.48f, 0f, 0f);

        Vector3 scaleSm     = Vector3.one * 0.18f;
        Vector3 scaleMd     = Vector3.one * 0.26f;
        Vector3 scaleLg     = Vector3.one * 0.32f;

        // ─── Einheit 1: Einführung (0:00 – 0:14) ────────────────
        var unit0 = new TutorialUnit { name = "Einführung", startTime = 0f };
        unit0.cues.Add(MakeCue(4f,  CueAction.Show, "CH4Tetraedrisch",  nearVideo, scaleMd));
        unit0.cues.Add(MakeCue(5f,  CueAction.Show, "Arrow3D",          midRight,  scaleSm));
        unit0.cues.Add(MakeCue(6f,  CueAction.Show, "KeilstrichBild",   new Vector3(-0.55f, 0f, -0.1f),  scaleSm));
        timeline.units.Add(unit0);

        // ─── Einheit 2: Bindungsarten (0:14 – 1:21) ─────────────
        var unit1 = new TutorialUnit { name = "Bindungsarten", startTime = 14f };
        unit1.cues.Add(MakeCue(21f, CueAction.Show,    "NormalBond", center, scaleMd));
        unit1.cues.Add(MakeCue(36f, CueAction.Replace, "DashedBond", center, scaleMd));
        unit1.cues.Add(MakeCue(64f, CueAction.Replace, "WedgeBond",  center, scaleMd));
        timeline.units.Add(unit1);

        // ─── Einheit 3: Keilstrichformel (1:21 – 1:45) ──────────
        var unit2 = new TutorialUnit { name = "Keilstrichformel", startTime = 81f };
        unit2.cues.Add(MakeCue(84f, CueAction.Show, "Methan3D",         leftSide,  scaleMd));
        unit2.cues.Add(MakeCue(93f, CueAction.Show, "KeilstrichBild",   new Vector3(-0.25f, 0f, -0.1f), scaleSm));
        timeline.units.Add(unit2);

        // ─── Einheit 4: Elektronenpaarabstoßung (1:45 – 2:10) ───
        var unit3 = new TutorialUnit { name = "Elektronenpaarabstoßung", startTime = 105f };
        // No objects – just video explanation
        timeline.units.Add(unit3);

        // ─── Einheit 5: Molekülgeometrien Überblick (2:10 – 2:24) ─
        var unit4 = new TutorialUnit { name = "Molekülgeometrien", startTime = 130f };
        unit4.cues.Add(MakeCue(136f, CueAction.Show,    "CO2Linear",            center, scaleMd));
        unit4.cues.Add(MakeCue(138f, CueAction.Replace, "H2OGewinkelt",         center, scaleMd));
        unit4.cues.Add(MakeCue(140f, CueAction.Replace, "BF3TrigonalPlanar",    center, scaleMd));
        unit4.cues.Add(MakeCue(142f, CueAction.Replace, "NH3TrigonalPyramidal", center, scaleMd));
        unit4.cues.Add(MakeCue(143f, CueAction.Replace, "CH4Tetraedrisch",      center, scaleMd));
        timeline.units.Add(unit4);

        // ─── Einheit 6: Linearer Bau (2:24 – 3:02) ─────────────
        var unit5 = new TutorialUnit { name = "Linearer Bau", startTime = 144f };
        unit5.cues.Add(MakeCue(171f, CueAction.Show,    "CO2Linear",          center, scaleLg));
        unit5.cues.Add(MakeCue(180f, CueAction.Replace, "CO2LinearHighlight", center, scaleLg));
        timeline.units.Add(unit5);

        // ─── Einheit 7: Gewinkelter Bau (3:02 – 4:01) ──────────
        var unit6 = new TutorialUnit { name = "Gewinkelter Bau", startTime = 182f };
        unit6.cues.Add(MakeCue(188f, CueAction.Show, "H2OGewinkeltEPHighlight", center, scaleLg));
        timeline.units.Add(unit6);

        // ─── Einheit 8: Trigonal Planar (4:01 – 4:44) ───────────
        var unit7 = new TutorialUnit { name = "Trigonal Planar", startTime = 241f };
        unit7.cues.Add(MakeCue(266f, CueAction.Show, "BF3TrigonalPlanar", center, scaleLg));
        timeline.units.Add(unit7);

        // ─── Einheit 9: Trigonal Pyramidal (4:44 – 5:18) ────────
        var unit8 = new TutorialUnit { name = "Trigonal Pyramidal", startTime = 284f };
        unit8.cues.Add(MakeCue(301f, CueAction.Show, "NH3TrigonalPyramidalEPHighlight", center, scaleLg));
        timeline.units.Add(unit8);

        // ─── Einheit 10: Tetraedrisch (5:18 – 5:35) ────────────
        var unit9 = new TutorialUnit { name = "Tetraedrisch", startTime = 318f };
        unit9.cues.Add(MakeCue(323f, CueAction.Show, "CH4Tetraedrisch", center, scaleLg));
        timeline.units.Add(unit9);

        // ─── Einheit 11: Abschluss (5:35 – Ende) ────────────────
        var unit10 = new TutorialUnit { name = "Abschluss", startTime = 335f };
        // No objects – closing video plays to end
        timeline.units.Add(unit10);

        // Save asset
        string dir = "Assets/Tutorial";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Tutorial");

        string path = $"{dir}/TutorialTimeline.asset";
        AssetDatabase.CreateAsset(timeline, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TutorialBuilder] Timeline saved: {path} ({timeline.units.Count} units)");
        Selection.activeObject = timeline;
        return timeline;
    }

    // ════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════

    private static TutorialCue MakeCue(float time, CueAction action, string objectName,
                                        Vector3 position, Vector3 scale)
    {
        return new TutorialCue
        {
            time = time,
            action = action,
            objectName = objectName,
            position = position,
            rotation = Vector3.zero,
            scale = scale,
            triggered = false
        };
    }

    private static void AutoAssignTimeline(TutorialTimeline timeline)
    {
        TutorialManager manager = FindObjectOfType<TutorialManager>();
        if (manager != null)
        {
            Undo.RecordObject(manager, "Assign TutorialTimeline");
            manager.timeline = timeline;
            EditorUtility.SetDirty(manager);
            Debug.Log("[TutorialBuilder] Auto-assigned timeline to TutorialManager in scene");
        }
        else
        {
            Debug.Log("[TutorialBuilder] No TutorialManager in scene – assign timeline manually");
        }
    }
}
