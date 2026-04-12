using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction.Input;

/// <summary>
/// Hauptsteuerung des VR-Molekülbaukastens.
///
/// STEUERUNG:
/// - Zeigefinger-Poke: Tool-Buttons + Expand Button antippen
/// - Daumen+Zeigefinger Pinch: Atome aus PSE ziehen + bestehende Atome draggen
/// - Pinch auf Atom (in Tool-Modi): Atom für Bond/Unbond/Delete/Charge auswählen
/// </summary>
public class BuilderManager : MonoBehaviour
{
    public static BuilderManager Instance { get; private set; }

    [Header("References")]
    public WebSocketServer webSocket;
    public MoleculeLibrary moleculeLibrary;
    public ElementDatabase elementDatabase;

    [Header("Hand Tracking")]
    public Hand rightHand;
    public Hand leftHand;

    [Header("Settings")]
    [Range(0.5f, 1f)] public float pinchThreshold = 0.7f;
    [Range(0.1f, 0.5f)] public float pinchReleaseThreshold = 0.35f;
    public float bondLength = 0.05f;

    // State
    private bool isActive = false;
    private GameObject builderRoot;
    private PeriodicTableDisplay periodicTable;
    private List<BuilderAtom> placedAtoms = new List<BuilderAtom>();

    // Drag
    private BuilderAtom draggedAtom = null;
    private bool isDragging = false;
    private Hand draggingHand = null;
    private bool wasPinchingR = false, wasPinchingL = false;

    // Modes
    public enum BuilderMode { PlaceAtoms, BondTool, DeleteTool, UnbondTool, ChargePlus, ChargeMinus, MoveTool }
    private BuilderMode currentMode = BuilderMode.PlaceAtoms;

    // Move tool
    private bool isMovingTable = false;
    private Hand movingHand = null;
    private Vector3 moveOffset = Vector3.zero;

    // Bond tool
    private BuilderAtom bondPrimaryAtom = null;
    private LineRenderer bondPreviewLine = null;
    private Hand bondToolHand = null;

    // Unbond tool
    private BuilderAtom unbondPrimaryAtom = null;
    private List<BondInfo> highlightedBonds = new List<BondInfo>();

    // Poke cooldown (prevents double-tap)
    private float lastPokeTimeR = -1f, lastPokeTimeL = -1f;
    private const float POKE_COOLDOWN = 0.4f;

    // Properties
    public bool IsActive => isActive;
    public bool IsDragging => isDragging;
    public BuilderMode CurrentMode => currentMode;

    // Bonds
    public class BondInfo
    {
        public BuilderAtom atomA, atomB;
        public int bondOrder;
        public GameObject lineObject;
        public float creationTime;
    }
    private List<BondInfo> bonds = new List<BondInfo>();

    // ═══════════ LIFECYCLE ═══════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartBuilder()
    {
        if (isActive) return;
        isActive = true;
        FindHands();

        // Clear chirality markers (star markers on chiral centers)
        var chiralVis = FindObjectOfType<ChiralityVisualizer>();
        if (chiralVis != null) chiralVis.ClearMarkers();

        // Hide chirality control panel
        var chiralPanel = FindObjectOfType<ChiralityPanelDisplay>();
        if (chiralPanel != null) chiralPanel.gameObject.SetActive(false);

        // Clear isomer display
        var animator = FindObjectOfType<IsomerAnimator>();
        if (animator != null) animator.ClearEnantiomer();

        // Hide the molecule renderer
        var renderer = FindObjectOfType<MoleculeRenderer>();
        if (renderer != null)
        {
            renderer.gameObject.SetActive(false);

            // Also hide the stereo plane if visible
            var planeAlign = renderer.GetComponent<MoleculePlaneAlignment>();
            if (planeAlign != null) planeAlign.SetPlaneVisibility(false);
        }

        // Clear the loaded molecule data
        var lib = moleculeLibrary ?? FindObjectOfType<MoleculeLibrary>();
        if (lib != null) lib.ClearCurrentMolecule();

        if (elementDatabase == null)
        {
            if (lib != null && lib.elementDatabase != null) elementDatabase = lib.elementDatabase;
        }

        builderRoot = new GameObject("BuilderRoot");

        var ptObj = new GameObject("PeriodicTableDisplay");
        ptObj.transform.SetParent(builderRoot.transform);
        periodicTable = ptObj.AddComponent<PeriodicTableDisplay>();
        periodicTable.builderManager = this;
        periodicTable.elementDatabase = elementDatabase;
        periodicTable.Initialize();

        currentMode = BuilderMode.PlaceAtoms;
        if (webSocket != null)
            webSocket.BroadcastMessage("{\"type\":\"builder_state\",\"active\":true}");
    }

