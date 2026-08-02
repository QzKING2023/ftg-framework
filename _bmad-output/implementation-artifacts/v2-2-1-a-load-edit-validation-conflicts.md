---
story_key: v2-2-1-a-load-edit-validation-conflicts
story_id: v2-2-1-a
date_created: 2026-08-02
epic: v2-2
parent_story: v2-2-1-editorplugin-move-authoring
baseline_commit: 6ee5051e9022e02db6fccef7677848ef03324b35
---

# Story 2.1-A: Load, Edit, Validation, and Conflicts

Status: done

## Story

As a Godot developer authoring character moves,
I want to load and edit canonical move data with complete inline validation and recoverable conflict results,
so that I can correct invalid values or reconcile external changes without losing my work or corrupting authoritative data.

## Acceptance Criteria

1. **Canonical load and editable projection (parent S2.1-AC01/03).**
   **Given** a valid current-version move dataset is loaded from the configured move-data root
   **When** the authoring ViewModel selects an existing move
   **Then** it exposes move identity, timing, damage, advantage, cancel windows, collision frames, and physics-profile references through a separate candidate/form model
   **And** the immutable runtime `MoveDefinition` and every compatible untouched value, collection item, ordering decision, reference, document identity, and content identity remain unchanged until an authoritative reload or successful commit.

2. **Complete deterministic validation (parent S2.1-AC02/04).**
   **Given** one candidate contains multiple invalid scalar, collection, nested-row, identity, range, non-finite, overflow, duplicate, or dangling-reference values
   **When** validation runs
   **Then** Data returns the complete deterministic ordered set of field-addressable errors rather than only the first failure
   **And** every error identifies the field/path, rejected value where safe, and recovery action while the summary and first-invalid-field focus intent are derived from that same result.

3. **One canonical validation authority (parent S2.1-AC01/02/04).**
   **Given** startup, editor load/edit/save, hot reload, runtime load, and later runtime tuning consume move data
   **When** schema or semantic validation is required
   **Then** all paths reuse the Data-owned schema-version-1 codec and logical-dataset/profile-reference validation
   **And** neither the Godot dock nor a new service duplicates schema rules, adds implicit defaults/coercion/migration, or introduces another JSON library.

4. **Failure preserves all authoritative and candidate state (parent S2.1-AC04).**
   **Given** loading, parsing, validation, reference validation, or pre-commit I/O fails
   **When** the failure is returned to the authoring flow
   **Then** the original file, committed `DataStore`, active runtime snapshots, selected move, form values, and loaded expected identity remain unchanged
   **And** the developer can correct the displayed errors and retry without reconstructing the edit.

5. **Recoverable optimistic conflict (parent S2.1-AC09).**
   **Given** the canonical document changed after the editor loaded its content identity
   **When** Save compares the expected identity again inside the coordinated commit boundary
   **Then** the stale write is rejected with both expected and current identities and no unseen change is overwritten
   **And** Reload explicitly discards the pending candidate while Reapply merges only the user's changed fields onto the latest compatible dataset, revalidates the complete candidate, and reports rename/duplicate conflicts without throwing or silently dropping edits.

6. **Thin, lifecycle-safe presentation (parent S2.1-AC01/10).**
   **Given** the Godot authoring dock presents validation and conflict state
   **When** errors include scalar and nested collection paths, or the plugin is disabled/reloaded/re-enabled
   **Then** presentation maps every error to its field or nested row, shows a non-color-only summary, focuses the first invalid editable control, and preserves keyboard navigation and form state
   **And** all business state remains pure C#, Godot singleton access stays behind the existing adapter, subscriptions are disposed exactly once, stale callbacks cannot act, and re-entry reconstructs from committed Data.

7. **Regression and evidence closure.**
   **Given** the slice is proposed for completion
   **When** automated and editor evidence is reviewed
   **Then** E2.1-U/D/C/L/R evidence covers the complete invalid matrix, deterministic multi-error ordering, semantic preservation, conflict identities, reload/reapply, lifecycle isolation, and runtime round trip
   **And** the full regression suite confirms that the already-completed path-confinement, atomic persistence, UndoRedo, scaffold, and runtime-load behavior from slices B/C remains intact.

## Tasks / Subtasks

- [x] **Reconcile the completed parent implementation with this slice** (AC: 1-7)
  - [x] Treat commit `6ee5051` and the completed parent story as the implementation baseline; extend existing types and tests instead of recreating authoring, codec, persistence, ViewModel, or dock layers.
  - [x] Record which S2.1-A requirements already pass and which require changes; the known gap is fail-fast/single-error validation and single-error dock presentation.

