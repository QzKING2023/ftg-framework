# Epic 2: Move Authoring & Training Suite

Developers can visually author moves, tune and persist values during play, inspect combo results, reproduce exact training inputs, and save or restore complete practice situations through one coherent iteration workflow.

## Story 2.1: Safe EditorPlugin Move Authoring

As a Godot developer authoring character moves,
I want a form-based editor dock that validates and atomically saves canonical move data,
So that I can create and edit moves without hand-writing JSON or risking files outside the move-data root.

**Acceptance Criteria:**

**S2.1-AC01**
**Given** the FTG Framework EditorPlugin is enabled
**When** the developer opens the move-authoring dock
**Then** it displays the current canonical fields for move identity, timing, damage, advantage, cancel windows, and physics-profile references
**And** the Godot class delegates state, validation, transformation, and persistence to pure-C# services.

**S2.1-AC02**
**Given** the developer enters a complete valid move definition
**When** Save is requested
**Then** the service builds and validates the complete logical candidate using the Data-owned current schema and validator
**And** the persisted UTF-8 JSON uses canonical `snake_case` field names and explicit required values.

**S2.1-AC03**
**Given** the developer edits one field in an existing current-version move
**When** the candidate is saved
**Then** all other compatible fields and references retain their prior semantic values
**And** loading the written file through the runtime Data path produces the same validated move represented by the editor model.

**S2.1-AC04**
**Given** a required field is absent, null, invalid, non-finite, out of domain, or references a missing profile
**When** Save is requested
**Then** the complete candidate is rejected before filesystem mutation
**And** the original file, committed DataStore, form state, and active runtime snapshots remain unchanged.

**S2.1-AC05**
**Given** a move identifier or derived path is rooted, contains a directory separator, contains parent traversal, or canonically resolves outside the configured move-data root
**When** the save path is derived
**Then** the request is rejected before opening a file
**And** no external path is read, created, modified, or deleted.

**S2.1-AC06**
**Given** another move identity aliases the same canonical destination or the validated parent/link changes before replacement
**When** the persistence service opens or replaces the actual path
**Then** canonical collisions and changed containment are rejected at the access boundary
**And** neither the existing move file nor an out-of-root target is modified.

**S2.1-AC07**
**Given** the destination is inside the approved move-data root and the candidate is valid
**When** persistence begins
**Then** the service writes a same-filesystem staged file, flushes and validates it, and completes every fallible Data preparation before entering the coordinated commit boundary
**And** the commit performs the fallible atomic file replacement before a no-fail committed-dataset reference swap, so no observer reads a partial file or mismatched committed version.

**S2.1-AC08**
**Given** serialization, staging, validation, flush, replacement, or cleanup fails
**When** Save returns an error
**Then** the original destination remains byte-identical and loadable
**And** cleanup touches only invocation-owned staging artifacts under the validated root.

**S2.1-AC09**
**Given** the file changed after the editor loaded its content version
**When** the developer saves a stale candidate
**Then** the write is rejected with a conflict result identifying expected and current versions
**And** unseen external changes are not overwritten.

**S2.1-AC10**
**Given** the approved EditorInterface, EditorSelection, and UndoRedo adapter spike
**When** the dock integrates editor selection and undoable edits
**Then** Godot singleton access remains confined to the thin adapter
**And** pure-C# authoring services run under `dotnet test` without a Godot process.

**S2.1-AC11**
**Given** a successful save participates in UndoRedo
**When** the developer invokes Undo or Redo
**Then** the corresponding complete validated file version is restored through the same atomic persistence boundary
**And** invalid or conflicting restoration is rejected without corrupting the current file.

**S2.1-AC12**
**Given** Story 2.1 is proposed for `ready-for-dev` or completion
**When** its risk/evidence row is reviewed
**Then** unit, Data/service integration, path-boundary, atomic-write fault injection, stale-version conflict, JSON round-trip, Godot editor, UndoRedo, scaffold, and runtime-load evidence is explicitly linked
**And** PO, Architect, and QA approvals required by PREP-2.4 are recorded.

## PREP-2.4 Execution Contract

The five stories above preserve FR-23 through FR-27. Their binding cross-story risks, evidence layers, concrete-decision owners, dependency graph, and bounded vertical slices are defined in [`../epic-2-risk-and-evidence-checklist.md`](../epic-2-risk-and-evidence-checklist.md). Their user-facing behavior is constrained by [`../epic-2-ux-contract.md`](../epic-2-ux-contract.md).

