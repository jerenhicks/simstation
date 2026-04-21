using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads console_layout.json from StreamingAssets at game start and
/// spawns the correct prefab at each configured position.
///
/// Setup:
///   1. Add this component to a GameObject in your scene (SceneBuilder does this automatically).
///   2. In the Inspector, expand "Console Types" and add one entry per console prefab:
///        Type Name  →  the exact string used in the JSON  (e.g. "X_Console")
///        Prefab     →  drag the matching prefab from Assets/Prefabs
///   3. Make sure each prefab has a URP-compatible material assigned directly on it.
///   4. Edit StreamingAssets/console_layout.json to set positions and types.
/// </summary>
public class CrewConsoleSpawner : MonoBehaviour
{
    [Tooltip("Register each console type here. Type Name must match the 'type' field in the JSON.")]
    public List<CrewConsolePrefabEntry> consoleTypes = new List<CrewConsolePrefabEntry>();

    private const string LayoutFileName = "console_layout.json";

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
            Debug.LogError($"[ConsoleSpawner] Config file not found at: {path}");
            return;
        }

        CrewConsoleLayout layout;
        try
        {
            var json = File.ReadAllText(path);
            layout = JsonUtility.FromJson<CrewConsoleLayout>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ConsoleSpawner] Failed to parse {LayoutFileName}: {e.Message}");
            return;
        }

        if (layout?.consoles == null || layout.consoles.Count == 0)
        {
            Debug.LogWarning("[ConsoleSpawner] No consoles found in layout file.");
            return;
        }

        // ── Build lookup: type name → entry ──────────────────────────────────
        var entryMap = new Dictionary<string, CrewConsolePrefabEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in consoleTypes)
        {
            if (entry.prefab != null && !string.IsNullOrEmpty(entry.typeName))
                entryMap[entry.typeName] = entry;
        }

        // ── Spawn ────────────────────────────────────────────────────────────
        int spawned = 0;
        foreach (var data in layout.consoles)
        {
            if (string.IsNullOrEmpty(data.type))
            {
                Debug.LogWarning($"[ConsoleSpawner] Console '{data.id}' has no type — skipping.");
                continue;
            }

            if (!entryMap.TryGetValue(data.type, out var entry))
            {
                Debug.LogWarning($"[CrewConsoleSpawner] No prefab registered for type '{data.type}' (id: '{data.id}'). " +
                                 "Check the CrewConsoleSpawner Inspector.");
                continue;
            }

            var position = new Vector3(data.x, data.y, data.z);
            var instance = Instantiate(entry.prefab, position, Quaternion.identity, transform);
            instance.name = string.IsNullOrEmpty(data.id) ? data.type : data.id;
            spawned++;
        }

        Debug.Log($"[CrewConsoleSpawner] Spawned {spawned} of {layout.consoles.Count} consoles.");
    }
}

// ── Supporting type ───────────────────────────────────────────────────────────

[Serializable]
public class CrewConsolePrefabEntry
{
    [Tooltip("Must match the 'type' string in console_layout.json exactly (case-insensitive).")]
    public string typeName;

    [Tooltip("The prefab to instantiate for this console type. Assign a URP material directly on the prefab for correct colors.")]
    public GameObject prefab;
}
