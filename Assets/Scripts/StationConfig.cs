using System;
using System.Collections.Generic;

/// <summary>
/// Data model for the station_layout.json file.
/// JsonUtility requires flat structures, so x/y/z are direct fields.
/// </summary>

[Serializable]
public class StationLayout
{
    public List<StationEntry> stations = new List<StationEntry>();
}

[Serializable]
public class StationEntry
{
    /// <summary>Optional label shown in the Hierarchy (e.g. "crafting_west").</summary>
    public string id;

    /// <summary>
    /// Must exactly match a Type Name registered on the StationSpawner component
    /// and the name of the prefab file (e.g. "X_Station").
    /// </summary>
    public string type;

    public float x;
    public float y;
    public float z;
}
