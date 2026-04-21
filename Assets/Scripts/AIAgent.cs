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
        navAgent = GetComponent<NavMeshAgent>();
        agentRenderer = GetComponentInChildren<Renderer>();

        // Cache all CrewConsoles once (all agents share this reference)
        if (allCrewConsoles == null || allCrewConsoles.Length == 0)
            allCrewConsoles = FindObjectsByType<CrewConsole>(FindObjectsSortMode.None);

        BuildSelectionRing();
        SetColor(idleColor);
        StartCoroutine(AgentRoutine());
    }

    // -----------------------------------------------------------------------
    // Main Behavior Loop
    // -----------------------------------------------------------------------

    private IEnumerator AgentRoutine()
    {
        while (true)
        {
            switch (currentState)
            {
                case AgentState.Idle:
                    SetColor(idleColor);
                    yield return new WaitForSeconds(idleDelay);
                    TryPickCrewConsole();
                    break;

                case AgentState.MovingToCrewConsole:
                    SetColor(movingColor);

                    // Wait until NavMesh path is calculated and agent is close enough
                    yield return new WaitUntil(() =>
                        !navAgent.pathPending &&
                        navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f);

                    currentState = AgentState.WorkingAtCrewConsole;
                    break;

                case AgentState.WorkingAtCrewConsole:
                    SetColor(workingColor);
                    navAgent.isStopped = true;

                    yield return new WaitForSeconds(workDuration);

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

            yield return null; // safety yield so we never freeze Unity
        }
    }

    // -----------------------------------------------------------------------
    // CrewConsole Selection
    // -----------------------------------------------------------------------

    private void TryPickCrewConsole()
    {
        // Refresh list in case CrewConsoles were added/removed at runtime
        allCrewConsoles = FindObjectsByType<CrewConsole>(FindObjectsSortMode.None);

        // Build a list of unoccupied CrewConsoles, excluding the one we just left
        var available = new List<CrewConsole>();
        foreach (var CrewConsole in allCrewConsoles)
        {
            if (!CrewConsole.IsOccupied)
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

        if (target.TryOccupy())
        {
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
    // Helpers
    // -----------------------------------------------------------------------

    private void SetColor(Color color)
    {
        if (agentRenderer != null)
            agentRenderer.material.color = color;
    }
}
