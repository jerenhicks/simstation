using UnityEngine;

/// <summary>
/// The type of value this crew console provides to visiting agents.
/// Must be set in the Inspector on each crew console prefab.
/// </summary>
public enum CrewConsoleType { X, Y, Z }

/// <summary>
/// Represents an interactive crew console that AI agents can visit and occupy.
/// Attach this to any GameObject you want to act as a crew console.
/// </summary>
public class CrewConsole : MonoBehaviour
{
    [Header("Console Type")]
    [Tooltip("Which agent value this console increases when visited.")]
    public CrewConsoleType CrewConsoleType = CrewConsoleType.X;

    [Tooltip("How much this console increases the matching agent value per visit (0–100 scale).")]
    public float valueIncrease = 10f;

    [Header("Settings")]
    [Tooltip("How close the agent needs to be to count as 'arrived'")]
    public float arrivalRadius = 0.8f;

    public bool IsOccupied { get; private set; }

    /// <summary>
    /// Attempt to claim this console. Returns true if successful, false if already occupied.
    /// </summary>
    public bool TryOccupy()
    {
        if (IsOccupied) return false;
        IsOccupied = true;
        return true;
    }

    /// <summary>
    /// Release the console so another agent can use it.
    /// </summary>
    public void Release()
    {
        IsOccupied = false;
    }

    // Visual feedback in the Scene view (not visible in game)
    private void OnDrawGizmos()
    {
        Gizmos.color = IsOccupied ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, arrivalRadius);
    }
}
