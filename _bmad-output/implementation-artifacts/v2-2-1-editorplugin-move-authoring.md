---
story_key: v2-2-1-editorplugin-move-authoring
story_id: v2-2-1
date_created: 2026-08-01
epic: v2-2
baseline_commit: 9c84c43c790229062fe69c06f81d19de2423a38c
---

# Story 2.1: Safe EditorPlugin Move Authoring

Status: done

> **Readiness gate:** Satisfied. Following the 2026-08-01 reconciled-planning approvals and Project Lead kickoff, the changed planning set received the user-confirmed multi-role independent review required to enter `ready-for-dev`.

## Story

As a Godot developer authoring character moves,
I want a form-based editor dock that validates and atomically saves canonical move data,
so that I can create and edit moves without hand-writing JSON or risking files outside the move-data root.

## Acceptance Criteria

1. **S2.1-AC01 — Thin dock over canonical fields.**  
   **Given** the FTG Framework EditorPlugin is enabled  
   **When** the developer opens the move-authoring dock  
   **Then** it displays the current canonical fields for move identity, timing, damage, advantage, cancel windows, and physics-profile references  
   **And** the Godot class delegates state, validation, transformation, and persistence to pure-C# services.

2. **S2.1-AC02 — Current-schema canonical output.**  
   **Given** the developer enters a complete valid move definition  
   **When** Save is requested  
   **Then** the service builds and validates the complete logical candidate using the Data-owned current schema and validator  
   **And** the persisted UTF-8 JSON uses canonical `snake_case` field names and explicit required values.

3. **S2.1-AC03 — Semantic preservation and round trip.**  
   **Given** the developer edits one field in an existing current-version move  
   **When** the candidate is saved  
   **Then** all other compatible fields and references retain their prior semantic values  
   **And** loading the written file through the runtime Data path produces the same validated move represented by the editor model.

4. **S2.1-AC04 — Complete rejection before mutation.**  
   **Given** a required field is absent, null, invalid, non-finite, out of domain, or references a missing profile  
   **When** Save is requested  
   **Then** the complete candidate is rejected before filesystem mutation  
   **And** the original file, committed DataStore, form state, and active runtime snapshots remain unchanged.

5. **S2.1-AC05 — Path confinement.**  
   **Given** a move-dataset document identifier or derived path is rooted, contains a directory separator, contains parent traversal, or canonically resolves outside the configured move-data root  
   **When** the save path is derived from that document identifier  
   **Then** the request is rejected before opening a file  
   **And** no external path is read, created, modified, or deleted.

6. **S2.1-AC06 — Access-boundary collision defense.**  
   **Given** another move-dataset document identity aliases the same canonical destination or the validated parent/link changes before replacement  
   **When** the persistence service opens or replaces the actual path  
   **Then** canonical collisions and changed containment are rejected at the access boundary  
   **And** neither the existing move file nor an out-of-root target is modified.

7. **S2.1-AC07 — Coordinated atomic commit.**  
   **Given** the destination is inside the approved move-data root and the candidate is valid  
   **When** persistence begins  
   **Then** the service writes a same-filesystem staged file, flushes and validates it, and completes every fallible Data preparation before entering the coordinated commit boundary  
   **And** the commit performs the fallible atomic file replacement before a no-fail committed-dataset reference swap, so no observer reads a partial file or mismatched committed version.

8. **S2.1-AC08 — Failure equivalence.**  
   **Given** serialization, staging, validation, flush, replacement, or cleanup fails  
   **When** Save returns an error  
   **Then** the original destination remains byte-identical and loadable  
   **And** all error-reporting cleanup occurs before commit and touches only invocation-owned staging artifacts under the validated root; post-commit cleanup is best-effort diagnostic work and cannot convert a successful commit into a reported failure.

9. **S2.1-AC09 — Optimistic conflict.**  
   **Given** the file changed after the editor loaded its content version  
   **When** the developer saves a stale candidate  
   **Then** the write is rejected with a conflict result identifying expected and current versions  
   **And** unseen external changes are not overwritten.

10. **S2.1-AC10 — Editor adapter isolation.**  
    **Given** an enabled move-authoring dock using the approved EditorInterface, EditorSelection, and UndoRedo adapter boundary  
    **When** it integrates selection or undoable edits, or the plugin is disabled, its assembly reloads, or it is re-enabled  
    **Then** Godot singleton access remains confined to the thin adapter and pure-C# authoring services remain executable under `dotnet test` without a Godot process  
    **And** the dock, selection subscriptions, and transient adapter state are disposed exactly once, stale callbacks cannot act, and re-entry reconstructs the form from committed authoritative Data.

11. **S2.1-AC11 — Safe UndoRedo.**  
    **Given** a successful save participates in UndoRedo  
    **When** the developer invokes Undo or Redo  
    **Then** the corresponding complete validated file version is restored through the same atomic persistence boundary  
    **And** invalid or conflicting restoration is rejected without corrupting the current file.

