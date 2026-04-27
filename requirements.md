# SimStation — Requirements

A space-station management simulation. The player constructs a modular station, populates it with astronaut agents, and guides them through jobs, research, and economic activity.

Status legend: **[Implemented]** exists in code today · **[Partial]** scaffolded but incomplete · **[Planned]** not yet started.

---

## 1. Vision

The player runs a growing space station. Astronauts (AI agents) live aboard, take on jobs, keep themselves alive, and unlock new technology that expands what the station can do. The player earns and spends currency to construct modules, hire crew, and progress along tech trees.

Target engine: **Unity 6000.3.9f1**.

---

## 2. Core Pillars

1. **Station construction** — modular, snap-together rooms with doorways and pathfinding.
2. **Autonomous agents** — astronauts with needs, jobs, and decision-making.
3. **Progression** — currency, research, and tech trees that change what the player can build and what agents can do.

---

## 3. Station & Construction

### 3.1 Modules **[Implemented]**
- A station is composed of **modules** (rooms) instantiated from prefabs.
- Each module exposes one or more **connectors** (doorways) with a port id (e.g., `North`, `South`).
- Modules snap to one another at matching connectors. Connected doorways open; unconnected ones are sealed.
- NavMesh links bake automatically across open doorways so agents can pathfind between modules.

### 3.2 Module placement **[Partial]**
- Runtime UI exists for adding a module to the next open connector.
- **[Planned]** Free placement: choose which connector to attach to, rotate, validate overlap.
- **[Planned]** Module catalog: multiple module types (habitation, mess hall, lab, workshop, hydroponics, power, life support, etc.).
- **[Planned]** Module deletion / refund.

### 3.3 Interactable objects
- **Crew consoles [Implemented]** — typed objects placed inside modules. An agent that uses one boosts a corresponding need stat. Consoles can be occupied or reserved.
- **[Planned]** Additional interactables tied to jobs: workbenches, research stations, hydroponics planters, medical beds, storage, communications array.

---

## 4. Astronauts (Agents)

### 4.1 Existing behavior **[Implemented]**
- Agents use Unity's NavMeshAgent for movement.
- Agents track three numeric **need stats** (currently `X`, `Y`, `Z`; 0–100). Stats decay slowly over in-game time.
- When a stat drops below 50, the agent seeks the nearest reachable console of the matching type.
- State machine: `Idle` → `MovingToCrewConsole` → `WorkingAtCrewConsole`.
- Agents avoid consoles that are occupied or reserved by another agent.
- One agent at a time can be selected by clicking; a selection ring is shown.

### 4.2 Need stats **[Partial]**
- **[Planned]** Replace `X/Y/Z` with named needs. Proposed initial set:
  - **Hunger** (satisfied at galley/mess hall)
  - **Energy/Sleep** (satisfied at habitation)
  - **Hygiene** (satisfied at sanitation module)
  - **Recreation/Morale** (satisfied at recreation room)
- **[Planned]** Critical thresholds: at very low values, agents take a hit to job performance and morale.

### 4.3 Identity & roster **[Partial]**
- **[Implemented]** Agents have a name and can be added or deleted at runtime.
- **[Planned]** Per-agent skill levels (one per job category) that improve with use.
- **[Planned]** Per-agent traits (e.g., *Engineer*, *Lazy*, *Insomniac*) that modify behavior.
- **[Planned]** Hire / dismiss flow paid for in currency.

---

## 5. Jobs & Tasks **[Planned]**

Currently agents only seek consoles to refill needs. The job system is not yet implemented.

### 5.1 Job system requirements
- The station has a **job board** / queue of pending tasks.
- Each job specifies: required job type, required skill level (optional), location, duration, reward (currency / research / resource), and prerequisites.
- Agents idle below a configurable need threshold pull jobs from the board, prioritized by:
  1. Player-pinned priority,
  2. Agent specialization fit,
  3. Distance / availability.
- A job in progress is reserved by one agent until completed, abandoned, or cancelled.

