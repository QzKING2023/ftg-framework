---
story_key: v2-2-1-b-path-confined-atomic-persistence
story_id: v2-2-1-b
date_created: 2026-08-02
epic: v2-2
parent_story: v2-2-1-editorplugin-move-authoring
baseline_commit: f4a0aee8807787a87fab87e8c951ff4c92d1d6e0
---

# Story 2.1-B: Path-Confined Atomic Persistence

Status: done

## Story

As a Godot developer authoring character moves,
I want saves to remain confined to the configured move-data root and commit as one failure-atomic dataset transaction,
so that I can save and reload canonical move data without corrupting the current file, exposing a partial dataset, or touching an unintended path.

## Acceptance Criteria

1. **Complete canonical candidate before I/O (parent S2.1-AC02/03/04).**
   **Given** an editor candidate for one schema-version-1 move-dataset document
   **When** Save is requested
   **Then** Data validates the complete document and every currently committed move/physics reference before filesystem mutation
   **And** a one-field edit preserves all other compatible moves, fields, ordering decisions, and references while invalid, missing, null, non-finite, out-of-domain, overflowed, duplicate, or dangling values leave destination bytes, committed `DataStore`, form state, and active runtime snapshots unchanged.

2. **Document identity cannot escape the root (parent S2.1-AC05).**
   **Given** a document identifier is empty, rooted, separator-bearing, dot/traversal-bearing, or canonically resolves outside the configured move-data root
   **When** Load, Save, Restore, Undo, or Redo derives its destination
   **Then** the request is rejected before any target file is opened
   **And** no outside path is read, created, modified, deleted, or registered as an owned staging artifact; `move_id` is never used as a path or filename source.

3. **Canonical aliases and link/parent races are rejected at access (parent S2.1-AC06).**
   **Given** two ordinal document identities alias one canonical destination, or a validated destination/parent is replaced by a symlink, junction, reparse point, or other containment-changing link before access or replacement
   **When** the persistence boundary opens or commits the actual destination
   **Then** it repeats canonical identity and containment checks at that boundary and rejects the operation
   **And** both the prior destination and every out-of-root link target remain byte-identical.

4. **Preparation precedes one atomic commit (parent S2.1-AC07).**
   **Given** an in-root destination, a valid candidate, and the expected content identity
   **When** persistence runs
   **Then** it serializes canonical UTF-8-without-BOM `snake_case` JSON, creates an invocation-owned same-directory staging file, flushes it to disk, reloads and validates the staged bytes, prepares the immutable Data candidate, and rechecks cancellation, containment, file identity, and committed dataset version before replacement
   **And** the commit performs the fallible same-filesystem atomic replacement first and exactly one no-fail prepared `DataStore` reference/version swap second, with no observer-visible partial file or mismatched committed version.

5. **Every pre-commit failure is byte-preserving (parent S2.1-AC08).**
   **Given** fault injection at serialization, staging creation/write/flush, staged reload/validation, Data preparation, pre-commit cleanup, containment recheck, expected-version comparison, or replacement
   **When** Save returns failure or conflict
   **Then** the original destination is byte-identical and loadable, the prior `DataStore` reference/version remains authoritative, and candidate/form/runtime snapshots remain available
   **And** cleanup touches only this invocation's staging artifact below the revalidated root and leaves no unintended temporary or outside-root file.

6. **Post-commit work cannot revoke success (parent S2.1-AC08).**
   **Given** atomic replacement and the no-fail committed-reference swap have completed
   **When** deletion of an already-owned leftover staging artifact or diagnostic work fails
   **Then** Save still returns success, the new file and `DataStore` version remain committed, and an English `[Data]` diagnostic records the cleanup problem
   **And** no rollback, foreign-file deletion, or reported failure occurs after the first committed mutation.

7. **Optimistic concurrency has exactly one winner (parent S2.1-AC09).**
   **Given** the destination changes after load, or two writers start from the same content and dataset versions
   **When** they attempt to commit
   **Then** at most one writer replaces the document and swaps the committed dataset
   **And** every loser receives a conflict containing expected and current content identities without overwriting unseen data or interleaving bytes.

8. **Save, Undo, and Redo share the transaction (parent S2.1-AC11).**
   **Given** an editor save participates in UndoRedo
   **When** Save, Undo, or Redo restores a complete version
   **Then** every operation uses the same validation, path-confinement, expected-version, staging, replacement, and prepared-swap boundary
   **And** invalid or stale restoration fails without direct file writes, hidden conflict wins, or current-file corruption.

