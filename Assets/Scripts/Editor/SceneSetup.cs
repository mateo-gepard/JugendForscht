using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Comprehensive Scene Setup Tool – Creates or validates ALL required GameObjects,
/// Components and References for the MolekülVR project.
///
/// Menu: Tutorial > Setup Entire Scene
///
/// This is designed to recover a fully working scene from scratch or to validate
/// that an existing scene has all required pieces properly linked.
/// </summary>
public class SceneSetup : Editor
{
    // ════════════════════════════════════════════════════════════
    // MENU ITEMS
    // ════════════════════════════════════════════════════════════

    [MenuItem("Tutorial/★ Setup Entire Scene")]
    public static void SetupEntireScene()
    {
        // Ensure scene is open
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            EditorUtility.DisplayDialog("Error",
                "Keine aktive Szene geladen!\n\n" +
                "Bitte öffne zuerst eine Szene (Assets/Scenes/SampleScene.unity).",
                "OK");
            return;
        }

        int created = 0;
        int linked = 0;
        string report = "";

        // ── 1. MoleculeSystem ──────────────────────────────
        var library = FindObjectOfType<MoleculeLibrary>();
        var renderer = FindObjectOfType<MoleculeRenderer>();
        var planeAlign = FindObjectOfType<MoleculePlaneAlignment>();