12. **S2.1-AC12 — Evidence and approval gate.**  
    **Given** Story 2.1 is proposed for `ready-for-dev` or completion  
    **When** its risk/evidence row is reviewed  
    **Then** unit, Data/service integration, path-boundary, atomic-write fault injection, stale-version conflict, plugin disable/reload lifecycle, JSON round-trip, Godot editor, UndoRedo, scaffold, and runtime-load evidence is explicitly linked  
    **And** PO, Architect, and QA approvals required by PREP-2.4 are recorded.

## Tasks / Subtasks

- [x] **S2.1-A — Load, edit, validate, and report conflicts** (AC: 1-4, 9-10)
  - [x] Define a separate immutable authoring candidate/document model and field-addressable result types in `Scripts/Framework/Data/`; do not make runtime `MoveDefinition` objects mutable.
  - [x] Make Data the single presence-aware current-schema authority used by startup, editor load/save, hot reload, and later runtime tuning. Reject missing/null required fields, duplicate properties, coercion, non-finite values, unsupported schema semantics, duplicate IDs, and dangling references.
  - [x] Implement load/edit mapping that preserves every compatible untouched field and the loaded content identity.
  - [x] Implement a pure-C# ViewModel/service exposing canonical fields, inline errors that identify the field, rejected value, and recovery action, summary status, first-invalid-field focus intent, expected/current conflict details, reload, and explicit reapply through a newly validated candidate.
  - [x] Unit-test the complete P-DATA valid/invalid matrix (including null collection entries), semantic preservation, immutable form/candidate separation, preserved form state after validation/I/O/conflict failures, and stale-version result without a Godot process.

- [x] **S2.1-B — Path-confined, failure-atomic save and reload** (AC: 2-9, 11)
  - [x] Extend/generalize the existing `PhysicsDataPersistence` pattern rather than adding an unrelated writer: inject filesystem/replace/fault seams; keep same-directory staging, UTF-8 without BOM, flush-to-disk, and invocation-owned cleanup.
  - [x] Reject rooted, separator-bearing, traversal, empty, and canonical-escape identifiers before opening a path. Detect ordinal identity collisions, canonical destination aliases, and symlink/reparse/parent changes; revalidate containment at the actual access/replacement boundary.
  - [x] Add a move logical-dataset candidate and content-version transaction to `DataStore`: validate cross-document profile references and perform exactly one no-fail committed-reference swap only after successful atomic replacement.
  - [x] Ensure all fallible parsing, serialization, reference validation, allocation, staging, flush, and staged reload complete before commit; no rollback is allowed to become necessary after the first committed-state swap.
  - [x] Route Save, Undo, and Redo through the same expected-version transaction. Never restore by direct file write or silently overwrite a conflict.
  - [x] Fault-inject serialization, staging, staged validation, flush, replacement, pre-commit cleanup, containment recheck, and committed-version race. For every pre-commit/error-reporting failure, assert prior destination bytes/DataStore/form/runtime snapshots remain unchanged and Save returns an error.
  - [x] Separately fault-inject post-commit deletion of an invocation-owned staging artifact. Assert the new destination and DataStore stay committed, Save remains successful, a diagnostic is emitted, and no foreign or out-of-root artifact is touched.
  - [x] Prove canonical JSON reloads through `MoveDataLoader` to semantic equality, including restart/runtime load.

- [x] **S2.1-C — Enableable Godot dock, lifecycle, and distribution proof** (AC: 1, 10-12)
  - [x] Complete the existing `IEditorContext` / `GodotEditorContext` spike: register real Godot 4.5.1 `EditorUndoRedoManager` do/undo calls, expose required lifecycle behavior, and deterministically disconnect `EditorSelection` subscriptions.
  - [x] Add the thin `[Tool]` `EditorPlugin` entry and dock Control/scene under the fixed `Scripts/Framework/Editor/` / `FTG_Framework.Editor` ownership boundary; do not create a competing editor layer.
  - [x] Make `addons/ftg-framework/plugin.cfg` genuinely enableable and update addon/scaffold packaging and installation documentation. Preserve the Rider plugin and existing addon metadata policy.
  - [x] Add/remove the dock in plugin enter/exit, free it, dispose subscriptions/transient selection exactly once, and reconstruct from authoritative Data after disable/reload.
  - [x] Attach lifecycle callbacks/subscriptions before activation can emit notifications, and unwind every acquired resource after partial startup failure so no callback can target a half-initialized or disposed adapter.
  - [x] Implement the approved UX: keyboard operation and visual-order focus, focus restoration, named destructive confirmation, non-color-only status, 100%-200% scale readability, minimum width plus scrolling, progress/busy state, and cancellation only before commit.
  - [x] Capture real Godot 4.5.1 editor evidence for enable/open/select/edit/save/conflict/Undo/Redo/disable/reload plus a clean generated-scaffold plugin/load run and author-save-runtime-load E2E.
  - [x] Ensure manual/smoke acceptance restores any mutated tracked example data in finally-style cleanup, including failure paths.

