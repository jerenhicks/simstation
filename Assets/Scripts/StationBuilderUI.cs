using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Bottom-left HUD panel with runtime station-management buttons:
///   • + Add Module — snaps a new room prefab onto the next open connector.
///   • + Add Agent  — spawns a new crew agent at the AgentSpawnPoint.
///
/// Setup after running Build Scene:
///   1. Select "Game Scripts" in the Hierarchy.
///   2. Find StationBuilderUI in the Inspector.
///   3. Drag your TestRoom prefab into the "Module Prefab" slot.
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

    // ── Private state ─────────────────────────────────────────────────────────
    private Text _statusText;
    private int  _agentCount = 0;

    // Agent appearance — matches what SceneBuilder used for consistency
    private static readonly Color AgentColor = new Color(0.45f, 0.55f, 0.95f);

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureEventSystem();
        BuildUI();
    }

    // ── Add Module ────────────────────────────────────────────────────────────

    private void OnAddModuleClicked()
    {
        if (modulePrefab == null)
        {
            SetStatus("No module prefab assigned!", Color.red);
            return;
        }

        ModuleConnector targetPort = FindAvailablePort(targetPortId);
        if (targetPort == null)
        {
            SetStatus($"No open '{targetPortId}' port found.", Color.red);
            return;
        }

        var stationRoot = GameObject.Find("SimStation Root");
        var parent      = stationRoot != null ? stationRoot.transform : null;
        var newGo       = Instantiate(modulePrefab, Vector3.zero, Quaternion.identity, parent);
        newGo.name      = $"{modulePrefab.name} (added)";

        var newModule = newGo.GetComponent<StationModule>();
        if (newModule == null)
        {
            SetStatus("Prefab missing StationModule component.", Color.red);
            Destroy(newGo);
            return;
        }

        ModuleConnector myPort = newModule.GetConnector(newModulePortId);
        if (myPort == null)
        {
            SetStatus($"New module has no '{newModulePortId}' connector.", Color.red);
            Destroy(newGo);
            return;
        }

        newModule.ConnectTo(myPort, targetPort);
        RebakeNavMesh();

        SetStatus($"Added {newModule.moduleName}!", Color.green);
    }

    // ── Add Agent ─────────────────────────────────────────────────────────────

    private void OnAddAgentClicked()
    {
        var spawnPoint = FindFirstObjectByType<AgentSpawnPoint>();
        if (spawnPoint == null)
        {
            SetStatus("No AgentSpawnPoint in scene!", Color.red);
            return;
        }

        Vector3 pos = spawnPoint.GetSpawnPosition();

        _agentCount++;
        var a = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        a.name = $"Agent {_agentCount}";

        // Parent under SimStation Root to keep the Hierarchy tidy
        var stationRoot = GameObject.Find("SimStation Root");
        if (stationRoot != null) a.transform.SetParent(stationRoot.transform);

        a.transform.position   = pos;
        a.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        // NavMesh Agent
        var nav = a.AddComponent<NavMeshAgent>();
        nav.speed           = 3.5f;
        nav.stoppingDistance = 0.6f;
        nav.angularSpeed    = 360f;
        nav.radius          = 0.25f;
        nav.height          = 1f;

        // AI behaviour
        a.AddComponent<AIAgent>();

        // Colour
        Colorize(a, AgentColor);

        SetStatus($"Spawned {a.name}", Color.green);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private void RebakeNavMesh()
    {
        var surfaces = FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        foreach (var s in surfaces)
            s.BuildNavMesh();

        if (surfaces.Length == 0)
            Debug.LogWarning("[StationBuilderUI] No NavMeshSurface found.");
    }

    private void SetStatus(string msg, Color color)
    {
        if (_statusText == null) return;
        _statusText.text  = msg;
        _statusText.color = color;
    }

    private static void Colorize(GameObject go, Color col)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat    = shader != null ? new Material(shader) : new Material(r.sharedMaterial);
        mat.SetColor("_BaseColor", col);
        mat.color       = col;
        r.sharedMaterial = mat;
    }

    // ── EventSystem ───────────────────────────────────────────────────────────

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>();
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
        pRT.sizeDelta        = new Vector2(220f, 152f); // taller for two buttons

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
        MakeButton(panel.transform, "AddModuleButton",
                   "+ Add Module", new Color(0.20f, 0.50f, 0.90f),
                   OnAddModuleClicked);

        // ── Add Agent button ──────────────────────────────────────────────────
        MakeButton(panel.transform, "AddAgentButton",
                   "+ Add Agent", new Color(0.18f, 0.55f, 0.35f),
                   OnAddAgentClicked);

        // ── Status text ───────────────────────────────────────────────────────
        var statusGo = new GameObject("Status", typeof(RectTransform));
        statusGo.transform.SetParent(panel.transform, false);
        var statusLE = statusGo.AddComponent<LayoutElement>();
        statusLE.preferredHeight = 22f;
        _statusText = MakeText(statusGo, "Ready", 11, FontStyle.Normal,
                               new Color(0.6f, 0.6f, 0.6f), TextAnchor.MiddleCenter);
    }

    private static void MakeButton(Transform parent, string goName,
                                    string label, Color baseColor,
                                    UnityEngine.Events.UnityAction onClick)
    {
        var btnGo = new GameObject(goName, typeof(RectTransform));
        btnGo.transform.SetParent(parent, false);
        var btnLE = btnGo.AddComponent<LayoutElement>();
        btnLE.preferredHeight = 40f;

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = baseColor;

        var btn    = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = baseColor;
        colors.highlightedColor = baseColor + new Color(0.1f, 0.1f, 0.1f, 0f);
        colors.pressedColor     = baseColor - new Color(0.05f, 0.05f, 0.05f, 0f);
        btn.colors       = colors;
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(onClick);

        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(btnGo.transform, false);
        var lblRT = lblGo.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
        MakeText(lblGo, label, 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private static Text MakeText(GameObject go, string content, int size,
                                  FontStyle style, Color color, TextAnchor align)
    {
        var t             = go.AddComponent<Text>();
        t.text            = content;
        t.fontSize        = size;
        t.fontStyle       = style;
        t.color           = color;
        t.alignment       = align;
        t.supportRichText = false;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) t.font = font;
        return t;
    }
}