9. **Runtime/restart semantic equivalence and evidence closure.**
   **Given** this slice is proposed for completion
   **When** the committed file is loaded through the ordinary runtime `MoveDataLoader` path and evidence is reviewed
   **Then** the loaded schema and complete move collection are semantically equal to the validated candidate, including preserved peer moves and references
   **And** E2.1-U/D/F/C/R evidence links the path matrix, every pre-commit fault, post-commit cleanup fault, concurrent writers, UndoRedo conflict behavior, canonical JSON round trip, and current focused/full regression results.

## Tasks / Subtasks

- [x] **Reconcile the existing parent implementation with this standalone slice** (AC: 1-9)
  - [x] Treat commits `6ee5051` and `f4a0aee` as the implementation baseline; inventory which B requirements already pass and record only reproducible gaps.
  - [x] Extend existing Data persistence, codec, candidate, and test seams; do not create a parallel writer, validator, content-identity type, editor namespace, or `DataStore`.
  - [x] Preserve slice A's complete validation/conflict presentation and parent slice C's plugin/UndoRedo/scaffold behavior.

- [x] **Harden path identity and access-boundary confinement** (AC: 2-3)
  - [x] Exercise empty, whitespace, rooted, separator, dot, traversal, canonical escape, invalid-character, and platform case/alias identities before file open.
  - [x] Verify the configured root, existing parents, destination, and staging path remain link/reparse-free at construction and again immediately before actual access/replacement.
  - [x] Add deterministic seams for a parent/destination link swap between validation and replacement; assert the outside target and original destination are untouched.
  - [x] Keep platform path comparison explicit: ordinal-ignore-case on Windows and ordinal on case-sensitive targets; document any platform limitation instead of silently weakening confinement.

- [x] **Prove the prepare/commit transaction and cleanup boundary** (AC: 1, 4-6)
  - [x] Reuse/generalize `PhysicsDataPersistence.StageSameDirectory`, `ReplaceStaged`, and `CleanupOwnedStaging`; stage with a collision-resistant invocation-owned name next to the destination, UTF-8 without BOM, exclusive creation, and `Flush(true)`.
  - [x] Validate staged bytes through the canonical codec and prepare the complete immutable move dataset before acquiring the coordinated commit boundary.
  - [x] Inside the `DataStore` commit lock, recheck dataset/content identities, cancellation, and path containment; replace the file before the no-fail reference/version swap.
  - [x] Ensure no exception-capable validation, allocation, cleanup, notification, or rollback remains after replacement; post-commit cleanup is diagnostic best effort only.

- [x] **Preserve optimistic concurrency and UndoRedo symmetry** (AC: 7-8)
  - [x] Test an external byte change and two simultaneous writers from one base version; assert exactly one winner and immutable expected/current identities for each conflict.
  - [x] Route Save/Restore/Undo/Redo through the same expected-version transaction; reject stale restoration without replacing unseen data.
  - [x] Preserve Reload/Reapply semantics established in slice A; this slice must not redesign editor conflict UX.

- [x] **Build the failure-equivalence and round-trip evidence matrix** (AC: 1-9)
  - [x] Extend `MoveDatasetPersistenceTests` at every fault seam and assert exact prior bytes, committed move reference/value/version, candidate/form state where observable, loadability, and absence of unowned staging artifacts.
  - [x] Inject pre-commit cleanup and replacement failures separately from post-commit cleanup failure; assert failure/no mutation for the former and success/diagnostic/committed state for the latter.
  - [x] Parse the written bytes through `MoveDatasetCodec` and ordinary `MoveDataLoader`; assert semantic equality for the complete collection and UTF-8/no-BOM canonical output.
  - [x] Run focused Data/UndoRedo tests, the full solution suite, and a warning-free framework build. Record discovered counts rather than copying historical results; tests must not mutate tracked evidence.
  - [x] Store accepted evidence under `_bmad-output/implementation-artifacts/evidence/v2-2-1/` with command/scenario, OS, SDK/Godot versions, reviewed commit/hash, counts, exit code, duration/seed, reviewer, acceptance, and SHA-256.

### Review Findings