        if (library == null || renderer == null)
        {
            GameObject molSys = FindOrCreate("MoleculeSystem", ref created);

            // ElementDatabase asset
            ElementDatabase elemDb = AssetDatabase.LoadAssetAtPath<ElementDatabase>(
                "Assets/Scripts/Chemistry/MainElementDatabase.asset");
            if (elemDb == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ElementDatabase");
                if (guids.Length > 0)
                    elemDb = AssetDatabase.LoadAssetAtPath<ElementDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // MoleculeRenderer
            renderer = EnsureComponent<MoleculeRenderer>(molSys, ref created);
            if (elemDb != null)
            {
                var so = new SerializedObject(renderer);
                var dbProp = so.FindProperty("elementDatabase");
                if (dbProp != null && dbProp.objectReferenceValue == null)
                {
                    dbProp.objectReferenceValue = elemDb;
                    so.ApplyModifiedProperties();
                    linked++;
                }
            }
            renderer.enableStereoDisplay = true;

            // MoleculeLibrary
            library = EnsureComponent<MoleculeLibrary>(molSys, ref created);
            if (elemDb != null)
            {
                var so = new SerializedObject(library);
                var dbProp = so.FindProperty("elementDatabase");
                if (dbProp != null && dbProp.objectReferenceValue == null)
                {
                    dbProp.objectReferenceValue = elemDb;
                    so.ApplyModifiedProperties();
                    linked++;
                }
                var rendProp = so.FindProperty("renderer");
                if (rendProp != null && rendProp.objectReferenceValue == null)
                {
                    rendProp.objectReferenceValue = renderer;
                    so.ApplyModifiedProperties();
                    linked++;
                }
            }

            // MoleculePlaneAlignment
            planeAlign = EnsureComponent<MoleculePlaneAlignment>(molSys, ref created);
            {
                var so = new SerializedObject(planeAlign);
                var rendProp = so.FindProperty("renderer");
                if (rendProp != null && rendProp.objectReferenceValue == null)
                {
                    rendProp.objectReferenceValue = renderer;
                    so.ApplyModifiedProperties();
                    linked++;
                }
            }

            // VRMoleculeController (for Quest controller thumbstick rotation)
            var vrCtrl = EnsureComponent<VRMoleculeController>(molSys, ref created);
            if (vrCtrl.planeAlignment == null)
            {
                vrCtrl.planeAlignment = planeAlign;
                EditorUtility.SetDirty(vrCtrl);
                linked++;
            }

            // HandRotationController (for hand tracking pinch-to-rotate)
            var handRotCtrl = EnsureComponent<HandRotationController>(molSys, ref created);
            if (handRotCtrl.moleculeRenderer == null)
            {
                handRotCtrl.moleculeRenderer = renderer;
                EditorUtility.SetDirty(handRotCtrl);
                linked++;
            }
            if (handRotCtrl.planeAlignment == null)
            {
                handRotCtrl.planeAlignment = planeAlign;
                EditorUtility.SetDirty(handRotCtrl);
                linked++;
            }

            // MoleculePositionLock
            EnsureComponent<MoleculePositionLock>(molSys, ref created);

            // VRPanelManager (auto-attaches grab+close to VR panels)
            EnsureComponent<VRPanelManager>(molSys, ref created);

            report += "✓ MoleculeSystem (Library, Renderer, PlaneAlign, Controllers, PositionLock, PanelManager)\n";
        }
        else
        {
            report += "✓ MoleculeSystem – already exists\n";
            GameObject molSys = library.gameObject;

            // Re-link if references are missing
            if (planeAlign == null)
            {
                planeAlign = library.gameObject.AddComponent<MoleculePlaneAlignment>();
                created++;
            }

            // Ensure HandRotationController exists (was missing before!)
            var handRotCtrl = EnsureComponent<HandRotationController>(molSys, ref created);
            if (handRotCtrl.moleculeRenderer == null)
            {
                handRotCtrl.moleculeRenderer = renderer;
                EditorUtility.SetDirty(handRotCtrl);
                linked++;
            }
            if (handRotCtrl.planeAlignment == null)
            {
                handRotCtrl.planeAlignment = planeAlign;
                EditorUtility.SetDirty(handRotCtrl);
                linked++;
            }

            // Ensure VRMoleculeController exists
            var vrCtrl = EnsureComponent<VRMoleculeController>(molSys, ref created);
            if (vrCtrl.planeAlignment == null)
            {
                vrCtrl.planeAlignment = planeAlign;
                EditorUtility.SetDirty(vrCtrl);
                linked++;
            }

            // Ensure MoleculePositionLock exists
            EnsureComponent<MoleculePositionLock>(molSys, ref created);

            // VRPanelManager (auto-attaches grab+close to VR panels)
            EnsureComponent<VRPanelManager>(molSys, ref created);
        }

        // ── 2. WebSocketServer ─────────────────────────────
        var wsServer = FindObjectOfType<WebSocketServer>();
        if (wsServer == null)
        {
            GameObject wsObj = FindOrCreate("WebSocketServer", ref created);
            wsServer = EnsureComponent<WebSocketServer>(wsObj, ref created);
            report += "✓ WebSocketServer – created\n";
        }
        else
        {
            report += "✓ WebSocketServer – already exists\n";
        }
        // Link library
        if (wsServer.library == null && library != null)
        {
            wsServer.library = library;
            EditorUtility.SetDirty(wsServer);
            linked++;
        }
        // Link renderer
        if (wsServer.moleculeRenderer == null && renderer != null)
        {
            wsServer.moleculeRenderer = renderer;
            EditorUtility.SetDirty(wsServer);
            linked++;
        }

        // ── 3. TutorialManager ─────────────────────────────
        var tutManager = FindObjectOfType<TutorialManager>();
        TutorialTimeline timeline = null;
        if (tutManager == null)
        {
            GameObject tutObj = FindOrCreate("TutorialManager", ref created);
            tutManager = EnsureComponent<TutorialManager>(tutObj, ref created);

            // VideoPlayer
            var vp = EnsureComponent<VideoPlayer>(tutObj, ref created);
            vp.playOnAwake = false;
            vp.isLooping = false;

            // Try to find video clip
            var videoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Tutorial/0304.mp4");
            if (videoClip != null)
            {
                vp.clip = videoClip;
                linked++;
            }

            // RenderTexture
            RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                "Assets/Tutorial/TutorialVideoRT.renderTexture");
            if (rt != null)
            {
                vp.targetTexture = rt;
                tutManager.videoRenderTexture = rt;
                vp.renderMode = VideoRenderMode.RenderTexture;
                linked++;
            }

            tutManager.videoPlayer = vp;
            EditorUtility.SetDirty(tutManager);

            report += "✓ TutorialManager + VideoPlayer – created\n";
        }
        else
        {
            report += "✓ TutorialManager – already exists\n";
        }

