using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-right HUD panel that shows the selected agent's name, X/Y/Z stat
/// bars, and a Delete Agent button.
///
/// Panel appears on selection, disappears on deselect — driven by polling
/// SelectionManager.Instance.SelectedAgent each frame.
///
/// Added to the scene automatically by SceneBuilder; no Inspector setup needed.
/// </summary>
public class AgentInfoUI : MonoBehaviour
{
    // ── Runtime references ────────────────────────────────────────────────────
    private GameObject _panel;
    private Text       _agentNameText;

    private (RectTransform fill, Text valueLabel) _rowX, _rowY, _rowZ;

    // Cached so the delete button still has a reference even if SelectionManager
    // clears SelectedAgent before the button's onClick fires.
    private AIAgent _trackedAgent;

    // ── Palette ───────────────────────────────────────────────────────────────
    static readonly Color C_Panel  = new Color(0.08f, 0.09f, 0.11f, 0.93f);
    static readonly Color C_BarBg  = new Color(0.20f, 0.20f, 0.23f, 1.00f);
    static readonly Color C_X      = new Color(0.95f, 0.40f, 0.20f);   // orange-red
    static readonly Color C_Y      = new Color(0.25f, 0.60f, 0.95f);   // blue
    static readonly Color C_Z      = new Color(0.25f, 0.85f, 0.45f);   // green
    static readonly Color C_Text   = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color C_Dim    = new Color(0.60f, 0.60f, 0.65f);
    static readonly Color C_Delete = new Color(0.75f, 0.20f, 0.20f);   // red

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        BuildUI();
        _panel.SetActive(false);
    }

    void Update()
    {
        var agent = SelectionManager.Instance?.SelectedAgent;

        if (agent == null)
        {
            _trackedAgent = null;
            _panel.SetActive(false);
            return;
        }

        _trackedAgent = agent;   // keep a direct reference for the delete button
        _panel.SetActive(true);
        _agentNameText.text = agent.name;
        Refresh(_rowX, agent.valueX);
        Refresh(_rowY, agent.valueY);
        Refresh(_rowZ, agent.valueZ);
    }

    // ── Delete action ─────────────────────────────────────────────────────────

    private void OnDeleteAgentClicked()
    {
        // Use the cached reference — SelectionManager may have already cleared
        // SelectedAgent by the time this button handler fires (same-frame click).
        var agent = _trackedAgent;
        if (agent == null) return;

        _trackedAgent = null;
        SelectionManager.Instance.DeselectCurrent();
        Destroy(agent.gameObject);
    }

    // ── Bar update ────────────────────────────────────────────────────────────

    static void Refresh((RectTransform fill, Text label) row, float v)
    {
        row.fill.anchorMax = new Vector2(Mathf.Clamp01(v / 100f), 1f);
        row.fill.sizeDelta = Vector2.zero;
        row.label.text     = ((int)v).ToString();
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas
        var cGo    = UIObject("AgentInfoCanvas", transform);
        var canvas = cGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        cGo.AddComponent<GraphicRaycaster>();

        // ── Panel — bottom-right ──────────────────────────────────────────────
        _panel = UIObject("Panel", cGo.transform);
        var pRT = _panel.GetComponent<RectTransform>();
        pRT.anchorMin        = new Vector2(1f, 0f);
        pRT.anchorMax        = new Vector2(1f, 0f);
        pRT.pivot            = new Vector2(1f, 0f);
        pRT.anchoredPosition = new Vector2(-16f, 16f);
        pRT.sizeDelta        = new Vector2(220f, 178f);
        Img(_panel, C_Panel);

        var vl = _panel.AddComponent<VerticalLayoutGroup>();
        vl.padding               = new RectOffset(12, 12, 10, 10);
        vl.spacing               = 8;
        vl.childControlWidth     = true;
        vl.childControlHeight    = true;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        // Agent name title
        var titleGo = UIObject("Title", _panel.transform);
        LE(titleGo, preferredHeight: 20);
        _agentNameText = Txt(titleGo, "—", 13, FontStyle.Bold, C_Text, TextAnchor.MiddleCenter);

        // Stat rows
        _rowX = StatRow(_panel, "X", C_X);
        _rowY = StatRow(_panel, "Y", C_Y);
        _rowZ = StatRow(_panel, "Z", C_Z);

        // ── Delete Agent button ───────────────────────────────────────────────
        var btnGo = UIObject("DeleteButton", _panel.transform);
        LE(btnGo, preferredHeight: 34);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = C_Delete;

        var btn    = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = C_Delete;
        colors.highlightedColor = new Color(0.90f, 0.28f, 0.28f);
        colors.pressedColor     = new Color(0.58f, 0.14f, 0.14f);
        btn.colors        = colors;
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(OnDeleteAgentClicked);

        var lblGo = UIObject("Label", btnGo.transform);
        var lblRT = lblGo.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
        Txt(lblGo, "Delete Agent", 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    (RectTransform, Text) StatRow(GameObject parent, string letter, Color color)
    {
        var row = UIObject($"Row{letter}", parent.transform);
        LE(row, preferredHeight: 22);

        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing               = 6;
        hl.childControlWidth     = true;
        hl.childControlHeight    = true;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = true;

        var lbl = UIObject("Label", row.transform);
        LE(lbl, preferredWidth: 16, flexibleWidth: 0);
        Txt(lbl, letter, 12, FontStyle.Bold, color, TextAnchor.MiddleLeft);

        var track = UIObject("Track", row.transform);
        LE(track, preferredWidth: 100, flexibleWidth: 1);
        Img(track, C_BarBg);

        var fillGo = UIObject("Fill", track.transform);
        var fillRT = fillGo.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Img(fillGo, color);

        var val     = UIObject("Value", row.transform);
        LE(val, preferredWidth: 28, flexibleWidth: 0);
        var valText = Txt(val, "0", 11, FontStyle.Normal, C_Dim, TextAnchor.MiddleRight);

        return (fillRT, valText);
    }

    // ── Low-level helpers ─────────────────────────────────────────────────────

    static GameObject UIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void LE(GameObject go, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1)
    {
        var le = go.AddComponent<LayoutElement>();
        if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        if (flexibleWidth   >= 0) le.flexibleWidth   = flexibleWidth;
    }

    static void Img(GameObject go, Color col) => go.AddComponent<Image>().color = col;

    static Text Txt(GameObject go, string content, int size,
                    FontStyle style, Color col, TextAnchor align)
    {
        var t             = go.AddComponent<Text>();
        t.text            = content;
        t.fontSize        = size;
        t.fontStyle       = style;
        t.color           = col;
        t.alignment       = align;
        t.supportRichText = false;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) t.font = font;
        return t;
    }
}
