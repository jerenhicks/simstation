using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

/// <summary>
/// Attach to the root GameObject of each room/module prefab.
///
/// Automatically discovers all ModuleConnector children and exposes them
/// for use by the StationBuilder (or future UI). Provides the core
/// positioning math for snapping one module against another.
///
/// Setup for a new module prefab:
///   1. Add this component to the prefab root.
///   2. Create empty child GameObjects at each doorway opening.
///   3. Rotate each child so its blue (Z) arrow faces OUTWARD from the room.
///   4. Add a ModuleConnector component to each of those children.
///   5. Give each connector a descriptive portId (e.g. "North", "Airlock-Left").
/// </summary>
public class StationModule : MonoBehaviour
{
    [Tooltip("Display name for this module type (e.g. 'Crew Quarters', 'Engineering Bay').")]
    public string moduleName = "Module";

    // ── Connectors ────────────────────────────────────────────────────────────

    /// <summary>All ModuleConnector children on this module.</summary>
    public IReadOnlyList<ModuleConnector> Connectors => _connectors;
    private List<ModuleConnector> _connectors = new();

    private void Awake()
    {
        // Auto-discover connectors from children so the prefab doesn't need
        // manual wiring — just add ModuleConnector components in the right spots.
        _connectors = new List<ModuleConnector>(
            GetComponentsInChildren<ModuleConnector>());
    }

    /// <summary>All connectors that do not yet have a neighbour attached.</summary>
    public List<ModuleConnector> GetAvailableConnectors()
    {
        var open = new List<ModuleConnector>();
        foreach (var c in _connectors)
            if (c.IsAvailable) open.Add(c);
        return open;
    }

    /// <summary>
    /// Returns the first connector whose portId matches <paramref name="id"/> (case-insensitive),
    /// or null if none is found.
    /// </summary>
    public ModuleConnector GetConnector(string id)
    {
        foreach (var c in _connectors)
            if (string.Equals(c.portId, id, System.StringComparison.OrdinalIgnoreCase))
                return c;
        return null;
    }

    // ── Alignment ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves and rotates this module so that <paramref name="myConnector"/>
    /// sits back-to-back with <paramref name="targetConnector"/>.
    ///
    /// The two connectors will end up at the same world position, facing each
    /// other (myConnector.forward == -targetConnector.forward).
    ///
    /// Call this BEFORE LinkTo so the NavMesh Link gets the correct positions.
    /// </summary>
    public void AlignConnectorTo(ModuleConnector myConnector, ModuleConnector targetConnector)
    {
        Debug.Log($"[StationModule] Aligning '{myConnector.portId}' " +
                  $"localPos={myConnector.transform.localPosition}  worldPos={myConnector.transform.position}  forward={myConnector.transform.forward}\n" +
                  $"  → target '{targetConnector.portId}' worldPos={targetConnector.transform.position}  forward={targetConnector.transform.forward}");

        // Step 1 — Rotate the module so myConnector faces the opposite direction
        //          to targetConnector (i.e. they face each other).
        Quaternion desiredPortRot = Quaternion.LookRotation(
            -targetConnector.transform.forward,
             targetConnector.transform.up);

        Quaternion rotDelta = desiredPortRot * Quaternion.Inverse(myConnector.transform.rotation);
        transform.rotation = rotDelta * transform.rotation;

        // Step 2 — Slide the module so myConnector lands exactly on targetConnector.
        //          (After the rotation above, myConnector has a new world position.)
        transform.position += targetConnector.transform.position - myConnector.transform.position;

        Debug.Log($"[StationModule] After align — module root: {transform.position}, connector landed at: {myConnector.transform.position}");
    }

    /// <summary>
    /// Convenience method: aligns this module then links the two connectors,
    /// which also creates the NavMesh Link bridge.
    /// </summary>
    public void ConnectTo(ModuleConnector myConnector, ModuleConnector targetConnector)
    {
        AlignConnectorTo(myConnector, targetConnector);
        myConnector.LinkTo(targetConnector);
    }

    // ── NavMesh ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Bakes the NavMesh for this module in isolation.
    /// Call after positioning the module and before linking connectors.
    /// </summary>
    public void BakeNavMesh()
    {
        var surface = GetComponentInChildren<NavMeshSurface>();
        if (surface != null)
            surface.BuildNavMesh();
        else
            Debug.LogWarning($"[StationModule] {moduleName} has no NavMeshSurface — skipping bake.");
    }

    // ── Scene-view label ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.5f,
            moduleName,
            new GUIStyle { normal = { textColor = Color.cyan }, fontSize = 12 });
#endif
    }
}
