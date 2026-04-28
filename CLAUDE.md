# CLAUDE.md

Project memory for Claude Code working on **SimStation** — a Unity space-station management sim.

---

## Read these first

Before planning or implementing anything non-trivial, read both of these files in full. They are the authoritative context for this project; do not guess about gameplay or structure when the answer is in here.

- **[requirements.md](./requirements.md)** — what the game is, what's implemented vs. planned (jobs, currencies, tech trees), open design questions.
- **[architecture.md](./architecture.md)** — layers, key abstractions, communication patterns, data flow, known debts, and the proposed shape for the planned systems.

When the user asks a question that touches game systems, **cite which doc and section you're relying on** so the user can correct it if the docs are stale.

If you change behavior in code, check whether requirements.md or architecture.md needs an update in the same change. Status tags **[Implemented]** / **[Partial]** / **[Planned]** in requirements.md must stay accurate.

---

## What this project is (one paragraph)

The player builds a modular space station out of snap-together room prefabs and populates it with AI astronauts. Astronauts have decaying need stats and autonomously seek **CrewConsoles** to refill them. The shipped foundation is the station-building, NavMesh pathfinding, agent AI, and clock systems. The roadmap adds a job board, two currencies (Credits + Research Points), resource stocks, and tech trees.

Engine: **Unity 6000.3.9f1** · C# · URP · Unity AI Navigation · Unity Input System.

---

## Where things live

```
Assets/Scripts/             Domain + UI MonoBehaviours (one type per file)
Assets/Scripts/Editor/      SceneBuilder.cs (menu: SimStation → Build Scene)
Assets/Prefabs/             Room and console prefabs
Assets/StreamingAssets/     console_layout.json — runtime-loaded data
ProjectSettings/            Unity config (don't edit casually)
requirements.md             Game requirements
architecture.md             Code architecture
```

The scene is **rebuilt by `Editor/SceneBuilder.cs`**, not stored as the source of truth in a `.unity` file. To get a working scene, run **SimStation → Build Scene** in the Unity menu.

---

## Conventions you must follow

These are pulled from existing scripts. New code must match.

1. **One `MonoBehaviour` per file**; filename equals type name.
2. **XML doc comments** on every public type and member.
3. **Box-drawing section headers** in code (`// ── Inspector ─────────────────────`).
4. **Singletons** use `public static Instance { get; private set; }` set in `Awake` (see `SimClock`, `SelectionManager`).
5. **React to time via events**, not polling: subscribe to `SimClock.OnTimeChanged` / `OnPauseChanged` in `Start`, unsubscribe in `OnDestroy`.
6. **Pause-aware timing**: any coroutine timer must use `PausableWait` or guard with `WaitUntil(() => !SimClock.Instance.IsPaused)`. Never trust `Time.deltaTime` alone for gameplay.
7. **Time scaling**: convert real seconds to in-game minutes with `Time.deltaTime * SimClock.Instance.minutesPerSecond`.
8. **New managers register in `SceneBuilder`** on the `Game Scripts` child of `SimStation Root`, so scene rebuild stays the source of truth.
9. **Runtime-tunable data** goes into `StreamingAssets/*.json` (see `CrewConsoleSpawner`) or a `ScriptableObject` asset — never hard-code lists in C#.
10. **Reservations before action**: pattern is `TryReserve()` → move → work → `Release()`, mirroring `CrewConsole`. Reuse this for jobs.

---

## Architectural extension points

When extending the game, prefer plugging into the existing patterns described in **architecture.md §8**:

- **Jobs** → new `JobBoard` singleton; `AIAgent` state machine gains `MovingToJob` / `WorkingJob` states **above** need-seeking in priority.
- **Currencies** → `EconomyManager` singleton with `OnCreditsChanged` / `OnRPChanged` events. Resource stocks go in a separate `ResourceManager`.
- **Tech trees** → `TechNodeSO` / `TechTreeSO` ScriptableObjects + `ResearchManager` singleton. Unlocks apply via an `IUnlockEffect` strategy.
- **Save/load** → `GameStateService` collects a JSON snapshot from each manager; reuse the `JsonUtility` style from `CrewConsoleSpawner`.

If a request doesn't fit these shapes, surface that — don't quietly invent a new pattern.

---

## Things to remember

- `valueX / valueY / valueZ` on `AIAgent` are **abstract placeholder need stats**, not final names. Renaming to concrete needs (Hunger / Energy / etc.) is open question §11.1 in requirements.md.
- `CrewConsoleType` enum is `X / Y / Z` for the same reason — it pairs with the agent fields.
- `AIAgent.TryPickCrewConsole` calls `FindObjectsByType<CrewConsole>` every idle cycle. Acceptable today; flagged in architecture.md §7 as a debt to revisit at scale.
- Doors must be hidden **before** NavMesh rebake so the doorway is included as walkable floor (see `ModuleConnector.LinkTo`). Don't reorder this.
- There is no save/load yet — assume scene-start is a fresh game until a `GameStateService` exists.

---

## Don't

- Don't add new top-level docs without being asked. Update requirements.md and architecture.md instead.
- Don't bypass `SimClock` for gameplay timing.
- Don't introduce a DI framework or new architecture style — the project uses MonoBehaviour singletons + events. Stay consistent unless explicitly asked to refactor.
- Don't edit `ProjectSettings/`, `Library/`, or `Packages/manifest.json` without explaining why.