- [x] [Review][Patch] High — Make destination identity comparison and replacement one race-safe operation; an external writer can currently change the destination after the final identity read and be silently overwritten. [Scripts/Framework/Data/MoveDatasetPersistence.cs:141]
- [x] [Review][Patch] High — Bind the validated staged bytes to the object used by replacement; the staged pathname can currently be substituted after parsing, causing committed file bytes and the prepared DataStore candidate to diverge. [Scripts/Framework/Data/MoveDatasetPersistence.cs:131]
- [x] [Review][Patch] High — Eliminate check-then-open TOCTOU in Load, destination identity reads, and staged validation by opening with no-follow semantics and validating the opened object rather than reopening an unchecked pathname. [Scripts/Framework/Data/MoveDatasetPersistence.cs:60]
- [x] [Review][Patch] Medium — Complete the identifier and containment matrix: explicitly reject platform-invalid/ADS-capable identifiers and prove configured-root/existing-parent replacement boundaries instead of marking the task complete from destination-only coverage. [Scripts/Framework/Data/MoveDatasetPersistence.cs:216]
- [x] [Review][Patch] Medium — Replace silent `if (!OperatingSystem.IsWindows()) return` passes with explicit capability-aware skips or deterministic platform-neutral boundary probes. [Tests/FTG_Framework.Tests/Data/MoveDatasetPersistenceTests.cs:170]
- [x] [Review][Patch] Medium — Correct the evidence manifest and completed-task claims, which currently assert replacement-race and root/parent coverage that the tests do not provide. [_bmad-output/implementation-artifacts/evidence/v2-2-1/slice-b-manifest.md:17]

## Dev Notes

### Baseline and Non-Reinvention Guardrail

- The parent Story 2.1 completion record says all three slices were historically implemented in `6ee5051`, while sprint tracking now lists standalone B as `backlog`. Slice A subsequently completed in `f4a0aee`. Resolve this by auditing, testing, and narrowly hardening the existing implementation; do not assume either that B is empty or that historical parent evidence proves the current standalone gate.
- Existing strengths to preserve: schema-v1 codec, immutable authoring candidates, SHA-256 content identities, complete ordered validation, stale-write conflict results, same-directory staging, `Flush(true)`, staged reload, `DataStore.TryCommitMoveDataset`, reparse checks, fault injection, and shared UndoRedo persistence.
- A new production file is not expected. Add a dedicated confined-filesystem abstraction only if a reproducible access-boundary gap cannot be fixed or tested cleanly through the existing services.

### Binding Data and Persistence Contract

- Envelope: `{ "schema_version": 1, "moves": [...] }`; both fields are explicit and `moves` is non-null. Ordinary load accepts only integral version `1` and performs no migration or implicit defaulting.
- Persistence granularity is the complete move-dataset document. A separately validated document identifier selects the file. `move_id` is ordinal data identity only and never supplies a path.
- The transaction root includes the replacement document plus all committed move/physics documents required to prove unique IDs and valid references before one `DataStore` swap.
- Required move fields and domains remain P-DATA's canonical contract. Use only built-in `System.Text.Json`, canonical `snake_case`, strict values, and UTF-8 without BOM.
- A transaction is cancellable only before commit. Failure stages must be distinguishable as validation, conflict, cancellation, containment, staging/flush/validation, replacement, or post-commit diagnostic without exposing sensitive outside-path contents.

### Existing Code: Update, Change, Preserve

- `Scripts/Framework/Data/MoveDatasetPersistence.cs` (**UPDATE, primary**) currently validates identifiers, reserves canonical destinations, rejects reparse points, compares SHA-256 source identity, stages/validates, coordinates with `DataStore`, replaces, swaps, and cleans up. Harden access-boundary races and result semantics here; preserve expected/current identity and slice A's indexed validation errors.
- `Scripts/Framework/Data/PhysicsDataPersistence.cs` (**UPDATE only if required**) owns same-directory `CreateNew` staging, `Flush(true)`, `File.Replace`/`File.Move`, and owned cleanup. Strengthen shared primitives here instead of introducing another atomic-write algorithm; do not regress physics persistence.
- `Scripts/Framework/Data/DataStore.cs` (**preserve; narrow UPDATE only if a proven defect**) prepares a complete immutable candidate before locking, checks the expected dataset version, calls the fallible file commit, and then performs the reference/version swap. Nothing may throw after the file commit succeeds.
- `Scripts/Framework/Data/MoveDatasetCodec.cs` and `MoveAuthoringModels.cs` (**preserve**) are the canonical schema/content-identity/candidate authorities. Slice A owns validation aggregation and conflict recovery; do not fork them.
- `Scripts/Framework/Editor/MoveAuthoringUndoService.cs` (**preserve; tests may expose a narrow fix**) routes save/undo/redo through persistence. Never restore with `File.WriteAll*` or bypass expected identities.
- `Tests/FTG_Framework.Tests/Data/MoveDatasetPersistenceTests.cs` (**UPDATE, primary**) already covers identifiers, Windows aliasing, fault points, symlink replacement, concurrency, cancellation, committed swap, cleanup, and runtime reload. Convert uncovered invariants into assertions rather than duplicating broad scenarios.
- `Tests/FTG_Framework.Tests/Editor/MoveAuthoringUndoServiceTests.cs` (**UPDATE if needed**) proves transaction symmetry and stale restore behavior.

