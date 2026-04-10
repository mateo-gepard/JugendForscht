using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Echtes Periodensystem-Layout für den VR-Molekülbaukasten.
/// KEINE OnTriggerEnter - alles über Distanzmessung vom BuilderManager.
/// Tracked World-Positionen aller Tiles und Tools.
/// </summary>
public class PeriodicTableDisplay : MonoBehaviour
{
    [Header("References")]
    public BuilderManager builderManager;
    public ElementDatabase elementDatabase;

    [Header("Layout")]
    public float tileSize = 0.035f;
    public float tileSpacing = 0.005f;
    public float distanceFromPlayer = 0.55f;
    public float heightBelowEyes = -0.3f;
    public float tiltAngle = 40f;

    // ═══════════ ELEMENT DATA ═══════════

    private struct PTElement
    {
        public string symbol;
        public int atomicNumber;
        public int period;
        public int group;
        public bool isCore;

        public PTElement(string sym, int num, int per, int grp, bool core)
        { symbol = sym; atomicNumber = num; period = per; group = grp; isCore = core; }
    }

    private static readonly PTElement[] allElements = {
        new PTElement("H",  1,  1, 1,  true),
        new PTElement("He", 2,  1, 18, false),
        new PTElement("Li", 3,  2, 1,  false),
        new PTElement("Be", 4,  2, 2,  false),
        new PTElement("B",  5,  2, 13, false),
        new PTElement("C",  6,  2, 14, true),
        new PTElement("N",  7,  2, 15, true),
        new PTElement("O",  8,  2, 16, true),
        new PTElement("F",  9,  2, 17, true),
        new PTElement("Ne", 10, 2, 18, false),
        new PTElement("Na", 11, 3, 1,  false),
        new PTElement("Mg", 12, 3, 2,  false),
        new PTElement("Al", 13, 3, 13, false),
        new PTElement("Si", 14, 3, 14, false),
        new PTElement("P",  15, 3, 15, true),
        new PTElement("S",  16, 3, 16, true),
        new PTElement("Cl", 17, 3, 17, true),
        new PTElement("Ar", 18, 3, 18, false),
        new PTElement("K",  19, 4, 1,  false),
        new PTElement("Ca", 20, 4, 2,  false),
        new PTElement("Fe", 26, 4, 8,  false),
        new PTElement("Cu", 29, 4, 11, false),
        new PTElement("Zn", 30, 4, 12, false),
        new PTElement("Br", 35, 4, 17, true),
        new PTElement("I",  53, 5, 17, false),
    };

    private static readonly Dictionary<string, Color> fallbackColors = new Dictionary<string, Color>
    {
        {"H",  new Color(0.85f, 0.85f, 0.85f)}, {"He", new Color(0.8f, 1f, 1f)},
        {"Li", new Color(0.7f, 0.2f, 0.7f)},    {"Be", new Color(0.6f, 0.7f, 0.2f)},
        {"B",  new Color(1f, 0.7f, 0.7f)},      {"C",  new Color(0.3f, 0.3f, 0.3f)},
        {"N",  new Color(0.2f, 0.4f, 0.9f)},    {"O",  new Color(0.9f, 0.2f, 0.2f)},
        {"F",  new Color(0.5f, 0.9f, 0.5f)},    {"Ne", new Color(0.7f, 0.9f, 1f)},
        {"Na", new Color(0.6f, 0.3f, 0.9f)},    {"Mg", new Color(0.5f, 0.7f, 0.2f)},
        {"Al", new Color(0.75f, 0.65f, 0.65f)}, {"Si", new Color(0.78f, 0.67f, 0.45f)},
        {"P",  new Color(1f, 0.5f, 0f)},        {"S",  new Color(1f, 1f, 0.2f)},
        {"Cl", new Color(0.2f, 0.9f, 0.2f)},    {"Ar", new Color(0.7f, 0.85f, 1f)},
        {"K",  new Color(0.55f, 0.25f, 0.8f)},  {"Ca", new Color(0.4f, 0.6f, 0.1f)},
        {"Fe", new Color(0.88f, 0.4f, 0.2f)},   {"Cu", new Color(0.78f, 0.5f, 0.2f)},
        {"Zn", new Color(0.49f, 0.5f, 0.69f)},  {"Br", new Color(0.65f, 0.16f, 0.16f)},
        {"I",  new Color(0.58f, 0f, 0.58f)},
    };

    // State
    private GameObject tableRoot;
    private bool isExpanded = false;
    private bool hasPositioned = false;

