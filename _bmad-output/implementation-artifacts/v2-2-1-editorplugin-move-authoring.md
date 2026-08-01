---
story_key: v2-2-1-editorplugin-move-authoring
story_id: v2-2-1
date_created: 2026-08-01
epic: v2-2
---

# Story 2.1: Safe EditorPlugin Move Authoring

Status: backlog

> **Readiness gate:** This context-complete draft must not transition to `ready-for-dev` until Product Owner, Architect, and QA approve fresh hashes for the current reconciled Epic 2 planning set and the project lead records the separate Epic 2 kickoff after re-verifying every start-gate condition.

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

- [ ] **S2.1-A — Load, edit, validate, and report conflicts** (AC: 1-4, 9-10)
  - [ ] Define a separate immutable authoring candidate/document model and field-addressable result types in `Scripts/Framework/Data/`; do not make runtime `MoveDefinition` objects mutable.
  - [ ] Make Data the single presence-aware current-schema authority used by startup, editor load/save, hot reload, and later runtime tuning. Reject missing/null required fields, duplicate properties, coercion, non-finite values, unsupported schema semantics, duplicate IDs, and dangling references.
  - [ ] Implement load/edit mapping that preserves every compatible untouched field and the loaded content identity.
  - [ ] Implement a pure-C# ViewModel/service exposing canonical fields, inline errors, summary status, first-invalid-field focus intent, expected/current conflict details, reload, and explicit reapply through a newly validated candidate.
  - [ ] Unit-test the complete P-DATA valid/invalid matrix, semantic preservation, immutable form/candidate separation, and stale-version result without a Godot process.

- [ ] **S2.1-B — Path-confined, failure-atomic save and reload** (AC: 2-9, 11)
  - [ ] Extend/generalize the existing `PhysicsDataPersistence` pattern rather than adding an unrelated writer: inject filesystem/replace/fault seams; keep same-directory staging, UTF-8 without BOM, flush-to-disk, and invocation-owned cleanup.
  - [ ] Reject rooted, separator-bearing, traversal, empty, and canonical-escape identifiers before opening a path. Detect ordinal identity collisions, canonical destination aliases, and symlink/reparse/parent changes; revalidate containment at the actual access/replacement boundary.
  - [ ] Add a move logical-dataset candidate and content-version transaction to `DataStore`: validate cross-document profile references and perform exactly one no-fail committed-reference swap only after successful atomic replacement.
  - [ ] Ensure all fallible parsing, serialization, reference validation, allocation, staging, flush, and staged reload complete before commit; no rollback is allowed to become necessary after the first committed-state swap.
  - [ ] Route Save, Undo, and Redo through the same expected-version transaction. Never restore by direct file write or silently overwrite a conflict.
  - [ ] Fault-inject serialization, staging, staged validation, flush, replacement, pre-commit cleanup, containment recheck, and committed-version race. For every pre-commit/error-reporting failure, assert prior destination bytes/DataStore/form/runtime snapshots remain unchanged and Save returns an error.
  - [ ] Separately fault-inject post-commit deletion of an invocation-owned staging artifact. Assert the new destination and DataStore stay committed, Save remains successful, a diagnostic is emitted, and no foreign or out-of-root artifact is touched.
  - [ ] Prove canonical JSON reloads through `MoveDataLoader` to semantic equality, including restart/runtime load.

- [ ] **S2.1-C — Enableable Godot dock, lifecycle, and distribution proof** (AC: 1, 10-12)
  - [ ] Complete the existing `IEditorContext` / `GodotEditorContext` spike: register real Godot 4.5.1 `EditorUndoRedoManager` do/undo calls, expose required lifecycle behavior, and deterministically disconnect `EditorSelection` subscriptions.
  - [ ] Add the thin `[Tool]` `EditorPlugin` entry and dock Control/scene under the fixed `Scripts/Framework/Editor/` / `FTG_Framework.Editor` ownership boundary; do not create a competing editor layer.
  - [ ] Make `addons/ftg-framework/plugin.cfg` genuinely enableable and update addon/scaffold packaging and installation documentation. Preserve the Rider plugin and existing addon metadata policy.
  - [ ] Add/remove the dock in plugin enter/exit, free it, dispose subscriptions/transient selection exactly once, and reconstruct from authoritative Data after disable/reload.
  - [ ] Implement the approved UX: keyboard operation and visual-order focus, focus restoration, named destructive confirmation, non-color-only status, 100%-200% scale readability, minimum width plus scrolling, progress/busy state, and cancellation only before commit.
  - [ ] Capture real Godot 4.5.1 editor evidence for enable/open/select/edit/save/conflict/Undo/Redo/disable/reload plus a clean generated-scaffold plugin/load run and author-save-runtime-load E2E.

- [ ] **Parent acceptance and evidence closure** (AC: 12)
  - [ ] Complete slices S2.1-A, S2.1-B, and S2.1-C; the parent story is not done from a partial horizontal layer.
  - [ ] Store evidence under `_bmad-output/implementation-artifacts/evidence/v2-2-1/` using IDs E2.1-U/D/F/C/L/G/S/R/E and an SHA-256 inventory.
  - [ ] Record command/scenario, Windows x64, exact .NET SDK and Godot 4.5.1 version, reviewed commit/hash, pass/fail/skip counts, exit code, duration/seed where relevant, reviewer, and acceptance state. Ordinary tests must not mutate tracked evidence.
  - [ ] Obtain and record Product Owner, Architect, and QA acceptance of the story evidence and final E2.1-E outcome.

## Dev Notes

