---
baseline_commit: cddba608b099b00e7a0f25cc5d0e635286ff00a4
---

# Story PREP-2.3: Data, Event, and Snapshot Contracts

Status: done

## Story

As a framework architect and senior developer preparing Epic 2 foundations,
I want the corrected event, lifecycle, data, replay, and snapshot contracts enforced through bounded implementation slices,
so that Epic 2 stories can depend on proven architecture instead of resolving shared invariants independently.

## Acceptance Criteria

1. **P2.3-AC01 — Bounded execution.**  
   **Given** PREP-2.3 begins  
   **When** implementation work is planned  
   **Then** it is executed as four independently reviewable slices—event/lifecycle boundaries, transactional data, snapshot foundation, and completed-runtime audit  
   **And** the umbrella status remains open until all four slices have accepted evidence.

2. **P2.3-AC02 — Event and lifecycle contract.**  
   **Given** the event/lifecycle slice is implemented  
   **When** EventBus and StateMachine contract tests run  
   **Then** `MoveStarted` precedes its first `MoveFrameChanged`, dispatch remains non-reentrant, state events carry transitively immutable canonical snapshots, and inactive-epoch envelopes are rejected  
   **And** exact knockback tuple behavior remains compatible with PREP-2.1.

3. **P2.3-AC03 — Transactional data contract.**  
   **Given** the transactional-data slice is implemented  
   **When** every required physics field and logical-dataset boundary is tested  
   **Then** missing, null, duplicated, coerced, non-finite, negative, duplicate-ID, and dangling-reference candidates are rejected before swap  
   **And** startup fails fast while reload/writeback retains the prior valid dataset and file.

4. **P2.3-AC04 — Snapshot participant contract.**  
   **Given** the snapshot-foundation slice is implemented  
   **When** participants register with the Core snapshot coordinator  
   **Then** each owns one stable discriminator and versioned codec, capture occurs at one quiescent frame boundary, and serialized DTOs contain no Godot objects or mutable implementation containers  
   **And** the coordinator provides immutable Prepare results plus a deterministic no-fail Commit boundary under one reserved epoch.

5. **P2.3-AC05 — Failure-atomic Prepare.**  
   **Given** any participant, codec, component reference, cross-component identity, or generation high-water validation fails during snapshot Prepare  
   **When** restore is attempted  
   **Then** the reserved epoch and prepared replacements are discarded without mutating live state  
   **And** quarantined work is released against the unchanged active epoch.

6. **P2.3-AC06 — No-fail Commit.**  
   **Given** all participants prepare successfully  
   **When** the coordinator commits  
   **Then** state references and the reserved epoch/high-water map are installed without a fallible operation after the first live swap  
   **And** normal restore emits exactly one observe-only `StateRestored` before resuming at `frame + 1`, while replay bootstrap emits none.

7. **P2.3-AC07 — Completed-runtime audit.**  
   **Given** the completed-runtime audit slice examines EventBus, Replay, SceneManager, FileWatcher, Data, Physics, StateMachine, and scaffold behavior  
   **When** it compares implementation and tests with amended AD-12, AD-13, AD-15, AD-18, AD-19, and AD-20  
   **Then** every Adoption Gate row links passing evidence or an explicit corrective change completed within PREP-2.3  
   **And** Epic 3 product scope remains closed.

8. **P2.3-AC08 — Replay contract audit.**  
   **Given** Replay is audited against the corrected contracts  
   **When** full-catalog round-trip and playback tests run  
   **Then** the registry is exhaustive, phase order matches live dispatch, authoritative application suppresses duplicate derived publication, and replay bootstrap/end rebind lifecycle state correctly  
   **And** training input playback remains a distinct mutually exclusive mode.

9. **P2.3-AC09 — Fault-injection equivalence.**  
   **Given** failure injection is applied at every fallible snapshot Prepare point and supported persistence boundary  
   **When** tests compare pre-attempt and post-failure state  
   **Then** live component state, active epoch, queues, committed datasets, and prior files remain byte- or value-equivalent as applicable  
   **And** the evidence names every injected failure location.

10. **P2.3-AC10 — Isolated closure.**  
    **Given** all four slices and Adoption Gate rows are accepted  
    **When** PREP-2.3 closes  
    **Then** evidence is indexed under PREP-2.3 and linked from the relevant architecture and `v2-1` retrospective actions  
    **And** `v2-prep-2-3-data-event-and-snapshot-contracts` may move to `done` while remaining gate conditions stay unchanged.

## Tasks / Subtasks

