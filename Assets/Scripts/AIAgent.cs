using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Simple AI agent that wanders between stations, occupies one at a time,
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
    [Tooltip("How many seconds the agent spends 'working' at a station")]
    public float workDuration = 3f;

    [Tooltip("How long to wait before picking the next station after finishing work")]
    public float idleDelay = 1f;

    [Header("Agent Values (0–100)")]
    [Tooltip("Increased by visiting X stations")]
    public float valueX = 0f;

    [Tooltip("Increased by visiting Y stations")]
    public float valueY = 0f;

    [Tooltip("Increased by visiting Z stations")]
    public float valueZ = 0f;

    [Header("Visual Feedback")]
    [Tooltip("Color while moving")]
    public Color movingColor = Color.blue;

    [Tooltip("Color while working at a station")]
    public Color workingColor = Color.yellow;

    [Tooltip("Color while idle / waiting")]
    public Color idleColor = Color.gray;

    // -----------------------------------------------------------------------
    // Internal State
    // -----------------------------------------------------------------------

    private enum AgentState { Idle, MovingToStation, WorkingAtStation }

    private NavMeshAgent navAgent;
    private AgentState currentState = AgentState.Idle;
    private Station currentStation;
    private Renderer agentRenderer;
    private LineRenderer selectionRing;

    private static Station[] allStations; // shared across all agents for efficiency

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

        // Cache all stations once (all agents share this reference)
        if (allStations == null || allStations.Length == 0)
            allStations = FindObjectsByType<Station>(FindObjectsSortMode.None);

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
                    TryPickStation();
                    break;

                case AgentState.MovingToStation:
                    SetColor(movingColor);

                    // Wait until NavMesh path is calculated and agent is close enough
                    yield return new WaitUntil(() =>
                        !navAgent.pathPending &&
                        navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f);

                    currentState = AgentState.WorkingAtStation;
                    break;

                case AgentState.WorkingAtStation:
                    SetColor(workingColor);
                    navAgent.isStopped = true;

                    yield return new WaitForSeconds(workDuration);

                    navAgent.isStopped = false;

                    if (currentStation != null)
                    {
                        ApplyStationEffect(currentStation);
                        currentStation.Release();
                        currentStation = null;
                    }

                    currentState = AgentState.Idle;
                    break;
            }

            yield return null; // safety yield so we never freeze Unity
        }
    }

    // -----------------------------------------------------------------------
    // Station Selection
    // -----------------------------------------------------------------------

    private void TryPickStation()
    {
        // Refresh list in case stations were added/removed at runtime
        allStations = FindObjectsByType<Station>(FindObjectsSortMode.None);

        // Build a list of unoccupied stations, excluding the one we just left
        var available = new List<Station>();
        foreach (var station in allStations)
        {
            if (!station.IsOccupied)
                available.Add(station);
        }

        if (available.Count == 0)
        {
            // All stations busy — stay idle and try again after delay
            currentState = AgentState.Idle;
            return;
        }

        // Pick randomly from available stations
        Station target = available[Random.Range(0, available.Count)];

        if (target.TryOccupy())
        {
            currentStation = target;
            navAgent.SetDestination(target.transform.position);
            currentState = AgentState.MovingToStation;
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
    /// Increases the agent value that matches the station's type, capped at 100.
    /// </summary>
    private void ApplyStationEffect(Station station)
    {
        switch (station.stationType)
        {
            case StationType.X:
                valueX = Mathf.Clamp(valueX + station.valueIncrease, 0f, 100f);
                break;
            case StationType.Y:
                valueY = Mathf.Clamp(valueY + station.valueIncrease, 0f, 100f);
                break;
            case StationType.Z:
                valueZ = Mathf.Clamp(valueZ + station.valueIncrease, 0f, 100f);
                break;
        }

        Debug.Log($"[{name}] Visited {station.stationType} station — X:{valueX:F0}  Y:{valueY:F0}  Z:{valueZ:F0}");
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
