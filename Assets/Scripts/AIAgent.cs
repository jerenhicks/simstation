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

    private static Station[] allStations; // shared across all agents for efficiency

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

    private void SetColor(Color color)
    {
        if (agentRenderer != null)
            agentRenderer.material.color = color;
    }
}
