using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// Marks a doorway attachment point on a station module.
///
/// Place an empty GameObject at each door opening inside a room prefab,
/// face its Z-axis (blue arrow) OUTWARD from the room, then add this component.
///
/// When two connectors are linked (via StationModule.ConnectTo), the module
/// positions itself so the connectors sit back-to-back, and a NavMesh Link
/// is automatically created so agents can walk between the rooms.
/// </summary>
public class ModuleConnector : MonoBehaviour
{
    [Tooltip("Human-readable label for this port, e.g. North, South, Docking-Left. " +
             "Used to identify connectors when building layouts programmatically.")]
    public string portId = "Port";

    // ── Runtime state ─────────────────────────────────────────────────────────

    /// <summary>The connector on the neighbouring module this port is linked to.</summary>
    public ModuleConnector LinkedConnector { get; private set; }

    /// <summary>True when this port has no neighbour attached.</summary>
    public bool IsAvailable => LinkedConnector == null;

    // NavMesh Link bridging the gap between the two rooms at this connector.
    private NavMeshLink _navLink;

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Links this connector to <paramref name="other"/> and creates a NavMesh Link
    /// so agents can navigate between the two modules.
    /// Call this AFTER the new module has been positioned via AlignConnectorTo.
    /// </summary>
    public void LinkTo(ModuleConnector other)
    {
        if (!IsAvailable || !other.IsAvailable)
        {
            Debug.LogWarning($"[ModuleConnector] Cannot link {portId} — one or both ports are already occupied.");
            return;
        }

        LinkedConnector = other;
        other.LinkedConnector = this;

        // Add a NavMesh Link on this connector's GameObject so agents can cross.
        // The link runs from this connector to the neighbour (bidirectional).
        _navLink = gameObject.AddComponent<NavMeshLink>();
        _navLink.startPoint = Vector3.zero;                     // local — this connector position
        _navLink.endPoint   = transform.InverseTransformPoint(other.transform.position);
        _navLink.width      = 2f;                               // match doorway width
        _navLink.bidirectional = true;
        _navLink.UpdateLink();

        Debug.Log($"[ModuleConnector] Linked {portId} ↔ {other.portId}");
    }

    /// <summary>
    /// Removes the link and destroys the NavMesh Link bridge.
    /// Call on both connectors when a module is detached.
    /// </summary>
    public void Unlink()
    {
        if (LinkedConnector != null)
        {
            LinkedConnector.LinkedConnector = null;
            LinkedConnector = null;
        }

        if (_navLink != null)
        {
            Destroy(_navLink);
            _navLink = null;
        }
    }

    // ── Scene-view gizmos ─────────────────────────────────────────────────────
    // Green  = available     Red = linked
    // Arrow points OUTWARD (the direction a new module would attach from).

    private void OnDrawGizmos()
    {
        Color c = (LinkedConnector == null) ? Color.green : Color.red;
        Gizmos.color = c;

        Vector3 pos = transform.position;
        Vector3 fwd = transform.forward;

        // Sphere at the port position
        Gizmos.DrawSphere(pos, 0.12f);

        // Arrow shaft
        Gizmos.DrawLine(pos, pos + fwd * 0.8f);

        // Arrow head (two angled lines)
        Vector3 tip   = pos + fwd * 0.8f;
        Vector3 right = transform.right * 0.2f;
        Vector3 up    = transform.up    * 0.2f;
        Gizmos.DrawLine(tip, tip - fwd * 0.25f + right);
        Gizmos.DrawLine(tip, tip - fwd * 0.25f - right);
        Gizmos.DrawLine(tip, tip - fwd * 0.25f + up);
        Gizmos.DrawLine(tip, tip - fwd * 0.25f - up);
    }

    private void OnDrawGizmosSelected()
    {
        // Show a disc in the door plane so it's easy to check alignment
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(2f, 2.5f, 0.05f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