- Story 2.4 standalone recording/playback is independently completable before Story 2.5. The later recording-in-snapshot proof is a Story 2.5 slice and runs only after the Story 2.4 codec is accepted.
- Every story exceeding one developer-agent context follows the checklist's vertical slice plan; each slice produces an observable user or integration outcome and accepted evidence.
- Stable execution/evidence IDs are S2.1-A–C, S2.2-A–C, S2.3-A–C, S2.4-A–D, and S2.5-A–E. A parent story remains the FR/user-outcome key and becomes done only after every required slice and final parent E2E acceptance pass. Add slice status keys only when the parent story file is created.
- A story remains `backlog` until its blocking decisions have concrete values/fixtures, its risk/evidence row is complete, and PO/Architect/QA approve the same reviewed artifact hash.
- No story may bypass Core interfaces, duplicate Data validation/persistence, serialize Godot/live implementation containers, weaken AD-12/13/15/18/19/20, or create a third playback mode.
- Findings that do not violate an approved invariant or gate enter the normal backlog. Expanding this gate requires a newly approved course correction.
- Concrete schema/domain limits, combo arithmetic, recording codec limits, snapshot limits, deterministic windows, UX behavior, and reference environments are binding in the checklist's Parameter Registry. Every referenced “documented” parameter must resolve to a concrete value or fixture before `ready-for-dev`; a story may not replace it with an undocumented implementation default.

## Story 2.2: Concurrent Runtime Tuning and Atomic Writeback

As a developer tuning a running match,
I want to edit move and physics values through a runtime panel and persist them safely,
So that I can iterate quickly without changing in-flight behavior or overwriting concurrent edits.

**Acceptance Criteria:**

**S2.2-AC01**
**Given** the runtime tuning panel opens for a valid move or physics profile
**When** its ViewModel loads the selection
**Then** it displays the current committed values and content version from Data
**And** all presentation and edit-state logic is testable without a Godot process.

**S2.2-AC02**
**Given** a developer changes a value in the panel but has not committed it
**When** the current move, trajectory, state occupancy, or combo continues
**Then** runtime engine state remains unchanged
**And** the edit exists only in the panel's candidate model.

**S2.2-AC03**
**Given** a complete tuned candidate satisfies the current Data schema and logical-dataset references
**When** Apply/Write is requested with the expected content version
**Then** the shared persistence service completes validation and staging, enters the coordinated commit lock, atomically replaces the canonical file, and performs a no-fail DataStore reference swap to one committed dataset version
**And** a failed file replacement performs no swap while FileWatcher recognizes a successful committed identity without applying a second logical version.

**S2.2-AC04**
**Given** a tuned move or physics value is successfully committed
**When** existing and new gameplay actions proceed
**Then** active move, trajectory, state, and combo snapshots retain their initiation values
**And** only the next applicable initiation snapshots the new committed value.

**S2.2-AC05**
**Given** EditorPlugin or another writer commits the file after the tuning panel loaded it
**When** the panel submits its stale expected version
**Then** the write is rejected with a conflict showing expected and current identities
**And** the panel cannot overwrite unseen changes.

**S2.2-AC06**
**Given** FileWatcher reports duplicate notifications caused by the atomic replacement
**When** reload processing reaches the frame boundary
**Then** notifications are coalesced by canonical path and committed content/version identity
**And** subscribers observe at most one logical dataset transition for that commit.

**S2.2-AC07**
**Given** validation, staging, flush, replacement, reload validation, or Data commit fails
**When** the writeback operation terminates
**Then** the prior canonical file, committed DataStore, panel's candidate, and all active snapshots remain available and uncorrupted
**And** the error identifies the failed stage without reporting success.

**S2.2-AC08**
**Given** simultaneous EditorPlugin and runtime tuning writes target the same file from the same base version
**When** both attempt to commit
**Then** exactly one version may win through the supported optimistic concurrency boundary
**And** the loser receives a conflict rather than an interleaved or silently overwritten file.

**S2.2-AC09**
**Given** the game restarts after a successful writeback
**When** startup loads the canonical data
**Then** the tuned value is present through the ordinary current-schema startup path
**And** no runtime-only migration, default, or special tuning format is required.

**S2.2-AC10**
**Given** the panel or training scene exits, a match restarts, state is restored, or replay starts
**When** lifecycle cleanup runs
**Then** stale panel subscriptions and pending candidate commits cannot mutate the new epoch
**And** reopening the panel reads the current committed version.