        // Link timeline if not set
        if (tutManager.timeline == null)
        {
            string[] tlGuids = AssetDatabase.FindAssets("t:TutorialTimeline");
            if (tlGuids.Length > 0)
            {
                timeline = AssetDatabase.LoadAssetAtPath<TutorialTimeline>(
                    AssetDatabase.GUIDToAssetPath(tlGuids[0]));
                tutManager.timeline = timeline;
                EditorUtility.SetDirty(tutManager);
                linked++;
            }
        }

        // TutorialVideoPanel (child of TutorialManager)
        if (tutManager.videoDisplayPanel == null)
        {
            Transform existingPanel = tutManager.transform.Find("TutorialVideoPanel");
            if (existingPanel == null)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panel.name = "TutorialVideoPanel";
                panel.transform.SetParent(tutManager.transform);
                panel.transform.localPosition = Vector3.zero;
                panel.transform.localScale = new Vector3(0.622f, 0.35f, 1f); // 16:9
                panel.SetActive(false);
                Object.DestroyImmediate(panel.GetComponent<Collider>());

                tutManager.videoDisplayPanel = panel;
                created++;
            }
            else
            {
                tutManager.videoDisplayPanel = existingPanel.gameObject;
            }
            EditorUtility.SetDirty(tutManager);
            linked++;
        }

        // ChromaKey Material
        if (tutManager.chromaKeyMaterial == null)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Tutorial/TutorialVideoMat.mat");
            if (mat != null)
            {
                tutManager.chromaKeyMaterial = mat;
                EditorUtility.SetDirty(tutManager);
                linked++;
            }
        }

        // TutorialObjectPool
        if (tutManager.objectPoolParent == null)
        {
            GameObject pool = GameObject.Find("TutorialObjectPool");
            if (pool == null)
            {
                pool = new GameObject("TutorialObjectPool");
                pool.transform.position = new Vector3(0, -100, 0); // hidden below scene
                created++;
            }
            tutManager.objectPoolParent = pool.transform;
            EditorUtility.SetDirty(tutManager);
            linked++;
        }

        // ── 4. Directional Light ───────────────────────────
        if (FindObjectOfType<Light>() == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            created++;
            report += "✓ Directional Light – created\n";
        }
        else
        {
            report += "✓ Directional Light – already exists\n";
        }

        // ── 5. EventSystem ─────────────────────────────────
        var eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            created++;
            report += "✓ EventSystem – created\n";
        }
        else
        {
            report += "✓ EventSystem – already exists\n";
        }

        // ── 6. URLDisplay ──────────────────────────────────
        var urlDisplay = FindObjectOfType<TabletURLDisplay>();
        if (urlDisplay == null)
        {
            GameObject urlObj = FindOrCreate("URLDisplay", ref created);
            urlDisplay = EnsureComponent<TabletURLDisplay>(urlObj, ref created);
            report += "✓ URLDisplay – created\n";
        }
        else
        {
            report += "✓ URLDisplay – already exists\n";
        }

        // ── 7. ShaderIncluder ──────────────────────────────
        var shaderIncluder = FindObjectOfType<ShaderIncluder>();
        if (shaderIncluder == null)
        {
            // Try prefab first
            string[] siGuids = AssetDatabase.FindAssets("t:Prefab ShaderIncluder");
            if (siGuids.Length > 0)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(siGuids[0]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeScene);
                    instance.name = "ShaderIncluder";
                    created++;
                }
            }
            else
            {
                // Create from scratch
                GameObject siObj = new GameObject("ShaderIncluder");
                siObj.AddComponent<ShaderIncluder>();
                created++;
            }
            report += "✓ ShaderIncluder – created\n";
        }
        else
        {
            report += "✓ ShaderIncluder – already exists\n";
        }

        // ── 8. Verify VR Building Blocks (deep search) ─────
        string[] buildingBlocks = {
            "Camera Rig", "Hand Tracking left", "Hand Tracking right",
            "Controller Tracking Left", "Controller Tracking Right", "Passthrough"
        };
        foreach (string bb in buildingBlocks)
        {
            string fullName = "[BuildingBlock] " + bb;
            var found = FindInSceneByName(fullName);
            if (found != null)
                report += $"✓ {fullName}\n";
            else
                report += $"⚠ {fullName} – nicht gefunden! Über Meta > Building Blocks hinzufügen.\n";
        }

        // ── 11. Debug Objects ──────────────────────────────
        GameObject debug1 = GameObject.Find("Debug1");
        GameObject debug2 = GameObject.Find("Debug2");
        GameObject debug3 = GameObject.Find("Debug3");
        if (debug1 == null)
        {
            debug1 = new GameObject("Debug1");
            EnsureComponent<TextMesh>(debug1, ref created);
            created++;
        }
        if (debug2 == null)
        {
            debug2 = new GameObject("Debug2");
            EnsureComponent<TextMesh>(debug2, ref created);
            created++;
        }
        if (debug3 == null)
        {
            debug3 = new GameObject("Debug3");
            EnsureComponent<TextMesh>(debug3, ref created);
            created++;
        }
        report += "✓ Debug Objects (1-3)\n";

        // ── 12. Ensure scene is in Build Settings ──────────
        bool sceneInBuild = false;
        string scenePath = activeScene.path;
        foreach (var bs in EditorBuildSettings.scenes)
        {
            if (bs.path == scenePath && bs.enabled)
            {
                sceneInBuild = true;
                break;
            }
        }
        if (!sceneInBuild && !string.IsNullOrEmpty(scenePath))
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            report += "✓ Scene zu Build Settings hinzugefügt\n";
        }
        else
        {
            report += "✓ Scene in Build Settings\n";
        }

        // ── Save ───────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log($"[SceneSetup] Setup complete! Created {created} items, linked {linked} references.");
        Debug.Log($"[SceneSetup] Report:\n{report}");

        EditorUtility.DisplayDialog("★ Szene Setup Komplett!",
            $"Erstellt: {created} Objekte/Komponenten\n" +
            $"Verknüpft: {linked} Referenzen\n\n" +
            $"─── Ergebnis ───\n{report}\n" +
            "Nächste Schritte:\n" +
            "1. Play Mode starten (▶)\n" +
            "2. iPad URL wird in der Konsole angezeigt\n" +
            "3. Zum Testen: Tutorial > Check Server Status",
            "OK");
    }

    [MenuItem("Tutorial/★ Validate Scene")]
    public static void ValidateScene()
    {
        string report = "─── Scene Validation Report ───\n\n";
        int issues = 0;

        // Check core systems
        var ws = FindObjectOfType<WebSocketServer>();
        var lib = FindObjectOfType<MoleculeLibrary>();
        var rend = FindObjectOfType<MoleculeRenderer>();
        var tut = FindObjectOfType<TutorialManager>();
        var plane = FindObjectOfType<MoleculePlaneAlignment>();
        var url = FindObjectOfType<TabletURLDisplay>();
        var shader = FindObjectOfType<ShaderIncluder>();

        report += CheckPresence("WebSocketServer", ws, ref issues);
        report += CheckPresence("MoleculeLibrary", lib, ref issues);
        report += CheckPresence("MoleculeRenderer", rend, ref issues);
        report += CheckPresence("TutorialManager", tut, ref issues);
        report += CheckPresence("MoleculePlaneAlignment", plane, ref issues);
        report += CheckPresence("TabletURLDisplay (URLDisplay)", url, ref issues);
        report += CheckPresence("ShaderIncluder", shader, ref issues);

        // Check hand interaction controller
        var handRot = FindObjectOfType<HandRotationController>();
        report += CheckPresence("HandRotationController", handRot, ref issues);

        // Check references
        if (ws != null)
        {
            if (ws.library == null) { report += "  ⚠ WebSocketServer.library = null!\n"; issues++; }
        }
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var r = so.FindProperty("renderer");
            if (r != null && r.objectReferenceValue == null)
            { report += "  ⚠ MoleculeLibrary.renderer = null!\n"; issues++; }
        }
        if (tut != null)
        {
            if (tut.timeline == null) { report += "  ⚠ TutorialManager.timeline = null!\n"; issues++; }
            if (tut.videoPlayer == null) { report += "  ⚠ TutorialManager.videoPlayer = null!\n"; issues++; }
            if (tut.videoDisplayPanel == null) { report += "  ⚠ TutorialManager.videoDisplayPanel = null!\n"; issues++; }
        }

        // Camera / VR – use deep search (GameObject.Find misses inactive/nested objects)
        report += CheckBuildingBlock("Camera Rig", ref issues);
        report += CheckBuildingBlock("Hand Tracking left", ref issues);
        report += CheckBuildingBlock("Hand Tracking right", ref issues);
        report += CheckBuildingBlock("Controller Tracking Left", ref issues);
        report += CheckBuildingBlock("Controller Tracking Right", ref issues);
        report += CheckBuildingBlock("Passthrough", ref issues);

        report += $"\n─── {(issues == 0 ? "Alles OK! ✓" : $"{issues} Probleme gefunden")} ───";

        EditorUtility.DisplayDialog("Scene Validation",
            report + (issues > 0 ? "\n\nTipp: 'Tutorial > ★ Setup Entire Scene' kann die meisten Probleme automatisch beheben." : ""),
            "OK");
    }

    // ════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════

    private static GameObject FindOrCreate(string name, ref int created)
    {
        GameObject obj = GameObject.Find(name);
        if (obj == null)
        {
            obj = new GameObject(name);
            created++;
        }
        return obj;
    }

    private static T EnsureComponent<T>(GameObject obj, ref int created) where T : Component
    {
        T comp = obj.GetComponent<T>();
        if (comp == null)
        {
            comp = obj.AddComponent<T>();
            created++;
        }
        return comp;
    }

    private static string CheckPresence(string label, Object obj, ref int issues)
    {
        if (obj != null)
            return $"✓ {label}\n";
        issues++;
        return $"✗ {label} – FEHLT!\n";
    }

    private static string CheckPresenceObj(string label, GameObject obj, ref int issues)
    {
        if (obj != null)
            return $"✓ {label}\n";
        issues++;
        return $"⚠ {label} – nicht gefunden (Meta Building Block)\n";
    }

    /// <summary>
    /// Searches ALL scene objects (including inactive and deeply nested) for a Building Block by name.
    /// GameObject.Find() only returns active root objects, so it misses prefab children and inactive objects.
    /// </summary>
    private static string CheckBuildingBlock(string shortName, ref int issues)
    {
        string fullName = "[BuildingBlock] " + shortName;
        GameObject found = FindInSceneByName(fullName);
        if (found != null)
            return $"✓ {fullName}\n";
        issues++;
        return $"⚠ {fullName} – nicht gefunden\n";
    }

    /// <summary>
    /// Finds any GameObject in the scene by name, including inactive and nested objects.
    /// </summary>
    private static GameObject FindInSceneByName(string name)
    {
        // First try standard search (fast, but only finds active objects)
        GameObject found = GameObject.Find(name);
        if (found != null) return found;

        // Deep search: iterate all scene root objects and search their entire hierarchy
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameObject result = SearchHierarchy(root.transform, name);
            if (result != null) return result;
        }
        return null;
    }

    private static GameObject SearchHierarchy(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject result = SearchHierarchy(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