- [x] **Return complete field-addressable validation results** (AC: 2-4)
  - [x] Refactor candidate validation in `MoveDatasetCodec` to accumulate all independent semantic errors in stable field/path order while preserving strict parse/load exceptions and rejection of invalid documents.
  - [x] Cover every P-DATA scalar and nested rule, including null collection entries, duplicate identifiers/properties, checked frame totals, ranges, finite coordinates/dimensions, and missing physics profiles.
  - [x] Keep malformed JSON/type/schema failures bounded and deterministic; do not synthesize a partially valid candidate merely to collect later semantic errors.
  - [x] Preserve manual token-level duplicate-property detection for `net8.0`; do not rely on the .NET 10-only `JsonDocumentOptions.AllowDuplicateProperties` API.

- [x] **Present all validation errors without moving business rules into Godot** (AC: 2, 6)
  - [x] Add or extract a pure-C# presentation mapping from validation paths to scalar controls and cancel/collision/box rows.
  - [x] Update `MoveAuthoringDock` to render inline errors plus a summary, restore focus to the first invalid editable control, and keep pending values intact.
  - [x] Preserve non-color-only status, visual-order keyboard navigation, scroll/minimum-width behavior, and 100%-200% editor scaling.

- [x] **Harden conflict recovery around existing optimistic concurrency** (AC: 4-5)
  - [x] Verify Save exposes immutable expected/current content identities and never converts a stale result into success.
  - [x] Verify Reload is an explicit discard and Reapply is a three-way merge based on stable baseline identity, preserves unrelated external changes, handles move renames/duplicate IDs, and revalidates before any persistence request.
  - [x] Do not alter root confinement, same-directory staging, atomic replacement, coordinated DataStore swap, or UndoRedo transaction semantics owned by slice B/C.

- [x] **Add deterministic tests and evidence** (AC: 1-7)
  - [x] Extend `MoveAuthoringTests` with multi-invalid candidates, complete ordered error sets, all canonical/nested paths, form/candidate immutability, untouched semantic preservation, and reload/reapply behavior.
  - [x] Extend persistence tests only where needed for expected/current identity, stale-write no-mutation, and candidate preservation; reuse existing fault/path coverage.
  - [x] Test any extracted presentation mapper without instantiating Godot `Control`; retain `#if TOOLS` isolation.
  - [x] Run focused Data/Editor tests, the full solution regression suite, a warning-free framework build, and the existing Godot editor lifecycle/conflict smoke scenario. Store accepted slice evidence under `_bmad-output/implementation-artifacts/evidence/v2-2-1/` without allowing ordinary tests to modify tracked evidence.

### Review Findings

- [x] [Review][Patch] High — Route malformed scalar form values through complete field-addressable validation instead of throwing on the first `ParseInt`/`ParseBool` failure [Scripts/Framework/Editor/MoveAuthoringDock.cs:254]
- [x] [Review][Patch] High — Present persistence `ValidationFailed` errors inline instead of replacing indexed missing-profile results with a generic save failure [Scripts/Framework/Editor/MoveAuthoringDock.cs:273]
- [x] [Review][Patch] High — Preserve move identity when grouping and focusing validation errors so non-selected moves cannot annotate the currently selected form [Scripts/Framework/Data/MoveAuthoringModels.cs:38]

## Dev Notes

### Baseline and Non-Reinvention Guardrail

- Parent Story 2.1 is already `done`, and commit `6ee5051` implemented all three parent slices. This standalone slice exists because sprint tracking still listed S2.1-A separately and code analysis found a specific conformance gap. Do not create parallel models, codecs, persistence services, ViewModels, editor namespaces, or plugin entries.
- Existing strengths to preserve: immutable authoring records; canonical schema-v1 codec; SHA-256 content identities; stale-write rejection; three-way reapply; DataStore prepared-reference swap; form preservation; root-confined failure-atomic persistence; thin `#if TOOLS` Godot adapter; tested plugin lifecycle.
- Completion means closing the complete-inline-validation gap and proving no regression. It does not reopen the completed path/atomic/plugin distribution work.

### Binding Data Contract

- Envelope: `{ "schema_version": 1, "moves": [...] }`; both fields are explicit, version is integral and exactly `1`, and `moves` is non-null. Ordinary load rejects older/newer/missing/null versions and performs no migration.
- Required move fields: `move_id`, `startup`, `active`, `recovery`, `hit_advantage`, `block_advantage`, `damage`, `chain_repeatable`, `knockback_profile_id`, `cancel_windows`, and `collision_frames`. `move_name` is optional display metadata.
- IDs are non-empty ordinal strings. Timing and damage are non-negative `int`; checked total frames must fit `int`. Advantages are signed `int`. Arrays are explicit; empty is valid.
- Cancel ranges satisfy `0 <= start_frame <= end_frame <= total_frames`. Collision frames are unique in `1..total_frames`; box IDs are unique in their frame/list; coordinates are finite; dimensions are finite and non-negative. Physics-profile IDs and references obey the schema-v1 AD-19 contract and must resolve in the staged logical dataset.
- Persisted names are canonical `snake_case`, encoded as UTF-8 through built-in `System.Text.Json`. Keep numeric handling strict and validate finite/domain constraints explicitly.