### Architecture Compliance

- Data exclusively owns schema validation, transformation, serialization, path/content identity, persistence, and the prepared committed dataset. Editor/Godot code remains a thin adapter.
- Preserve AD-15 transactional reload/writeback, AD-19 presence-aware schema semantics, AD-21 lifecycle-safe interaction, and the no-bypass requirements for AD-12/13/18/20. This slice publishes no gameplay EventBus sequence and introduces no lifecycle generation.
- Keep editor files under `Scripts/Framework/Editor/` in `FTG_Framework.Editor`; do not create `Scripts/Editor/`.
- No external NuGet runtime dependency, alternate JSON library, service locator, mutable runtime definition, implicit migration/default, target-framework upgrade, arbitrary filesystem browser, or gameplay feature belongs here.

### Platform and Library Requirements

- Preserve repository pins: Godot.NET.Sdk `4.5.1`, desktop `net8.0`, Android `net9.0`, and repository .NET SDK policy. Use built-in `System.IO` and `System.Text.Json` only.
- `FileStream.Flush(true)` flushes intermediate buffers; retain it for staged durability. `File.Replace` replaces an existing file, while cross-volume move can become copy/delete; same-directory staging is therefore mandatory for the intended commit boundary.
- `FileAttributes.ReparsePoint` is supported across Windows, Linux, and macOS, but filesystem replacement/link behavior is platform-specific. The binding reference environment is Windows x64; any additional release OS needs explicit portability evidence.
- Catch and classify expected `IOException`, `UnauthorizedAccessException`, cancellation, and validation/conflict outcomes at the service boundary; never claim universal crash/power-loss durability beyond the guarantees actually tested on the reference filesystem.

### Testing Requirements

- Focused command: `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore --filter "FullyQualifiedName~MoveDatasetPersistenceTests|FullyQualifiedName~MoveAuthoringUndoServiceTests" --verbosity minimal`.
- Full regression: `dotnet test FTG_Framework.sln --no-restore --verbosity minimal`; framework build: `dotnet build FTG_Framework.csproj --no-restore` with zero warnings/errors.
- Every pre-commit fault assertion must cover byte equality, ordinary loadability, unchanged `DataStore` value/reference/version, and staging ownership/cleanup. Avoid assertions that merely check `Status != Succeeded`.
- Post-commit cleanup injection must assert success plus diagnostic and the new committed file/store, not rollback. Concurrency must assert exactly one winner. Symlink/reparse tests must skip with an explicit capability reason if the host cannot create the fixture; never silently pass.
- Isolate static/singleton state, use no fixed delays, and keep evidence generation outside ordinary automated tests.

### Previous Story Intelligence

- Slice A (`v2-2-1-a-load-edit-validation-conflicts`) is done at `f4a0aee`. It established complete deterministic field-addressable validation, pure-C# validation presentation, preserved form/candidate state, and Reload/Reapply behavior.
- Do not change the complete error ordering/indexing or collapse missing-profile errors back to a generic result. B may consume validation and conflict identities but owns confinement, atomicity, failure equivalence, and transaction proof.
- Slice A's latest recorded results were 44/44 focused and 902/902 full, but those are historical context only; report the current run's discovered counts.

### Git Intelligence

- `f4a0aee feat(editor): complete move validation conflict slice` is the immediate baseline.
- `6ee5051 feat(editor): complete safe move authoring workflow` introduced the current persistence/UndoRedo/plugin implementation and broad B/C tests. Extend it rather than replacing it.
- `9c84c43`, `ad19378`, and `55c3284` contain kickoff/readiness/planning reconciliation. `a0c7043` is the accepted transactional Data/event/snapshot foundation.

### Scope Boundaries