### Binding Data Contract

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
- `Tests/FTG.Framework.Tests/Editor/EditorContextSpikeTests.cs` — expand beyond callback logging to contract/lifecycle/conflict tests; actual EditorUndoRedo behavior still requires Godot editor evidence.
- `Tests/FTG.Framework.Tests/Data/CollisionDataTests.cs` — update fixtures that currently treat omitted arrays/defaults as valid; explicit required arrays are now binding while existing collision validation coverage remains.
- `Scripts/Framework/Data/example_moves.json` — migrate to the approved current canonical document, including explicit arrays/required fields, while preserving runtime example semantics.
- `addons/ftg-framework/plugin.cfg`, `addons/ftg-framework/README.md`, `project.godot`, scaffold/package manifests and tests — the addon currently has no plugin script and the README says there is nothing to enable. Add and distribute the real plugin while preserving unrelated enabled plugins and generated-runtime behavior.

### File Structure Guidance

- Pure authoring models/services/persistence belong under `Scripts/Framework/Data/` and remain Godot-free.
- Godot API adapters/dock entry belong only in `Scripts/Framework/Editor/` under `FTG_Framework.Editor`, matching the accepted spike and scaffold manifest. Do not create a parallel `Scripts/Editor/` boundary.
- Add addon registration/resources under `addons/ftg-framework/`; ensure the scaffold/package pipeline includes them.
- Put focused tests alongside existing Data, Editor, and Scaffold suites under `Tests/FTG.Framework.Tests/`.
- Evidence belongs only under `_bmad-output/implementation-artifacts/evidence/v2-2-1/` and is generated by an explicit acceptance workflow, never by normal tests.

### Testing Requirements

- **E2.1-U:** pure candidate, ViewModel, schema, validation, error/focus, and undo contract tests under `dotnet test`.
- **E2.1-D:** runtime loader plus DataStore/logical-dataset transaction integration, including complete profile references.
- **E2.1-F:** deterministic fault injection at every persistence seam. Pre-commit failures preserve prior byte/load/committed-reference/form identity and return error; post-commit owned-artifact deletion failure preserves the new committed file/reference, returns success with a diagnostic, and remains root-confined.
- **E2.1-C:** stale external modification, canonical alias, changed link/parent, simultaneous writers, and UndoRedo expected-version conflicts; exactly one writer wins.
- **E2.1-L:** enable/disable/reload/reconstruction and repeated selection subscribe/unsubscribe with no stale callback.
- **E2.1-G:** real Godot 4.5.1 dock, selection, keyboard/focus/scale/error recovery, and UndoRedo behavior. `dotnet test` cannot substitute for this evidence.
- **E2.1-S:** clean generated scaffold contains an enableable plugin and authored data loads by the scaffold runtime.
- **E2.1-R:** canonical UTF-8 JSON round-trips through ordinary current-schema `MoveDataLoader` with semantic equality and no migration/default path.
- **E2.1-E:** end-to-end select/create → edit → validate → save/conflict recovery → Undo/Redo → disable/reload → runtime-load confirmation.
- Run the full regression suite. Tests must isolate static/singleton state and avoid fixed-delay watcher assertions, following PREP-2.2.

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
- Godot 4.5 requires C# editor scripts to be compiled before enabling the plugin; the entry derives from `EditorPlugin` and uses `[Tool]`. Initialize in `_EnterTree()` and remove/free the dock and subscriptions in `_ExitTree()`.
- `EditorPlugin.AddControlToDock(...)` must be paired with `RemoveControlFromDocks(...)` and freeing the Control on deactivation. `EditorPlugin.GetUndoRedo()` returns `EditorUndoRedoManager`; verify the exact C# callable registration signatures against the pinned 4.5.1 build and prove them in-editor.
- `EditorInterface` is globally accessible in Godot 4.5 and `GetEditorInterface()` is deprecated, but singleton access must remain behind `GodotEditorContext` to preserve test isolation.

### Project Structure Notes

- No `project-context.md` was found; the Architecture Spine, Epic 2 package, UX contract, risk/evidence checklist, and accepted PREP records are the authoritative context for this story.
- The implementation prerequisites and tracked V2-1 actions are complete, but current planning hashes no longer match the preserved PREP-2.4 approval record after planning reconciliation. Epic 2 therefore remains in backlog pending fresh PO/Architect/QA approval and a separate project-lead kickoff. The three approved slice keys are recorded as backlog for traceability only.

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
- [Source: _bmad-output/implementation-artifacts/v2-prep-2-4-epic-2-planning-and-evidence-review.md#Dev-Notes]
- [Source: Scripts/Framework/Data/MoveDataLoader.cs]
- [Source: Scripts/Framework/Data/PhysicsDataPersistence.cs]
- [Source: Scripts/Framework/Data/DataStore.cs]
- [Source: Scripts/Framework/Editor/IEditorContext.cs]
- [Source: Scripts/Framework/Editor/GodotEditorContext.cs]
- [Source: https://docs.godotengine.org/en/4.5/tutorials/plugins/editor/making_plugins.html]
- [Source: https://docs.godotengine.org/en/4.5/classes/class_editorplugin.html]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Story-context analysis only; no implementation or test execution performed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Comprehensive story context was saved, but status remains `backlog` because the binding refreshed-hash approval and separate Epic 2 kickoff conditions are not evidenced.
- Canonical story ACs, approved vertical slices, binding P-DATA values, UX contract, architecture invariants, existing-code state, and evidence gates are integrated.

### File List

- `_bmad-output/implementation-artifacts/v2-2-1-editorplugin-move-authoring.md` (NEW)