### Existing Code: Update, Change, Preserve

- `Scripts/Framework/Data/MoveAuthoringModels.cs` — currently owns immutable identities/errors/candidates and load/edit/save/reload/reapply ViewModel behavior. Preserve candidate/form and source identity on failures. Extend result consumption for complete ordered errors; keep reapply rename-safe and based on the original baseline.
- `Scripts/Framework/Data/MoveDatasetCodec.cs` — sole schema-v1 parse/serialize/semantic authority. Its current fail-fast path supplies only one validation error. Accumulate candidate semantic errors without weakening strict document parsing, canonical serialization, duplicate-property rejection, or runtime loader behavior.
- `Scripts/Framework/Data/MoveDatasetPersistence.cs` — owns load/save, reference validation, SHA-256 optimistic concurrency, confinement, staging, and coordinated commit. Preserve `ExpectedIdentity`/`CurrentIdentity` and every no-mutation/failure-atomic invariant; only touch it if tests expose a slice-A conflict-result defect.
- `Scripts/Framework/Editor/MoveAuthoringDock.cs` — currently renders a single status/error and focuses only the first result. Map and render the complete error set, including nested rows, while delegating all validation to Data.
- `Scripts/Framework/Editor/FTGEditorPlugin.cs`, `GodotEditorContext.cs`, and `IEditorContext.cs` — preserve lifecycle and singleton isolation. Change only if the new presentation API requires a narrow compatible adjustment.
- `Tests/FTG_Framework.Tests/Data/MoveAuthoringTests.cs` and `MoveDatasetPersistenceTests.cs` — extend the established xUnit patterns. Add a pure presenter test under the existing Data/Editor test structure if presentation mapping is extracted.

### Architecture Compliance

- Data owns schemas, validation, transformation, serialization, reference validation, persistence, and content identity. Editor code adapts UI/editor APIs only.
- Runtime data values remain immutable after construction. Form edits are candidate state and cannot mutate committed DataStore values or active gameplay snapshots.
- Keep editor ownership under `Scripts/Framework/Editor/` and namespace `FTG_Framework.Editor`; do not introduce `Scripts/Editor/`.
- No gameplay EventBus changes, third-party JSON dependency, service locator, implicit default, automatic migration, or target-framework/Godot upgrade belongs in this slice.
- Preserve AD-12/13/15/18/19/20/21 and the parent commit order: complete preparation before coordinated commit, atomic file replacement before the no-fail committed-reference swap.

### Library and Framework Requirements

- Preserve repository pins: Godot.NET.Sdk `4.5.1`, desktop `net8.0`, Android `net9.0`, and repository .NET SDK policy. Do not upgrade as part of this story.
- Use `System.Text.Json` only. `JsonNamingPolicy.SnakeCaseLower`, strict number handling, and unmapped-member rejection are available in .NET 8, but the existing canonical contract remains authoritative.
- .NET 8 support ends on 2026-11-10; track framework migration separately. The slice must remain compatible with the checked-in target and latest supported servicing patch.

### Testing Requirements

- Validation tests must assert the exact complete ordered error set, not merely `IsValid == false` or the first message.
- Include absent/null/type/coercion/unknown/duplicate properties, unsupported schema, duplicate/null collection items, invalid nested paths, non-finite numeric values, overflow, range violations, duplicate IDs, and dangling profile references.
- Assert invalid load/validation/conflict leaves destination bytes, DataStore reference/version, active snapshots, candidate/form, selection, and expected identity unchanged.
- Assert semantic preservation and ordinary runtime round trip for a single-field edit. Assert reapply keeps latest unrelated external changes and the user's intended changes, then validates again.
- Exercise repeated plugin disable/reload/re-enable and subscription disposal only as a regression guard; do not duplicate already accepted parent evidence unnecessarily.
- Tests must isolate static/singleton state, avoid fixed-delay watcher assertions, and report current discovered pass/fail/skip counts rather than copying the historical 897/897 baseline.

### Scope Boundaries

- In scope: canonical load/edit projection, complete validation aggregation/presentation, field focus/summary, optimistic conflict details, Reload/Reapply recovery, and regression evidence.
- Out of scope: path-confinement or atomic-write redesign (S2.1-B), UndoRedo/plugin/scaffold distribution redesign (S2.1-C), runtime tuning (Story 2.2), graphical timeline/hitbox editing, arbitrary filesystem browsing, schema migration, and framework upgrades.

### Git Intelligence

