# SimStation — Architecture

Companion to [requirements.md](./requirements.md). This document describes **how the code is organized**: the layers, the key abstractions, the patterns used to glue them together, and where the planned systems (jobs, currencies, tech trees) plug in.

Engine: **Unity 6000.3.9f1**, C#, Universal Render Pipeline, Unity AI Navigation (NavMesh + NavMeshLink), Unity Input System.

---

## 1. Layered view

```
┌──────────────────────────────────────────────────────────────────────┐
│  UI Layer        SimClockUI · AgentInfoUI · StationBuilderUI         │
│                  (uGUI canvases, read state, drive input)            │
├──────────────────────────────────────────────────────────────────────┤
│  Sim / Domain    SimClock · SelectionManager · AIAgent               │
│  Layer           CrewConsole · CrewConsoleSpawner                    │
│                  StationModule · ModuleConnector                     │
├──────────────────────────────────────────────────────────────────────┤
│  Engine Layer    Unity NavMesh · NavMeshSurface · NavMeshLink        │
│                  Input System · Physics raycast · Coroutines         │
├──────────────────────────────────────────────────────────────────────┤
│  Authoring       Editor/SceneBuilder · Prefabs · StreamingAssets     │
│                  (console_layout.json)                               │
└──────────────────────────────────────────────────────────────────────┘
```

The **Sim/Domain layer** is where almost all gameplay lives. UI only reads from it (and pushes button presses back). The Authoring layer exists at edit time only.

---

## 2. Key abstractions

| Type                   | Role                                                                                    |
|------------------------|-----------------------------------------------------------------------------------------|
| `SimClock`             | Singleton authoritative clock. Drives every time-dependent system via events.            |
| `SelectionManager`     | Singleton. Owns "currently selected agent" and broadcasts via a public property.        |
| `AIAgent`              | One per astronaut. Coroutine-driven state machine over needs and consoles.              |
| `StationModule`        | Root component on a room prefab. Owns its `ModuleConnector` children, handles snap math.|
| `ModuleConnector`      | A doorway port. Knows whether it is linked, controls door visibility, drives bake.      |
| `CrewConsole`          | Interactable. Holds `CrewConsoleType` (X/Y/Z) and reserve/occupy state.                  |
| `CrewConsoleSpawner`   | Reads `StreamingAssets/console_layout.json` and instantiates console prefabs.            |
| `AgentSpawnPoint`      | Marker that returns a NavMesh-valid spawn position.                                      |
| `SceneBuilder` (editor)| Menu command that rebuilds the scene root from prefabs. Idempotent.                      |

These are all `MonoBehaviour`s — there are no plain C# services or DI container yet.

---

## 3. Communication patterns

The code uses three patterns consistently. New systems should follow the same conventions.

### 3.1 Singleton + static `Instance`
`SimClock` and `SelectionManager` use `public static Instance { get; private set; }` set in `Awake`. Other code calls `SimClock.Instance.IsPaused` / `SelectionManager.Instance.SelectedAgent` directly.

### 3.2 C# events for clock-driven changes
`SimClock` exposes `OnTimeChanged(int hour, int minute)` and `OnPauseChanged(bool paused)`. `AIAgent.OnClockPauseChanged` subscribes in `Start` and unsubscribes in `OnDestroy`. **This is the preferred way to react to time** — do not poll `SimClock.Instance.Hour` every frame.

### 3.3 Polling (state-display only)
`AgentInfoUI.Update` polls `SelectionManager.Instance.SelectedAgent` every frame to refresh stat bars. This is acceptable for low-cost UI updates but should not be used for game logic.

### 3.4 Service location via `FindObjectsByType<T>` / `FindFirstObjectByType<T>`
The codebase locates services by type, not by registry:
- `AIAgent.TryPickCrewConsole` calls `FindObjectsByType<CrewConsole>` (refreshed each idle cycle to catch runtime spawns).
- `StationBuilderUI.FindAvailablePort` calls `FindObjectsByType<StationModule>`.
- `RebakeNavMesh` calls `FindObjectsByType<NavMeshSurface>`.

This is fine at current scale. If the agent count grows, consider a central `StationRegistry` that caches modules / consoles / jobs and emits add/remove events.

---

## 4. Data flow — current systems

### 4.1 Time → stat decay → console seeking
```
SimClock.Update
   └─► broadcasts OnTimeChanged + drives Time.deltaTime use elsewhere

AIAgent.Update                 (each unpaused frame)
   └─► decays valueX / valueY / valueZ

AIAgent.AgentRoutine           (coroutine)
   ├─ Idle      → wander on NavMesh, then TryPickCrewConsole()
   ├─ Moving    → wait until NavMeshAgent arrives
   └─ Working   → PausableWait(workDuration) → ApplyCrewConsoleEffect → Release()

CrewConsole.TryReserve / Release                (reservation lock)
   └─► prevents two agents racing to the same console
```

