using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Invisible marker that defines where new agents enter the station.
///
/// Place one (or more) of these anywhere on the NavMesh — typically just
/// inside a room's entrance.  The StationBuilderUI "Add Agent" button finds
/// the nearest one and spawns the new agent there.
///
/// In Play mode this object has no renderer, so it is completely invisible
/// to the player.  In the Scene view you'll see a cyan sphere + ring gizmo
/// so you can find and reposition it easily.
///
/// Future ideas: give each spawn point a max-capacity, a team/faction tag,
/// or an animation trigger for a door/airlock opening effect.
/// </summary>
public class AgentSpawnPoint : MonoBehaviour
{
    [Tooltip("Agents spawn at a random position within this radius of the marker. " +
             "Keeps multiple agents from stacking on top of each other.")]
    public float spawnRadius = 0.8f;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a NavMesh-valid world position near this spawn point.
    /// Tries up to 8 random offsets within <see cref="spawnRadius"/>, then
    /// falls back to the marker's own position if nothing is found.
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 offset    = Random.insideUnitSphere * spawnRadius;
            offset.y          = 0f;
            Vector3 candidate = transform.position + offset;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, spawnRadius + 0.5f, NavMesh.AllAreas))
                return hit.position;
        }

        // Last resort — snap the marker itself onto the NavMesh
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit fallback, 2f, NavMesh.AllAreas))
            return fallback.position;

        return transform.position;
    }

    // ── Scene-view gizmos ─────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        // Cyan sphere at the marker position
        Gizmos.color = new Color(0f, 0.85f, 0.85f, 0.6f);
        Gizmos.DrawSphere(transform.position, 0.25f);

        // Ring on the floor showing spawn radius
        Gizmos.color = new Color(0f, 0.85f, 0.85f, 0.25f);
        DrawCircle(transform.position, spawnRadius, 32);

        // Upward arrow so it's visible from above
        Gizmos.color = new Color(0f, 0.85f, 0.85f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.2f);
    }

    private void OnDrawGizmosSelected()
    {
        // Solid disc when selected to make radius obvious
        Gizmos.color = new Color(0f, 0.85f, 0.85f, 0.15f);
        DrawCircle(transform.position, spawnRadius, 32);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.4f,
            gameObject.name,
            new GUIStyle { normal = { textColor = new Color(0f, 0.85f, 0.85f) }, fontSize = 11 });
#endif
    }

    private static void DrawCircle(Vector3 center, float radius, int segments)
    {
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step * Mathf.Deg2Rad;
            float a2 = (i + 1) * step * Mathf.Deg2Rad;
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius),
                center + new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius));
        }
    }
}