- [x] **Parent acceptance and evidence closure** (AC: 12)
  - [x] Complete slices S2.1-A, S2.1-B, and S2.1-C; the parent story is not done from a partial horizontal layer.
  - [x] Store evidence under `_bmad-output/implementation-artifacts/evidence/v2-2-1/` using IDs E2.1-U/D/F/C/L/G/S/R/E and an SHA-256 inventory.
  - [x] Record command/scenario, Windows x64, exact .NET SDK and Godot 4.5.1 version, reviewed commit/hash, pass/fail/skip counts, exit code, duration/seed where relevant, reviewer, and acceptance state. Ordinary tests must not mutate tracked evidence.
  - [x] Obtain and record Product Owner, Architect, and QA acceptance of the story evidence and final E2.1-E outcome.

### Review Findings

- [x] [Review][Patch] Implement the selected complete structured authoring interaction: Move creation/selection plus structured add/edit/remove controls for `cancel_windows` and `collision_frames`, preserving pending edits safely when switching moves [Scripts/Framework/Editor/MoveAuthoringDock.cs:32]
- [x] [Review][Patch] Resolve packaged-addon data from its installed layout instead of assuming the repository layout [Scripts/Framework/Editor/FTGEditorPlugin.cs:20]
- [x] [Review][Patch] Refresh the ViewModel content identity after a successful undoable save so a second local save does not falsely conflict [Scripts/Framework/Editor/MoveAuthoringDock.cs:134]
- [x] [Review][Patch] Reapply conflicted edits by stable baseline identity and report duplicate-ID validation instead of losing renamed-move edits or throwing from `ToDictionary` [Scripts/Framework/Data/MoveAuthoringModels.cs:177]
- [x] [Review][Patch] Validate non-empty nested identifiers for cancel targets and collision boxes in in-memory authoring candidates [Scripts/Framework/Data/MoveDatasetCodec.cs:183]
- [x] [Review][Patch] Reconcile the evidence index's stale pending-approval wording with the recorded final role approvals, then regenerate the reviewed inventory as required [\_bmad-output/implementation-artifacts/evidence/v2-2-1/index.md:3]

## Dev Notes

### Binding Data Contract

- The current move-dataset schema version is `1`. The canonical top-level envelope is `{ "schema_version": 1, "moves": [...] }`, with both fields explicitly present and `moves` non-null. Startup, editor load/save, hot reload, and runtime load reject a missing, null, non-integral, or unsupported version. Ordinary load does not migrate older versions or accept newer versions; migration, if ever added, is a separate explicit command.
- Required canonical move fields are `move_id`, `startup`, `active`, `recovery`, `hit_advantage`, `block_advantage`, `damage`, `chain_repeatable`, `knockback_profile_id`, `cancel_windows`, and `collision_frames`; `move_name` is optional display metadata.
- The persisted unit is one current-schema move-dataset JSON document containing the complete `moves` collection. Editing one move atomically rewrites that document while preserving all other compatible moves. `move_id` never derives a path; a separate validated document identifier selects the file and its canonical identity scopes optimistic concurrency and UndoRedo.
- IDs are non-empty ordinal identifiers. Frame counts and damage are non-negative `int`; their checked total must fit `int`. Advantage values are signed `int`.
- Arrays must be explicit; empty is valid. Cancel ranges satisfy `0 <= start_frame <= end_frame <= total_frames`. Collision frames are unique within `1..total_frames`; box IDs are unique per frame/list; coordinates are finite; dimensions are finite and non-negative.
- Physics profiles remain schema version 1. AD-19 exact JSON types and presence semantics apply; IDs are unique, references resolve in the staged logical dataset, and numeric physics values are finite and non-negative.
- Persist with built-in `System.Text.Json` and canonical `snake_case`. Do not introduce another JSON package, a second validator, implicit defaults, ordinary-load migration, or editor-only format.

### Architecture and Transaction Guardrails

- `Data` owns schema, parsing, validation, serialization, migrations, logical-dataset preparation, persistence, and content identity. The Godot dock and editor context only adapt UI/editor APIs.
- Preserve immutable runtime models and initiation snapshots. A form edit is candidate state only; failed validation or persistence cannot alter the committed DataStore or active gameplay.
- Commit order is normative: validate complete logical candidate → serialize/stage beside destination → flush → reload/validate staged bytes → acquire coordinated commit boundary and recheck expected identity/path containment → atomically replace file → no-fail committed-dataset reference swap.
- Optimistic concurrency is shared infrastructure for Story 2.2. Exactly one writer from a common base version may win; the loser gets a recoverable conflict.
- Path checks must be enforced before file access and rechecked against the actual parent/link immediately before replacement. Never create parent directories for an untrusted derived path. Error-reporting cleanup is pre-commit and restricted to an invocation-owned staged artifact beneath the validated root; post-commit cleanup is best-effort and cannot reverse or report failure for an accepted commit.
- Generation and gameplay EventBus ordering are not part of this story. Do not publish a new gameplay event or weaken AD-12, AD-13, AD-15, AD-18, AD-19, or AD-20.