**S2.2-AC11**
**Given** Story 2.2 is proposed for `ready-for-dev` or completion
**When** its risk/evidence row is reviewed
**Then** ViewModel unit, Data integration, atomic-write fault injection, optimistic-concurrency, watcher-coalescing, initiation-snapshot, restart round-trip, lifecycle, Godot runtime, and concurrent EditorPlugin/tuning evidence is linked
**And** the required PO, Architect, and QA approvals are recorded.

## Story 2.3: Lifecycle-Safe Combo Counter and Damage Display

As a developer practicing combos,
I want a per-attacker combo counter driven by authoritative hit outcomes,
So that I can trust the displayed hit count and damage while tuning or replaying interactions.

**Acceptance Criteria:**

**S2.3-AC01**
**Given** no combo is active for an attacker
**When** its ViewModel is queried
**Then** `HitCount` and `TotalDamage` are zero
**And** no state from another attacker is displayed.

**S2.3-AC02**
**Given** a valid `HitConnected` carries attacker ID, defender ID, move ID, frame, and initiation-snapshotted damage
**When** the ViewModel handles the event for an active combo
**Then** it increments only that attacker's hit count and adds the event's damage
**And** it does not re-read mutable move data or the current DataStore.

**S2.3-AC03**
**Given** two attackers or training slots generate independent combo events
**When** their hits interleave
**Then** each attacker's count and damage remain isolated
**And** selecting one display bucket cannot mutate another.

**S2.3-AC04**
**Given** a `MoveBlocked` terminates the tracked attacker's sequence
**When** the event is processed
**Then** that attacker's count and damage reset to zero
**And** unrelated attacker buckets remain unchanged.

**S2.3-AC05**
**Given** `ComboEnded` is dispatched for an attacker
**When** the ViewModel processes it
**Then** that attacker's display resets to zero in the Combo phase
**And** a Physics-phase hit earlier in the same frame cannot remain visible after the later combo-end reset.

**S2.3-AC06**
**Given** multiple valid hit, block, or combo-end events occur near one frame boundary
**When** EventBus dispatches them
**Then** the final display follows AD-12 phase order and within-phase sequence deterministically
**And** repeated execution of the same envelope sequence yields the same final values.

**S2.3-AC07**
**Given** an event contains an invalid attacker/defender identity, negative or non-finite damage, impossible frame, or incomplete required payload
**When** the ViewModel validates it
**Then** the complete event is rejected without changing any display bucket
**And** a diagnostic identifies the invalid payload boundary.

**S2.3-AC08**
**Given** a valid hit would make `HitCount` or `TotalDamage` exceed its documented non-wrapping representation
**When** checked accumulation is attempted
**Then** that event is rejected and the prior count/damage values remain unchanged
**And** an overflow diagnostic identifies the attacker and attempted value without wrapping the display.

**S2.3-AC09**
**Given** the training scene exits, the match changes, state restore succeeds, or replay starts/ends
**When** lifecycle handling runs
**Then** transient combo display state resets for the new lifecycle and stale-epoch events are ignored
**And** no old value reappears after the next render.

**S2.3-AC10**
**Given** the UI node exits the scene tree or the panel closes
**When** cleanup runs
**Then** all EventBus subscriptions owned by that instance are removed exactly once
**And** reopening the UI does not duplicate hit counting or event handling.

**S2.3-AC11**
**Given** the combo display is tested
**When** pure ViewModel tests and EventBus integration tests process authoritative damage, multiple attackers, same-frame ordering, invalid payloads, lifecycle changes, and repeated subscribe/unsubscribe cycles
**Then** the results remain deterministic without a Godot process
**And** Godot runtime evidence confirms the rendered text matches the ViewModel state.

**S2.3-AC12**
**Given** Story 2.3 is proposed for `ready-for-dev` or completion
**When** its risk/evidence row is reviewed
**Then** unit, EventBus integration, lifecycle, invalid-input, subscription-leak, same-frame ordering, replay/restore, and Godot display evidence is linked
**And** the required PO, Architect, and QA approvals are recorded.

## Story 2.4: Versioned Relative-Frame Input Recording and Playback

As a developer practicing against a training dummy,
I want to record canonical inputs and replay them from any chosen start frame,
So that I can reproduce an exact sequence without tying it to the original session timeline.

**Acceptance Criteria:**

**S2.4-AC01**
**Given** recording starts for a valid source player at frame S
**When** canonical input events are accepted after SOCD cleaning
**Then** each immutable entry stores its input type/value and `relative_frame = event_frame - S`
**And** raw pre-SOCD hardware state is not recorded.

