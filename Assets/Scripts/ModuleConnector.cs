using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// Marks a doorway attachment point on a station module.
///
/// Place an empty GameObject at each door opening inside a room prefab,
/// face its Z-axis (blue arrow) OUTWARD from the room, then add this component.
///
/// When two connectors are linked (via StationModule.ConnectTo), the module
/// positions itself so the connectors sit back-to-back and the NavMesh bakes
/// continuously through the open doorway.
///
/// Door setup:
///   Drag the door mesh GameObject into the "Door Object" slot.
///   The door is visible (closed) when the port is unlinked, and hidden
///   (open) when a neighbouring module connects — BEFORE the NavMesh rebakes,
///   so the open doorway is included in the bake with no obstacles.
///
///   Important: the door object's collider should be set to Is Trigger (or
///   removed) so it never blocks the NavMesh bake even when visible.
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

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Links this connector to <paramref name="other"/>.
    /// Hides the door on both sides so the passage is open before the
    /// NavMesh rebakes — the bake then covers the doorway natively.
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

        // Hide doors on both sides BEFORE the NavMesh rebakes.
        // This ensures the open doorway is baked as walkable floor,
        // letting agents walk through naturally without any off-mesh link.
        SetDoorOpen(true);
        other.SetDoorOpen(true);

        Debug.Log($"[ModuleConnector] Linked {portId} ↔ {other.portId}");
    }

    /// <summary>
    /// Removes the link and restores both doors.
    /// Call on both connectors when a module is detached.
    /// </summary>
    public void Unlink()
    {
        SetDoorOpen(false);

        if (LinkedConnector != null)
        {
            LinkedConnector.SetDoorOpen(false);
            LinkedConnector.LinkedConnector = null;
            LinkedConnector = null;
        }
    }

    // ── Door control ──────────────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides the door mesh.
    /// open=true  → door hidden  (passage is connected / walkable)
    /// open=false → door visible (passage is sealed)
    /// </summary>
    private void SetDoorOpen(bool open)
    {
        if (doorObject != null)
            doorObject.SetActive(!open);
    }

    // ── Scene-view gizmos ─────────────────────────────────────────────────────
    // Green = available   Red = linked
    // Arrow points OUTWARD from the room.

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