    // Position tracking for BuilderManager distance checks
    public struct TrackedItem
    {
        public string id;
        public GameObject obj;
        public Vector3 worldPos;
    }
    private List<TrackedItem> elementItems = new List<TrackedItem>();
    private List<TrackedItem> toolItems = new List<TrackedItem>();

    // Tool materials for visual highlighting
    private Dictionary<string, Material> toolMats = new Dictionary<string, Material>();

    // Table root access for move-tool
    public Transform TableTransform => tableRoot != null ? tableRoot.transform : null;

    public void Initialize()
    {
        hasPositioned = false;
        RebuildTable();
    }

    void LateUpdate()
    {
        // Position ONCE on first frame (so camera is ready)
        if (!hasPositioned)
        {
            PositionTableInitial();
            hasPositioned = true;
        }
        UpdateWorldPositions();
        UpdateToolHighlights();
    }

    // ═══════════ PUBLIC API (called by BuilderManager) ═══════════

    public string GetElementAtPosition(Vector3 worldPos, float maxDist)
    {
        string best = null; float bd = maxDist;
        foreach (var e in elementItems)
        {
            if (e.obj == null) continue;
            float d = Vector3.Distance(worldPos, e.worldPos);
            if (d < bd) { bd = d; best = e.id; }
        }
        return best;
    }

    public string GetToolAtPosition(Vector3 worldPos, float maxDist)
    {
        string best = null; float bd = maxDist;
        foreach (var t in toolItems)
        {
            if (t.obj == null) continue;
            float d = Vector3.Distance(worldPos, t.worldPos);
            if (d < bd) { bd = d; best = t.id; }
        }
        return best;
    }

    public void ToggleExpand()
    {
        isExpanded = !isExpanded;
        RebuildTable();
    }

    // ═══════════ TABLE BUILDING ═══════════

    private void RebuildTable()
    {
        Vector3 savedPos = Vector3.zero;
        Quaternion savedRot = Quaternion.identity;
        bool hasSavedTransform = false;

        if (tableRoot != null)
        {
            savedPos = tableRoot.transform.position;
            savedRot = tableRoot.transform.rotation;
            hasSavedTransform = true;
            Destroy(tableRoot);
        }
        
        elementItems.Clear(); toolItems.Clear(); toolMats.Clear();

        tableRoot = new GameObject("PeriodicTable");
        tableRoot.transform.SetParent(transform);

        if (hasSavedTransform)
        {
            tableRoot.transform.position = savedPos;
            tableRoot.transform.rotation = savedRot;
        }

        float cell = tileSize + tileSpacing;
        int totalCols = isExpanded ? 18 : 8;
        float tableWidth = totalCols * cell;

        // ── TOOL BUTTONS (row above table) ──
        float toolY = cell * 1.2f;
        string[] toolIds =     { "bond",   "unbond", "trash", "compile", "charge+", "charge-", "expand", "move" };
        string[] toolIcons =   { "\u2014",  "/",      "X",     "\u2713",  "+",       "-",       isExpanded ? "\u25B2" : "\u25BC", "\u2725" };
        string[] toolLabels =  { "Bond",   "Unbond", "Trash", "Check",   "+Q",      "-Q",      isExpanded ? "Ein" : "Aus", "Move" };
        Color[] toolColors = {
            new Color(0.15f, 0.15f, 0.2f), new Color(0.3f, 0.15f, 0.1f),
            new Color(0.4f, 0.1f, 0.1f),   new Color(0.1f, 0.3f, 0.15f),
            new Color(0.1f, 0.15f, 0.35f), new Color(0.35f, 0.1f, 0.15f),
            new Color(0.2f, 0.2f, 0.25f),  new Color(0.15f, 0.2f, 0.2f),
        };

        float toolStart = -tableWidth / 2f + cell / 2f;
        for (int i = 0; i < toolIds.Length; i++)
        {
            float x = toolStart + i * cell * 1.1f;
            CreateToolTile(toolIds[i], toolIcons[i], toolLabels[i], toolColors[i], new Vector3(x, toolY, 0));
        }

        // ── GROUP NUMBERS ──
        float groupY = cell * 0.3f;
        if (isExpanded)
        {
            for (int g = 1; g <= 18; g++)
            {
                float x = (g - 1) * cell - tableWidth / 2f + cell / 2f;
                CreateLabel(g.ToString(), new Vector3(x, groupY, 0), 16, 0.0025f, new Color(0.45f, 0.5f, 0.6f));
            }
        }
        else
        {
            int[] mainGroups = { 1, 2, 13, 14, 15, 16, 17, 18 };
            foreach (int g in mainGroups)
            {
                int col = g <= 2 ? g - 1 : g - 11;
                float x = col * cell - tableWidth / 2f + cell / 2f;
                CreateLabel(g.ToString(), new Vector3(x, groupY, 0), 16, 0.0025f, new Color(0.45f, 0.5f, 0.6f));
            }
        }

        // ── ELEMENT TILES ──
        int maxPeriod = isExpanded ? 5 : 3;
        foreach (var elem in allElements)
        {
            if (elem.period > maxPeriod) continue;
            if (!isExpanded && !elem.isCore) continue;
            if (!isExpanded && elem.group >= 3 && elem.group <= 12) continue;

            int col = isExpanded ? elem.group - 1 : (elem.group <= 2 ? elem.group - 1 : elem.group - 11);
            float x = col * cell - tableWidth / 2f + cell / 2f;
            float y = -(elem.period - 1) * cell;
            CreateElementTile(elem, new Vector3(x, y, 0));
        }

        // ── BACKGROUND ──
        float bgW = tableWidth + 0.02f;
        float bgH = (maxPeriod + 1) * cell + cell * 1.5f;
        CreateBackground(bgW, bgH);
    }