- `6ee5051 feat(editor): complete safe move authoring workflow` is the direct baseline and must be extended, not replaced.
- `9c84c43`, `ad19378`, and `55c3284` contain kickoff/readiness/planning contracts; `a0c7043` is the accepted transactional Data/event/snapshot foundation.
- Follow the existing record/ViewModel/xUnit patterns and singular DataStore prepared-reference-swap architecture.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md#Story-21-Safe-EditorPlugin-Move-Authoring]
- [Source: _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md#Story-21-Binding-Persistence-and-Ownership-Decisions]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#P-DATA--Current-Move-Authoring-Schema]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Bounded-Vertical-Slice-Plans]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Evidence-Matrix]
- [Source: _bmad-output/planning-artifacts/epic-2-ux-contract.md#Shared-Interaction-Rules]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#Consistency-Conventions]
- [Source: _bmad-output/implementation-artifacts/v2-2-1-editorplugin-move-authoring.md#Tasks--Subtasks]
- [Source: Scripts/Framework/Data/MoveAuthoringModels.cs]
- [Source: Scripts/Framework/Data/MoveDatasetCodec.cs]
- [Source: Scripts/Framework/Data/MoveDatasetPersistence.cs]
- [Source: Scripts/Framework/Editor/MoveAuthoringDock.cs]
- [Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties]
- [Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members]
- [Source: https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonnumberhandling?view=net-8.0]
- [Source: https://docs.godotengine.org/en/4.5/classes/class_editorinterface.html]
- [Source: https://docs.godotengine.org/en/4.5/classes/class_editorplugin.html]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Story-context analysis only; no implementation or test execution performed.
- Baseline parent implementation: commit `6ee5051`; parent completion record reports 897/897 tests after adversarial review.
- 2026-08-02 RED: multi-error and pure presentation tests failed because validation returned only one error and no presentation mapper existed.
- 2026-08-02 GREEN: focused authoring tests passed 15/15 after deterministic candidate validation aggregation and pure-C# presentation mapping.
- 2026-08-02 persistence RED/GREEN: missing-profile test proved only the first unindexed reference error was returned; indexed aggregation fixed it. Focused Data/Editor/UndoRedo regression passed 42/42.
- 2026-08-02 final validation: `dotnet test FTG_Framework.sln --no-restore --verbosity minimal` passed 900/900 in 68.2 seconds; `dotnet build FTG_Framework.csproj --no-restore` passed with 0 warnings and 0 errors; Godot 4.5.1 Mono headless editor/plugin smoke exited 0.
- 2026-08-02 code-review remediation: resolved all three findings; focused validation/conflict tests passed 44/44, the isolated transient scaffold packaging failure passed on immediate rerun, final full regression passed 902/902, framework build remained warning-free, and Godot 4.5.1 headless editor/plugin smoke exited 0.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Existing parent implementation and tracking inconsistency were analyzed; this slice is scoped to the remaining complete-inline-validation conformance gap and regression proof.
- Implemented stable, complete candidate validation across scalar, collection, nested row, overflow, finite-number, duplicate, null-entry, and profile-reference failures while preserving strict document parsing.
- Added a Godot-free validation presentation model and wired the dock to complete summaries, inline scalar/nested-row messages, and first-invalid-control focus without moving validation rules into Editor code.
- Preserved optimistic conflict identities, Reload/Reapply semantics, atomic persistence, path confinement, UndoRedo, candidate state, and runtime DataStore invariants; all focused and full regressions pass.
- Resolved all adversarial review findings: malformed scalar text now aggregates through a pure-C# parser, persistence validation errors use the same inline presentation path, and full move indexes are preserved so errors cannot annotate the wrong selected move.

### File List

- `_bmad-output/implementation-artifacts/v2-2-1-a-load-edit-validation-conflicts.md` (NEW)
- `Scripts/Framework/Data/MoveAuthoringModels.cs` (MODIFIED)
- `Scripts/Framework/Data/MoveDatasetCodec.cs` (MODIFIED)
- `Scripts/Framework/Data/MoveDatasetPersistence.cs` (MODIFIED)
- `Scripts/Framework/Editor/MoveAuthoringDock.cs` (MODIFIED)
- `Tests/FTG_Framework.Tests/Data/MoveAuthoringTests.cs` (MODIFIED)
- `Tests/FTG_Framework.Tests/Data/MoveDatasetPersistenceTests.cs` (MODIFIED)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED)

### Change Log

- 2026-08-02: Implemented complete deterministic validation aggregation, indexed missing-profile reporting, pure-C# presentation mapping, and inline nested Dock errors; passed 42/42 focused tests, 900/900 full regression, warning-free framework build, and Godot 4.5.1 headless editor smoke. Status moved to review.
- 2026-08-02: Addressed all three code-review findings; passed 44/44 focused tests and 902/902 final full regression, then moved the story to done.
