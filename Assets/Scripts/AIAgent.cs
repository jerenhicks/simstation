using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple AI agent that wanders between CrewConsoles, occupies one at a time,
/// waits to simulate "doing work", then moves on.
///
/// Requires: NavMeshAgent component on the same GameObject.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AIAgent : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector Settings
    // -----------------------------------------------------------------------

    [Header("Behavior")]
    [Tooltip("How many seconds the agent spends 'working' at a CrewConsole")]
    public float workDuration = 3f;

    [Tooltip("How long to wait before picking the next CrewConsole after finishing work")]
    public float idleDelay = 1f;

    [Header("Agent Values (0–100)")]
    [Tooltip("Restored by visiting X CrewConsoles. Starts full.")]
    public float valueX = 100f;

    [Tooltip("Restored by visiting Y CrewConsoles. Starts full.")]
    public float valueY = 100f;

    [Tooltip("Restored by visiting Z CrewConsoles. Starts full.")]
    public float valueZ = 100f;

    [Tooltip("How much each stat decays per in-game minute. " +
             "At the default clock speed (1 min/sec) a value of 1 drops ~1 point per real second.")]
    public float decayPerMinute = 2f;

    [Header("Wandering")]
    [Tooltip("Radius (in world units) around the agent to pick a random wander point while idle")]
    public float wanderRadius = 4f;

    [Header("Visual Feedback")]
    [Tooltip("Color while moving")]
    public Color movingColor = Color.blue;

    [Tooltip("Color while working at a CrewConsole")]
    public Color workingColor = Color.yellow;

    [Tooltip("Color while idle / waiting")]
    public Color idleColor = Color.gray;

    // -----------------------------------------------------------------------
    // Internal State
    // -----------------------------------------------------------------------

    private enum AgentState { Idle, MovingToCrewConsole, WorkingAtCrewConsole }

    private NavMeshAgent navAgent;
    private AgentState currentState = AgentState.Idle;
    private CrewConsole currentCrewConsole;
    private Renderer agentRenderer;
    private LineRenderer selectionRing;

    private static CrewConsole[] allCrewConsoles; // shared across all agents for efficiency

    // Selection ring appearance
    private const int   RING_SEGMENTS = 40;
    private const float RING_RADIUS   = 0.55f; // slightly wider than the agent capsule
    private const float RING_WIDTH    = 0.07f;
    private const float RING_Y_OFFSET = -0.45f; // local Y — sits just above the floor

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        navAgent      = GetComponent<NavMeshAgent>();
        agentRenderer = GetComponentInChildren<Renderer>();

        // Cache all CrewConsoles once (all agents share this reference)
        if (allCrewConsoles == null || allCrewConsoles.Length == 0)
            allCrewConsoles = FindObjectsByType<CrewConsole>(FindObjectsSortMode.None);

        // Subscribe to clock pause events
        if (SimClock.Instance != null)
            SimClock.Instance.OnPauseChanged += OnClockPauseChanged;

        BuildSelectionRing();
        SetColor(idleColor);
        StartCoroutine(AgentRoutine());
    }

    private void OnDestroy()
    {
        if (SimClock.Instance != null)
            SimClock.Instance.OnPauseChanged -= OnClockPauseChanged;
    }

    private void Update()
    {
        // Decay all stats over time, scaled to in-game minutes.
        // Does nothing while the sim clock is paused.
        if (SimClock.Instance == null || SimClock.Instance.IsPaused) return;

        float decay = decayPerMinute * SimClock.Instance.minutesPerSecond * Time.deltaTime;
        valueX = Mathf.Clamp(valueX - decay, 0f, 100f);
        valueY = Mathf.Clamp(valueY - decay, 0f, 100f);
        valueZ = Mathf.Clamp(valueZ - decay, 0f, 100f);
    }

    /// <summary>
    /// Immediately stops or restarts NavMesh movement when the clock is paused/resumed.
    /// The coroutine's WaitClockRunning() calls handle the rest.
    /// </summary>
    private void OnClockPauseChanged(bool paused)
    {
        if (navAgent != null && navAgent.isOnNavMesh)
            navAgent.isStopped = paused;
    }

    // -----------------------------------------------------------------------
    // Main Behavior Loop
    // -----------------------------------------------------------------------

    private IEnumerator AgentRoutine()
    {
        while (true)
        {
            // ── Pause gate: suspend here whenever the clock is paused ──────────
            yield return new WaitUntil(() =>
                SimClock.Instance == null || !SimClock.Instance.IsPaused);

            switch (currentState)
            {
                case AgentState.Idle:
                    SetColor(idleColor);

                    if (TryGetWanderDestination(out Vector3 wanderTarget))
                    {
                        navAgent.SetDestination(wanderTarget);

                        // Wait until arrived — also stops waiting while paused
                        // (isStopped = true keeps remainingDistance unchanged).
                        yield return new WaitUntil(() =>
                            (SimClock.Instance == null || !SimClock.Instance.IsPaused) &&
                            !navAgent.pathPending &&
                            (navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f ||
                             navAgent.pathStatus == NavMeshPathStatus.PathInvalid));
                    }

                    // Pause-aware idle delay — only counts real time while unpaused.
                    yield return PausableWait(idleDelay);
                    TryPickCrewConsole();
                    break;

                case AgentState.MovingToCrewConsole:
                    SetColor(movingColor);

                    yield return new WaitUntil(() =>
                        (SimClock.Instance == null || !SimClock.Instance.IsPaused) &&
                        !navAgent.pathPending &&
                        navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f);

                    currentState = AgentState.WorkingAtCrewConsole;
                    break;

                case AgentState.WorkingAtCrewConsole:
                    SetColor(workingColor);
                    navAgent.isStopped = true;

                    // Pause-aware work timer — clock stops ticking while paused.
                    yield return PausableWait(workDuration);

                    navAgent.isStopped = false;

                    if (currentCrewConsole != null)
                    {
                        ApplyCrewConsoleEffect(currentCrewConsole);
                        currentCrewConsole.Release();
                        currentCrewConsole = null;
                    }

                    currentState = AgentState.Idle;
                    break;
            }

            yield return null; // safety yield
        }
    }

    /// <summary>
    /// Waits for <paramref name="seconds"/> of real time, but does NOT count
    /// time while the SimClock is paused. Agents resume exactly where they
    /// left off in their timers when the clock unpauses.
    /// </summary>
    private System.Collections.IEnumerator PausableWait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (SimClock.Instance == null || !SimClock.Instance.IsPaused)
                elapsed += Time.deltaTime;
        }
    }

    // -----------------------------------------------------------------------
    // CrewConsole Selection
    // -----------------------------------------------------------------------

    private void TryPickCrewConsole()
    {
        // Determine which stat types are in need (below 50).
        // Agents only seek a console if at least one stat is low.
        bool needX = valueX < 50f;
        bool needY = valueY < 50f;
        bool needZ = valueZ < 50f;

        if (!needX && !needY && !needZ)
        {
            // All stats are healthy — stay idle and wander instead.
            currentState = AgentState.Idle;
            return;
        }

        // Refresh list in case CrewConsoles were added/removed at runtime.
        allCrewConsoles = FindObjectsByType<CrewConsole>(FindObjectsSortMode.None);

        // Build a list of unreserved consoles whose type matches a stat this agent needs
        // AND that this agent can actually reach on the NavMesh.
        var available = new List<CrewConsole>();
        var path = new NavMeshPath();
        foreach (var console in allCrewConsoles)
        {
            if (console.Reserved) continue;

            // Only consider consoles that address a stat below 50.
            bool consoleIsNeeded =
                (console.CrewConsoleType == CrewConsoleType.X && needX) ||
                (console.CrewConsoleType == CrewConsoleType.Y && needY) ||
                (console.CrewConsoleType == CrewConsoleType.Z && needZ);

            if (!consoleIsNeeded) continue;

            navAgent.CalculatePath(console.transform.position, path);
            if (path.status == NavMeshPathStatus.PathComplete)
                available.Add(console);
        }

        if (available.Count == 0)
        {
            // No reachable console for a needed stat — stay idle and try again later.
            currentState = AgentState.Idle;
            return;
        }

        // Prefer the console that matches the lowest stat so the most urgent need is
        // addressed first. Among ties, pick randomly.
        available.Sort((a, b) =>
        {
            float aVal = StatValueForType(a.CrewConsoleType);
            float bVal = StatValueForType(b.CrewConsoleType);
            return aVal.CompareTo(bVal); // ascending: most urgent first
        });

        // Build a shortlist of consoles tied for most urgent, then pick randomly from them.
        float lowestVal = StatValueForType(available[0].CrewConsoleType);
        var topNeed = available.FindAll(c => Mathf.Approximately(StatValueForType(c.CrewConsoleType), lowestVal));
        CrewConsole target = topNeed[Random.Range(0, topNeed.Count)];

        if (target.TryReserve())
        {
            currentCrewConsole = target;
            navAgent.SetDestination(target.transform.position);
            currentState = AgentState.MovingToCrewConsole;
        }
        else
        {
            // Race condition: another agent grabbed it — try again next idle cycle.
            currentState = AgentState.Idle;
        }
    }

    /// <summary>Returns this agent's current value for the given console type.</summary>
    private float StatValueForType(CrewConsoleType type)
    {
        return type switch
        {
            CrewConsoleType.X => valueX,
            CrewConsoleType.Y => valueY,
            CrewConsoleType.Z => valueZ,
            _ => 100f
        };
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Increases the agent value that matches the CrewConsole's type, capped at 100.
    /// </summary>
    private void ApplyCrewConsoleEffect(CrewConsole CrewConsole)
    {
        switch (CrewConsole.CrewConsoleType)
        {
            case CrewConsoleType.X:
                valueX = Mathf.Clamp(valueX + CrewConsole.valueIncrease, 0f, 100f);
                break;
            case CrewConsoleType.Y:
                valueY = Mathf.Clamp(valueY + CrewConsole.valueIncrease, 0f, 100f);
                break;
            case CrewConsoleType.Z:
                valueZ = Mathf.Clamp(valueZ + CrewConsole.valueIncrease, 0f, 100f);
                break;
        }

    }

    // -----------------------------------------------------------------------
    // Selection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called by SelectionManager to show or hide this agent's selection ring.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectionRing != null)
            selectionRing.gameObject.SetActive(selected);
    }

    /// <summary>
    /// Creates a green circle ring as a child of this agent using a LineRenderer.
    /// Because it is a child, it follows the agent automatically with no extra code.
    /// </summary>
    private void BuildSelectionRing()
    {
        var ringGo = new GameObject("SelectionRing");
        ringGo.transform.SetParent(transform);
        ringGo.transform.localPosition = new Vector3(0f, RING_Y_OFFSET, 0f);
        ringGo.transform.localRotation = Quaternion.identity;

        selectionRing = ringGo.AddComponent<LineRenderer>();
        selectionRing.useWorldSpace  = false;
        selectionRing.loop           = true;
        selectionRing.positionCount  = RING_SEGMENTS;
        selectionRing.startWidth     = RING_WIDTH;
        selectionRing.endWidth       = RING_WIDTH;
        selectionRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        selectionRing.receiveShadows = false;

        // URP-compatible unlit green material
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", Color.green);
        mat.color = Color.green;
        selectionRing.material = mat;

        // Plot the circle points in local space
        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            float angle = i * Mathf.PI * 2f / RING_SEGMENTS;
            selectionRing.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * RING_RADIUS,
                0f,
                Mathf.Sin(angle) * RING_RADIUS));
        }

        ringGo.SetActive(false); // hidden until the agent is selected
    }

    // -----------------------------------------------------------------------
    // Wandering
    // -----------------------------------------------------------------------

    /// <summary>
    /// Picks a random point on the NavMesh within wanderRadius of the agent's
    /// current position. Tries up to 8 candidates and returns the first one that
    /// has a fully reachable path. Returns false if nothing suitable is found
    /// (e.g. the agent is in a very small space), in which case the agent simply
    /// waits in place before looking for a console.
    /// </summary>
    private bool TryGetWanderDestination(out Vector3 result)
    {
        var path = new NavMeshPath();

        for (int i = 0; i < 8; i++)
        {
            // Random direction at a random distance up to wanderRadius
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            randomOffset.y = 0f; // keep it flat
            Vector3 candidate = transform.position + randomOffset;

            // Snap the candidate onto the nearest NavMesh surface
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                // Confirm the agent can actually walk there
                navAgent.CalculatePath(hit.position, path);
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    result = hit.position;
                    return true;
                }
            }
        }

        result = transform.position;
        return false;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void SetColor(Color color)
    {
        if (agentRenderer != null)
            agentRenderer.material.color = color;
    }
}
