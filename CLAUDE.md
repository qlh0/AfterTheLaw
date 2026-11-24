# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MYLIFE** is a Unity 2D emergent life-simulation installation. Autonomous agents form and lose relationships, experience conditions (hungry / lonely / joyful / hurt), trigger behaviors (Love / Violence / Silence), eat, reproduce, found churches, and die. A drag-and-drop **Rule Book** UI lets the audience edit world rules in real time. The project has been shown as a physical installation (dual-display setup, auto-reset, restart hotspots). Unity 6, URP.

## Development

Unity project — no CLI build/test commands. All development happens through the Unity Editor:

- **Open project**: Unity Hub → open this folder
- **Scenes** (`Assets/_Project/Scenes/`): `Boot.unity` (startup, activates Display 2, loads main scene) → `World.unity` (main simulation + Rule Book UI). `Synth.unity` is a sound-design sandbox.
- **Play**: open `World.unity` (or `Boot.unity` for the full installation flow) and press Play

## Architecture

### Data Layer (`Scripts/Data/`, `Scripts/Core/WorldState.cs`) — pure C# (no MonoBehaviours)
- **`AgentData`** — position/velocity, conditions (`hungry`, `lonely`, `joyful`, `hurt`, all 0–1), HP, food, age, generation, current behavior + timer, reproduction cooldown, and per-agent relation dictionaries keyed by other agent IDs:
  - `kinshipRelations` (`KinshipType`: None/Parent/Child — fixed once set)
  - `socialRelations` (`SocialRelationType`: Friend/Enemy/Other — changes at runtime)
  - `closenessTimers` / `distanceTimers` drive relation upgrades/downgrades
  - Relations are **asymmetric**: A's perception of B can differ from B's of A
- **`FoodData`**, **`HomeData`**, **`ChurchData`**, **`CombatMarkerData`**, **`DeathMarkerData`** — world entities
- **`WorldState`** — container of all entity lists + `worldTime` + id counters
- **`SocialRelationTable`**, **`ConditionSpeedConfig`**, **`BehaviorConfig`** (in `Configs/`) — static lookup/config

### Simulation Layer (`Scripts/Simulation/`)
- **`SimulationEngine`** — tick loop (`ticksPerSecond`, default 3) with game-time scaling (`gameSecondsPerRealSecond`) and unit/pixel/meter conversion helpers. Calls `AgentSystem.RunSimulationStep()` per tick; max 1 tick per frame.
- **`AgentSpawner`** — creates initial agents/foods, owns `worldState`, supports `ResetWorld()`.
- **`AgentSystem`** — orchestrator; step order per tick: aging/death → cooldowns → reset transient hurt → food respawn/consumption/pickup (`AgentSystem_FoodHungry`) → conditions → church spawning + church lonely-spread → Rulebook rules → behavior triggers (threshold-based, locked for `BehaviorConfig` duration) → violence (damage + enemy marking + escape impulse) → love bonds → births (population cap 120, max births/tick, food cost, cooldown) → relation changes (proximity timers) → movement (`AgentSystem_MovementSystem`).
- **`AgentSystem_MovementSystem`** — force-based movement: per-pair attraction from kinship (0.8) or `SocialRelationTable`, modified by Rulebook global factors and active behaviors (Love adds force, Violence adds force, Silence dampens; Silent targets exert none). Plus food-seeking and church attraction forces. World bounds are camera-derived with wall bounce.
- **`AgentSystem_Rulebook`** — applies the 5 audience-editable rules (reads singleton bridges each tick).

### Rule Book & UI (`Scripts/Rulebook/`)
The original dual-display drag-and-drop Rule Book was retired; the UI is now a runtime-built drawer system (`DisplayModeManager`): four top drawers (SYSTEM / GUIDE / AGENT inspector / RULE BOOK) and two bottom drawers (CHART / SPEAK), folder-style tabs riding each drawer's edge.
- **`RuleBookState`** — static plain-data store of the five rules; the UI writes it, `AgentSystem_Rulebook` reads it once per tick (enum snapshots via `RulebookTypes`).
- Rules (identities are *perceived* per-observer: Child/Friend/Enemy/Other):
1. **Draw Together** (per-pair attraction bonus) 2. **Turn Away** (per-pair repulsion) 3. **Becomes** (perceived identity substitution) 4. **Condition Spreads** (contagion within radius; "Dead" sources from graves) 5. **When [Identity] is [Condition], [Behavior] rises**
- **`ClaudeRuleCompiler`** — "Speak to the World": sends the user's wish + a live world snapshot to the Anthropic API; the strict-JSON reply patches `RuleBookState` (validated vocabulary) and tunes physics via **`WorldParameterRegistry`** (~18 whitelisted, range-clamped parameters). Model output is treated as untrusted input. API key: `StreamingAssets/anthropic_key.txt` (gitignored).

### View Layer (`Scripts/View/`)
- **`WorldView`** — reads `WorldState` each frame; instantiates/swaps agent prefabs by visual state (Default/Joyful/Love/Lonely/Hurt — separate prefabs, swapped when thresholds cross), lerps positions, renders homes/churches/foods (weighted random prefabs, scale = growth)/combat markers/death markers, nearest friend/enemy orbit markers (`AgentRelationMarkers`), click-to-inspect agent info panel. `Application.targetFrameRate = 30`.
- **`AgentVisual_Number`** — per-agent id + generation label for click detection.
- **`CameraLastAgentFocus`**, **`AgentVisualEffect`** — camera/effect helpers.

### Sound (`Scripts/Sound/`)
- **`Synthesizer/`** — custom modular synth (Oscillator/Filter/LFO/Gain/Mixer/Noise/`OnAudioFilterRead` output modules)
- **`Sound_Agent/`** — agent-state-driven audio (`AgentStateAudioManager`, synth voices, love tones, alphabet vocalizations); plus bird/combat/tree/birth audio controllers

### Performance layer
`SpatialGrid` (shared neighbor queries + far-field aggregates), sparse relation storage, corpse/food lifecycle cleanup, `SimulationStats` (per-tick counters + rule attribution lists driving `RuleAttributionView` and `BehaviorChartView`). Benchmark: editor menu **MYLIFE → Run Simulation Benchmark** (results in `Benchmarks/`).

## Key Design Patterns

- **Simulation is data-only**: simulation mutates plain C# `AgentData`/`WorldState`; `AgentSystem.RunSimulationStep()` is the single entry point. View reads state, never writes it.
- **Tick-based, not frame-based**: simulation rate and render rate are decoupled.
- **UI → Simulation via singleton bridges**: Rule Book slots never touch simulation directly; bridges expose current rule strings, simulation polls them each tick.
- **Reset by rebuild**: restart = new `WorldState` + `WorldView.ResetView()` destroys and rebuilds all visuals.

## Notes

- `Assets/_Project/AgentSystemExplain.cs` is a fully commented-out tuning-parameter reference (Chinese annotations for every inspector value) — documentation, not live code.
- `Recordings/` (large screen captures) and `.claude/settings.local.json` are gitignored.
- Sound assets in `Assets/_Project/Asset/Sound/` include several large .wav files (~140 MB total) — consider Git LFS before pushing to GitHub.

## Key Unity Packages

- `com.unity.render-pipelines.universal` 17.3.0 — URP
- `com.unity.inputsystem` 1.18.0 (legacy `Input` still used in some view scripts)
- `com.unity.2d.*` — sprites, animation, Aseprite/PSD importers
- `com.unity.ugui` + TextMeshPro — Rule Book UI
- `com.unity.recorder` 5.1.6 — capture
