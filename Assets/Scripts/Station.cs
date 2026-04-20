using UnityEngine;

/// <summary>
/// Represents a workstation that AI agents can visit and occupy.
/// Attach this to any GameObject you want to act as a station.
/// </summary>
public class Station : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How close the agent needs to be to count as 'arrived'")]
    public float arrivalRadius = 0.8f;

    public bool IsOccupied { get; private set; }

    /// <summary>
    /// Attempt to claim this station. Returns true if successful, false if already occupied.
    /// </summary>
    public bool TryOccupy()
    {
        if (IsOccupied) return false;
        IsOccupied = true;
        return true;
    }

    /// <summary>
    /// Release the station so another agent can use it.
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
        Gizmos.DrawIcon(transform.position + Vector3.up * 0.5f, "sv_icon_dot3_pix16_gizmo", true);
    }
}