The state machine is intentionally a single coroutine with a `switch (currentState)` per iteration plus `WaitUntil` for arrival, plus `PausableWait` so the clock pause halts in-flight timers.

### 4.2 Module construction
```
StationBuilderUI.OnAddModuleClicked
   ├─ FindAvailablePort(targetPortId)            ← scans existing modules
   ├─ Instantiate(modulePrefab) under SimStation Root
   ├─ newModule.ConnectTo(myPort, targetPort)
   │     ├─ AlignConnectorTo  (rotate + slide so ports sit back-to-back)
   │     └─ myPort.LinkTo(targetPort) → hides both door GameObjects
   └─ RebakeNavMesh()                            ← BuildNavMesh on every NavMeshSurface
```

Doors are hidden **before** the bake so the doorway is included as walkable floor — there's no off-mesh link.

### 4.3 Console authoring
`CrewConsoleSpawner.Start` reads `Application.streamingAssetsPath/console_layout.json`, deserializes with `JsonUtility`, looks each entry's `type` up in the inspector-configured `consoleTypes` map, and instantiates the prefab. This is the project's only **data-driven** authoring path today and is the model to follow for jobs and tech trees.

### 4.4 Selection
```
Mouse left-click  ── (Input System) ──►  SelectionManager.HandleClick
   ├─ EventSystem.IsPointerOverGameObject → ignore (UI guard)
   ├─ Camera.main.ScreenPointToRay → Physics.Raycast
   ├─ hit.collider.GetComponentInParent<AIAgent>
   └─ SelectAgent / DeselectCurrent → AIAgent.SetSelected → toggles ring
```

`AgentInfoUI` polls `SelectionManager.Instance.SelectedAgent` to render the panel.

---

## 5. Scene & asset organization

```
Assets/
  Prefabs/                       Room and console prefabs
  Scripts/                       Domain + UI MonoBehaviours
    Editor/
      SceneBuilder.cs            Menu: SimStation → Build Scene
      MeshReadWriteEnabler.cs    Asset post-processor
  StreamingAssets/
    console_layout.json          Runtime-loaded console layout
```

**SceneBuilder is the source of truth for the scene root**, not a saved scene asset. Re-running `SimStation → Build Scene` destroys the `SimStation Root` GameObject and recreates it. All managers live as components on a child `Game Scripts` GameObject. This keeps the project diff-friendly and prevents scene drift.

---

## 6. Time, threading, and pausing

- All gameplay runs on Unity's main thread; no jobs/burst/multithreading.
- `SimClock.minutesPerSecond` is the global rate. Other systems multiply `Time.deltaTime * SimClock.Instance.minutesPerSecond` to convert to in-game minutes (see `AIAgent.Update`).
- Pause semantics: anything time-sensitive must check `SimClock.Instance.IsPaused`. Coroutines use `WaitUntil(() => !IsPaused)` and the helper `PausableWait(seconds)` to halt mid-timer. `OnClockPauseChanged` flips `NavMeshAgent.isStopped` so movement freezes immediately.

When you add new systems they MUST integrate the same way — use the clock event, not `Time.deltaTime` alone.

---

## 7. Known architectural debts

| Debt                                                                 | Impact                                       |
|----------------------------------------------------------------------|----------------------------------------------|
| `FindObjectsByType` scans on every idle cycle (`AIAgent.TryPickCrewConsole`) | O(agents × consoles) churn at scale.   |
| `static CrewConsole[] allCrewConsoles` shared across all agents      | First agent to start populates it; race-y if scenes load async. |
| AIAgent stats (`valueX/Y/Z`) are duplicated fields, not collection-driven | Hard to add a 4th need. Refactor first before §8.2. |
| No save/load layer — managers don't expose serializable state       | Blocks save game work in requirements §9.2.   |
| UI builds its hierarchy in C# (`StationBuilderUI.BuildUI`) rather than Prefabs | New panels need lots of layout boilerplate.   |
| Scene is built by an editor script, not a `.unity` asset            | Can't open a scene and just play; must run the menu first. |

These are not blockers for the current feature set. Address as the planned systems push complexity past their current limits.

---

## 8. Architecture for the planned systems

Each of the three pillars in [requirements.md](./requirements.md) §5–7 plugs into the existing layers.

### 8.1 Jobs system