- In scope: canonical document identity, path confinement/alias/link-race defenses, complete-document staging and validation, flush and atomic replacement, prepared no-fail `DataStore` swap, optimistic concurrency, Save/Undo/Redo symmetry, fault injection, byte preservation, and runtime/restart round trip.
- Out of scope: validation aggregation/presentation (slice A), plugin/dock lifecycle and packaging redesign (slice C), runtime tuning (Story 2.2), graphical timeline/hitbox editing, schema migration, arbitrary file browsing, framework upgrades, and gameplay/EventBus changes.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md#Story-21-Safe-EditorPlugin-Move-Authoring]
- [Source: _bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md#Story-21-Binding-Persistence-and-Ownership-Decisions]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#P-DATA--Current-Move-Authoring-Schema]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Risk-Matrix]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Evidence-Matrix]
- [Source: _bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md#Bounded-Vertical-Slice-Plans]
- [Source: _bmad-output/planning-artifacts/epic-2-ux-contract.md#Shared-Interaction-Rules]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-15--Atomic-Hot-Reload]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#AD-19--Explicit-Physics-Data-Presence-Semantics]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#Consistency-Conventions]
- [Source: _bmad-output/implementation-artifacts/v2-2-1-editorplugin-move-authoring.md#Tasks--Subtasks]
- [Source: _bmad-output/implementation-artifacts/v2-2-1-a-load-edit-validation-conflicts.md#Scope-Boundaries]
- [Source: Scripts/Framework/Data/MoveDatasetPersistence.cs]
- [Source: Scripts/Framework/Data/PhysicsDataPersistence.cs]
- [Source: Scripts/Framework/Data/DataStore.cs]
- [Source: Tests/FTG_Framework.Tests/Data/MoveDatasetPersistenceTests.cs]
- [Source: Tests/FTG_Framework.Tests/Editor/MoveAuthoringUndoServiceTests.cs]
- [Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush]
- [Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-8.0]
- [Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=net-8.0]
- [Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.fileattributes?view=net-8.0]

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- Story-context analysis only; no implementation or test execution performed.
- Baseline commits inspected: `f4a0aee`, `6ee5051`, `9c84c43`, `ad19378`, `55c3284`.
- Implementation plan: preserve the parent transaction, add failing access-boundary tests, centralize confined identity/staging reads, and rerun focused plus full regressions after each production change.
- RED 1: focused compilation failed because `BeforeInitialIdentityRead` and `BeforeCoordinatedCommit` seams did not exist.
- GREEN 1: confined identity reads and both deterministic seams implemented; focused Data/UndoRedo suite passed 37/37.
- RED 2: staged-file reparse test proved staged reload lacked an explicit access-boundary rejection signal.
- GREEN 2: staged reload now uses the same reparse boundary through an injectable deterministic probe; final focused suite passed 38/38.
- Final validation: full solution passed 913/913 (seed 2202, 0 skipped); framework build passed with 0 warnings and 0 errors; `git diff --check` passed.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Reconciled the completed parent implementation with the independently tracked B slice and scoped work to verification plus narrow hardening of reproducible gaps.
- Hardened initial, coordinated-conflict, in-lock, and staged identity reads against changed reparse containment without adding a parallel persistence layer.
- Added deterministic fault seams and path/link tests proving outside identities are not consumed, prior bytes remain loadable, and `DataStore` values/versions remain coherent.
- Proved stale Undo conflict preservation, exactly-one-winner concurrency, UTF-8 no-BOM round trip, content identity equality, and post-commit diagnostic semantics.
- Evidence manifest records the Windows x64/.NET environment, commands, counts, acceptance state, and reviewed file hashes.
- Resolved all six parallel-review findings: final destination CAS and staged-byte validation now run inside a shared canonical-destination commit lock with no injectable work after the last checks; identifiers are strict path-free ASCII tokens; boundary tests are deterministic and platform-neutral.
- Post-review validation passed 45/45 focused tests, 920/920 full tests, and a framework build with 0 warnings and 0 errors.

### File List

- `_bmad-output/implementation-artifacts/v2-2-1-b-path-confined-atomic-persistence.md` (NEW)
- `Scripts/Framework/Data/MoveDatasetPersistence.cs` (MODIFIED)
- `Tests/FTG_Framework.Tests/Data/MoveDatasetPersistenceTests.cs` (MODIFIED)
- `Tests/FTG_Framework.Tests/Editor/MoveAuthoringUndoServiceTests.cs` (MODIFIED)
- `_bmad-output/implementation-artifacts/evidence/v2-2-1/slice-b-manifest.md` (NEW)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED)

### Change Log

- 2026-08-02: Hardened path-confined destination/staging identity reads, added deterministic access-race seams and comprehensive persistence/UndoRedo tests, recorded evidence, and moved the story to review after 38/38 focused and 913/913 full tests plus a warning-free build.
- 2026-08-02: Addressed all 6 parallel code-review findings; added final CAS/staged-byte/root-boundary guards and strict identifiers, corrected evidence, passed 45/45 focused and 920/920 full tests, and marked the story done.