**S2.4-AC02**
**Given** several inputs occur on the same relative frame
**When** the recording is materialized
**Then** entries retain a stable documented within-frame sequence
**And** playback order does not depend on dictionary, collection, or subscriber iteration order.

**S2.4-AC03**
**Given** recording stops at frame E
**When** the immutable recording is finalized
**Then** it contains schema version, source player, duration, ordered entries, and optional user-facing name
**And** later mutation of the capture buffer cannot change the finalized recording.

**S2.4-AC04**
**Given** a recording candidate has an invalid player, unsupported input type/value, negative frame, decreasing order, entry beyond duration, duplicate forbidden identity, unsupported schema version, or exceeds a documented codec resource limit
**When** validation runs
**Then** the complete candidate is rejected without replacing the currently assigned recording
**And** no partial input schedule is created.

**S2.4-AC05**
**Given** `playback_start + relative_frame`, loop rebasing, or declared duration cannot be represented without overflow, or loop mode is requested for an empty or zero-duration recording
**When** a playback schedule is prepared
**Then** checked non-wrapping arithmetic rejects the complete start or loop operation before input injection
**And** the previously assigned recording and current input ownership remain unchanged.

**S2.4-AC06**
**Given** a valid recording is assigned to a dummy and playback begins at frame P
**When** EventBus reaches `P + relative_frame` for an entry
**Then** the input is injected through the canonical input boundary in stable recorded order
**And** live input for that dummy cannot compete with the playback-owned source.

**S2.4-AC07**
**Given** authoritative full-event Replay is active/starting, or the same player is already owned by recording capture or training playback in the lifecycle epoch
**When** another capture or training playback request would overlap that ownership
**Then** the request is rejected without capturing or scheduling entries
**And** Replay, capture, training playback, and live dummy input cannot recursively observe or simultaneously own the same canonical input stream.

**S2.4-AC08**
**Given** loop mode is disabled
**When** playback reaches its declared duration
**Then** every entry whose `relative_frame == duration` is injected in stable order before playback releases dummy-input ownership and stops exactly once
**And** entries beyond duration are invalid and no entry remains scheduled after the terminal frame.

**S2.4-AC09**
**Given** loop mode is enabled
**When** playback reaches the loop boundary
**Then** input transient state is reset according to the documented loop baseline and the next iteration rebases to a new start frame
**And** held or buffered state from the previous iteration cannot leak unless explicitly represented in the recording.

**S2.4-AC10**
**Given** a scene/match transition, successful restore, replay lifecycle transition, or explicit stop allocates or activates a different epoch
**When** old scheduled entries become due
**Then** they are discarded without input injection
**And** playback ownership and UI state reflect the active lifecycle only.

**S2.4-AC11**
**Given** the same validated recording and equivalent initial state are used repeatedly
**When** playback starts at different absolute frames
**Then** the relative input sequence and resulting live-system behavior are identical modulo the absolute frame offset
**And** a deterministic state hash over the documented observation window matches across runs.

**S2.4-AC12**
**Given** multiple recordings are captured
**When** the training ViewModel lists, names, selects, assigns, starts, stops, or loops them
**Then** selection changes do not mutate immutable recording contents
**And** the ViewModel remains testable without Godot.

**S2.4-AC13**
**Given** Story 2.4 is proposed for `ready-for-dev` or completion
**When** its risk/evidence row is reviewed
**Then** codec/validation unit, input integration, SOCD-boundary, ordering, rebasing, ownership-conflict, loop reset, lifecycle cancellation, determinism, ViewModel, and Godot dummy-playback evidence is linked
**And** Story 2.4 requires no future Story 2.5 implementation for standalone acceptance.

## Story 2.5: Failure-Atomic Training State Save and Load

As a developer practicing complex situations,
I want to save and restore the complete training-room state,
So that I can resume a mid-combo setup exactly without exposing partial or incompatible state.

**Acceptance Criteria:**

**S2.5-AC01**
**Given** the PREP-2.3 snapshot coordinator foundation is available
**When** training-mode participants register
**Then** character resources/positions, StateMachine, FrameData, Physics trajectories/generations, Input, Combo, and Story 2.4 recordings each use their component-owned discriminator and codec
**And** this Story does not introduce a second snapshot coordinator or persistence authority.

