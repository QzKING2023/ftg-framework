# FTG Framework — Project Overview

FTG Framework is a local fighting-game framework for Godot 4.x and C#. It provides deterministic input, data, frame-data, combo, physics, state-machine, replay, pooling, scene, authoring, training, and debugging foundations so games can build on shared contracts instead of reimplementing them.

This generated page is a navigation aid. Use the [V2 Architecture Spine](../_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md) for normative design and [sprint-status.yaml](../_bmad-output/implementation-artifacts/sprint-status.yaml) for current status and completion evidence.

## Technical Profile

| Item | Current contract |
|------|------------------|
| Engine | Godot .NET 4.5.1 |
| Runtime targets | `net8.0`; Android `net9.0` |
| Build SDK | .NET SDK 10.x |
| Language | C# with nullable reference types |
| Testing | xUnit plus Godot/scaffold evidence where required |
| Architecture | Event-driven layered framework with pure-C# services and thin Godot adapters |

## Module Map

- `Core` — EventBus, lifecycle epochs, Replay, snapshots, pooling, FileWatcher, scene/lifecycle services, shared contracts.
- `Input` — canonical inputs, history, buffering, leniency, charge, SOCD cleaning, training recording/playback.
- `Data` — immutable versioned definitions, presence-aware validation, transactional persistence and reload.
- `Engine/FrameData` and `Engine/Combo` — move timelines, cancel windows, chains, and combo state.
- `Engine/Physics` and `Engine/StateMachine` — collision, knockback, guarded state stacks, and response profiles.
- `UI/Training` — ViewModel-backed training controls, tuning, playback, combo, and save/load surfaces.
- `Scripts/Editor` — thin EditorPlugin adapters for authoring, debugging, and distribution tooling.

## Delivery Navigation

Epic numbers are stable capability identifiers, not execution chronology. For current completion, next-work, and gating state, use the canonical [sprint status](../_bmad-output/implementation-artifacts/sprint-status.yaml); this generated page intentionally does not duplicate those volatile values.

- [V2 PRD](../_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-26/prd.md)
- [V2 Epics](../_bmad-output/planning-artifacts/epics/index.md)
- [Project UX](../_bmad-output/planning-artifacts/ux-v2-lean-contract.md)
- [Documentation index](./index.md)
