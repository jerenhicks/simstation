using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click utility to enable Read/Write on every mesh asset in the project.
///
/// Unity's runtime NavMesh builder needs Read/Write access to bake mesh geometry.
/// Imported asset packs (e.g. SciFi Modular Kit) ship with it off by default to
/// save memory, which causes "does not allow read access" warnings during baking.
///
/// Usage: SimStation → Enable Mesh Read/Write
/// Run this once after importing any new asset pack.
/// </summary>
public static class MeshReadWriteEnabler
{
    [MenuItem("SimStation/Enable Mesh Read\u2215Write (fix NavMesh warnings)")]
    public static void EnableAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Mesh");
        int changed = 0;
        int skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Only touch meshes inside our own Assets folder —
                // skip built-in Unity resources and Package Cache.
                if (!path.StartsWith("Assets/")) { skipped++; continue; }

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { skipped++; continue; } // not an imported model

                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    changed++;
                }
                else
                {
                    skipped++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        Debug.Log($"[MeshReadWriteEnabler] Done — enabled Read/Write on {changed} mesh(es), {skipped} were already set or skipped.");
        EditorUtility.DisplayDialog(
            "Mesh Read/Write Enabled",
            $"Updated {changed} mesh asset(s).\n{skipped} were already correct or skipped (packages/built-ins).\n\nNavMesh bake warnings should no longer appear.",
            "OK");
    }
}