    public void StopBuilder()
    {
        if (!isActive) return;
        isActive = false;
        CancelBondTool(); CancelUnbondTool(); ReleaseDraggedAtom();

        foreach (var b in bonds) { if (b.lineObject != null) Destroy(b.lineObject); }
        bonds.Clear();
        foreach (var a in placedAtoms) { if (a != null) Destroy(a.gameObject); }
        placedAtoms.Clear();

        if (builderRoot != null) Destroy(builderRoot);
        builderRoot = null; periodicTable = null;

        var renderer = FindObjectOfType<MoleculeRenderer>();
        if (renderer != null) renderer.gameObject.SetActive(true);
        if (webSocket != null)
            webSocket.BroadcastMessage("{\"type\":\"builder_state\",\"active\":false}");
    }

    /// <summary>
    /// Clear all placed atoms and bonds without stopping the builder.
    /// The periodic table and builder mode stay active.
    /// </summary>
    public void ClearAllAtoms()
    {
        if (!isActive) return;
        CancelBondTool(); CancelUnbondTool(); ReleaseDraggedAtom();

        foreach (var b in bonds) { if (b.lineObject != null) Destroy(b.lineObject); }
        bonds.Clear();
        foreach (var a in placedAtoms) { if (a != null) Destroy(a.gameObject); }
        placedAtoms.Clear();

        currentMode = BuilderMode.PlaceAtoms;
        Debug.Log("[Builder] All atoms and bonds cleared");
    }

    // ═══════════ MODES ═══════════

    public void SetMode(BuilderMode mode)
    {
        if (currentMode == BuilderMode.BondTool) CancelBondTool();
        if (currentMode == BuilderMode.UnbondTool) CancelUnbondTool();
        if (currentMode == BuilderMode.MoveTool) { isMovingTable = false; movingHand = null; }
        currentMode = mode;
    }
    public void ToggleBondTool()   { SetMode(currentMode == BuilderMode.BondTool   ? BuilderMode.PlaceAtoms : BuilderMode.BondTool); }
    public void ToggleUnbondTool() { SetMode(currentMode == BuilderMode.UnbondTool ? BuilderMode.PlaceAtoms : BuilderMode.UnbondTool); }
    public void ToggleDeleteTool() { SetMode(currentMode == BuilderMode.DeleteTool ? BuilderMode.PlaceAtoms : BuilderMode.DeleteTool); }
    public void ToggleChargeTool(int sign)
    {
        var target = sign > 0 ? BuilderMode.ChargePlus : BuilderMode.ChargeMinus;
        SetMode(currentMode == target ? BuilderMode.PlaceAtoms : target);
    }

    // ═══════════ UPDATE ═══════════

    void Update()
    {
        if (!isActive) return;

        // Zeigefinger-Poke: check index finger tip proximity to tool buttons
        if (rightHand != null) ProcessFingerPoke(rightHand, ref lastPokeTimeR);
        if (leftHand != null) ProcessFingerPoke(leftHand, ref lastPokeTimeL);

        // Pinch: drag atoms from PSE, drag placed atoms, select atoms for tools
        if (rightHand != null) ProcessPinch(rightHand, ref wasPinchingR);
        if (leftHand != null) ProcessPinch(leftHand, ref wasPinchingL);

        if (isDragging && draggedAtom != null) UpdateDragPosition();
        if (isMovingTable && movingHand != null) UpdateMoveTable();
        if (bondPrimaryAtom != null || unbondPrimaryAtom != null) UpdateBondPreviewLine();
        UpdateBondVisuals();
    }