- [x] **Slice 1 — Event and lifecycle boundaries** (AC: 1, 2, 7)
  - [x] Change AD-12 phase 3 so `MoveStarted` dispatches before the first `MoveFrameChanged` for the same move instance. Update both publisher order and EventBus phase order; do not reopen a phase or dispatch subscriber publications in the current frame.
  - [x] Introduce a canonical `StateStackSnapshot` immutable value, ordered bottom-to-top, with documented empty-snapshot → Idle fallback. `StateChangedEvent` must carry `PlayerId`, old top, new top, and the new canonical snapshot; `StateStackChangedEvent` must carry `PlayerId`, old canonical snapshot, and new canonical snapshot. Remove mutable arrays from both public contracts and materialize every reachable value at publication.
  - [x] Preserve PREP-2.1 lifecycle envelopes, inactive-epoch rejection, nested-lifecycle handling, non-wrapping epoch allocation, and exact `(PlayerId, LifecycleEpoch, GenerationId)` knockback semantics without compatibility shims.
  - [x] Prove each structural stack mutation emits its own old/new canonical snapshot; emit `StateChanged` only when the top changes; preserve committed effective-profile observation.
  - [x] Verification point: focused EventBus/FrameData/StateMachine tests pass and an immutable-payload mutation attempt cannot affect recorded or observed state.
  - [x] Store Slice 1 logs and contract matrix under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-3/slice-1-event-lifecycle/`.
  - [x] Complete that directory's `manifest.md` with owned scope, entry baseline, exact commands/results, evidence hashes, reviewer, and `pending|accepted|rejected` state. Slice 2 may consume Slice 1 contracts only after this manifest is accepted.

- [x] **Slice 2 — Transactional physics data** (AC: 1, 3, 7, 9)
  - [x] Implement presence-aware parsing before domain construction for the current `schema_version`. Reject missing/null required fields, duplicate properties, coercion, unknown required-version semantics, non-finite/negative numeric values, empty IDs, duplicate IDs, and dangling move/state references.
  - [x] Keep Data as the single schema/validator authority reused by startup, hot reload, transaction writeback, and future EditorPlugin callers. Do not add alternate validators.
  - [x] Treat the logical physics dataset—not one object—as the swap root. Validate all staged documents and cross-references before one versioned swap.
  - [x] Require each Data transaction/writeback to present the expected committed dataset/content version. Reject a stale writer before file or `DataStore` mutation; deterministic race tests must prove concurrent runtime/editor callers cannot overwrite unseen changes and that the losing writer preserves dataset and file bytes.
  - [x] Process watcher candidates by canonical path/content version in deterministic canonical-path order. Never infer multi-file atomicity from watcher burst timing; multi-file changes use the Data transaction API.
  - [x] Ordinary startup, reload, and writeback accept only the current schema. Add one explicit migration command that validates its declared source version, traverses an ordered complete migration registry to the one current target, validates the target, and atomically persists it; never apply migrated values only in memory. Reject unsupported sources, missing migration steps, invalid targets, and failed persistence without mutation.
  - [x] Implement writeback through a same-directory/same-volume temporary file and atomic replacement. Define the transaction commit point explicitly: validation, serialization, flush, and every injected fallible operation occur before replacement; after successful replacement, cleanup/notification diagnostics do not retroactively report the transaction as failed. Document unsupported-platform/existing-vs-new-file behavior, backup/cleanup rules, and which file metadata is preserved. Startup remains fail-fast; pre-commit reload/writeback failure retains both the prior committed dataset and prior file bytes.
  - [x] Verification point: the complete invalid-input matrix and every persistence failure point prove pre/post dataset and file equivalence.
  - [x] Store Slice 2 logs, invalid-input matrix, and fault map under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-3/slice-2-transactional-data/`.
  - [x] Complete that directory's `manifest.md` with scope, commands/results, hashes, reviewer, and acceptance state before Slice 3 consumes the dataset/version contract.