**S2.5-AC02**
**Given** Save State is requested
**When** the coordinator reaches the named quiescent frame boundary
**Then** every participant captures the same completed frame and active epoch into immutable versioned value snapshots
**And** no snapshot serializes Godot objects, concrete stacks, mutable arrays/dictionaries, or internal engine instances directly.

**S2.5-AC03**
**Given** a complete training snapshot is serialized
**When** the JSON file is inspected
**Then** it contains container schema version, framework version, source-epoch provenance, completed frame identity, component payloads, and integrity metadata using descriptive canonical field names
**And** it remains human-readable without making manual edits implicitly valid.

**S2.5-AC04**
**Given** Story 2.4 recordings are assigned, selected, or mid-playback at capture
**When** the training snapshot is saved and loaded
**Then** immutable recordings, selection, loop configuration, and the documented resumable playback state round-trip through the Input-owned codec
**And** old-epoch scheduled entries are not restored as active work.

**S2.5-AC05**
**Given** an existing save destination is present
**When** a new valid snapshot is written
**Then** serialization, integrity calculation, flush, and validation complete in an invocation-owned staged file before atomic replacement
**And** any failure preserves the prior save byte-for-byte.

**S2.5-AC06**
**Given** a save has an unsupported container/component version, failed integrity check, duplicate, missing, or unknown component discriminator, missing required field, invalid identity, dangling data reference, inconsistent frame, invalid generation relationship, declared-count mismatch, or exceeded codec resource limit
**When** Load State validates it
**Then** the complete candidate is rejected before live mutation
**And** the current training state, epoch, queues, active recordings, and prior save remain unchanged.

**S2.5-AC07**
**Given** a compatible schema contains an unknown optional field
**When** its owning codec reads the payload
**Then** it may ignore that field only according to the compatible-version policy
**And** unsupported versions require an explicit tested migration rather than partial loading or synthesized defaults.

**S2.5-AC08**
**Given** a candidate passes decoding and local participant validation
**When** the coordinator executes Prepare
**Then** it reserves but does not activate one fresh epoch, creates immutable prepared replacements, rebinds lifecycle identities, and validates the complete cross-component graph
**And** no observer or live component can see prepared state.

**S2.5-AC09**
**Given** any participant or cross-component validation fails during Prepare
**When** the restore attempt ends
**Then** all prepared values and the epoch reservation are discarded and live state remains value-equivalent to its pre-load state
**And** quarantined work is released against the unchanged epoch.

**S2.5-AC10**
**Given** every participant prepares successfully
**When** the coordinator performs Commit
**Then** deterministic no-fail reference swaps and reserved-epoch/high-water activation install the entire state atomically with observer notifications suppressed
**And** no operation capable of failure occurs after the first live-state swap.

**S2.5-AC11**
**Given** a normal restore commits successfully for a snapshot whose completed frame is F
**When** post-commit lifecycle handling finishes
**Then** old and quarantined envelopes are discarded, exactly one observe-only `StateRestored` is dispatched in phase 7, and processing resumes with `FrameAdvanced(F + 1)`
**And** publication attempted by a `StateRestored` subscriber is rejected.

**S2.5-AC12**
**Given** a `StateRestored` observer throws after restore Commit has succeeded
**When** EventBus completes the controlled observe-only notification
**Then** it records the observer failure without rolling back committed state, permits no observer publication or authoritative mutation, and exits quiescence deterministically
**And** processing still resumes from `FrameAdvanced(F + 1)` after the controlled notification pass.

**S2.5-AC13**
**Given** a snapshot captured during an active combo and in-flight knockback is restored in a new session
**When** both the original and restored states advance through the documented comparison window
**Then** positions, resources, stacks, move progress, trajectories, generations, recordings, combo state, and per-frame deterministic hashes match
**And** the next newly launched generation remains above every restored generation for its player.

**S2.5-AC14**
**Given** the save/load ViewModel lists and selects valid saves
**When** it requests capture or restore
**Then** it reports success only after atomic persistence or restore completion and exposes actionable validation errors otherwise
**And** all ViewModel behavior is testable without Godot.

**S2.5-AC15**
**Given** Story 2.5 is proposed for `ready-for-dev` or completion
**When** its risk/evidence row is reviewed
**Then** component codec, cross-graph validation, atomic-file fault injection, Prepare failure injection, no-fail Commit audit, recording round-trip, lifecycle/epoch, restart portability, multi-frame hash, ViewModel, and Godot mid-combo restore evidence is linked
**And** the required PO, Architect, and QA approvals are recorded.

---
