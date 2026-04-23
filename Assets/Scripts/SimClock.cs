using UnityEngine;

/// <summary>
/// Singleton that tracks the in-game 24-hour clock.
///
/// Default rate: 1 real second = 1 game minute  (a full day takes 24 real minutes).
/// Change <see cref="minutesPerSecond"/> at runtime to speed up or slow down time.
///
/// Agents and other systems subscribe to <see cref="OnPauseChanged"/> and
/// <see cref="OnTimeChanged"/> to react without polling every frame.
/// </summary>
public class SimClock : MonoBehaviour
{
    public static SimClock Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Time Settings")]
    [Tooltip("In-game minutes that pass per real second. " +
             "1 = 1 min/sec (full day = 24 real minutes). " +
             "Increase to accelerate time, decrease to slow it down.")]
    public float minutesPerSecond = 1f;

    [Tooltip("Hour the clock starts at when the scene loads (0–23).")]
    [Range(0, 23)]
    public int startHour = 6;

    // ── State ─────────────────────────────────────────────────────────────────

    private float _totalMinutes;

    /// <summary>True while the clock (and all agents) are paused.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Current in-game hour (0–23).</summary>
    public int Hour => (int)(_totalMinutes / 60f) % 24;

    /// <summary>Current in-game minute (0–59).</summary>
    public int Minute => (int)(_totalMinutes % 60f);

    /// <summary>Raw accumulated minutes — useful for save/load or day counting.</summary>
    public float TotalMinutes => _totalMinutes;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired every frame the clock is running. Args: (hour, minute).</summary>
    public event System.Action<int, int> OnTimeChanged;

    /// <summary>Fired when pause state changes. Arg: true = just paused, false = just resumed.</summary>
    public event System.Action<bool> OnPauseChanged;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _totalMinutes = startHour * 60f;
    }

    private void Update()
    {
        if (IsPaused) return;

        _totalMinutes += minutesPerSecond * Time.deltaTime;
        if (_totalMinutes >= 1440f) _totalMinutes -= 1440f; // wrap at midnight

        OnTimeChanged?.Invoke(Hour, Minute);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Pause the clock and broadcast to all listeners.</summary>
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        OnPauseChanged?.Invoke(true);
    }

    /// <summary>Resume the clock and broadcast to all listeners.</summary>
    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        OnPauseChanged?.Invoke(false);
    }

    /// <summary>Toggle between paused and running.</summary>
    public void TogglePause()
    {
        if (IsPaused) Resume(); else Pause();
    }

    /// <summary>
    /// Change how fast time passes.
    /// Examples: 1 = normal, 2 = double speed, 0.5 = half speed.
    /// </summary>
    public void SetSpeed(float newMinutesPerSecond)
    {
        minutesPerSecond = Mathf.Max(0f, newMinutesPerSecond);
    }

    /// <summary>Jump to a specific time of day.</summary>
    public void SetTime(int hour, int minute = 0)
    {
        _totalMinutes = Mathf.Clamp(hour, 0, 23) * 60f + Mathf.Clamp(minute, 0, 59);
    }
}