- [x] **Slice 3 — Versioned, failure-atomic snapshot foundation** (AC: 1, 4-6, 9)
  - [x] Add Core-owned coordinator, participant, codec, container DTO, immutable prepared-replacement, quiescence barrier, and restore-mode contracts. The minimum participant inventory is StateMachine, FrameData, Physics/character motion identity, Input history/buffer/charge state, and replay/training recording state; any omitted bound component requires an explicit architecture rationale. Registration order is deterministic; discriminators are unique and stable; component codecs are versioned and owned by their participant.
  - [x] Define `frame` as the last fully completed AD-12 frame. Capture all participants at one active epoch/frame while GameLoop advancement and EventBus dispatch are quiesced and newly queued work is quarantined.
  - [x] Keep serialized DTOs value-only: schema/framework versions, provenance, identities, and component payloads. Never serialize Godot objects, concrete stacks, mutable arrays/dictionaries, or engine implementation instances.
  - [x] During Prepare, reserve but do not activate one epoch; decode/migrate/validate each component; rebind lifecycle identities; validate shared character/move/trajectory/state/recording/data references; and prepare per-player generation counters strictly greater than restored in-flight generations. No live mutation is allowed.
  - [x] Make Commit a deterministic sequence of non-throwing reference swaps plus `ActivateReservedEpoch(preparedHighWaterMap)`. Prepared replacements contain every final reference. Participant Commit methods permit no allocation, serialization, validation, logging/event publication, user/plugin callback, timeout-capable lock, or fault-injection seam; complete these in Prepare before the first swap.
  - [x] On Prepare failure, discard replacements/reservation and release quarantine against the unchanged epoch. On normal success, discard old/quarantined envelopes, emit exactly one phase-7 observe-only `StateRestored` while quiesced, reject publication by its subscribers, and resume with `FrameAdvanced(frame + 1)`.
  - [x] Replay bootstrap uses the same machinery without `StateRestored`; replay end rebinds final state under a fresh epoch/high-water commit. Preserve the existing `IFrameDataEngine` rewind API until all its current UI and replay consumers are migrated explicitly.
  - [x] Verification point: failure injection at every Prepare branch proves live state/epoch/queues unchanged; success tests prove exact notification count, frame continuity, and deterministic multi-frame output.
  - [x] Maintain a checked-in stable fault-point catalog. For each ID, record the injected location and equivalence comparator: component value graph; active/reserved epoch and generation high-water state; current/next/pending/quarantined queue payload order plus envelope frame/epoch/sequence metadata; committed dataset version; and persisted bytes/existence/required metadata.
  - [x] Store Slice 3 logs, codec catalog, participant order, and complete fault-injection map under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-3/slice-3-snapshot-foundation/`.
  - [x] Complete that directory's `manifest.md` with scope, commands/results, hashes, reviewer, and acceptance state before Slice 4 treats AD-20 as implemented.

- [x] **Slice 4 — Completed-runtime and replay adoption audit** (AC: 1, 7-10)
  - [x] Audit GameLoop, EventBus, Replay, SceneManager, FileWatcher, Data, Input, FrameData, Physics, StateMachine, Combo, training/replay mode ownership, and scaffold runtime/scaffold parity against AD-12/13/15/18/19/20. Record every Adoption Gate row as `pending`, `accepted with evidence`, or `corrected and accepted in PREP-2.3`.
  - [x] Make the replay registry exhaustive over the recordable event catalog with stable discriminators, versioned payload codecs, and exactly one policy/applier per event class. Reject duplicate/unknown/incomplete registrations and incompatible payloads before playback mutation.
  - [x] Implement `ReplayCodec` as the sole persistence authority for the versioned UTF-8 container: header, AD-20 initial snapshot, ordered envelopes with discriminator/frame/phase/within-phase sequence/source-epoch provenance/payload, and integrity hash. Decode and validate the entire container atomically before playback mutation.
  - [x] Prove replay envelope phase and within-phase sequence match live EventBus dispatch. Authoritative playback must apply owner state while suppressing derived publications already in the stream; observe-only events cannot mutate authoritative state.
  - [x] Prove bootstrap and end use the snapshot coordinator lifecycle rebind rules and that deterministic replay and training input playback cannot be active in the same epoch.
  - [x] Treat architecture violations as corrective PREP-2.3 work. Put unrelated findings in the normal backlog; do not add product behavior, reopen Epic 3, or expand this gate without an approved course correction.
  - [x] Verification point: full-catalog round trip, phase parity, no-duplicate-publisher, lifecycle rebind, scaffold parity, Godot runtime, and full regression evidence pass.
  - [x] Store the adoption matrix and Slice 4 evidence under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-3/slice-4-runtime-audit/`.
  - [x] Complete that directory's `manifest.md` with scope, commands/results, hashes, reviewer, and acceptance state. The umbrella may close only when all four manifests and every Adoption Gate row are accepted.

- [x] **Integrate evidence and close only owned tracking** (AC: 1, 7-10)
  - [x] Create `_bmad-output/implementation-artifacts/evidence/v2-prep-2-3/index.md` linking each AC, slice, Adoption Gate row, exact command, test names, environment, exit/pass/fail/skip counts, and checked-in artifact hash.
  - [x] Record real focused/full-suite/Godot/scaffold execution logs and SHA-256 inventory. Tests must not write into tracked evidence directories.
  - [x] Use the PREP-2.2 deterministic stress harness conventions for singleton-mutating suites: fixed reproducible seeds/orders, bounded hang/outer timeouts, and a replay command for any failure.
  - [x] Update each AD-12/13/15/18/19/20 row in the canonical `ARCHITECTURE-SPINE.md` Adoption Gate with immutable evidence links and its acceptance result. Link the owned evidence from the `v2-1` retrospective action “Define immutable state-event boundaries, event ordering, and missing physics-field semantics.” Do not mark PREP-2.3 done while any row lacks accepted evidence.
  - [x] Keep this umbrella story open until every slice and row is accepted. At closure change only its owned story/action/evidence links; preserve `v2-epic-1: done`, `v2-epic-2: backlog`, PREP-2.4, and unrelated actions.