### Existing Code: Update, Change, Preserve

- `Scripts/Framework/Data/MoveDataLoader.cs` — currently loads runtime JSON with case-insensitive/default-tolerant behavior and partial validation. Extend it (or Data-owned shared schema components it calls) for exact required presence/types, duplicate-property rejection, complete P-DATA validation, canonical serialization, and profile references. Preserve `LoadFromFile`/`LoadFromJson`, runtime semantic output, and `[Data]` errors.
- `Scripts/Framework/Data/MoveDefinition.cs`, `CancelWindow.cs`, `CollisionFrameDefinition.cs`, `CollisionBoxDefinition.cs` — preserve immutable init-only runtime shapes. Use separate form/candidate objects.
- `Scripts/Framework/Data/DataStore.cs` and `Scripts/Framework/Core/IDataStore.cs` — current move data is read-only after construction while physics already has a lock/version/candidate-swap pattern. Add a compatible move/logical-dataset transaction and content identity without breaking existing reads; keep the previous committed reference on every failure.
- `Scripts/Framework/Data/PhysicsDataPersistence.cs` — reuse its same-directory staging, UTF-8-no-BOM, flush, replace/move, and owned-temp cleanup. Its arbitrary-path/parent-creation behavior is insufficient for untrusted authoring; add root confinement, access-boundary recheck, aliases, injection seams, and staged validation.
- `Scripts/Framework/Data/PhysicsProfileReferenceValidator.cs` — preserve ordinal Data-owned reference validation, extending it to the complete move candidate/logical dataset rather than duplicating it in Editor code.
- `Scripts/Framework/Editor/IEditorContext.cs` — retain the no-Godot abstraction but evolve its undo/lifecycle contract enough to report real registration and conflict-safe results.
- `Scripts/Framework/Editor/GodotEditorContext.cs` — currently subscribes with an anonymous handler that cannot be removed, and `CreateUndoAction` executes the do callback but registers no actual do/undo methods. Replace the placeholder with verified Godot 4.5.1 wiring and exact disposal; do not preserve the fake-success behavior.
- `Tests/FTG_Framework.Tests/Editor/EditorContextSpikeTests.cs` — expand beyond callback logging to contract/lifecycle/conflict tests; actual EditorUndoRedo behavior still requires Godot editor evidence.
- `Tests/FTG_Framework.Tests/Data/CollisionDataTests.cs` — update fixtures that currently treat omitted arrays/defaults as valid; explicit required arrays are now binding while existing collision validation coverage remains.
- `Scripts/Framework/Data/example_moves.json` — migrate to the approved current canonical document, including explicit arrays/required fields, while preserving runtime example semantics.
- `addons/ftg-framework/plugin.cfg`, `addons/ftg-framework/README.md`, `project.godot`, scaffold/package manifests and tests — the addon currently has no plugin script and the README says there is nothing to enable. Add and distribute the real plugin while preserving unrelated enabled plugins and generated-runtime behavior.

- `Scaffold/ftg-cli/ProjectScaffolder.cs` and `Scaffold/ftg-project-template/project.godot` require explicit updates: the scaffolder copies declared framework sources and the template but does not currently copy the `addons/ftg-framework` tree. Add plugin resources and registration through the existing validated scaffold pipeline; preserve source-manifest confinement, unrelated plugin settings, and generated-project build behavior.

### Plugin, UndoRedo, and Version Identity Contracts

- Make `addons/ftg-framework/plugin.cfg` a real Godot 4.5 C# editor-plugin manifest by adding a valid `script="..."` entry that resolves beneath the addon directory. The entry class must be a compiled `[Tool]` partial class derived from `EditorPlugin` (and guarded for tool builds where required by the project). Update the addon README to remove the statement that no EditorPlugin is shipped and document build-before-enable behavior.
- The `EditorPlugin` entry owns Godot editor API acquisition. Obtain its `EditorUndoRedoManager` from `EditorPlugin.GetUndoRedo()` and inject that dependency into `GodotEditorContext`; do not service-locate it from pure-C# authoring code or introduce another editor singleton boundary.
- One undoable save performs `CreateAction`, registers the do and undo `Callable` operations, and calls `CommitAction` exactly once. Because the default commit executes registered do operations, do not manually execute the same save before committing. Both callbacks restore a complete candidate through the normal atomic expected-version transaction; neither callback writes a file directly. Callback targets/lifetimes must remain valid while the action is registered, and disposal must prevent stale callbacks from mutating state.
- Represent persisted identity with immutable values that distinguish: validated document identifier, canonical destination identity, loaded content version/hash, and committed logical-dataset version. Use ordinal document-ID semantics and platform-correct canonical path identity. Conflict results expose expected and current content identities; Save, Undo, and Redo compare the expected content identity again inside the coordinated commit boundary.

