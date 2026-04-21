using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Unity.AI.Navigation;

/// <summary>
/// Adds a runtime "Add Module" button to the screen.
/// When clicked it instantiates a copy of <see cref="modulePrefab"/>, snaps it
/// to the next open connector named <see cref="targetPortId"/> on any module
/// already in the scene, then rebakes the NavMesh so agents can cross.
///
/// Setup after running Build Scene:
///   1. Select the "Game Scripts" GameObject in the Hierarchy.
///   2. In the Inspector find this component.
///   3. Drag your TestRoom prefab into the "Module Prefab" slot.
///   4. Make sure the prefab has a StationModule + ModuleConnectors set up.
/// </summary>
public class StationBuilderUI : MonoBehaviour
{
    [Header("Module to add")]
    [Tooltip("The room prefab to instantiate. Must have a StationModule component.")]
    public GameObject modulePrefab;

    [Header("Connection ports")]
    [Tooltip("Port ID on the EXISTING module to attach to (e.g. 'South').")]
    public string targetPortId = "South";

    [Tooltip("Port ID on the NEW module that faces the existing room (e.g. 'North').")]
    public string newModulePortId = "North";

    // ── UI references ─────────────────────────────────────────────────────────
    private Button _addButton;
    private Text   _statusText;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureEventSystem();
        BuildUI();
    }

    // ── Button action ─────────────────────────────────────────────────────────

    private void OnAddModuleClicked()
    {
        Debug.Log("[StationBuilderUI] Add Module button clicked.");
        if (modulePrefab == null)
        {
            SetStatus("No module prefab assigned!", Color.red);
            return;
        }

        // Find the first open connector with the matching port ID in the scene.
        ModuleConnector targetPort = FindAvailablePort(targetPortId);
        if (targetPort == null)
        {
            SetStatus($"No open '{targetPortId}' port found.", Color.red);
            return;
        }

        // Instantiate the new module under the SimStation Root if it exists,
        // otherwise at the scene root.
        var stationRoot = GameObject.Find("SimStation Root");
        var parent      = stationRoot != null ? stationRoot.transform : null;
        var newGo       = Instantiate(modulePrefab, Vector3.zero, Quaternion.identity, parent);
        newGo.name      = $"{modulePrefab.name} (added)";

        // Make sure it has a StationModule component.
        var newModule = newGo.GetComponent<StationModule>();
        if (newModule == null)
        {
            SetStatus("Prefab is missing a StationModule component.", Color.red);
            Destroy(newGo);
            return;
        }

        // Find the port on the new module to align.
        ModuleConnector myPort = newModule.GetConnector(newModulePortId);
        if (myPort == null)
        {
            SetStatus($"New module has no '{newModulePortId}' connector.", Color.red);
            Destroy(newGo);
            return;
        }

        // Snap the new module into place and link the connectors.
        newModule.ConnectTo(myPort, targetPort);

        // Rebake the whole-station NavMesh so agents can cross the new room.
        RebakeNavMesh();

        SetStatus($"Added {newModule.moduleName}!", Color.green);
        Debug.Log($"[StationBuilderUI] Module '{newModule.moduleName}' connected to '{targetPort.portId}' on '{targetPort.transform.root.name}'.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches all StationModules in the scene and returns the first open
    /// connector whose portId matches <paramref name="portId"/>.
    /// </summary>
    private ModuleConnector FindAvailablePort(string portId)
    {
        var allModules = FindObjectsByType<StationModule>(FindObjectsSortMode.None);
        foreach (var module in allModules)
        {
            var connector = module.GetConnector(portId);
            if (connector != null && connector.IsAvailable)
                return connector;
        }
        return null;
    }

    /// <summary>
    /// Rebuilds the NavMesh on every NavMeshSurface in the scene so agents
    /// can traverse the newly connected room.
    /// </summary>
    private void RebakeNavMesh()
    {
        var surfaces = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        foreach (var s in surfaces)
            s.BuildNavMesh();

        if (surfaces.Length == 0)
            Debug.LogWarning("[StationBuilderUI] No NavMeshSurface found — agents may not reach the new module.");
    }

    private void SetStatus(string msg, Color color)
    {
        if (_statusText == null) return;
        _statusText.text  = msg;
        _statusText.color = color;
    }

    // ── EventSystem ───────────────────────────────────────────────────────────

    /// <summary>
    /// Unity UI buttons only fire if an EventSystem exists in the scene.
    /// SceneBuilder doesn't create one, so we make sure one exists here.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>();
        Debug.Log("[StationBuilderUI] Created missing EventSystem.");
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas
        var cGo    = new GameObject("StationBuilderCanvas", typeof(RectTransform));
        cGo.transform.SetParent(transform, false);
        var canvas = cGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        // ── Panel — bottom-left ───────────────────────────────────────────────
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(cGo.transform, false);
        var pRT = panel.GetComponent<RectTransform>();
        pRT.anchorMin        = Vector2.zero;
        pRT.anchorMax        = Vector2.zero;
        pRT.pivot            = Vector2.zero;
        pRT.anchoredPosition = new Vector2(20f, 20f);
        pRT.sizeDelta        = new Vector2(220f, 100f);

        var pImg = panel.AddComponent<Image>();
        pImg.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.padding               = new RectOffset(12, 12, 10, 10);
        vl.spacing               = 8;
        vl.childControlWidth     = true;
        vl.childControlHeight    = true;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        // ── Add Module button ─────────────────────────────────────────────────
        var btnGo  = new GameObject("AddModuleButton", typeof(RectTransform));
        btnGo.transform.SetParent(panel.transform, false);
        var btnLE = btnGo.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 40f;

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.20f, 0.50f, 0.90f);

        _addButton = btnGo.AddComponent<Button>();
        var colors = _addButton.colors;
        colors.normalColor      = new Color(0.20f, 0.50f, 0.90f);
        colors.highlightedColor = new Color(0.30f, 0.60f, 1.00f);
        colors.pressedColor     = new Color(0.15f, 0.40f, 0.75f);
        _addButton.colors = colors;
        _addButton.targetGraphic = btnImg;
        _addButton.onClick.AddListener(OnAddModuleClicked);

        // Label must be a child — a GameObject can only have one Graphic component,
        // and the button already owns the Image.
        var btnLblGo = new GameObject("Label", typeof(RectTransform));
        btnLblGo.transform.SetParent(btnGo.transform, false);
        var btnLblRT = btnLblGo.GetComponent<RectTransform>();
        btnLblRT.anchorMin = Vector2.zero;
        btnLblRT.anchorMax = Vector2.one;
        btnLblRT.offsetMin = Vector2.zero;
        btnLblRT.offsetMax = Vector2.zero;
        MakeText(btnLblGo, "+ Add Module", 14, FontStyle.Bold,
                 Color.white, TextAnchor.MiddleCenter);

        // ── Status text ───────────────────────────────────────────────────────
        var statusGo = new GameObject("Status", typeof(RectTransform));
        statusGo.transform.SetParent(panel.transform, false);
        var statusLE = statusGo.AddComponent<LayoutElement>();
        statusLE.preferredHeight = 22f;
        _statusText = MakeText(statusGo, "Ready", 11, FontStyle.Normal,
                               new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter);
    }

    private static Text MakeText(GameObject go, string content, int size,
                                  FontStyle style, Color color, TextAnchor align)
    {
        var t         = go.AddComponent<Text>();
        t.text        = content;
        t.fontSize    = size;
        t.fontStyle   = style;
        t.color       = color;
        t.alignment   = align;
        t.supportRichText = false;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) t.font = font;
        return t;
    }
}