### Review Findings

- [x] [Review][Patch] Make snapshot Commit failure-atomic and reject frame overflow before the first swap [Scripts/Framework/Core/StateSnapshotCoordinator.cs:99]
- [x] [Review][Patch] Integrate required runtime snapshot participants and consume AD-20 snapshots at replay bootstrap/end [Scripts/Framework/Core/Replay/ReplayOrchestrator.cs:88]
- [x] [Review][Patch] Acquire playback mode before mutating ReplayPlayer or EventBus and roll back failed startup [Scripts/Framework/Core/Replay/ReplayOrchestrator.cs:95]
- [x] [Review][Patch] Replace separate profile-family commits with one logical physics-dataset swap root [Scripts/Framework/Data/DataStore.cs:216]
- [x] [Review][Patch] Use and document guaranteed atomic replacement for an existing persisted file [Scripts/Framework/Data/PhysicsDataPersistence.cs:28]
- [x] [Review][Patch] Validate snapshot framework compatibility before epoch reservation [Scripts/Framework/Core/StateSnapshotCoordinator.cs:67]
- [x] [Review][Patch] Reject replay entries outside FrameCount before player mutation [Scripts/Framework/Core/Replay/ReplayCodec.cs:64]
- [x] [Review][Patch] Reject case-variant duplicate JSON properties consistently with binding [Scripts/Framework/Data/PhysicsDataLoader.cs:171]
- [x] [Review][Patch] Suppress replay-derived publication by exact CLR type rather than type name [Scripts/Framework/Core/EventBus.cs:146]
- [x] [Review][Patch] Normalize missing/null replay container fields and entries to controlled invalid-data rejection [Scripts/Framework/Core/Replay/ReplayCodec.cs:45]
- [x] [Review][Patch] Validate within-phase replay sequence uniqueness and live-order provenance [Scripts/Framework/Core/Replay/ReplayCodec.cs:70]
- [x] [Review][Patch] Reject null snapshot component entries with a controlled invalid-data error [Scripts/Framework/Core/StateSnapshotCodec.cs:39]

## Dev Notes

### Developer Context and Guardrails

- This is non-epic readiness work with no FR coverage. It does not implement the user-facing FR-27 training save/load workflow, reopen Epic 1 or Epic 3, or start Epic 2.
- Execute and review slices in order: event/lifecycle → transactional data → snapshot foundation → runtime audit. Each slice must remain independently testable and reviewable while the single umbrella key stays open.
- PREP-2.1 is a binding baseline, not code to redesign: immutable epoch-stamped envelopes, inactive-epoch rejection, exact knockback phases/generations/occupancy, nested lifecycle safety, and Physics-publishes/StateMachine-mutates ownership must remain intact.
- PREP-2.2 is the binding test baseline: use `EventBusTestScope` and `EventBusTestCollection` for every direct or indirect singleton mutator; quiesce producers before teardown; do not silently erase dirty state; preserve deterministic seeded ordering and caller `FTG_TEST_SEED`.
- Preserve end-to-end behavior not restated by the ACs: current pause/step/frame semantics, FileWatcher main-thread handoff, legacy FrameData rewind consumers, generated-scaffold parity, replay v3 knockback compatibility, and closed Epic 3 features.
- No new runtime NuGet dependency is allowed. Use checked-in Godot 4.5.1, `net8.0` runtime targets, built-in `System.Text.Json`, and `System.IO` APIs.

### Existing Files to Update

- `Scripts/Framework/Core/EventBus.cs`
  - **Current:** double-buffered, non-reentrant phase dispatch with PREP-2.1 epoch envelopes; phase 3 currently orders `MoveFrameChanged` before `MoveStarted`.
  - **Change:** correct phase order; add coordinator quiescence/quarantine/reserved-epoch boundaries and replay policy integration without weakening epoch rejection.
  - **Preserve:** subscriber semantics, next-frame publication during dispatch, background reload synchronization, pause/step, recorder hooks, test-scope diagnostics, and exhaustion behavior.