### File Structure Guidance

- Pure authoring models/services/persistence belong under `Scripts/Framework/Data/` and remain Godot-free.
- Godot API adapters/dock entry belong only in `Scripts/Framework/Editor/` under `FTG_Framework.Editor`, matching the accepted spike and scaffold manifest. Do not create a parallel `Scripts/Editor/` boundary.
- Add addon registration/resources under `addons/ftg-framework/`; ensure the scaffold/package pipeline includes them.
- Put focused tests alongside existing Data, Editor, and Scaffold suites under `Tests/FTG_Framework.Tests/`.
- Evidence belongs only under `_bmad-output/implementation-artifacts/evidence/v2-2-1/` and is generated by an explicit acceptance workflow, never by normal tests.

### Testing Requirements

- **E2.1-U:** pure candidate, ViewModel, schema, validation, error/focus, and undo contract tests under `dotnet test`.
- **E2.1-D:** runtime loader plus DataStore/logical-dataset transaction integration, including complete profile references.
- **E2.1-F:** deterministic fault injection at every persistence seam. Pre-commit failures preserve prior byte/load/committed-reference/form identity and return error; post-commit owned-artifact deletion failure preserves the new committed file/reference, returns success with a diagnostic, and remains root-confined.
- **E2.1-C:** stale external modification, canonical alias, changed link/parent, simultaneous writers, and UndoRedo expected-version conflicts; exactly one writer wins.
- **E2.1-C cancellation boundary:** prove cancellation accepted before the coordinated commit leaves prior state intact, while cancellation arriving after commit begins cannot turn the accepted commit into failure.
- **E2.1-L:** enable/disable/reload/reconstruction and repeated selection subscribe/unsubscribe with no stale callback.
- **E2.1-G:** real Godot 4.5.1 dock, selection, keyboard/focus/scale/error recovery, and UndoRedo behavior. `dotnet test` cannot substitute for this evidence.
- **E2.1-S:** clean generated scaffold contains an enableable plugin and authored data loads by the scaffold runtime.
- **E2.1-R:** canonical UTF-8 JSON round-trips through ordinary current-schema `MoveDataLoader` with semantic equality and no migration/default path.
- **E2.1-E:** end-to-end select/create → edit → validate → save/conflict recovery → Undo/Redo → disable/reload → runtime-load confirmation.
- Run the full regression suite. Tests must isolate static/singleton state and avoid fixed-delay watcher assertions, following PREP-2.2.
- Record the discovered pre-change regression baseline and final pass/fail/skip totals in evidence; do not copy a stale historical test count.

### Scope Boundaries

- In scope: form-based canonical move authoring, inline validation, safe save, content-version conflicts, EditorSelection, UndoRedo, plugin lifecycle, addon/scaffold distribution, and runtime-load parity.
- Out of scope: Story 2.2 runtime tuning UI, graphical per-frame hitbox/hurtbox editor, drag-and-drop move timeline, implicit schema migration/defaulting, arbitrary filesystem browsing, and gameplay EventBus changes.

### Previous Work and Git Intelligence

- PREP-2.3 commit `a0c7043` is the accepted implementation foundation for Data/event/snapshot contracts. The newer commits `55c3284` and `ad19378` reconcile planning and readiness; treat them as contract changes, not implementation patterns to bypass.
- PREP-2.4 established stable slices S2.1-A/B/C and the E2.1 evidence vocabulary. Do not mark the parent done until all slices and final E2.1-E are accepted.
- V2 Epic 1 retrospective requires per-story code review, deterministic tests without fixed delays, EventBus/static isolation, generated-project evidence, and explicit manual Godot verification where editor/runtime APIs are involved.
- Extend the accepted editor context spike instead of starting another adapter. Its comments explicitly leave exact Godot 4.5.1 UndoRedo wiring for this story.

### Latest Technical Information