**New types:**
- `JobBoard` (singleton MonoBehaviour) — owns the queue. Methods: `Post(Job)`, `TryClaim(AIAgent) → Job?`, `Complete(Job)`, `Cancel(Job)`. Events: `OnJobPosted`, `OnJobClaimed`, `OnJobCompleted`.
- `Job` (plain C# class or `ScriptableObject` for templates) — fields: `id`, `category`, `requiredSkill`, `targetPosition`, `durationMinutes`, `rewardCredits`, `rewardRP`, `prerequisites`.
- `JobCategory` enum (`Engineer`, `Scientist`, `Botanist`, `Medic`, `Operator`).

**AIAgent integration:**
The current state machine grows two states — `MovingToJob`, `WorkingJob` — inserted **above** need-seeking in priority. Pseudocode:
```
Idle:
  if anyNeedCritical          → TryPickCrewConsole   (existing)
  else if JobBoard.HasOpenJob → claim & MoveToJob
  else                        → wander
```
Need-seeking stays the safety valve. A "job" for the agent is just a richer reservation than a console reserve.

**Why this fits:** the AIAgent already has reservation semantics (`CrewConsole.TryReserve/Release`); jobs reuse that exact pattern.

### 8.2 Currencies

**New singleton:** `EconomyManager` with:
- `int Credits`, `int ResearchPoints`
- `event Action<int,int> OnCreditsChanged` and `OnRPChanged`
- `bool TrySpend(int credits, int rp)` and `Earn(int credits, int rp)`
- Subscribes to `SimClock.OnTimeChanged` for any per-tick income (e.g., contract trickle).

**Resource stocks** (Power, Oxygen, Food, Water, Materials) get a parallel `ResourceManager` so currencies and physical stocks don't conflate. Modules and agents call `ResourceManager.Consume` / `Produce` per minute.

**UI:** a new `ResourceHUD` MonoBehaviour subscribes to both managers' events and renders a top-right strip — same construction pattern as `SimClockUI`.

### 8.3 Tech trees

Tech-tree data is the strongest case in the project for **ScriptableObjects** (the JSON-driven console pattern in §4.3 also works, but ScriptableObjects let designers wire prerequisite references in the Inspector).

**New types:**
- `TechNodeSO : ScriptableObject` — fields: `displayName`, `description`, `costRP`, `costCredits`, `prerequisites: TechNodeSO[]`, `unlockEffect`.
- `TechTreeSO : ScriptableObject` — a named collection of nodes (Engineering, Science, etc.).
- `ResearchManager` (singleton) — tracks `HashSet<TechNodeSO> Researched`. Methods: `IsUnlocked(node)`, `CanResearch(node)`, `TryResearch(node)`. Events: `OnNodeResearched`.
- `IUnlockEffect` — implementations apply the effect when researched: `EnableModuleEffect`, `EnableJobCategoryEffect`, `BoostJobOutputEffect`, `RaiseStatCapEffect`.

**Where unlocks land:** `EnableModuleEffect` toggles a flag the **module catalog** UI checks before listing a prefab; `EnableJobCategoryEffect` flips a flag on `JobBoard`. The managers don't need to know about each other — they each consult `ResearchManager.IsUnlocked`.

### 8.4 Save / load (cross-cutting)

A `GameStateService` collects a snapshot from each manager (`SimClock.TotalMinutes`, `EconomyManager` totals, `JobBoard` queue, `ResearchManager.Researched`, agent positions and stats, the linked `StationModule`/`ModuleConnector` graph). Persist as JSON in `Application.persistentDataPath`. Reuse the JSON-via-`JsonUtility` style already in `CrewConsoleSpawner` for consistency.

Save is the work that benefits most from first refactoring debts ② and ③ in §7.

---

## 9. Dependency map after planned work

```
                ┌──────────── SimClock ────────────┐
                │                                  │
                ▼                                  ▼
        ResourceManager                      EconomyManager
                │     ▲                            │
                │     │ produces/consumes          │ rewards
                │     │                            │
                ▼     │                            ▼
            StationModule  ◄──── JobBoard ────►  AIAgent
                ▲                  ▲                ▲
                │                  │ unlocks job    │ skill
                │                  │ category       │ XP
                │                  │                │
                └──────── ResearchManager ──────────┘
                                  ▲
                                  │ spends RP
                                  │
                              EconomyManager
```

Arrows point in the direction of the call. Note that **no manager has a hard reference to another at construction time** — they communicate through events and singleton lookup. This keeps each manager independently testable and unloadable.

---

## 10. Coding conventions to keep

Drawn from the existing codebase:

1. **One `MonoBehaviour` per file**, file name = type name.
2. **Section comments** with the box-drawing style used throughout (`// ── Inspector ─────`).
3. **XML doc comments** on every public type and member — consistent with current scripts.
4. Subscribe in `Start` (or `Awake` for singletons), unsubscribe in `OnDestroy`.
5. Guard time-sensitive logic with `SimClock.Instance == null || !SimClock.Instance.IsPaused` — never assume the clock exists.
6. New managers register on the `Game Scripts` GameObject in `SceneBuilder` so the scene rebuild stays the source of truth.
7. New runtime-tunable data goes into `StreamingAssets/*.json` or a `ScriptableObject` asset — never hard-code lists in C#.