- `Scripts/Framework/Engine/FrameData/FrameDataEngine.cs`
  - **Current:** publishes first frame change before move start and owns legacy in-memory rewind snapshots.
  - **Change:** correct first-move publication and integrate component snapshot preparation.
  - **Preserve:** move/cancel timing and existing rewind/replay behavior until deliberate consumer migration.
- `Scripts/Framework/Core/Events/StateChangedEvent.cs`, `StateStackChangedEvent.cs`; `Scripts/Framework/Engine/StateMachine/StateMachine.cs`; `Scripts/Framework/Characters/CharacterController.cs`
  - **Current:** state events expose mutable `CharacterState[]`; StateMachine materializes arrays at publication; CharacterController consumes them.
  - **Change:** use the canonical immutable snapshot and participant boundary.
  - **Preserve:** per-mutation emission, top-state rules, profile observation, stack ownership, and character presentation behavior.
- `Scripts/Framework/Data/PhysicsDataLoader.cs`, `PhysicsProfileReferenceValidator.cs`, `PhysicsProfileHotReloadService.cs`, `DataStore.cs`, `KnockbackProfile.cs`, `PhysicsResponseProfile.cs`
  - **Current:** DTO deserialization, candidate validation, and last-known-good hot reload exist, but missing fields can become CLR defaults and the full logical-dataset/writeback transaction is incomplete.
  - **Change:** presence-aware schema validation, cross-document transaction, deterministic candidates, and atomic persistence.
  - **Preserve:** Data ownership, immutable domain values, startup fail-fast, and retained last-valid reload behavior.
- `Scripts/Framework/Data/MoveDataLoader.cs` and the move/state profile reference paths in `DataStore.cs`/`StateMachine.cs`
  - **Current:** moves and state-owned profile IDs are loaded/resolved separately from physics profile candidates.
  - **Change:** include their IDs/references in the one logical physics-dataset candidate graph; reuse Data-owned validation instead of adding a parallel cross-reference validator.
  - **Preserve:** existing move schema/behavior outside the references governed by AD-19.
- `Scripts/Framework/Core/Replay/EventTypeRegistry.cs`, `ReplayEventJsonContext.cs`, `ReplayPlayer.cs`, `ReplayRecorder.cs`, `ReplayVersionValidator.cs`, `ReplayOrchestrator.cs`
  - **Current:** type registry/JSON replay pipeline exists but catalog coverage, stable versioned codecs, phase parity, authoritative suppression, and snapshot handoff are incomplete.
  - **Change:** enforce AD-13 catalog/policy/codec and AD-20 bootstrap/end behavior.
  - **Preserve:** accepted replay v3 validation, exact knockback payload contract, pause/real-time playback, and incompatible-file fail-fast behavior.
- `Scripts/Framework/Input/` and its `Scripts/Framework/Core/IInput*.cs` contracts; `Scripts/Framework/Engine/Combo/` and its `Scripts/Framework/Core/ICombo*.cs` contracts
  - **Current:** Input owns canonical history/buffer/charge mutation; Combo owns combo outcomes. Replay currently routes events without the complete owner-applier and snapshot participant contracts.
  - **Change:** audit/update Input snapshot participation and AD-13 Input/Combo owner appliers with derived-publication suppression.
  - **Preserve:** live Input/Combo ownership, SOCD-before-history ordering, accepted cancel/combo behavior, and ordinary live publication.
- `Scripts/Framework/Core/FrameStateSnapshot.cs`, `PhysicsMotionSnapshot.cs`, `PhysicsParticipantSnapshot.cs`, `IFrameDataEngine.cs`, `IPhysicsParticipant.cs`; `Scripts/Framework/Engine/Physics/PhysicsEngine.cs`; `Scripts/Framework/Core/GameLoop.cs`, `SceneManager.cs`, `FileWatcher.cs`
  - Adapt or audit these boundaries for coordinator participation, lifecycle/quiescence, and adoption evidence. Do not serialize their live implementation objects.
- `Scripts/Framework/UI/Training/ViewModels/PlaybackControlsViewModel.cs`, `Scripts/Framework/UI/Training/PlaybackControls.cs`, and `Scripts/Framework/Scenes/TrainingScene.cs`
  - Audit/migrate legacy rewind consumers and enforce training-input versus authoritative-replay mode mutual exclusion without moving gameplay logic into Godot UI adapters.

### Expected New Files

- Core immutable `StateStackSnapshot` and `StateRestoredEvent` contracts.
- Core snapshot coordinator, participant/codec interfaces, container/component DTOs, quiescence barrier, immutable prepared restore/result, restore mode, and fault-injection seams. Use repository naming and Core ownership conventions.
- Owning participant/codecs for StateMachine, FrameData, Physics/character motion, Input, and recording state, plus any additional component required by the validated cross-component graph.
- Core `ReplayCodec` (or the repository-equivalent exact name) as the sole replay-file persistence authority; do not leave persistence split between recorder/player/serializer helpers.
- Focused contract/fault-injection tests and the PREP-2.3 evidence tree described above.

