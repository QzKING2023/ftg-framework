# FTG Framework — Architecture Navigation

The [V2 Architecture Spine](../_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md) is normative. This generated document is only a compact map; when wording differs, the Spine and its inherited archived V1 decisions win.

## Architecture Shape

The framework is event-driven and layered. Core infrastructure is shared; Input and Data own canonical input and immutable configuration; Engine owns frame data, combo, physics, and state-machine behavior; UI and EditorPlugin classes are thin Godot adapters over pure-C# ViewModels/services.

```text
Godot hosts/adapters
        |
UI + Editor tooling
        |
Engine: FrameData | Combo | Physics | StateMachine
        |
Input + Data
        |
Core: EventBus | Replay | Snapshots | Pool | FileWatcher | lifecycle
```

Cross-module communication uses EventBus contracts and injected interfaces. Runtime mutation respects the fixed AD-12 phase order, immutable publication boundaries, lifecycle epochs, and transactional/failure-atomic persistence.

## Decision Map

- AD-1–AD-8 — normative V1 baseline; the `.archive` location records chronology only.
- AD-9–AD-17 — initiation snapshots, physics/state ownership, V2 frame order, authoritative Replay, pooling, reload, and infrastructure boundaries.
- AD-18–AD-20 — immutable lifecycle state, presence-aware transactional data, and versioned failure-atomic snapshots.
- AD-21 — accessible, lifecycle-safe interaction ownership for runtime Controls, EditorPlugin docks, training, character selection, debugging, and the unified toolbox.

The Architecture Adoption Gate in the Spine identifies accepted evidence for amended implementation contracts. AD-21 is a new planning contract and must not be treated as historical implementation evidence.

## Playback Boundaries

Three modes remain separate:

1. V1 FR-11 training frame controls operate the training session.
2. FR-26 canonical-input training playback injects recorded inputs and re-simulates current live systems.
3. FR-28 authoritative EventBus Replay reproduces recorded envelopes exactly and supports real-time playback plus pause/resume, but not seeking, rewind, or replay-envelope single-frame stepping.

## Current Sources

- [V2 Architecture Spine](../_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md)
- [Final V1 Architecture Spine](../_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-15.archive/ARCHITECTURE-SPINE.md)
- [V2 PRD](../_bmad-output/planning-artifacts/prds/prd-ftg-framework-2026-07-26/prd.md)
- [Project V2 UX Contract](../_bmad-output/planning-artifacts/ux-v2-lean-contract.md)
- [Canonical Status](../_bmad-output/implementation-artifacts/sprint-status.yaml)