    // ── FINGER POKE: just touching with index tip (no pinch needed) ──
    private void ProcessFingerPoke(Hand hand, ref float lastPokeTime)
    {
        if (!hand.IsTrackedDataValid) return;
        if (Time.time - lastPokeTime < POKE_COOLDOWN) return;
        if (!hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip)) return;

        Vector3 fp = tip.position;
        if (periodicTable == null) return;

        // Check tool buttons (small radius = must physically touch)
        string tool = periodicTable.GetToolAtPosition(fp, 0.03f);
        if (tool != null)
        {
            lastPokeTime = Time.time;
            HandleToolPoke(tool);
        }
    }

    private void HandleToolPoke(string toolId)
    {
        switch (toolId)
        {
            case "bond":    ToggleBondTool(); break;
            case "unbond":  ToggleUnbondTool(); break;
            case "trash":   ToggleDeleteTool(); break;
            case "compile": CompileMolecule(); break;
            case "charge+": ToggleChargeTool(+1); break;
            case "charge-": ToggleChargeTool(-1); break;
            case "expand":  periodicTable.ToggleExpand(); break;
            case "move":    SetMode(currentMode == BuilderMode.MoveTool ? BuilderMode.PlaceAtoms : BuilderMode.MoveTool); break;
        }
    }

    // ── PINCH: for dragging atoms and selecting atoms in tool modes ──
    private void ProcessPinch(Hand hand, ref bool wasPinching)
    {
        if (!hand.IsTrackedDataValid) { wasPinching = false; return; }

        float pinch = hand.GetFingerPinchStrength(HandFinger.Index);
        bool isPinching = pinch > pinchThreshold;
        bool justStarted = isPinching && !wasPinching;

        // RELEASE drag or move
        if (isDragging && draggingHand == hand && pinch < pinchReleaseThreshold)
        {
            ReleaseDraggedAtom();
            wasPinching = isPinching; return;
        }
        if (isMovingTable && movingHand == hand && pinch < pinchReleaseThreshold)
        {
            isMovingTable = false; movingHand = null;
            wasPinching = isPinching; return;
        }

        // START PINCH
        if (justStarted && !isDragging)
        {
            if (!hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            { wasPinching = isPinching; return; }

            Vector3 fp = tip.position;

            switch (currentMode)
            {
                case BuilderMode.PlaceAtoms:
                    HandlePlaceAtomPinch(fp, hand);
                    break;
                case BuilderMode.BondTool:
                    HandleBondToolPinch(fp, hand);
                    break;
                case BuilderMode.UnbondTool:
                    HandleUnbondToolPinch(fp);
                    break;
                case BuilderMode.DeleteTool:
                    HandleDeleteToolPinch(fp);
                    break;
                case BuilderMode.ChargePlus:
                    HandleChargePinch(fp, +1);
                    break;
                case BuilderMode.ChargeMinus:
                    HandleChargePinch(fp, -1);
                    break;
                case BuilderMode.MoveTool:
                    StartMovingTable(fp, hand);
                    break;
            }
        }

        wasPinching = isPinching;
    }

    // ═══════════ PLACE ATOMS (Pinch) ═══════════

    private void HandlePlaceAtomPinch(Vector3 fp, Hand hand)
    {
        // 1. Grab existing placed atom
        BuilderAtom near = FindNearestAtom(fp, 0.06f);
        if (near != null) { StartDragging(near, hand); return; }

        // 2. Spawn from periodic table
        if (periodicTable != null)
        {
            string el = periodicTable.GetElementAtPosition(fp, 0.05f);
            if (el != null)
            {
                var atom = SpawnAtom(el, fp);
                if (atom != null) StartDragging(atom, hand);
            }
        }
    }

    // ═══════════ BOND TOOL (Pinch on atoms) ═══════════

    private void HandleBondToolPinch(Vector3 fp, Hand hand)
    {
        // Pinch on PSE → cancel
        if (periodicTable != null && periodicTable.GetElementAtPosition(fp, 0.04f) != null)
        { CancelBondTool(); SetMode(BuilderMode.PlaceAtoms); return; }

        BuilderAtom near = FindNearestAtom(fp, 0.08f);
        if (near == null) return;

        if (bondPrimaryAtom == null)
        {
            bondPrimaryAtom = near;
            bondPrimaryAtom.SetHighlight(true);
            bondToolHand = hand;
            CreateBondPreviewLine();
        }
        else if (near != bondPrimaryAtom)
        {
            BondInfo existing = FindBondBetween(bondPrimaryAtom, near);
            if (existing != null)
                UpgradeBond(existing);
            else
                CreateBond(bondPrimaryAtom, near, 1);
            CancelBondTool();
        }
    }

    private void CreateBond(BuilderAtom a, BuilderAtom b, int order)
    {
        if (!a.CanAcceptBondOrder(order) || !b.CanAcceptBondOrder(order))
        { Debug.Log("[Builder] Valenz-Limit erreicht"); return; }

        Vector3 dir = (b.transform.position - a.transform.position).normalized;
        if (dir.magnitude < 0.01f) dir = Vector3.right;
        float dist = a.atomRadius + b.atomRadius + bondLength;
        b.transform.position = a.transform.position + dir * dist;

        var bondObj = CreateBondVisual(a, b, order);
        bonds.Add(new BondInfo { atomA = a, atomB = b, bondOrder = order, lineObject = bondObj, creationTime = Time.time });
        a.AddBondOrder(order); b.AddBondOrder(order);
        b.SetBondConstraint(a, dist);
    }

    private void UpgradeBond(BondInfo bond)
    {
        if (bond.bondOrder >= 3) { Debug.Log("[Builder] Vierfachbindung nicht erlaubt!"); return; }
        if (!bond.atomA.CanAcceptBondOrder(1) || !bond.atomB.CanAcceptBondOrder(1)) return;

        bond.atomA.AddBondOrder(1); bond.atomB.AddBondOrder(1);
        bond.bondOrder++;
        if (bond.lineObject != null) Destroy(bond.lineObject);
        bond.lineObject = CreateBondVisual(bond.atomA, bond.atomB, bond.bondOrder);
    }

    private void DowngradeBond(BondInfo bond)
    {
        if (bond.bondOrder <= 1) return;
        bond.atomA.RemoveBondOrder(1); bond.atomB.RemoveBondOrder(1);
        bond.bondOrder--;
        if (bond.lineObject != null) Destroy(bond.lineObject);
        bond.lineObject = CreateBondVisual(bond.atomA, bond.atomB, bond.bondOrder);
        Debug.Log($"[Builder] Bond downgraded to {bond.bondOrder}x: {bond.atomA.elementSymbol}-{bond.atomB.elementSymbol}");
    }

    private GameObject CreateBondVisual(BuilderAtom a, BuilderAtom b, int order)
    {
        var obj = new GameObject($"Bond_{a.elementSymbol}_{b.elementSymbol}_x{order}");
        obj.transform.SetParent(builderRoot.transform);

        float w = 0.006f, off = 0.008f;
        if (order == 1)
        {
            AddLine(obj, a.transform.position, b.transform.position, w);
        }
        else if (order == 2)
        {
            Vector3 p = GetPerp(a.transform.position, b.transform.position);
            AddLine(obj, a.transform.position + p * off * 0.5f, b.transform.position + p * off * 0.5f, w * 0.8f);
            AddLine(obj, a.transform.position - p * off * 0.5f, b.transform.position - p * off * 0.5f, w * 0.8f);
        }
        else
        {
            Vector3 p = GetPerp(a.transform.position, b.transform.position);
            AddLine(obj, a.transform.position, b.transform.position, w * 0.7f);
            AddLine(obj, a.transform.position + p * off, b.transform.position + p * off, w * 0.7f);
            AddLine(obj, a.transform.position - p * off, b.transform.position - p * off, w * 0.7f);
        }
        return obj;
    }

    private void AddLine(GameObject parent, Vector3 from, Vector3 to, float width)
    {
        var obj = new GameObject("L");
        obj.transform.SetParent(parent.transform);
        var lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = 2; lr.startWidth = width; lr.endWidth = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.black; lr.endColor = Color.black;
        lr.SetPosition(0, from); lr.SetPosition(1, to);
    }

    private Vector3 GetPerp(Vector3 a, Vector3 b)
    {
        Vector3 d = (b - a).normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(d, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        return Vector3.Cross(d, up).normalized;
    }

    private BondInfo FindBondBetween(BuilderAtom a, BuilderAtom b)
    {
        foreach (var bond in bonds)
            if ((bond.atomA == a && bond.atomB == b) || (bond.atomA == b && bond.atomB == a))
                return bond;
        return null;
    }

    private void CancelBondTool()
    {
        if (bondPrimaryAtom != null) { bondPrimaryAtom.SetHighlight(false); bondPrimaryAtom = null; }
        bondToolHand = null;
        if (bondPreviewLine != null) { Destroy(bondPreviewLine.gameObject); bondPreviewLine = null; }
    }

    private void CreateBondPreviewLine()
    {
        if (bondPreviewLine != null) Destroy(bondPreviewLine.gameObject);
        var obj = new GameObject("BondPreview");
        obj.transform.SetParent(builderRoot.transform);
        bondPreviewLine = obj.AddComponent<LineRenderer>();
        bondPreviewLine.positionCount = 2;
        bondPreviewLine.startWidth = 0.004f; bondPreviewLine.endWidth = 0.004f;
        bondPreviewLine.material = new Material(Shader.Find("Sprites/Default"));
        bondPreviewLine.startColor = new Color(0.15f, 0.15f, 0.15f);
        bondPreviewLine.endColor = new Color(0.15f, 0.15f, 0.15f);
    }

    private void UpdateBondPreviewLine()
    {
        if (bondPreviewLine == null) return;
        BuilderAtom primary = bondPrimaryAtom ?? unbondPrimaryAtom;
        if (primary == null) return;

        bondPreviewLine.SetPosition(0, primary.transform.position);
        Vector3 fp = primary.transform.position + Vector3.forward * 0.05f;
        if (bondToolHand != null && bondToolHand.IsTrackedDataValid)
            if (bondToolHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
                fp = tip.position;
        bondPreviewLine.SetPosition(1, fp);
    }

    private void UpdateBondVisuals()
    {
        foreach (var bond in bonds)
        {
            if (bond.lineObject == null || bond.atomA == null || bond.atomB == null) continue;
            var lines = bond.lineObject.GetComponentsInChildren<LineRenderer>();
            Vector3 p = GetPerp(bond.atomA.transform.position, bond.atomB.transform.position);
            float off = 0.008f;

            if (bond.bondOrder == 1 && lines.Length >= 1)
            {
                lines[0].SetPosition(0, bond.atomA.transform.position);
                lines[0].SetPosition(1, bond.atomB.transform.position);
            }
            else if (bond.bondOrder == 2 && lines.Length >= 2)
            {
                lines[0].SetPosition(0, bond.atomA.transform.position + p * off * 0.5f);
                lines[0].SetPosition(1, bond.atomB.transform.position + p * off * 0.5f);
                lines[1].SetPosition(0, bond.atomA.transform.position - p * off * 0.5f);
                lines[1].SetPosition(1, bond.atomB.transform.position - p * off * 0.5f);
            }
            else if (bond.bondOrder == 3 && lines.Length >= 3)
            {
                lines[0].SetPosition(0, bond.atomA.transform.position);
                lines[0].SetPosition(1, bond.atomB.transform.position);
                lines[1].SetPosition(0, bond.atomA.transform.position + p * off);
                lines[1].SetPosition(1, bond.atomB.transform.position + p * off);
                lines[2].SetPosition(0, bond.atomA.transform.position - p * off);
                lines[2].SetPosition(1, bond.atomB.transform.position - p * off);
            }
        }
    }

    // ═══════════ UNBOND TOOL ═══════════

    private void HandleUnbondToolPinch(Vector3 fp)
    {
        BuilderAtom near = FindNearestAtom(fp, 0.08f);
        if (near == null) return;

        if (unbondPrimaryAtom == null)
        {
            unbondPrimaryAtom = near;
            unbondPrimaryAtom.SetHighlight(true);
            bondToolHand = rightHand;
            CreateBondPreviewLine();

            highlightedBonds.Clear();
            foreach (var b in bonds)
            {
                if (b.atomA == near || b.atomB == near)
                {
                    highlightedBonds.Add(b);
                    foreach (var lr in b.lineObject.GetComponentsInChildren<LineRenderer>())
                    { lr.startColor = Color.red; lr.endColor = Color.red; lr.startWidth *= 1.5f; lr.endWidth *= 1.5f; }
                }
            }
        }
        else if (near != unbondPrimaryAtom)
        {
            var bond = FindBondBetween(unbondPrimaryAtom, near);
            if (bond != null)
            {
                if (bond.bondOrder > 1)
                    DowngradeBond(bond);   // Doppel→Einfach, Dreifach→Doppel
                else
                    RemoveBond(bond);       // Einfach→weg
            }
            CancelUnbondTool();
        }
    }

    private void CancelUnbondTool()
    {
        if (unbondPrimaryAtom != null) { unbondPrimaryAtom.SetHighlight(false); unbondPrimaryAtom = null; }
        bondToolHand = null;
        foreach (var b in highlightedBonds)
        {
            if (b.lineObject == null) continue;
            foreach (var lr in b.lineObject.GetComponentsInChildren<LineRenderer>())
            { lr.startColor = Color.black; lr.endColor = Color.black; lr.startWidth = 0.006f; lr.endWidth = 0.006f; }
        }
        highlightedBonds.Clear();
        if (bondPreviewLine != null) { Destroy(bondPreviewLine.gameObject); bondPreviewLine = null; }
    }

    // ═══════════ DELETE TOOL ═══════════

    private void HandleDeleteToolPinch(Vector3 fp)
    {
        BuilderAtom near = FindNearestAtom(fp, 0.06f);
        if (near != null) DeleteAtom(near);
    }

    public void DeleteAtom(BuilderAtom atom)
    {
        for (int i = bonds.Count - 1; i >= 0; i--)
            if (bonds[i].atomA == atom || bonds[i].atomB == atom)
                RemoveBond(bonds[i]);
        placedAtoms.Remove(atom);
        Destroy(atom.gameObject);
    }

    private void RemoveBond(BondInfo bond)
    {
        if (bond.atomA != null) bond.atomA.RemoveBondOrder(bond.bondOrder);
        if (bond.atomB != null) { bond.atomB.RemoveBondOrder(bond.bondOrder); bond.atomB.SetBondConstraint(null, 0); }
        if (bond.lineObject != null) Destroy(bond.lineObject);
        bonds.Remove(bond);
    }

    // ═══════════ CHARGE TOOL ═══════════

    private void HandleChargePinch(Vector3 fp, int delta)
    {
        BuilderAtom near = FindNearestAtom(fp, 0.06f);
        if (near != null) near.ModifyCharge(delta);
    }

    // ═══════════ COMPILE ═══════════

    public void CompileMolecule()
    {
        if (placedAtoms.Count == 0)
        {
            Debug.Log("[Builder] Keine Atome platziert!");
            return;
        }

        bool allValid = true;
        List<BuilderAtom> invalid = new List<BuilderAtom>();
        foreach (var atom in placedAtoms)
        {
            int expected = atom.MaxBonds + atom.FormalCharge;
            if (expected < 0) expected = 0;
            bool ok = atom.IsOctetSatisfied();
            Debug.Log($"[Builder] Check: {atom.elementSymbol} charge={atom.FormalCharge} bonds={atom.TotalBondOrderSum} expected={expected} → {(ok ? "OK" : "FAIL")}");
            if (!ok) { allValid = false; invalid.Add(atom); }
        }

        if (!allValid)
        {
            foreach (var a in invalid) a.SetHighlight(true);
            string errorText = $"Fehler: {invalid.Count} Atome verletzen die Oktettregel!";
            Debug.Log($"[Builder] Ungültig! {errorText}");
            
            if (webSocket != null)
                webSocket.BroadcastMessage($"{{\"type\":\"status\",\"message\":\"{errorText}\"}}");
                
            // === VR ERROR MESSAGE PIPELINE ===
            if (builderRoot != null)
            {
                var errObj = new GameObject("VR_ErrorMsg");
                errObj.transform.SetParent(builderRoot.transform);
                // Position it somewhat in front of the periodic table, near eye level
                Camera cam = Camera.main;
                if (cam != null)
                {
                    errObj.transform.position = cam.transform.position + cam.transform.forward * 0.45f + Vector3.down * 0.1f;
                    errObj.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
                }
                
                var tm = errObj.AddComponent<TextMesh>();
                tm.text = errorText;
                tm.color = Color.red;
                tm.characterSize = 0.005f;
                tm.fontSize = 40;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                
                // Add a black background for readability
                var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bg.transform.SetParent(errObj.transform, false);
                bg.transform.localPosition = new Vector3(0, 0, 0.01f);
                bg.transform.localScale = new Vector3(0.5f, 0.08f, 1f);
                Destroy(bg.GetComponent<Collider>());
                var mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = new Color(0, 0, 0, 0.8f);
                bg.GetComponent<Renderer>().material = mat;
                
                Destroy(errObj, 3.5f); // Automatically disappear after 3.5 seconds
            }

            // Highlights nach 3 Sekunden zurücksetzen
            StartCoroutine(ResetHighlightsAfterDelay(invalid, 3f));
            return;
        }

        Debug.Log("[Builder] Molekül ist valide! Konvertiere und rendere...");
        MoleculeData molData = ConvertToMoleculeData();
        
        // VSEPR-Optimierung: Räumlichen Bau korrigieren
        MoleculeOptimizer.Optimize(molData);
        Debug.Log("[Builder] VSEPR-Optimierung angewendet.");
        
        StopBuilder();

         // Use MoleculeLibrary pipeline for proper positioning, rotation, clear support
        var lib = moleculeLibrary ?? FindObjectOfType<MoleculeLibrary>();
        if (lib != null)
        {
            // Find renderer (might be inactive since StartBuilder disabled it)
            var renderer = lib.renderer ?? FindObjectsOfType<MoleculeRenderer>(true)[0];
            if (renderer != null)
            {
                renderer.gameObject.SetActive(true);
                
                // Disable plane (white quad) for builder molecules
                var planeAlign = renderer.GetComponent<MoleculePlaneAlignment>();
                if (planeAlign != null)
                {
                    planeAlign.showPlaneInVR = false;
                    planeAlign.SetPlaneVisibility(false);
                }
                renderer.enableStereoDisplay = false;
                
                Debug.Log($"[Builder] Renderer ready: active={renderer.gameObject.activeInHierarchy}, meshCombine=OFF");
            }
            lib.DisplayBuilderMolecule(molData);
        }
        else
        {
            // Fallback if no library found
            var rendererArray = FindObjectsOfType<MoleculeRenderer>(true);
            if (rendererArray.Length > 0)
            {
                var renderer = rendererArray[0];
                renderer.gameObject.SetActive(true);
                renderer.enableStereoDisplay = false;
                renderer.RenderMolecule(molData);
                // Position near camera
                Camera cam = Camera.main;
                if (cam != null)
                    renderer.transform.position = cam.transform.position + cam.transform.forward * 0.5f;
            }
        }

        // Force HandRotationController to re-find the renderer
        var hrc = FindObjectOfType<HandRotationController>();
        if (hrc != null)
        {
            hrc.ForceRefreshReferences();
        }
    }

    private System.Collections.IEnumerator ResetHighlightsAfterDelay(List<BuilderAtom> atoms, float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (var a in atoms)
            if (a != null) a.SetHighlight(false);
    }

    private MoleculeData ConvertToMoleculeData()
    {
        var mol = new MoleculeData();
        mol.name = "Custom Molecule";

        Dictionary<string, int> counts = new Dictionary<string, int>();
        Dictionary<BuilderAtom, int> idMap = new Dictionary<BuilderAtom, int>();

        for (int i = 0; i < placedAtoms.Count; i++)
        {
            var ba = placedAtoms[i];
            idMap[ba] = i;
            Vector3 rel = ba.transform.position;
            if (placedAtoms.Count > 0) rel -= placedAtoms[0].transform.position;
            mol.atoms.Add(new AtomData(i, ba.elementSymbol, rel));
            if (counts.ContainsKey(ba.elementSymbol)) counts[ba.elementSymbol]++;
            else counts[ba.elementSymbol] = 1;
        }

        foreach (var bond in bonds)
        {
            if (!idMap.ContainsKey(bond.atomA) || !idMap.ContainsKey(bond.atomB)) continue;
            BondType bt = bond.bondOrder == 2 ? BondType.Double : bond.bondOrder == 3 ? BondType.Triple : BondType.Single;
            mol.bonds.Add(new BondData(idMap[bond.atomA], idMap[bond.atomB], bt));
        }

        string formula = "";
        string[] hill = { "C", "H" };
        foreach (string e in hill) { if (counts.ContainsKey(e)) { formula += e + (counts[e] > 1 ? counts[e].ToString() : ""); counts.Remove(e); } }
        var rest = new List<string>(counts.Keys); rest.Sort();
        foreach (string e in rest) formula += e + (counts[e] > 1 ? counts[e].ToString() : "");
        mol.formula = formula;
        return mol;
    }

    // ═══════════ DRAG ═══════════

    private void StartDragging(BuilderAtom atom, Hand hand)
    { isDragging = true; draggedAtom = atom; draggingHand = hand; atom.SetDragState(true); }

    private void ReleaseDraggedAtom()
    {
        if (draggedAtom != null) draggedAtom.SetDragState(false);
        isDragging = false; draggedAtom = null; draggingHand = null;
    }

    private void UpdateDragPosition()
    {
        if (draggingHand == null || !draggingHand.IsTrackedDataValid) { ReleaseDraggedAtom(); return; }
        if (draggingHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
        {
            Vector3 target = tip.position;
            if (draggedAtom.BondedTo != null)
            {
                Vector3 d = (target - draggedAtom.BondedTo.transform.position).normalized;
                target = draggedAtom.BondedTo.transform.position + d * draggedAtom.BondRadius;
            }
            draggedAtom.transform.position = Vector3.Lerp(draggedAtom.transform.position, target, Time.deltaTime * 25f);
        }
    }

    // ═══════════ MOVE TABLE ═══════════

    private Vector3 lastHandPos;

    private void StartMovingTable(Vector3 fingerPos, Hand hand)
    {
        if (periodicTable == null || periodicTable.TableTransform == null) return;
        isMovingTable = true;
        movingHand = hand;
        
        if (hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            lastHandPos = tip.position;
        else
            lastHandPos = fingerPos;
    }

    private void UpdateMoveTable()
    {
        if (movingHand == null || !movingHand.IsTrackedDataValid)
        { isMovingTable = false; movingHand = null; return; }

        if (movingHand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
        {
            Vector3 delta = tip.position - lastHandPos;
            periodicTable.MoveTableDelta(delta);
            periodicTable.FaceCamera();
            lastHandPos = tip.position;
        }
    }

    // ═══════════ HELPERS ═══════════

    private BuilderAtom FindNearestAtom(Vector3 pos, float maxDist)
    {
        BuilderAtom best = null; float bd = maxDist;
        foreach (var a in placedAtoms)
        {
            if (a == null) continue;
            float d = Vector3.Distance(pos, a.transform.position);
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    public BuilderAtom SpawnAtom(string symbol, Vector3 position)
    {
        if (builderRoot == null) return null;
        var obj = new GameObject($"Atom_{symbol}_{placedAtoms.Count}");
        obj.transform.SetParent(builderRoot.transform);
        obj.transform.position = position;

        var atom = obj.AddComponent<BuilderAtom>();
        atom.Initialize(symbol, elementDatabase);
        placedAtoms.Add(atom);
        return atom;
    }

    public void UnregisterAtom(BuilderAtom atom) { placedAtoms.Remove(atom); }

    private void FindHands()
    {
        if (rightHand != null && leftHand != null) return;
        foreach (var h in FindObjectsOfType<Hand>())
        {
            if (h.Handedness == Handedness.Right && rightHand == null) rightHand = h;
            else if (h.Handedness == Handedness.Left && leftHand == null) leftHand = h;
        }
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