    private void CreateElementTile(PTElement elem, Vector3 localPos)
    {
        var obj = new GameObject($"Tile_{elem.symbol}");
        obj.transform.SetParent(tableRoot.transform, false);
        obj.transform.localPosition = localPos;

        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "V"; vis.transform.SetParent(obj.transform, false);
        vis.transform.localScale = new Vector3(tileSize, tileSize, 0.01f);
        var vc = vis.GetComponent<Collider>(); if (vc) Object.Destroy(vc);

        Color ec = GetElementColor(elem.symbol);
        Color tc = Color.Lerp(ec, Color.white, 0.15f);
        var mat = new Material(Shader.Find("Standard"));
        mat.color = tc; mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", tc * 0.1f);
        mat.SetFloat("_Glossiness", 0.5f);
        vis.GetComponent<Renderer>().material = mat;

        // Symbol
        bool isLight = ec.r > 0.6f && ec.g > 0.6f && ec.b > 0.6f;
        CreateLabel(elem.symbol, new Vector3(0, 0.002f, -0.007f), 32, 0.004f,
            isLight ? new Color(0.1f, 0.1f, 0.15f) : Color.white, obj.transform, FontStyle.Bold);
        // Atomic number
        CreateLabel(elem.atomicNumber.ToString(), new Vector3(-tileSize * 0.35f, tileSize * 0.32f, -0.007f),
            16, 0.0025f, new Color(0.5f, 0.5f, 0.6f), obj.transform);

        elementItems.Add(new TrackedItem { id = elem.symbol, obj = obj, worldPos = Vector3.zero });
    }

    private void CreateToolTile(string toolId, string icon, string label, Color color, Vector3 localPos)
    {
        var obj = new GameObject($"Tool_{toolId}");
        obj.transform.SetParent(tableRoot.transform, false);
        obj.transform.localPosition = localPos;

        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "V"; vis.transform.SetParent(obj.transform, false);
        vis.transform.localScale = new Vector3(tileSize, tileSize, 0.01f);
        var vc = vis.GetComponent<Collider>(); if (vc) Object.Destroy(vc);

        var mat = new Material(Shader.Find("Standard"));
        mat.color = color; mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.15f);
        mat.SetFloat("_Glossiness", 0.5f);
        vis.GetComponent<Renderer>().material = mat;
        toolMats[toolId] = mat;

        CreateLabel(icon, new Vector3(0, 0.003f, -0.007f), 34, 0.004f, Color.white, obj.transform, FontStyle.Bold);
        CreateLabel(label, new Vector3(0, -0.012f, -0.007f), 14, 0.002f, new Color(0.65f, 0.65f, 0.7f), obj.transform);