### Testing Requirements

- Update/add focused tests in `Tests/FTG_Framework.Tests/Core/EventBusEpochTests.cs`, `Engine/FrameData/FrameDataEngineTests.cs`, `Engine/StateMachine/StateMachineTests.cs`, Data loader/store/hot-reload suites, existing snapshot suites, and Replay registry/player/integration/recording/knockback suites.
- Add Input and Combo owner-applier tests plus TrainingScene/playback-control mode-ownership tests. Every EventBus-mutating test must use the PREP-2.2 scope/collection boundary.
- Test the full invalid JSON partition, not representative happy paths only. Explicitly cover duplicate property names because ordinary object binding can otherwise hide which occurrence won.
- Treat the required physics schema as authoritative: `KnockbackProfile` requires `schema_version`, `profile_id`, `horizontal`, `vertical`, `gravity`, and `friction`; `PhysicsResponseProfile` requires `schema_version`, `profile_id`, `knockback_multiplier`, `gravity_scale`, `friction`, `air_friction`, and `participates_in_hitstop`. IDs are non-empty; numeric values are finite and non-negative; the Boolean is explicitly present. The logical candidate also includes current move and state-profile references, duplicate-ID detection, and dangling-reference rejection.
- Snapshot fault injection must enumerate every fallible Prepare/persistence location and compare live component values, active epoch, queues, committed dataset versions, and file bytes before/after.
- `FrameStateSnapshot` is a legacy FrameData rewind/replay DTO, not the AD-20 container. Keep that distinction explicit until `PlaybackControlsViewModel` and `ReplayOrchestrator` are deliberately migrated.
- Successful restore must prove one and only one observe-only phase-7 `StateRestored`, prohibited subscriber publication, no replay-bootstrap notification, and the next frame equals captured frame + 1.
- Replay tests must prove catalog exhaustiveness, exact phase/sequence parity, no duplicate derived publication, atomic rejection, lifecycle rebind, and mutual exclusion with training input playback.
- Run focused suites first, deterministic seeded stress for singleton/lifecycle paths, then `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj`, generated-scaffold build/smoke, and Godot runtime restore/replay proof.

### Latest Technical Notes

- Stay on the repository pins; current upstream availability is not authorization to upgrade Godot or target frameworks. Godot 4.5 C# uses the .NET 8 runtime, matching the project target. [Source: https://docs.godotengine.org/en/4.5/getting_started/scripting_c_sharp/c_sharp_basics.html]
- In newer .NET APIs `JsonDocumentOptions.AllowDuplicateProperties = false` rejects duplicates; the implementation must achieve equivalent explicit rejection on the repository's `net8.0` target rather than depending on a newer API. [Source: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsondocumentoptions.allowduplicateproperties]
- `File.Replace` can fail and has same-volume/platform constraints, so replacement, flush, cleanup, and every failure branch require explicit tests; a filesystem call is part of Prepare/persistence, never a no-fail live-state Commit. [Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace]
- .NET 10 added early validation for certain `System.Text.Json` metadata-name conflicts; do not rely on that newer runtime behavior under `net8.0`. Keep persisted schema names explicit and validate them in Data-owned parsing. [Source: https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/10/property-name-validation]

### Previous Story and Git Intelligence

- PREP-2.1 (`e139032`) established the epoch envelope and exact knockback tuple contract. Its review fixes—nested lifecycle staleness, synchronized background stamping, occupancy displacement, temporal monotonicity, and atomic replay rejection—are regression requirements here.
- PREP-2.2 (`3b4d1b3`, closure `cddba60`) established deterministic singleton test scopes, watcher outcome observation, seeded stress, and durable evidence. Reuse those facilities rather than creating another isolation or wait mechanism.
- PREP-2.2 closed with 811/811 tests passing; use that accepted suite as the regression baseline and explain any count change in evidence.

### Project Structure Notes

