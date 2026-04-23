using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Builds the SimStation scene from scratch.
/// In Unity: click  SimStation → Build Scene  in the top menu bar.
/// Re-running it will tear down and rebuild everything cleanly.
/// </summary>
public static class SceneBuilder
{
    // ── Layout ───────────────────────────────────────────────────────────────
    // Room A: world XZ  0–10 × 0–10
    // Room B: world XZ 15–25 × 0–10  (east, connected via Corridor_AB)
    // Room C: world XZ  0–10 × 15–25  (north, connected via Corridor_AC)

    const float ROOM = 10f;   // room side length
    const float GAP = 5f;   // corridor length between rooms
    const float DOOR = 3f;   // doorway width
    const float WH = 2f;   // wall height (tall enough so NavMesh won't jump over)
    const float WT = 0.2f; // wall thickness

    // ── Entry Point ───────────────────────────────────────────────────────────
    [MenuItem("SimStation/Build Scene")]
    public static void BuildScene()
    {
        // Tear down any previous build so re-running is safe
        var old = GameObject.Find("SimStation Root");
        if (old != null) GameObject.DestroyImmediate(old);

        var root = new GameObject("SimStation Root");

        // ── Room (TestRoom prefab) ─────────────────────────────────────────────
        var roomPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TestRoom.prefab");
        if (roomPrefab != null)
        {
            var roomInstance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(roomPrefab, root.transform);
            roomInstance.name = "TestRoom";
            roomInstance.transform.position = Vector3.zero;
        }
        else
        {
            Debug.LogError("[SimStation] TestRoom prefab not found at Assets/Prefabs/TestRoom.prefab — check the path.");
        }

        // One Game Object to have all the scenes objects
        var gameScripts = new GameObject("Game Scripts");
        gameScripts.transform.SetParent(root.transform);

        // ── Sim Clock ─────────────────────────────────────────────────────────
        // Tracks in-game time. 1 real second = 1 game minute by default.
        // Adjust "Minutes Per Second" in the Inspector to change time speed.
        gameScripts.AddComponent<SimClock>();
        gameScripts.AddComponent<SimClockUI>();

        // ── Console Spawner ───────────────────────────────────────────────────
        // Crew consoles are no longer hardcoded here — they're driven by console_layout.json.
        // The CrewConsoleSpawner component reads that file at runtime and instantiates prefabs.
        // After running Build Scene, open the Game Scripts GameObject in the Inspector
        // and drag your prefabs from Assets/Prefabs into the Console Types list.
        gameScripts.AddComponent<CrewConsoleSpawner>();

        // ── Agent Spawn Point ─────────────────────────────────────────────────
        // Agents no longer spawn automatically at scene start.
        // Use the "Add Agent" button in the runtime UI to spawn them here.
        // Reposition this object in the Hierarchy to move the spawn location.
        var spawnGo = new GameObject("Agent Spawn Point");
        spawnGo.transform.SetParent(root.transform);
        spawnGo.transform.position = new Vector3(2f, 0f, 2f); // near room centre
        spawnGo.AddComponent<AgentSpawnPoint>();


        // ── Station Builder UI ────────────────────────────────────────────────
        // Adds the "+ Add Module" button to the screen.
        // After running Build Scene, select "Game Scripts" in the Hierarchy,
        // find StationBuilderUI in the Inspector, and drag your TestRoom prefab
        // into the "Module Prefab" slot.
        gameScripts.AddComponent<StationBuilderUI>();

        // ── Selection Manager ─────────────────────────────────────────────────
        gameScripts.AddComponent<SelectionManager>();

        // ── Agent Info UI ─────────────────────────────────────────────────────
        gameScripts.AddComponent<AgentInfoUI>();

        // ── NavMesh ───────────────────────────────────────────────────────────
        var nmGo = new GameObject("NavMesh Surface");
        nmGo.transform.SetParent(root.transform);
        var surface = nmGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        // Use physics colliders instead of render meshes so imported mesh assets
        // don't need Read/Write enabled — colliders work fine without it.
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh(); // bakes automatically — no need to click anything

        // ── Camera ────────────────────────────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            // Perspective mode — positioned above and angled in for a nice 3D starting view
            cam.transform.position = new Vector3(ROOM / 2f, 14f, -6f);
            cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            cam.orthographic       = false;
            cam.fieldOfView        = 60f;
            cam.nearClipPlane      = 0.1f;
            cam.farClipPlane       = 200f;
            cam.clearFlags         = CameraClearFlags.Skybox; // let skybox show so lighting renders correctly

            // Attach the free-roam controller so the camera is movable in Play mode
            if (cam.GetComponent<FreeCameraController>() == null)
                cam.gameObject.AddComponent<FreeCameraController>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[SimStation] Scene built! Press Play to watch agents move.");
    }

    // ── Room ─────────────────────────────────────────────────────────────────
    static void BuildRoom(Transform parent, string roomName, Vector3 origin,
        bool doorNorth = false, bool doorSouth = false,
        bool doorEast = false, bool doorWest = false)
    {
        var room = new GameObject(roomName);
        room.transform.SetParent(parent);
        room.transform.position = origin;

        // Floor — a Unity Plane is 10×10 at localScale (1,1,1)
        MakeFloor(room.transform,
            localPos: new Vector3(ROOM / 2f, 0f, ROOM / 2f),
            scale: new Vector3(ROOM / 10f, 1f, ROOM / 10f),
            col: new Color(0.55f, 0.55f, 0.55f));

        float seg = (ROOM - DOOR) / 2f; // half-wall length beside a doorway (3.5)

        // South edge (fixed Z = 0, runs along X)
        BuildEdge(room.transform, isXFixed: false, edgeVal: 0f, hasDoor: doorSouth, seg, "S");
        // North edge (fixed Z = ROOM, runs along X)
        BuildEdge(room.transform, isXFixed: false, edgeVal: ROOM, hasDoor: doorNorth, seg, "N");
        // West edge  (fixed X = 0, runs along Z)
        BuildEdge(room.transform, isXFixed: true, edgeVal: 0f, hasDoor: doorWest, seg, "W");
        // East edge  (fixed X = ROOM, runs along Z)
        BuildEdge(room.transform, isXFixed: true, edgeVal: ROOM, hasDoor: doorEast, seg, "E");
    }

