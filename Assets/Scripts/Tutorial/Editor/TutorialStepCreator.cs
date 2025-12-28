using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor utility to create tutorial step assets
/// </summary>
public class TutorialStepCreator : EditorWindow
{
    [MenuItem("Tutorial/Create Step 1 - Bond Introduction")]
    public static void CreateStep1()
    {
        // Create TutorialStep asset
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        
        step.stepName = "Schritt 1 - Bindungsarten";
        step.description = "Einführung in die verschiedenen Bindungstypen: Normal, Gestrichelt, Keil";
        step.videoPosition = new Vector3(0f, 0f, 0f); // Auf gleicher Ebene mit Prefabs
        step.videoScale = 0.5f;
        step.autoAdvance = false;
        step.continueDelay = 0.5f;
        
        // Define events based on user's timeline
        step.events = new List<TutorialEvent>
        {
            // 0:08 - Normal Bond erscheint (oben)
            new TutorialEvent {
                triggerTime = 8f,
                eventType = TutorialEventType.Show,
                targetObjectName = "NormalBond",
                position = new Vector3(0.2f, 0.2f, 0f), // Näher und oben
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            },
            
            // 0:09 - Gestrichelter Bond erscheint (Mitte)
            new TutorialEvent {
                triggerTime = 9f,
                eventType = TutorialEventType.Show,
                targetObjectName = "DashedBond",
                position = new Vector3(0.2f, 0f, 0f), // Näher, Mitte
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            },
            
            // 0:10 - Keil erscheint (unten)
            new TutorialEvent {
                triggerTime = 10f,
                eventType = TutorialEventType.Show,
                targetObjectName = "WedgeBond",
                position = new Vector3(0.2f, -0.2f, 0f), // Näher und unten
                rotation = Vector3.zero,
                scale = new Vector3(0.4f, 0.4f, 0.4f)
            },
            
            // 0:12 - Gestrichelter Bond und Keil verschwinden
            new TutorialEvent {
                triggerTime = 12f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "DashedBond"
            },
            new TutorialEvent {
                triggerTime = 12f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "WedgeBond"
            },
            
            // 0:22 - Normaler Bond verschwindet
            new TutorialEvent {
                triggerTime = 22f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "NormalBond"
            },
            
            // 0:23 - Gestrichelter Bond wieder da (für Ammonia Erklärung)
            new TutorialEvent {
                triggerTime = 23f,
                eventType = TutorialEventType.Show,
                targetObjectName = "DashedBond",
                position = new Vector3(0.2f, 0.15f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            },
            
            // 0:30 - Gestrichelter Bond verschwindet
            new TutorialEvent {
                triggerTime = 30f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "DashedBond"
            },
            
            // 0:31 - Ammoniak mit Dashed erscheint
            new TutorialEvent {
                triggerTime = 31f,
                eventType = TutorialEventType.Show,
                targetObjectName = "AmmoniaDashedHighlight",
                position = new Vector3(0.3f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            },
            
            // 0:35 - Ammoniak Dashed verschwindet
            new TutorialEvent {
                triggerTime = 35f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "AmmoniaDashedHighlight"
            },
            
            // 0:36 - Keil erscheint wieder
            new TutorialEvent {
                triggerTime = 36f,
                eventType = TutorialEventType.Show,
                targetObjectName = "WedgeBond",
                position = new Vector3(0.2f, -0.15f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            },
            
            // 0:46 - Keil verschwindet
            new TutorialEvent {
                triggerTime = 46f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "WedgeBond"
            },
            
            // 0:47 - Ammoniak mit Wedge erscheint
            new TutorialEvent {
                triggerTime = 47f,
                eventType = TutorialEventType.Show,
                targetObjectName = "AmmoniaWedgeHighlight",
                position = new Vector3(0.3f, -0.15f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            },
            
            // 0:51 - Ammoniak Wedge verschwindet (Ende)
            new TutorialEvent {
                triggerTime = 51f,
                eventType = TutorialEventType.Hide,
                targetObjectName = "AmmoniaWedgeHighlight"
            }
        };
        
        // Create folder if needed
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial"))
        {
            AssetDatabase.CreateFolder("Assets", "Tutorial");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial/Steps"))
        {
            AssetDatabase.CreateFolder("Assets/Tutorial", "Steps");
        }
        
        // Save asset
        string path = "Assets/Tutorial/Steps/Step01_BondIntroduction.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Select the created asset
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet1.mp4)\n2. Zum TutorialManager hinzufügen", 
            "OK");
    }
    
    [MenuItem("Tutorial/Schritt 2 erstellen - Methan Keilstrich")]
    public static void CreateStep2()
    {
        // Create TutorialStep asset
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        
        step.stepName = "Schritt 2 - Methan Keilstrichformel";
        step.description = "Methan räumlicher Bau und Keilstrichformel";
        step.videoPosition = new Vector3(0f, 0f, 0f); // Auf gleicher Ebene mit Prefabs
        step.videoScale = 0.5f;
        step.autoAdvance = false;
        step.continueDelay = 0.5f;
        
        // Define events based on user's timeline
        step.events = new List<TutorialEvent>
        {
            // 0:03 - Methan erscheint mit räumlichem Bau (3D Tetraeder)
            new TutorialEvent {
                triggerTime = 3f,
                eventType = TutorialEventType.Show,
                targetObjectName = "Methan3D",
                position = new Vector3(0.2f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            },
            
            // 0:10 - Keilstrichformel erscheint (Methan3D bleibt sichtbar - räumlicher Bau mit Keilstrich-Bindungen)
            new TutorialEvent {
                triggerTime = 10f,
                eventType = TutorialEventType.Show,
                targetObjectName = "MethanKeilstrich",
                position = new Vector3(0.2f, 0f, 0f),
                rotation = Vector3.zero,
                scale = new Vector3(0.25f, 0.25f, 0.25f)
            }
        };
        
        // Create folder if needed
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial"))
        {
            AssetDatabase.CreateFolder("Assets", "Tutorial");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial/Steps"))
        {
            AssetDatabase.CreateFolder("Assets/Tutorial", "Steps");
        }
        
        // Save asset
        string path = "Assets/Tutorial/Steps/Step02_MethanKeilstrich.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Select the created asset
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet2.mp4)\n2. Zum TutorialManager hinzufügen (Tutorial Steps Liste)", 
            "OK");
    }
    
    [MenuItem("Tutorial/Schritt 3 erstellen - Molekülgeometrie")]
    public static void CreateStep3()
    {
        // Create TutorialStep asset
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        
        step.stepName = "Schritt 3 - Molekülgeometrie";
        step.description = "Verschiedene räumliche Strukturen: Linear, Gewinkelt, Trigonal-planar, Trigonal-pyramidal, Tetraedrisch";
        step.videoPosition = new Vector3(0f, 0f, 0f);
        step.videoScale = 0.5f;
        step.autoAdvance = false;
        step.continueDelay = 0.5f;
        
        // Define events based on user's timeline
        // Position: rechts neben dem Video, mittig (x=0.35, y=0)
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
        
        // Create folder if needed
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial"))
        {
            AssetDatabase.CreateFolder("Assets", "Tutorial");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Tutorial/Steps"))
        {
            AssetDatabase.CreateFolder("Assets/Tutorial", "Steps");
        }
        
        // Save asset
        string path = "Assets/Tutorial/Steps/Step03_Molekuelgeometrie.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Select the created asset
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet3.mp4)\n2. Zum TutorialManager hinzufügen (Tutorial Steps Liste)", 
            "OK");
    }
    
    /// <summary>
    /// Step 4: Linearer Bau (Snippet4 - 16 Sekunden)
    /// 0:03 - Linearer Bau erscheint (CO2Linear)
    /// 0:06 - Bindungen werden grün hervorgehoben (CO2LinearHighlight)
    /// </summary>
    [MenuItem("Tutorial/Create Step 4 - Linearer Bau")]
    public static void CreateStep4()
    {
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        step.stepName = "Linearer Bau";
        step.description = "Linearer Molekülbau am Beispiel CO₂";
        step.videoPosition = new Vector3(0f, 0f, 0f); // Auf gleicher Ebene mit Prefabs
        
        // Standard position rechts neben dem Video
        Vector3 defaultPos = new Vector3(0.35f, 0f, 0f);
        Vector3 defaultScale = Vector3.one * 0.3f;
        
        // Events für Step 4
        step.events = new System.Collections.Generic.List<TutorialEvent>
        {
            // 0:03 - CO2 Linear erscheint
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 3f,
                targetObjectName = "CO2Linear",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            },
            // 0:06 - Bindungen werden grün hervorgehoben
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 6f,
                targetObjectName = "CO2LinearHighlight",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            }
        };
        
        // Save asset
        string path = "Assets/Tutorial/Steps/Step04_LinearerBau.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Select the created asset
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet4.mp4)\n2. Zum TutorialManager hinzufügen (Tutorial Steps Liste)", 
            "OK");
    }
    
    /// <summary>
    /// Step 5: Gewinkelter Bau - H2O (Snippet5 - 26 Sekunden)
    /// 0:02 - Gewinkelter Bau erscheint
    /// 0:05 - Bindungen grün hervorgehoben
    /// 0:07 - Nicht-bindendes EP rot hervorgehoben
    /// </summary>
    [MenuItem("Tutorial/Create Step 5 - Gewinkelter Bau")]
    public static void CreateStep5()
    {
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        step.stepName = "Gewinkelter Bau";
        step.description = "Gewinkelter Molekülbau am Beispiel H₂O";
        step.videoPosition = new Vector3(0f, 0f, 0f);
        
        Vector3 defaultPos = new Vector3(0.35f, 0f, 0f);
        Vector3 defaultScale = Vector3.one * 0.3f;
        
        step.events = new System.Collections.Generic.List<TutorialEvent>
        {
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 2f,
                targetObjectName = "H2OGewinkelt",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            },
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 5f,
                targetObjectName = "H2OGewinkeltHighlight",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            },
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 7f,
                targetObjectName = "H2OGewinkeltEPHighlight",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            }
        };
        
        string path = "Assets/Tutorial/Steps/Step05_GewinkelterBau.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet5.mp4)\n2. Zum TutorialManager hinzufügen", 
            "OK");
    }
    
    /// <summary>
    /// Step 6: Trigonal Planar - BF3 (Snippet6 - 31 Sekunden)
    /// 0:03 - Trigonal planar erscheint
    /// 0:11 - Bindungen grün hervorgehoben
    /// </summary>
    [MenuItem("Tutorial/Create Step 6 - Trigonal Planar")]
    public static void CreateStep6()
    {
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        step.stepName = "Trigonal Planar";
        step.description = "Trigonal-planarer Molekülbau am Beispiel BF₃";
        step.videoPosition = new Vector3(0f, 0f, 0f);
        
        Vector3 defaultPos = new Vector3(0.35f, 0f, 0f);
        Vector3 defaultScale = Vector3.one * 0.3f;
        
        step.events = new System.Collections.Generic.List<TutorialEvent>
        {
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 3f,
                targetObjectName = "BF3TrigonalPlanar",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            },
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 11f,
                targetObjectName = "BF3TrigonalPlanarHighlight",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            }
        };
        
        string path = "Assets/Tutorial/Steps/Step06_TrigonalPlanar.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet6.mp4)\n2. Zum TutorialManager hinzufügen", 
            "OK");
    }
    
    /// <summary>
    /// Step 7: Trigonal Pyramidal - NH3 (Snippet7 - 27 Sekunden)
    /// 0:02 - Trigonal pyramidal erscheint
    /// 0:05 - Bindungen grün hervorgehoben
    /// 0:08 - Nicht-bindendes EP rot hervorgehoben
    /// </summary>
    [MenuItem("Tutorial/Create Step 7 - Trigonal Pyramidal")]
    public static void CreateStep7()
    {
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        step.stepName = "Trigonal Pyramidal";
        step.description = "Trigonal-pyramidaler Molekülbau am Beispiel NH₃";
        step.videoPosition = new Vector3(0f, 0f, 0f);
        
        Vector3 defaultPos = new Vector3(0.35f, 0f, 0f);
        Vector3 defaultScale = Vector3.one * 0.3f;
        
        step.events = new System.Collections.Generic.List<TutorialEvent>
        {
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 2f,
                targetObjectName = "NH3TrigonalPyramidal",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            },
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 5f,
                targetObjectName = "NH3TrigonalPyramidalHighlight",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            },
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 8f,
                targetObjectName = "NH3TrigonalPyramidalEPHighlight",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            }
        };
        
        string path = "Assets/Tutorial/Steps/Step07_TrigonalPyramidal.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet7.mp4)\n2. Zum TutorialManager hinzufügen", 
            "OK");
    }
    
    /// <summary>
    /// Step 8: Tetraedrisch - CH4 (Snippet8 - 35 Sekunden)
    /// 0:02 - Tetraedrisch erscheint
    /// 0:11 - Bindungen grün hervorgehoben
    /// </summary>
    [MenuItem("Tutorial/Create Step 8 - Tetraedrisch")]
    public static void CreateStep8()
    {
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        step.stepName = "Tetraedrisch";
        step.description = "Tetraedrischer Molekülbau am Beispiel CH₄";
        step.videoPosition = new Vector3(0f, 0f, 0f);
        
        Vector3 defaultPos = new Vector3(0.35f, 0f, 0f);
        Vector3 defaultScale = Vector3.one * 0.3f;
        
        step.events = new System.Collections.Generic.List<TutorialEvent>
        {
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 2f,
                targetObjectName = "CH4Tetraedrisch",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            },
            new TutorialEvent
            {
                eventType = TutorialEventType.Show,
                triggerTime = 11f,
                targetObjectName = "CH4TetraedrischHighlight",
                position = defaultPos,
                rotation = Vector3.zero,
                scale = defaultScale
            }
        };
        
        string path = "Assets/Tutorial/Steps/Step08_Tetraedrisch.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet8.mp4)\n2. Zum TutorialManager hinzufügen", 
            "OK");
    }
    
    /// <summary>
    /// Step 9: Abschluss (Snippet9 - 29 Sekunden)
    /// Nur Video, keine 3D-Objekte
    /// </summary>
    [MenuItem("Tutorial/Create Step 9 - Abschluss")]
    public static void CreateStep9()
    {
        TutorialStep step = ScriptableObject.CreateInstance<TutorialStep>();
        step.stepName = "Abschluss";
        step.description = "Abschlussvideo - nur Video, keine 3D-Objekte";
        step.videoPosition = new Vector3(0f, 0f, 0f);
        
        // Keine Events - nur Video
        step.events = new System.Collections.Generic.List<TutorialEvent>();
        
        string path = "Assets/Tutorial/Steps/Step09_Abschluss.asset";
        AssetDatabase.CreateAsset(step, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Selection.activeObject = step;
        EditorGUIUtility.PingObject(step);
        
        Debug.Log($"[TutorialCreator] Created tutorial step at: {path}");
        EditorUtility.DisplayDialog("Tutorial Schritt erstellt", 
            $"Erstellt: {path}\n\nJetzt musst du:\n1. Video-Clip zuweisen (Snippet9.mp4)\n2. Zum TutorialManager hinzufügen", 
            "OK");
    }
    
    [MenuItem("Tutorial/Create Object Pool Template")]
    public static void CreateObjectPoolTemplate()
    {
        // Create parent object
        GameObject pool = new GameObject("TutorialObjectPool");
        
        // Create placeholder objects for Step 1
        CreatePlaceholder(pool, "NormalBond", PrimitiveType.Cylinder);
        CreatePlaceholder(pool, "DashedBond", PrimitiveType.Cylinder);
        CreatePlaceholder(pool, "WedgeBond", PrimitiveType.Cylinder);
        CreatePlaceholder(pool, "AmmoniaDashedHighlight", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "AmmoniaWedgeHighlight", PrimitiveType.Sphere);
        
        // Create placeholder objects for Step 2
        CreatePlaceholder(pool, "Methan3D", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "MethanKeilstrich", PrimitiveType.Sphere);
        
        // Create placeholder objects for Step 3 - Molekülgeometrie
        CreatePlaceholder(pool, "CO2Linear", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "H2OGewinkelt", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "BF3TrigonalPlanar", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "NH3TrigonalPyramidal", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "CH4Tetraedrisch", PrimitiveType.Sphere);
        
        // Create placeholder objects for Step 4 - Linearer Bau Highlight
        CreatePlaceholder(pool, "CO2LinearHighlight", PrimitiveType.Sphere);
        
        // Create placeholder objects for Step 5 - Gewinkelter Bau
        CreatePlaceholder(pool, "H2OGewinkeltHighlight", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "H2OGewinkeltEPHighlight", PrimitiveType.Sphere);
        
        // Create placeholder objects for Step 6 - Trigonal Planar
        CreatePlaceholder(pool, "BF3TrigonalPlanarHighlight", PrimitiveType.Sphere);
        
        // Create placeholder objects for Step 7 - Trigonal Pyramidal
        CreatePlaceholder(pool, "NH3TrigonalPyramidalHighlight", PrimitiveType.Sphere);
        CreatePlaceholder(pool, "NH3TrigonalPyramidalEPHighlight", PrimitiveType.Sphere);
        
        // Create placeholder objects for Step 8 - Tetraedrisch
        CreatePlaceholder(pool, "CH4TetraedrischHighlight", PrimitiveType.Sphere);
        
        Selection.activeGameObject = pool;
        
        Debug.Log("[TutorialCreator] Created TutorialObjectPool with placeholder objects");
        EditorUtility.DisplayDialog("Object Pool erstellt",
            "TutorialObjectPool erstellt mit Platzhaltern für Schritt 1-8.",
            "OK");
    }
    
    [MenuItem("Tutorial/Populate Object Pool from Prefabs")]
    public static void PopulateObjectPoolFromPrefabs()
    {
        // Find or create TutorialObjectPool
        GameObject pool = GameObject.Find("TutorialObjectPool");
        if (pool == null)
        {
            pool = new GameObject("TutorialObjectPool");
        }
        
        string[] prefabNames = {
            // Step 1
            "NormalBond", "DashedBond", "WedgeBond",
            "AmmoniaDashedHighlight", "AmmoniaWedgeHighlight",
            // Step 2
            "Methan3D", "MethanKeilstrich",
            // Step 3
            "CO2Linear", "H2OGewinkelt", "BF3TrigonalPlanar",
            "NH3TrigonalPyramidal", "CH4Tetraedrisch",
            // Step 4
            "CO2LinearHighlight",
            // Step 5
            "H2OGewinkeltHighlight", "H2OGewinkeltEPHighlight",
            // Step 6
            "BF3TrigonalPlanarHighlight",
            // Step 7
            "NH3TrigonalPyramidalHighlight", "NH3TrigonalPyramidalEPHighlight",
            // Step 8
            "CH4TetraedrischHighlight"
        };
        
        int added = 0;
        foreach (string prefabName in prefabNames)
        {
            // Skip if already exists as child
            if (pool.transform.Find(prefabName) != null)
            {
                continue;
            }
            
            // Load prefab from Resources
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{prefabName}");
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pool.transform);
                instance.name = prefabName;
                instance.SetActive(false);
                added++;
                Debug.Log($"[ObjectPool] Added: {prefabName}");
            }
            else
            {
                Debug.LogWarning($"[ObjectPool] Prefab not found: Prefabs/{prefabName}");
            }
        }
        
        Selection.activeGameObject = pool;
        
        EditorUtility.DisplayDialog("Object Pool befüllt",
            $"{added} Prefabs zum TutorialObjectPool hinzugefügt.\n\nVergiss nicht, den TutorialObjectPool im TutorialManager zuzuweisen!",
            "OK");
    }
    
    private static void CreatePlaceholder(GameObject parent, string name, PrimitiveType type)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent.transform);
        obj.transform.localScale = Vector3.one * 0.1f;
        obj.SetActive(false);
        
        // Remove collider
        var col = obj.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
    }
}