### 5.2 Initial job categories
| Job        | Performed at         | Produces                  |
|------------|----------------------|---------------------------|
| Engineer   | Workshop / breakages | Repairs, upkeep           |
| Scientist  | Research lab         | Research points           |
| Botanist   | Hydroponics          | Food                      |
| Medic      | Med bay              | Heals/revives crew        |
| Operator   | Comms / power        | Currency contracts, power |

### 5.3 Player controls
- View the job board.
- Add manual jobs (e.g., "construct module here", "repair object").
- Pin/prioritize, cancel, or block jobs.
- Assign a job to a specific agent.

---

## 6. Currencies **[Planned]**

### 6.1 Primary currency: **Credits**
- Earned by: completing contracts (Operator jobs), selling surplus resources, mission events.
- Spent on: new modules, hiring agents, purchasing high-tier resources, unlocking tech tree nodes that require funding.

### 6.2 Secondary currency: **Research Points (RP)**
- Earned by: Scientist jobs at research stations.
- Spent on: nodes in the tech tree.

### 6.3 Resources (stocks, not currencies)
- **Power**, **Oxygen**, **Food**, **Water**, **Raw materials**.
- Produced and consumed by modules and agents over time.
- Shortages trigger warnings and degrade agent stats.

### 6.4 UI
- Persistent HUD strip showing Credits, RP, and key resources.
- Per-resource production/consumption breakdown panel.

---

## 7. Tech Trees **[Planned]**

### 7.1 Structure
- Multiple parallel trees, e.g., **Engineering**, **Science**, **Biology**, **Operations**.
- A node defines: name, description, RP cost, optional Credit cost, prerequisites, unlock effect.
- Unlock effects can: enable a new module, enable a new job category, improve an existing module/job, unlock a trait, raise a stat cap.

### 7.2 Player flow
- Open a Research screen.
- See trees as graphs with locked / available / researched node states.
- Spend RP (and Credits if required) to unlock a node when prerequisites are met.

### 7.3 Persistence
- Tech-tree state persists across save/load (see §9).

---

## 8. UI

### 8.1 Existing **[Implemented]**
- **SimClockUI** — top-center clock (HH:MM) and pause/play.
- **AgentInfoUI** — bottom-right; shows selected agent name, three stat bars, delete button.
- **StationBuilderUI** — bottom-left; "+ Add Module" and "+ Add Agent" buttons with status text.

### 8.2 Planned
- **Resource HUD** — Credits, RP, Power, Oxygen, Food, Water (top-right).
- **Job Board panel** — list/queue of jobs, manual add, priority controls.
- **Research panel** — tech-tree graph view.
- **Module catalog panel** — pick a module to place; shows cost and prerequisites.
- **Agent roster panel** — list of all crew with vitals at a glance.
- **Notifications/log** — events, warnings, completed contracts.

---

## 9. Time, Save & Sessions

### 9.1 Clock **[Implemented]**
- Singleton **SimClock** drives in-game time. 1 real second = 1 game minute by default. Pause/resume and speed control supported.
- All time-dependent systems (stat decay, jobs, research progress) listen to SimClock.

### 9.2 Save/load **[Planned]**
- Persist: station layout, agents (positions, stats, skills), job queue, currencies, resources, tech tree state, clock time.
- One auto-save slot plus manual save slots.

---

## 10. Out of scope (initial release)

- Multiplayer / networking.
- Procedural narrative or scripted campaigns (aspirational, not v1).
- Combat or hostile factions.
- External station EVA / spacewalking.

---

## 11. Open questions

1. Are need stats `X/Y/Z` standing in for specific real needs, or should they remain abstract?
2. Should currency be earned passively (station upkeep/contracts ticking) or only via explicit Operator jobs?
3. How many module types should ship with v1?
4. Is research per-agent (skill XP) **and** global (RP-funded tech tree), or only one of those?
5. How catastrophic should resource shortages be — degraded performance only, or possible agent death?
