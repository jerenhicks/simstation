using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a top-right HUD panel at runtime that shows the selected agent's
/// X, Y, and Z values as live progress bars.
///
/// Panel appears on selection, disappears on deselect — driven entirely by
/// polling SelectionManager.Instance.SelectedAgent each frame.
///
/// Added to the scene automatically by SceneBuilder; no Inspector setup needed.
/// </summary>
public class AgentInfoUI : MonoBehaviour
{
    // ── Runtime references ────────────────────────────────────────────────────
    private GameObject panel;
    private Text        agentNameText;

    private (RectTransform fill, Text valueLabel) rowX, rowY, rowZ;

    // ── Palette ───────────────────────────────────────────────────────────────
    static readonly Color C_Panel = new Color(0.08f, 0.09f, 0.11f, 0.93f);
    static readonly Color C_BarBg = new Color(0.20f, 0.20f, 0.23f, 1.00f);
    static readonly Color C_X     = new Color(0.95f, 0.40f, 0.20f);  // orange-red
    static readonly Color C_Y     = new Color(0.25f, 0.60f, 0.95f);  // blue
    static readonly Color C_Z     = new Color(0.25f, 0.85f, 0.45f);  // green
    static readonly Color C_Text  = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color C_Dim   = new Color(0.60f, 0.60f, 0.65f);

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    void Awake()
    {
        BuildUI();
        panel.SetActive(false);
    }

    void Update()
    {
        var agent = SelectionManager.Instance?.SelectedAgent;

        if (agent == null)
        {
            panel.SetActive(false);
            return;
        }

        panel.SetActive(true);
        agentNameText.text = agent.name;
        Refresh(rowX, agent.valueX);
        Refresh(rowY, agent.valueY);
        Refresh(rowZ, agent.valueZ);
    }

    // ── Bar update ────────────────────────────────────────────────────────────
    static void Refresh((RectTransform fill, Text label) row, float v)
    {
        // Anchor-based fill: anchorMax.x = normalised value, sizeDelta zeroed
        // so the rect is sized purely by its anchors relative to the track.
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

        // Panel — anchored to top-right corner of the screen
        panel = UIObject("Panel", cGo.transform);
        var pRT = panel.GetComponent<RectTransform>();
        pRT.anchorMin        = Vector2.one;
        pRT.anchorMax        = Vector2.one;
        pRT.pivot            = Vector2.one;
        pRT.anchoredPosition = new Vector2(-16f, -16f);
        pRT.sizeDelta        = new Vector2(220f, 132f);
        Img(panel, C_Panel);

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.padding               = new RectOffset(12, 12, 10, 10);
        vl.spacing               = 8;
        vl.childControlWidth     = true;
        vl.childControlHeight    = true;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        // Agent name title
        var titleGo = UIObject("Title", panel.transform);
        LE(titleGo, preferredHeight: 20);
        agentNameText = Txt(titleGo, "—", 13, FontStyle.Bold, C_Text, TextAnchor.MiddleCenter);

        // Stat rows
        rowX = StatRow(panel, "X", C_X);
        rowY = StatRow(panel, "Y", C_Y);
        rowZ = StatRow(panel, "Z", C_Z);
    }

    (RectTransform, Text) StatRow(GameObject parent, string letter, Color color)
    {
        // Row: horizontal group — label | bar track | value
        var row = UIObject($"Row{letter}", parent.transform);
        LE(row, preferredHeight: 22);

        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing               = 6;
        hl.childControlWidth     = true;
        hl.childControlHeight    = true;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = true;

        // Letter label
        var lbl = UIObject("Label", row.transform);
        LE(lbl, preferredWidth: 16, flexibleWidth: 0);
        Txt(lbl, letter, 12, FontStyle.Bold, color, TextAnchor.MiddleLeft);

        // Bar track (background)
        var track = UIObject("Track", row.transform);
        LE(track, preferredWidth: 100, flexibleWidth: 1);
        Img(track, C_BarBg);

        // Fill — child of track, width driven by anchorMax.x
        var fillGo = UIObject("Fill", track.transform);
        var fillRT = fillGo.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f); // starts at 0%
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Img(fillGo, color);

        // Numeric value (right-aligned)
        var val = UIObject("Value", row.transform);
        LE(val, preferredWidth: 28, flexibleWidth: 0);
        var valText = Txt(val, "0", 11, FontStyle.Normal, C_Dim, TextAnchor.MiddleRight);

        return (fillRT, valText);
    }

    // ── Low-level helpers ─────────────────────────────────────────────────────

    /// Creates a GameObject with a RectTransform already attached.
    static GameObject UIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// Adds a LayoutElement and optionally sets preferred / flexible sizes.
    static void LE(GameObject go, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1)
    {
        var le = go.AddComponent<LayoutElement>();
        if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
        if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        if (flexibleWidth   >= 0) le.flexibleWidth   = flexibleWidth;
    }

    /// Adds an Image component with the given colour.
    static void Img(GameObject go, Color col)
    {
        go.AddComponent<Image>().color = col;
    }

    /// Adds a Text component using Unity's built-in font.
    static Text Txt(GameObject go, string content, int size,
                    FontStyle style, Color col, TextAnchor align)
    {
        var t         = go.AddComponent<Text>();
        t.text        = content;
        t.fontSize    = size;
        t.fontStyle   = style;
        t.color       = col;
        t.alignment   = align;
        t.supportRichText = false;

        // Unity 6 built-in font; falls back gracefully if unavailable
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) t.font = font;

        return t;
    }
}