- Cross-module contracts and infrastructure live under `Scripts/Framework/Core/`; concrete engine behavior remains in its owning Engine/Data module and should be `internal` where host construction permits.
- Public events are `readonly record struct` values; payloads contain domain values only, while envelope epoch/frame metadata stays in EventBus.
- Persisted JSON uses Data-owned `snake_case` canonical schemas. Do not introduce an unrelated global naming-policy change to existing replay payloads.
- Tests stay under `Tests/FTG_Framework.Tests/`; evidence stays under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-3/`.

### References

- [Source: _bmad-output/planning-artifacts/epics/v2-epic-2-readiness-gate-non-epic.md#PREP-23-Data-Event-and-Snapshot-Contracts]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-12--V2-Frame-Processing-Order]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-13--Replay-Event-Stream-Recording]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-15--Hot-Reload-Data-Lifecycle]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-18--Immutable-State-Boundaries-and-Generation-Ownership]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-19--Explicit-Physics-Data-Presence-Semantics]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-20--Versioned-Failure-Atomic-State-Snapshots]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#Adoption-Gate]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-31.md#V2-Epic-2-Readiness-Correction]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01.md#Epic-Quality-Review]
- [Source: _bmad-output/implementation-artifacts/v2-prep-2-1-runtime-and-scaffold-boundary-hardening.md]
- [Source: _bmad-output/implementation-artifacts/v2-prep-2-2-deterministic-test-infrastructure.md]

## Definition of Done Matrix

| Slice | Implementation complete when | Evidence complete when |
|---|---|---|
| Event/lifecycle | AD-12 order, non-reentrancy, immutable state snapshots, epochs, and PREP-2.1 tuple compatibility all hold | Focused contract tests and immutable payload proof pass |
| Transactional data | One Data-owned presence/schema/dataset/persistence transaction rejects every invalid or failed candidate before swap | Invalid-input and persistence fault matrices prove dataset/file equivalence |
| Snapshot foundation | Deterministic registration/capture/Prepare/no-fail Commit and normal/replay lifecycle rules are implemented | Full Prepare fault map, exact notification/frame tests, and deterministic continuation pass |
| Runtime audit | Every Adoption Gate row passes or is corrected; Replay catalog/policies/lifecycle and runtime/scaffold parity comply | Full-catalog, parity, Godot, scaffold, stress, and full-suite logs are indexed |

## Evidence Index (Complete During Implementation)

| AC | Required evidence | Status / link |
|---|---|---|
| AC01 | Four slice review checkpoints and umbrella status audit | Pending |
| AC02 | Event order, non-reentrancy, immutable state, inactive epoch, PREP-2.1 tuple regressions | Pending |
| AC03 | Presence/schema/cross-reference matrix; startup/reload/writeback atomicity | Pending |
| AC04-AC06 | Participant/catalog/capture tests; Prepare failure matrix; no-fail Commit, notification, and frame continuity | Pending |
| AC07-AC08 | Adoption Gate matrix; replay catalog/phase/suppression/rebind/mode tests; runtime and scaffold proof | Pending |
| AC09 | Named snapshot and persistence fault locations with pre/post equivalence | Pending |
| AC10 | Evidence index, architecture/retro links, and isolated tracking diff | Pending |

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Snapshot focused: 13 passed; Replay focused: 120+ passed; scaffold: 58 passed.
- Deterministic EventBus stress: 20/20 seeded iterations, no timeout.
- Godot 4.5.1 mono headless runtime: exit 0; existing viewport/ObjectDB warnings recorded.
- Final regression: 844 passed, 0 failed, 0 skipped (seed 2202).

### Implementation Plan

- Execute the accepted slices sequentially: immutable lifecycle boundaries, transactional Data, failure-atomic snapshots, then Replay/runtime adoption audit.
- Keep all fallible decoding, validation, persistence, and graph work in Prepare; reserve Commit for prepared reference installation and epoch activation.
- Preserve replay v3 and legacy FrameData rewind consumers while introducing the canonical AD-20 and ReplayCodec boundaries.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Story quality validation completed: concurrency/migration, exact state-event shapes, participant/applier inventory, ReplayCodec authority, commit/fault semantics, and slice acceptance manifests were made decision-complete.
- Implemented canonical state-stack values, transactional physics datasets/persistence, snapshot coordination/codecs/fault diagnostics, exhaustive replay policies and atomic codec, and playback-mode ownership.
- Verified all four slice manifests and the Adoption Gate matrix; linked architecture and retrospective evidence without changing unrelated epic gates.
- Resolved all 12 adversarial review findings, including production GameLoop wiring for StateMachine, FrameData, Physics/motion, Input/charge, and recording snapshots; focused regression passed 261 tests and the full suite passed 858 tests.

### File List

- `.gitignore`
- `Scripts/Framework/Characters/CharacterController.cs`
- `Scripts/Framework/Core/EventBus.cs`
- `Scripts/Framework/Core/GameLoop.cs`
- `Scripts/Framework/Core/IPhysicsParticipant.cs`
- `Scripts/Framework/Core/Events/StateChangedEvent.cs`
- `Scripts/Framework/Core/Events/StateRestoredEvent.cs`
- `Scripts/Framework/Core/Events/StateStackChangedEvent.cs`
- `Scripts/Framework/Core/JsonStateSnapshotParticipant.cs`
- `Scripts/Framework/Core/Replay/EventTypeRegistry.cs`
- `Scripts/Framework/Core/Replay/PlaybackModeCoordinator.cs`
- `Scripts/Framework/Core/Replay/ReplayCodec.cs`
- `Scripts/Framework/Core/Replay/ReplayEntry.cs`
- `Scripts/Framework/Core/Replay/ReplayFile.cs`
- `Scripts/Framework/Core/Replay/ReplayOrchestrator.cs`
- `Scripts/Framework/Core/Replay/ReplayPlayer.cs`
- `Scripts/Framework/Core/Replay/ReplayRecorder.cs`
- `Scripts/Framework/Core/RuntimeSnapshotDtos.cs`
- `Scripts/Framework/Core/RuntimeStateSnapshotParticipant.cs`
- `Scripts/Framework/Core/StateSnapshotCodec.cs`
- `Scripts/Framework/Core/StateSnapshotContracts.cs`
- `Scripts/Framework/Core/StateSnapshotCoordinator.cs`
- `Scripts/Framework/Core/StateStackSnapshot.cs`
- `Scripts/Framework/Data/DataStore.cs`
- `Scripts/Framework/Data/PhysicsDataLoader.cs`
- `Scripts/Framework/Data/PhysicsDataMigrationRegistry.cs`
- `Scripts/Framework/Data/PhysicsDataPersistence.cs`
- `Scripts/Framework/Data/PhysicsProfileHotReloadService.cs`
- `Scripts/Framework/Data/example_knockback_profiles.json`
- `Scripts/Framework/Data/example_physics_response_profiles.json`
- `Scripts/Framework/Engine/StateMachine/StateMachine.cs`
- `Scripts/Framework/Engine/FrameData/FrameDataEngine.cs`
- `Scripts/Framework/Engine/Physics/PhysicsEngine.cs`
- `Scripts/Framework/Input/ChargeTracker.cs`
- `Scripts/Framework/Input/CircularBuffer.cs`
- `Scripts/Framework/Input/InputHistory.cs`
- `Tests/FTG_Framework.Tests/Core/StateSnapshotCodecTests.cs`
- `Tests/FTG_Framework.Tests/Core/StateSnapshotCoordinatorTests.cs`
- `Tests/FTG_Framework.Tests/Data/DataStorePhysicsProfileTests.cs`
- `Tests/FTG_Framework.Tests/Data/PhysicsDataLoaderTests.cs`
- `Tests/FTG_Framework.Tests/Data/PhysicsDataPersistenceTests.cs`
- `Tests/FTG_Framework.Tests/Data/PhysicsProfileHotReloadServiceTests.cs`
- `Tests/FTG_Framework.Tests/Engine/FrameData/FrameDataEngineTests.cs`
- `Tests/FTG_Framework.Tests/Engine/Physics/PhysicsEngineTests.cs`
- `Tests/FTG_Framework.Tests/Engine/StateMachine/StateMachineTests.cs`
- `Tests/FTG_Framework.Tests/Replay/EventBusRecordingTests.cs`
- `Tests/FTG_Framework.Tests/Replay/EventBusReplaySuppressionTypeTests.cs`
- `Tests/FTG_Framework.Tests/Replay/EventTypeRegistryTests.cs`
- `Tests/FTG_Framework.Tests/Replay/PlaybackModeCoordinatorTests.cs`
- `Tests/FTG_Framework.Tests/Replay/ReplayCodecTests.cs`
- `Tests/FTG_Framework.Tests/Replay/ReplayPlayerTests.cs`
- `Tests/FTG_Framework.Tests/Replay/ReplayOrchestratorTests.cs`
- `Tests/FTG_Framework.Tests/eventbus-test-inventory.txt`
- `_bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md`
- `_bmad-output/implementation-artifacts/evidence/v2-prep-2-3/`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/v2-prep-2-3-data-event-and-snapshot-contracts.md`
- `_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md`

## Change Log

- 2026-08-01: Created implementation-ready PREP-2.3 story with four bounded slices, architecture/code guardrails, fault-injection requirements, and evidence plan.
- 2026-08-01: Validated against the BMad story-quality checklist and corrected architecture, data-transaction, snapshot, replay, audit, and acceptance-boundary gaps.
- 2026-08-01: Implemented and verified all four PREP-2.3 slices; accepted Adoption Gate evidence and moved the story to review.
- 2026-08-01: Applied all 12 code-review patches, integrated real runtime snapshot owners and AD-20 replay handoff, passed 858 tests, and moved the story to done.