        toolItems.Add(new TrackedItem { id = toolId, obj = obj, worldPos = Vector3.zero });
    }

    private void CreateLabel(string text, Vector3 localPos, int fontSize, float charSize, Color color,
        Transform parent = null, FontStyle style = FontStyle.Normal)
    {
        if (parent == null) parent = tableRoot.transform;
        var obj = new GameObject("L");
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        var tm = obj.AddComponent<TextMesh>();
        tm.text = text; tm.fontSize = fontSize; tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.color = color; tm.fontStyle = style;
    }

    private void CreateBackground(float w, float h)
    {
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "BG"; bg.transform.SetParent(tableRoot.transform, false);
        float cell = tileSize + tileSpacing;
        bg.transform.localScale = new Vector3(w, h, 1f);
        bg.transform.localPosition = new Vector3(0, cell * 1.2f - h / 2f + cell * 0.3f, 0.005f);
        var bc = bg.GetComponent<Collider>(); if (bc) Object.Destroy(bc);
        var m = new Material(Shader.Find("Standard"));
        m.color = new Color(0.02f, 0.02f, 0.06f, 0.9f);
        m.SetFloat("_Mode", 3);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON"); m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); m.renderQueue = 3000;
        bg.GetComponent<Renderer>().material = m;
    }

    // ═══════════ TOOL HIGHLIGHTS ═══════════

    private void UpdateToolHighlights()
    {
        if (builderManager == null) return;
        var mode = builderManager.CurrentMode;
        HighlightTool("bond",    mode == BuilderManager.BuilderMode.BondTool);
        HighlightTool("unbond",  mode == BuilderManager.BuilderMode.UnbondTool);
        HighlightTool("trash",   mode == BuilderManager.BuilderMode.DeleteTool);
        HighlightTool("charge+", mode == BuilderManager.BuilderMode.ChargePlus);
        HighlightTool("charge-", mode == BuilderManager.BuilderMode.ChargeMinus);
        HighlightTool("move",    mode == BuilderManager.BuilderMode.MoveTool);
    }

    private void HighlightTool(string id, bool active)
    {
        if (!toolMats.ContainsKey(id)) return;
        var mat = toolMats[id];
        Color c = active ? GetActiveColor(id) : GetInactiveColor(id);
        mat.color = c; mat.SetColor("_EmissionColor", c * (active ? 0.4f : 0.15f));
    }

    private Color GetActiveColor(string id)
    {
        switch (id) {
            case "bond": return new Color(0.2f, 0.6f, 1f);
            case "trash": return new Color(0.9f, 0.2f, 0.2f);
            case "unbond": return new Color(1f, 0.5f, 0.2f);
            case "charge+": return new Color(0.3f, 0.5f, 1f);
            case "charge-": return new Color(1f, 0.3f, 0.4f);
            case "move": return new Color(0.2f, 0.8f, 0.6f);
            default: return Color.white;
        }
    }

    private Color GetInactiveColor(string id)
    {
        switch (id) {
            case "bond": return new Color(0.15f, 0.15f, 0.2f);
            case "trash": return new Color(0.4f, 0.1f, 0.1f);
            case "unbond": return new Color(0.3f, 0.15f, 0.1f);
            case "charge+": return new Color(0.1f, 0.15f, 0.35f);
            case "charge-": return new Color(0.35f, 0.1f, 0.15f);
            default: return new Color(0.2f, 0.2f, 0.25f);
        }
    }

    // ═══════════ POSITIONING (once, on init) ═══════════

    private void PositionTableInitial()
    {
        Camera cam = Camera.main;
        if (cam == null || tableRoot == null) return;
        Vector3 fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
        if (fwd.magnitude < 0.01f) fwd = Vector3.forward;
        Vector3 pos = cam.transform.position + fwd * distanceFromPlayer + Vector3.up * heightBelowEyes;
        tableRoot.transform.position = pos;
        tableRoot.transform.rotation = Quaternion.LookRotation(fwd) * Quaternion.Euler(tiltAngle, 0, 0);
    }

    /// <summary>Move the entire table by a delta offset</summary>
    public void MoveTableDelta(Vector3 delta)
    {
        if (tableRoot == null) return;
        tableRoot.transform.position += delta;
    }

    /// <summary>Rotates the table to continually face the camera</summary>
    public void FaceCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || tableRoot == null) return;
        
        Vector3 fwd = cam.transform.forward; 
        fwd.y = 0; 
        fwd.Normalize();
        if (fwd.magnitude < 0.01f) fwd = Vector3.forward;
        
        tableRoot.transform.rotation = Quaternion.LookRotation(fwd) *  Quaternion.Euler(tiltAngle, 0, 0);
    }

    private void UpdateWorldPositions()
    {
        for (int i = 0; i < elementItems.Count; i++)
        {
            var e = elementItems[i];
            if (e.obj != null) { e.worldPos = e.obj.transform.position; elementItems[i] = e; }
        }
        for (int i = 0; i < toolItems.Count; i++)
        {
            var t = toolItems[i];
            if (t.obj != null) { t.worldPos = t.obj.transform.position; toolItems[i] = t; }
        }
    }

    private Color GetElementColor(string symbol)
    {
        if (elementDatabase != null && elementDatabase.HasElement(symbol))
            return elementDatabase.GetElement(symbol).cpkColor;
        return fallbackColors.ContainsKey(symbol) ? fallbackColors[symbol] : Color.gray;
    }

    void OnDestroy()
    {
        elementItems.Clear(); toolItems.Clear(); toolMats.Clear();
        if (tableRoot != null) Destroy(tableRoot);
    }
}