- The repository pin remains Godot .NET SDK 4.5.1, `net8.0` (Android `net9.0`), with repository .NET SDK policy. Do not upgrade as part of this story.
- Godot 4.5 requires C# editor scripts to be compiled before enabling the plugin; `plugin.cfg` must name the plugin script, and the entry derives from `EditorPlugin` and uses `[Tool]`. Initialize in `_EnterTree()` and remove/free the dock and subscriptions in `_ExitTree()`.
- `EditorPlugin.AddControlToDock(...)` must be paired with `RemoveControlFromDocks(...)` and freeing the Control on deactivation. `EditorPlugin.GetUndoRedo()` returns `EditorUndoRedoManager`; verify the exact C# callable registration signatures against the pinned 4.5.1 build and prove them in-editor.
- For the pinned C# API, create an action, register do/undo operations as Godot `Callable` values, then commit it. `CommitAction()` executes registered do operations by default, so the adapter must not invoke the do delegate separately before the default commit.
- `EditorInterface` is globally accessible in Godot 4.5 and `GetEditorInterface()` is deprecated, but singleton access must remain behind `GodotEditorContext` to preserve test isolation.

### Project Structure Notes

- No `project-context.md` was found; the Architecture Spine, Epic 2 package, UX contract, risk/evidence checklist, and accepted PREP records are the authoritative context for this story.
- The implementation prerequisites, readiness rerun, Project Lead kickoff, and subsequent user-confirmed multi-role independent review are complete. Story 2.1 is ready for development.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md#Story-21-Safe-EditorPlugin-Move-Authoring]
- [Source: _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md#PREP-24-Execution-Contract]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Binding-Parameter-Registry]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Risk-Matrix]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Evidence-Matrix]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Bounded-Vertical-Slice-Plans]
- [Source: _bmad-output/planning-artifacts/epic-2-ux-contract.md#Shared-Interaction-Rules]
- [Source: _bmad-output/planning-artifacts/epic-2-ux-contract.md#Required-Usability-Evidence]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-15--Hot-Reload-Data-Lifecycle]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-19--Explicit-Physics-Data-Presence-Semantics]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-21--Accessible-and-Lifecycle-Safe-Interaction-Boundary]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-prep24-closure.md#Remaining-Workflow-Conditions]
- [Source: _bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-rerun.md#Overall-Readiness-Status]
- [Source: _bmad-output/implementation-artifacts/evidence/v2-epic-2-reconciled-planning/role-approvals.md#Project-Lead-Kickoff]
- [Source: _bmad-output/implementation-artifacts/v2-prep-2-4-epic-2-planning-and-evidence-review.md#Dev-Notes]
- [Source: Scripts/Framework/Data/MoveDataLoader.cs]
- [Source: Scripts/Framework/Data/PhysicsDataPersistence.cs]
- [Source: Scripts/Framework/Data/DataStore.cs]
- [Source: Scripts/Framework/Editor/IEditorContext.cs]
- [Source: Scripts/Framework/Editor/GodotEditorContext.cs]
- [Source: https://docs.godotengine.org/en/4.5/tutorials/plugins/editor/making_plugins.html]
- [Source: https://docs.godotengine.org/en/4.5/classes/class_editorplugin.html]
- [Source: https://docs.godotengine.org/en/4.5/classes/class_editorinterface.html]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Story-context analysis only; no implementation or test execution performed.
- 2026-08-02 baseline: `dotnet test FTG_Framework.sln --no-restore --verbosity minimal` passed 858/858.
- 2026-08-02 RED/GREEN: added schema, authoring, persistence, conflict, cancellation, cleanup, and lifecycle tests.
- 2026-08-02 regression: full solution passed 878/878 in 68.5 seconds; framework build passed with 0 warnings and 0 errors.
- 2026-08-02 Godot 4.5.1 smoke: fixed addon discovery; verbose load/unload passed and the user confirmed the dock works normally. Full regression passed 885/885. Extended manual scenarios and role approvals remain pending.
- 2026-08-02 conflict-recovery RED/GREEN: added a failing reapply-on-latest test, implemented `ReapplyCommitted`, and exposed explicit reload/reapply dock actions. Final regression passed 886/886.
- 2026-08-02 generated-scaffold acceptance: clean CLI output built successfully; Godot loaded the FTG addon entry/plugin/dock and the generated runtime reported 6 moves loaded. Extended GUI scenarios and role approvals remain pending.
- 2026-08-02 conflict diagnosis RED/GREEN: reproduced false-success result reuse and stale-identity bypass in the UndoRedo save path; added regression tests, moved the Godot callback relay to a generated top-level type, cleared per-attempt results, and made Save/Undo/Redo carry forward committed content identities. Full regression passed 888/888.
- 2026-08-02 deferred-callback diagnosis RED/GREEN: real-editor retest proved the save result lagged one operation. Added a deterministic deferred-Do regression, made the initial persistence transaction synchronous, and registered the already-applied UndoRedo action with `CommitAction(false)`. Focused editor tests passed 9/9, the Godot project built with 0 warnings/errors, and the full regression passed 889/889.
- 2026-08-02 real-editor conflict acceptance: after restarting the external editor, the user confirmed first-attempt conflict detection, reapply preserving the `damage` edit, and the subsequent save updating the authoritative JSON all behaved correctly.
- 2026-08-02 real-editor UndoRedo acceptance: the user confirmed Godot Undo restored the prior complete JSON version and Redo restored the authored version, with authoritative Reload reflecting each state.
- 2026-08-02 real-editor lifecycle/accessibility/runtime acceptance: the user confirmed disable/re-enable, repeat re-entry, assembly reload, authoritative reconstruction, 200% scale, scrolling, keyboard traversal, and repository runtime loading 6 moves all passed. Manual data was restored byte-identically and the post-cleanup regression passed 889/889.
- 2026-08-02 real-editor EditorSelection acceptance: the user confirmed selection before disable, selection while disabled, and selection after re-enable produced no error or stale callback and reconstructed exactly one functional dock. S2.1-C evidence is complete.
- 2026-08-02 final cleanup regression: after the external editor reloaded from disk, restored `example_moves.json` to SHA-256 `0E702853F628408C3D1DA9B348EDB366D22C94D12583D73420613EBB5B1F9794`; the file retained that hash after the final 889/889 regression (65.5 seconds, seed 2202).
- 2026-08-02 evidence freeze: generated and verified `SHA256SUMS.md` for 28 implementation, test, packaging, restored-data, and evidence-index files. Inventory SHA-256 is `568E0F4E81063B38BC742E6FCCDAB12D5ECE8BA345FDE4569B75EF479FEA84BC`.
- 2026-08-02 independent role review round 1: Product Owner accepted; Architect and QA requested changes for canonical destination aliases, post-file-commit category rebuilding, missing flush/error-cleanup injection, and actual link-boundary evidence.
- 2026-08-02 review-fix RED/GREEN: added five regression cases; registered ordinal document identities per canonical destination; prebuilt move/category state for one no-fail reference swap after file commit; added flush and error-cleanup seams; and proved an actual Windows destination symlink race is rejected. Focused persistence passed 22/22, full regression passed 894/894, and Godot build passed with 0 warnings/errors.
- 2026-08-02 independent role review round 2: Product Owner and QA accepted; Architect found that staging failure before the temporary path returned could evade caller cleanup. Added a failing no-temp-artifact assertion, made `StageSameDirectory` clean its own created path on failure, then passed 22/22 focused persistence and 894/894 full regression with a 0-warning/error Godot build.
- 2026-08-02 independent role review round 3: Product Owner, Architect, and QA independently accepted inventory SHA-256 `B10CF21FAA795F74708FC1C3A6A086F8EAA44C64C2C23A8DF2451E5D27DAC65F` with no remaining blockers. QA independently reran 894/894 and verified restored example-data stability.
- 2026-08-02 adversarial code-review remediation: fixed all six accepted findings, including complete structured multi-move/collection authoring, packaged-addon data resolution, consecutive-save identity refresh, rename-safe conflict reapply, and nested identifier validation. Q1625 accepted every real-editor scenario; the restored-data regression passed 897/897 in 67.5 seconds and the framework build passed with 0 warnings/errors.
- 2026-08-02 post-review freeze: verified all 29 evidence inventory entries; `SHA256SUMS.md` SHA-256 is `C13E6BFBBD1859E6C4CB8B9A4CD5AB6BACF0D6DDD1110EEE16604B0876E43509`.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- The story is `ready-for-dev` after the required multi-role independent review was confirmed complete.
- Canonical story ACs, approved vertical slices, binding P-DATA values, UX contract, architecture invariants, existing-code state, and evidence gates are integrated.
- Implemented S2.1-A: strict schema-version-1 codec, immutable candidates, semantic editing/reapply, field-addressable validation, and pure-C# ViewModel behavior.
- Completed persistence: root confinement, canonical alias and reparse/link collision defense, content conflicts, staged reload, coordinated file/DataStore commit, cancellation, flush/error-cleanup injection, and cleanup diagnostics.
- Completed and manually accepted the EditorPlugin/UndoRedo/scaffold implementation in Godot 4.5.1, including lifecycle, selection, accessibility, conflict recovery, Undo/Redo, and runtime-load evidence.
- Confirmed real-editor enable/open/edit/save behavior with the user and restored the tracked example dataset after the save proof.
- Completed AC12 closure with independent Product Owner, Architect, and QA acceptance after two adversarial review-fix rounds; Story 2.1 is ready for code review.
- Completed adversarial code review, applied all six findings, obtained user acceptance for the new structured-authoring and consecutive-save flows, restored example data byte-identically, and closed Story 2.1.

### File List

- `_bmad-output/implementation-artifacts/v2-2-1-editorplugin-move-authoring.md` (NEW)
- `Scripts/Framework/Data/DataStore.cs` (MODIFIED)
- `Scripts/Framework/Data/MoveAuthoringModels.cs` (NEW)
- `Scripts/Framework/Data/MoveDataLoader.cs` (MODIFIED)
- `Scripts/Framework/Data/MoveDatasetCodec.cs` (NEW)
- `Scripts/Framework/Data/MoveDatasetPersistence.cs` (NEW)
- `Scripts/Framework/Data/PhysicsDataPersistence.cs` (MODIFIED)
- `Scripts/Framework/Data/example_moves.json` (MODIFIED)
- `Scripts/Framework/Editor/FTGEditorPlugin.cs` (NEW)
- `Scripts/Framework/Editor/GodotEditorContext.cs` (MODIFIED)
- `Scripts/Framework/Editor/IEditorContext.cs` (MODIFIED)
- `Scripts/Framework/Editor/MoveAuthoringDock.cs` (NEW)
- `Scripts/Framework/Editor/MoveAuthoringUndoService.cs` (NEW)
- `Tests/FTG_Framework.Tests/Data/CollisionDataTests.cs` (MODIFIED)
- `Tests/FTG_Framework.Tests/Data/MoveAuthoringTests.cs` (NEW)
- `Tests/FTG_Framework.Tests/Data/MoveDatasetPersistenceTests.cs` (NEW)
- `Tests/FTG_Framework.Tests/Editor/EditorContextSpikeTests.cs` (MODIFIED)
- `Tests/FTG_Framework.Tests/Editor/MoveAuthoringUndoServiceTests.cs` (NEW)
- `Tests/FTG_Framework.Tests/Scaffold/FtgCliTests.cs` (MODIFIED)
- `Scaffold/ftg-cli/ProjectScaffolder.cs` (MODIFIED)
- `Scaffold/ftg-project-template/project.godot` (MODIFIED)
- `Scaffold/package-addon.ps1` (MODIFIED)
- `Scaffold/package-addon.sh` (MODIFIED)
- `addons/ftg-framework/plugin.cfg` (MODIFIED)
- `addons/ftg-framework/FTGEditorPluginEntry.cs` (NEW)
- `FTG_Framework.csproj` (MODIFIED)
- `addons/ftg-framework/README.md` (MODIFIED)
- `project.godot` (MODIFIED)
- `_bmad-output/implementation-artifacts/evidence/v2-2-1/index.md` (NEW)
- `_bmad-output/implementation-artifacts/evidence/v2-2-1/SHA256SUMS.md` (NEW)
- `_bmad-output/implementation-artifacts/evidence/v2-2-1/role-approvals.md` (NEW)

### Change Log

- 2026-08-02: Implemented and regression-tested S2.1-A plus automated persistence and EditorPlugin foundations; retained `in-progress` because mandatory Godot editor evidence and role approvals are unavailable.
- 2026-08-02: Fixed addon discovery, recorded successful real-editor confirmation, restored manual-test data, and retained `in-progress` for remaining evidence and approvals.
- 2026-08-02: Added explicit conflict reload/reapply actions, passed 886/886 tests, and captured clean generated-scaffold plugin/build/runtime-load evidence; retained `in-progress` for extended real-editor E2E and role approvals.
- 2026-08-02: Fixed false-success and optimistic-conflict bypass defects found by real-editor acceptance; passed 888/888 tests and retained `in-progress` pending GUI retest and approvals.
- 2026-08-02: Eliminated the remaining one-operation UndoRedo callback lag, passed 889/889 tests, and retained `in-progress` pending real-editor conflict/reapply/UndoRedo retest and approvals.
- 2026-08-02: Recorded successful real-editor conflict/reapply/save acceptance; retained `in-progress` pending Undo/Redo, disable/reload, accessibility, runtime E2E, evidence inventory, and final role approvals.
- 2026-08-02: Recorded successful real-editor Undo/Redo acceptance; retained `in-progress` pending disable/reload, accessibility, runtime E2E, evidence inventory, and final role approvals.
- 2026-08-02: Recorded successful real-editor lifecycle, accessibility, and runtime E2E acceptance and restored manual-test data exactly; retained `in-progress` pending explicit EditorSelection capture, evidence inventory, and final role approvals.
- 2026-08-02: Completed S2.1-C with accepted EditorSelection lifecycle evidence; retained `in-progress` pending SHA-256 inventory and final PO/Architect/QA approvals.
- 2026-08-02: Generated and verified the final reviewed SHA-256 inventory; retained `in-progress` only for PO, Architect, and QA evidence acceptance.
- 2026-08-02: Addressed independent Architect/QA blocking findings with five new regression cases; passed 894/894 and retained `in-progress` for fresh final-inventory role approvals.
- 2026-08-02: Addressed the round-2 Architect staging-cleanup finding and retained `in-progress` for final inventory verification and fresh three-role acceptance.
- 2026-08-02: Product Owner, Architect, and QA independently accepted the final reviewed inventory; completed all tasks and moved the story to `review`.
- 2026-08-02: Completed adversarial code review, applied and verified all six findings, passed 897/897, restored example data exactly, and moved the story to `done`.
