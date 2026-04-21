using System;
using System.Collections.Generic;

/// <summary>
/// Data model for the console_layout.json file.
/// JsonUtility requires flat structures, so x/y/z are direct fields.
/// </summary>

[Serializable]
public class CrewConsoleLayout
{
    public List<ConsoleEntry> consoles = new List<ConsoleEntry>();
}

[Serializable]
public class ConsoleEntry
{
    /// <summary>Optional label shown in the Hierarchy (e.g. "nav_west").</summary>
    public string id;

    /// <summary>
    /// Must exactly match a Type Name registered on the ConsoleSpawner component
    /// (e.g. "X_Console").
    /// </summary>
    public string type;

    public float x;
    public float y;
    public float z;
}
