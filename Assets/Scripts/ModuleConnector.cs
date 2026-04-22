using UnityEngine;
using UnityEngine.AI;
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
///
/// Door setup:
///   Drag the door mesh GameObject into the "Door Object" slot.
///   The door is visible (closed) when the port is unlinked, and hidden
///   (open / removed) when a neighbouring module connects.
/// </summary>
public class ModuleConnector : MonoBehaviour
{
    [Tooltip("Human-readable label for this port, e.g. North, South, Docking-Left. " +
             "Used to identify connectors when building layouts programmatically.")]
    public string portId = "Port";

    [Tooltip("The door mesh GameObject at this opening. " +
             "It will be hidden when a module connects and shown when it disconnects.")]
    public GameObject doorObject;

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
    /// Hides the door on both sides of the connection.
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

        // Hide doors on both sides — the passage is now open.
        SetDoorOpen(true);
        other.SetDoorOpen(true);

        // Add a NavMesh Link bridging the two rooms.
        //
        // After AlignConnectorTo, both connectors share the same world position,
        // so a zero-length link would be useless. Instead we push each endpoint
        // 1 m INWARD along the connector's local Z axis:
        //   -Z = into THIS room  (forward points outward, so backwards = inward)
        //   +Z = into the OTHER room (other.forward is opposite, so +Z here goes in)
        //
        // This ensures both endpoints land on the baked NavMesh inside their rooms.
        const float linkDepth = 1f;
        _navLink = gameObject.AddComponent<NavMeshLink>();
        _navLink.startPoint    = new Vector3(0f, 0f, -linkDepth); // 1 m into this room
        _navLink.endPoint      = new Vector3(0f, 0f,  linkDepth); // 1 m into the other room
        _navLink.width         = 2f;
        _navLink.bidirectional = true;
        _navLink.UpdateLink();

        Debug.Log($"[ModuleConnector] Linked {portId} ↔ {other.portId}");
    }

    /// <summary>
    /// Snaps the NavMesh Link endpoints to the nearest real NavMesh positions.
    /// Call this AFTER the NavMesh has been rebaked so SamplePosition finds
    /// the freshly baked geometry. Without this, endpoints can land in wall
    /// geometry or the gap between rooms and agents won't traverse the link.
    /// </summary>
    public void SnapNavLinkToMesh()
    {
        if (_navLink == null) return;

        const float searchRadius = 3f;

        // Convert current link endpoints from local → world, snap to NavMesh, convert back.
        Vector3 startWorld = transform.TransformPoint(_navLink.startPoint);
        Vector3 endWorld   = transform.TransformPoint(_navLink.endPoint);

        if (NavMesh.SamplePosition(startWorld, out NavMeshHit sHit, searchRadius, NavMesh.AllAreas))
        {
            _navLink.startPoint = transform.InverseTransformPoint(sHit.position);
            Debug.Log($"[ModuleConnector] Snapped start to NavMesh at {sHit.position}");
        }
        else
            Debug.LogWarning($"[ModuleConnector] Could not snap start point to NavMesh (searched {searchRadius}m around {startWorld})");

        if (NavMesh.SamplePosition(endWorld, out NavMeshHit eHit, searchRadius, NavMesh.AllAreas))
        {
            _navLink.endPoint = transform.InverseTransformPoint(eHit.position);
            Debug.Log($"[ModuleConnector] Snapped end to NavMesh at {eHit.position}");
        }
        else
            Debug.LogWarning($"[ModuleConnector] Could not snap end point to NavMesh (searched {searchRadius}m around {endWorld})");

        _navLink.UpdateLink();
    }

    /// <summary>
    /// Removes the link and destroys the NavMesh Link bridge.
    /// Restores the door on this side (the neighbour's door is its own responsibility).
    /// Call on both connectors when a module is detached.
    /// </summary>
    public void Unlink()
    {
        // Restore this side's door first, while we still know who the neighbour is.
        SetDoorOpen(false);

        if (LinkedConnector != null)
        {
            LinkedConnector.SetDoorOpen(false);
            LinkedConnector.LinkedConnector = null;
            LinkedConnector = null;
        }

        if (_navLink != null)
        {
            Destroy(_navLink);
            _navLink = null;
        }
    }

    // ── Door control ──────────────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides the door mesh.
    /// open=true  → door hidden  (passage is connected / walkable)
    /// open=false → door visible (passage is a dead end / sealed)
    /// </summary>
    private void SetDoorOpen(bool open)
    {
        if (doorObject != null)
            doorObject.SetActive(!open);
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

        Gizmos.DrawSphere(pos, 0.12f);
        Gizmos.DrawLine(pos, pos + fwd * 0.8f);

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
        Gizmos.color  = new Color(0f, 1f, 0.5f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(2f, 2.5f, 0.05f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
