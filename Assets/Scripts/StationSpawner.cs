using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads station_layout.json from StreamingAssets at game start and
/// spawns the correct prefab at each configured position.
///
/// Setup:
///   1. Add this component to a GameObject in your scene (SceneBuilder does this automatically).
///   2. In the Inspector, expand "Station Types" and add one entry per station prefab:
///        Type Name  →  the exact string used in the JSON  (e.g. "X_Station")
///        Prefab     →  drag the matching prefab from Assets/Prefabs
///   3. Make sure each prefab has a URP-compatible material assigned directly on it.
///   4. Edit StreamingAssets/station_layout.json to set positions and types.
/// </summary>
public class StationSpawner : MonoBehaviour
{
    [Tooltip("Register each station type here. Type Name must match the 'type' field in the JSON.")]
    public List<StationPrefabEntry> stationTypes = new List<StationPrefabEntry>();

    private const string LayoutFileName = "station_layout.json";

    private void Start()
    {
        SpawnFromConfig();
    }

    public void SpawnFromConfig()
    {
        // ── Load JSON ────────────────────────────────────────────────────────
        var path = Path.Combine(Application.streamingAssetsPath, LayoutFileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[StationSpawner] Config file not found at: {path}");
            return;
        }

        StationLayout layout;
        try
        {
            var json = File.ReadAllText(path);
            layout = JsonUtility.FromJson<StationLayout>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[StationSpawner] Failed to parse {LayoutFileName}: {e.Message}");
            return;
        }

        if (layout?.stations == null || layout.stations.Count == 0)
        {
            Debug.LogWarning("[StationSpawner] No stations found in layout file.");
            return;
        }

        // ── Build lookup: type name → entry ──────────────────────────────────
        var entryMap = new Dictionary<string, StationPrefabEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in stationTypes)
        {
            if (entry.prefab != null && !string.IsNullOrEmpty(entry.typeName))
                entryMap[entry.typeName] = entry;
        }

        // ── Spawn ────────────────────────────────────────────────────────────
        int spawned = 0;
        foreach (var data in layout.stations)
        {
            if (string.IsNullOrEmpty(data.type))
            {
                Debug.LogWarning($"[StationSpawner] Station '{data.id}' has no type — skipping.");
                continue;
            }

            if (!entryMap.TryGetValue(data.type, out var entry))
            {
                Debug.LogWarning($"[StationSpawner] No prefab registered for type '{data.type}' (id: '{data.id}'). " +
                                 "Check the StationSpawner Inspector.");
                continue;
            }

            var position = new Vector3(data.x, data.y, data.z);
            var instance = Instantiate(entry.prefab, position, Quaternion.identity, transform);
            instance.name = string.IsNullOrEmpty(data.id) ? data.type : data.id;
            spawned++;
        }

        Debug.Log($"[StationSpawner] Spawned {spawned} of {layout.stations.Count} stations.");
    }
}

// ── Supporting type ───────────────────────────────────────────────────────────

[Serializable]
public class StationPrefabEntry
{
    [Tooltip("Must match the 'type' string in station_layout.json exactly (case-insensitive).")]
    public string typeName;

    [Tooltip("The prefab to instantiate for this station type. Assign a URP material directly on the prefab for correct colors.")]
    public GameObject prefab;
}
