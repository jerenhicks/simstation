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
    [Tooltip("Increased by visiting X CrewConsoles")]
    public float valueX = 0f;

    [Tooltip("Increased by visiting Y CrewConsoles")]
    public float valueY = 0f;

    [Tooltip("Increased by visiting Z CrewConsoles")]
    public float valueZ = 0f;

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
        // Refresh list in case CrewConsoles were added/removed at runtime
        allCrewConsoles = FindObjectsByType<CrewConsole>(FindObjectsSortMode.None);

        // Build a list of unreserved CrewConsoles that this agent can actually reach.
        // CalculatePath checks the NavMesh without moving the agent — PathComplete means
        // a full route exists. Consoles outside the room (off the NavMesh) return
        // PathInvalid or PathPartial and are excluded.
        var available = new List<CrewConsole>();
        var path = new NavMeshPath();
        foreach (var CrewConsole in allCrewConsoles)
        {
            if (CrewConsole.Reserved) continue;

            navAgent.CalculatePath(CrewConsole.transform.position, path);
            if (path.status == NavMeshPathStatus.PathComplete)
                available.Add(CrewConsole);
        }

        if (available.Count == 0)
        {
            // All CrewConsoles busy — stay idle and try again after delay
            currentState = AgentState.Idle;
            return;
        }

        // Pick randomly from available CrewConsoles
        CrewConsole target = available[Random.Range(0, available.Count)];

         Debug.Log($"[{name}]  Attempting to reserve CrewConsole {target.name}. Current reserved state: {target.Reserved}");

        if (target.TryReserve())
        {
            Debug.Log($"[{name}]  Successfully reserved CrewConsole {target.name}. Current reserved state: {target.Reserved}");
            //target.Reserved = true;
            currentCrewConsole = target;
            navAgent.SetDestination(target.transform.position);
            currentState = AgentState.MovingToCrewConsole;
        }
        else
        {
            // Race condition: another agent grabbed it — try again next idle cycle
            currentState = AgentState.Idle;
        }
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

        Debug.Log($"[{name}] Visited {CrewConsole.CrewConsoleType} CrewConsole — X:{valueX:F0}  Y:{valueY:F0}  Z:{valueZ:F0}");
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
