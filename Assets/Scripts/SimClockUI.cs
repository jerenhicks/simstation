using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a top-center HUD panel showing the current in-game time and a
/// pause / play button that controls <see cref="SimClock"/>.
///
/// Added automatically by SceneBuilder — no manual setup needed.
/// </summary>
public class SimClockUI : MonoBehaviour
{
    // ── UI refs ───────────────────────────────────────────────────────────────
    private Text   _timeText;
    private Button _pauseButton;
    private Text   _pauseLabel;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        // Start() is called after ALL components in the scene have finished
        // Awake/OnEnable, so SimClock.Instance is guaranteed to exist here.
        if (SimClock.Instance != null)
        {
            SimClock.Instance.OnTimeChanged  += HandleTimeChanged;
            SimClock.Instance.OnPauseChanged += HandlePauseChanged;

            // Sync the display to whatever time the clock started at
            // (avoids showing "06:00" when startHour is set to something else).
            HandleTimeChanged(SimClock.Instance.Hour, SimClock.Instance.Minute);
            HandlePauseChanged(SimClock.Instance.IsPaused);
        }
        else
        {
            Debug.LogWarning("[SimClockUI] No SimClock found in scene — add one to Game Scripts.");
        }
    }

    private void OnDestroy()
    {
        if (SimClock.Instance != null)
        {
            SimClock.Instance.OnTimeChanged  -= HandleTimeChanged;
            SimClock.Instance.OnPauseChanged -= HandlePauseChanged;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleTimeChanged(int hour, int minute)
    {
        if (_timeText != null)
            _timeText.text = $"{hour:D2}:{minute:D2}";
    }

    private void HandlePauseChanged(bool paused)
    {
        if (_pauseLabel != null)
            _pauseLabel.text = paused ? "▶" : "⏸";

        // Dim the time text while paused so the state is obvious
        if (_timeText != null)
            _timeText.color = paused ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var cGo    = new GameObject("SimClockCanvas", typeof(RectTransform));
        cGo.transform.SetParent(transform, false);
        var canvas = cGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = cGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        cGo.AddComponent<GraphicRaycaster>();

        // ── Panel — top-centre ────────────────────────────────────────────────
        var panel = new GameObject("ClockPanel", typeof(RectTransform));
        panel.transform.SetParent(cGo.transform, false);
        var pRT = panel.GetComponent<RectTransform>();
        pRT.anchorMin        = new Vector2(0f, 1f);
        pRT.anchorMax        = new Vector2(0f, 1f);
        pRT.pivot            = new Vector2(0f, 1f);
        pRT.anchoredPosition = new Vector2(16f, -12f);
        pRT.sizeDelta        = new Vector2(200f, 52f);

        var pImg   = panel.AddComponent<Image>();
        pImg.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

        var hLayout = panel.AddComponent<HorizontalLayoutGroup>();
        hLayout.padding              = new RectOffset(12, 8, 8, 8);
        hLayout.spacing              = 10;
        hLayout.childControlWidth    = false;
        hLayout.childControlHeight   = true;
        hLayout.childForceExpandWidth  = false;
        hLayout.childForceExpandHeight = true;
        hLayout.childAlignment       = TextAnchor.MiddleCenter;

        // ── Time label ────────────────────────────────────────────────────────
        var timeGo = new GameObject("TimeLabel", typeof(RectTransform));
        timeGo.transform.SetParent(panel.transform, false);
        var timeLE = timeGo.AddComponent<LayoutElement>();
        timeLE.preferredWidth = 110f;
        _timeText = MakeText(timeGo, "--:--", 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        // ── Pause / Play button ───────────────────────────────────────────────
        var btnGo  = new GameObject("PauseButton", typeof(RectTransform));
        btnGo.transform.SetParent(panel.transform, false);
        var btnLE = btnGo.AddComponent<LayoutElement>();
        btnLE.preferredWidth = 38f;

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.25f, 0.25f, 0.30f);

        _pauseButton = btnGo.AddComponent<Button>();
        var colors = _pauseButton.colors;
        colors.normalColor      = new Color(0.25f, 0.25f, 0.30f);
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.42f);
        colors.pressedColor     = new Color(0.18f, 0.18f, 0.22f);
        _pauseButton.colors      = colors;
        _pauseButton.targetGraphic = btnImg;
        _pauseButton.onClick.AddListener(OnPauseClicked);

        // Label as a child (can't add Text to a GameObject that already has Image)
        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(btnGo.transform, false);
        var lblRT = lblGo.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
        _pauseLabel = MakeText(lblGo, "⏸", 18, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
    }

    private void OnPauseClicked()
    {
        if (SimClock.Instance != null)
            SimClock.Instance.TogglePause();
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