    /// <param name="isXFixed">true → wall sits at a fixed X (West/East); false → fixed Z (South/North)</param>
    static void BuildEdge(Transform room, bool isXFixed, float edgeVal, bool hasDoor, float seg, string lbl)
    {
        if (!hasDoor)
        {
            if (isXFixed) // West or East — full wall along Z
                Wall(room, new Vector3(edgeVal, WH / 2f, ROOM / 2f), new Vector3(WT, WH, ROOM), $"Wall_{lbl}");
            else           // South or North — full wall along X
                Wall(room, new Vector3(ROOM / 2f, WH / 2f, edgeVal), new Vector3(ROOM, WH, WT), $"Wall_{lbl}");
            return;
        }

        float c1 = seg / 2f;         // center of first  half-wall
        float c2 = ROOM - seg / 2f;  // center of second half-wall

        if (isXFixed)
        {
            Wall(room, new Vector3(edgeVal, WH / 2f, c1), new Vector3(WT, WH, seg), $"Wall_{lbl}1");
            Wall(room, new Vector3(edgeVal, WH / 2f, c2), new Vector3(WT, WH, seg), $"Wall_{lbl}2");
        }
        else
        {
            Wall(room, new Vector3(c1, WH / 2f, edgeVal), new Vector3(seg, WH, WT), $"Wall_{lbl}1");
            Wall(room, new Vector3(c2, WH / 2f, edgeVal), new Vector3(seg, WH, WT), $"Wall_{lbl}2");
        }
    }

    // ── Corridor ─────────────────────────────────────────────────────────────
    /// <param name="origin">World-space min-corner of the corridor floor</param>
    /// <param name="lx">Extent along X</param>
    /// <param name="lz">Extent along Z</param>
    /// <param name="horizontal">true → travels along X; false → travels along Z</param>
    static void BuildCorridor(Transform parent, string cName,
        Vector3 origin, float lx, float lz, bool horizontal)
    {
        var go = new GameObject(cName);
        go.transform.SetParent(parent);
        go.transform.position = Vector3.zero; // children use world-space positions

        MakeFloor(go.transform,
            localPos: origin + new Vector3(lx / 2f, 0f, lz / 2f),
            scale: new Vector3(lx / 10f, 1f, lz / 10f),
            col: new Color(0.50f, 0.50f, 0.50f));

        if (horizontal) // side walls run along X (south & north)
        {
            Wall(go.transform, origin + new Vector3(lx / 2f, WH / 2f, 0f), new Vector3(lx, WH, WT), "Wall_S");
            Wall(go.transform, origin + new Vector3(lx / 2f, WH / 2f, lz), new Vector3(lx, WH, WT), "Wall_N");
        }
        else            // side walls run along Z (west & east)
        {
            Wall(go.transform, origin + new Vector3(0f, WH / 2f, lz / 2f), new Vector3(WT, WH, lz), "Wall_W");
            Wall(go.transform, origin + new Vector3(lx, WH / 2f, lz / 2f), new Vector3(WT, WH, lz), "Wall_E");
        }
    }

    // ── Agents ───────────────────────────────────────────────────────────────
    static void PlaceAgent(Transform parent, Vector3 worldPos, string aName)
    {
        var a = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        a.name = aName;
        a.transform.SetParent(parent);
        a.transform.position = worldPos;
        a.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f); // slightly larger so they're visible from top

        var nav = a.AddComponent<NavMeshAgent>();
        nav.speed = 3.5f;
        nav.stoppingDistance = 0.6f;
        nav.angularSpeed = 360f;
        nav.radius = 0.25f;
        nav.height = 1f;

        a.AddComponent<AIAgent>();
        Colorize(a, new Color(0.45f, 0.55f, 0.95f)); // blue
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    static void MakeFloor(Transform parent, Vector3 localPos, Vector3 scale, Color col)
    {
        var f = GameObject.CreatePrimitive(PrimitiveType.Plane);
        f.name = "Floor";
        f.transform.SetParent(parent);
        f.transform.localPosition = localPos;
        f.transform.localScale = scale;
        Colorize(f, col);
    }

    static void Wall(Transform parent, Vector3 localPos, Vector3 scale, string wName)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = wName;
        w.transform.SetParent(parent);
        w.transform.localPosition = localPos;
        w.transform.localScale = scale;
        Colorize(w, new Color(0.22f, 0.22f, 0.25f)); // dark grey
    }

    static void Colorize(GameObject go, Color col)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;

        // Use an Unlit shader so colors display correctly regardless of lighting.
        // Tries URP Unlit first; falls back to the built-in Unlit/Color shader.
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");

        Material mat;
        if (shader != null)
        {
            mat = new Material(shader);
            mat.SetColor("_BaseColor", col); // URP Unlit uses _BaseColor
            mat.color = col;                 // covers built-in Unlit/Color
        }
        else
        {
            // Last resort: copy whatever shader is on the object
            mat = new Material(r.sharedMaterial);
            mat.SetColor("_BaseColor", col);
            mat.color = col;
        }

        r.sharedMaterial = mat;
    }
}
