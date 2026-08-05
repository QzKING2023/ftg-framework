# Epic 2 Risk and Evidence Checklist

Status: Approved  
Date: 2026-08-01  
Scope: Stories 2.1-2.5 (FR-23-FR-27)

This is the canonical PREP-2.4 readiness artifact. A story cannot enter `ready-for-dev` until its risk row, evidence row, parameters, UX decisions, dependencies, and Product Owner/System Architect/QA decisions are complete. `N/A` always includes a rationale.

## Entry and Exit Gates

- Entry: PREP-2.1-2.3 and Architecture Adoption Gate rows AD-12/13/15/18/19/20 are accepted; the reviewed implementation is identified by a durable commit or content hash.
- Exit: every row below is complete; all blockers are resolved; PO, Architect, and QA approve the same artifact hash; the readiness assessment is rerun.
- Gate scope: ordinary implementation findings enter the backlog. A new gate requires an approved course correction.

## Canonical Execution and Dependency Graph

1. Story 2.1 and Story 2.3 may start independently after the gate.
2. Story 2.2 consumes Story 2.1's Data-authoring transaction contract but not its Godot dock implementation.
3. Story 2.4 standalone recording/playback is independently completable and consumes PREP-2.3 playback-mode ownership.
4. Story 2.5 consumes the accepted Story 2.4 recording codec and PREP-2.3 snapshot coordinator. Its recording round-trip slice runs only after the codec exists; Story 2.4 never depends on unfinished Story 2.5.
5. Epic numbering is product grouping, not executable order. Completed Epic 3 capabilities remain dependencies where named.

### Joint Godot Acceptance Dependency — JGA-2.3-CORR-1

- Story 2.3 automated implementation is frozen before CORR-1 changes shared training-scene files. Story 2.3-C interactive acceptance is not a prerequisite for starting CORR-1.
- After CORR-1 implementation and both automated gates pass, one immutable combined inventory enters the joint Godot session at `_bmad-output/implementation-artifacts/evidence/joint-2-3-corr-1/acceptance-plan.md`.
- Shared raw evidence is stored once. Story 2.3 and CORR-1 retain separate AC mappings, SHA-256 references, approval records, failure disposition, and completion status.
- Story 2.3 E2.3-G/E cannot pass from CORR-1 evidence alone; CORR-1 GODOT/SCAFFOLD evidence cannot pass from ComboDisplay observation alone.
- CORR-1 is complete. Story 2.4 slice D now depends on CORR-2 responsive-presentation and shortcut evidence under the approved 2026-08-04 correction.

## Blocking Decision Inventory

| ID | Owner | Affected story | Binding resolution | Evidence | Status |
|---|---|---|---|---|---|
| D-UX-01 | Product Owner + UX | 2.1-2.5 | Apply every shared interaction rule and story flow in `epic-2-ux-contract.md`; usability evidence is mandatory. | UX contract + E2.N-G scenarios | Resolved |
| D-UJ6-01 | Product Owner | 2.4/2.5 | V2 supports record, training playback, save, and restore. Replay frame-step/seeking remains out of V2. | UX contract Scope Decision | Resolved |
| D-DATA-01 | Data owner | 2.1/2.2 | Use the current-schema field and domain table below. Data remains the sole validator. | Parameter Registry P-DATA | Resolved |
| D-COMBO-01 | Product Owner + Engine owner | 2.3 | Use checked non-negative `int`; overflow rejects the whole event. Engine owns snapshotted damage; ViewModel owns aggregation. | Parameter Registry P-COMBO | Resolved |
| D-REC-01 | Product Owner + Input owner | 2.4 | Codec limits, sequence identity, loop baseline, and comparison window are fixed below. | Parameter Registry P-REC | Resolved |
| D-SAVE-01 | Product Owner + Architect | 2.5 | Container limits, integrity, compatibility, comparison window, and environments are fixed below. | Parameter Registry P-SAVE | Resolved |
| D-BASE-01 | Release owner | all | Durable PREP-2.3 implementation, tests, contracts, and evidence are committed at `a0c7043`; PO/Architect/QA repeated review against that exact commit and the refreshed content hashes. | PREP-2.3 commit `a0c7043`; refreshed role approvals | Resolved |

## Binding Parameter Registry

### P-DATA — Current Move Authoring Schema

- The current move-dataset schema version is `1`. Its canonical top-level envelope is an object with explicitly present `schema_version: 1` and `moves` fields; `moves` is a non-null array. Startup, editor load/save, hot reload, and runtime load reject a missing, null, non-integral, or unsupported `schema_version`. Ordinary load does not migrate older versions or ignore newer versions.
- Persistence granularity is one schema-version-1 move-dataset JSON document containing the complete `moves` collection. A move edit atomically replaces that document; `move_id` is never used to derive a path. A separately validated document identifier selects a file under the configured root, and that canonical document identity scopes optimistic concurrency and UndoRedo. The complete logical-dataset candidate remains the cross-document validation and DataStore swap root.
- Required canonical fields: `move_id`, `startup`, `active`, `recovery`, `hit_advantage`, `block_advantage`, `damage`, `chain_repeatable`, `knockback_profile_id`, `cancel_windows`, and `collision_frames`. `move_name` is optional display metadata.
- `move_id` and referenced IDs are non-empty ordinal identifiers. Frame counts and damage are non-negative `int`; their total must fit `int`. Advantage values are signed `int`.
- Arrays are explicit (empty is valid). Cancel ranges satisfy `0 <= start_frame <= end_frame <= total_frames`; collision frames are unique within `1..total_frames`; box IDs are unique per frame/list, coordinates are finite, and dimensions are finite/non-negative.
- Physics profiles remain schema version 1. AD-19 fields are present with exact JSON types; IDs are unique, references resolve within the staged logical dataset, and numeric physics values are finite/non-negative.

### P-COMBO — Display Arithmetic

- `HitCount` and `TotalDamage` are checked non-negative `int`. The maximum result is `int.MaxValue`; overflow rejects the incoming event and preserves the prior bucket.
- One bucket exists per valid attacker. Accumulate authoritative event damage without a DataStore reread; reset follows S2.3-AC04/05/09.

### P-REC — Recording Codec and Determinism

- Schema version 1; maximum duration 36,000 frames, 65,536 entries, 4 MiB encoded UTF-8 payload, and 128 Unicode scalar values in a name.
- Capture duration is derived with checked arithmetic from explicit start and stop frames; no duration is selected before capture. Reaching the duration limit auto-finalizes with an explicit reason.
- Stable identity is `(relative_frame, within_frame_sequence)`. Both are non-negative checked `int`; sequence begins at zero per frame and is contiguous. Invalid order/identity/value rejects the candidate.
- Loop baseline releases training-owned directions/buttons and clears its transient buffer/charge contribution through the canonical Input boundary before rebasing. Iterations use a `duration + 1` span so the next relative frame zero occurs on the immediately following schedulable frame without additional waiting.
- One ViewModel command and visible toggle own normal capture start/stop. Same-name replacement requires explicit confirmation and atomically installs the new content; cancellation/failure preserves the old recording, selection, and assignment, while active playback rejects replacement.
- Compare 600 frames after first injection, or terminal frame plus 120 when shorter, across starts separated by at least 10,000 frames and seeds 2202-2206.

### P-SAVE — Training Snapshot Container

- Container schema version 1; maximum file 16 MiB; 64 component records; 4 MiB per component; discriminator up to 128 ASCII characters. Validate counts and lengths before allocation.
- Integrity is lowercase hexadecimal SHA-256 over canonical UTF-8 payload bytes. Compatibility requires the same framework major/minor; migrations are explicit, ordered, complete, and tested.
- Compare 600 frames after restore with active combo, in-flight knockback, buffered/held input, and an assigned recording. Positions, resources, stacks, move progress, generations, recordings, and hashes must match.
- Reference environment: Windows x64, Godot 4.5.1 .NET, `net8.0`, repository SDK policy, plus one clean generated-scaffold run. Any additional release OS requires codec/persistence portability evidence.

### P-CORR-2 — Responsive Training Presentation

| Parameter | Binding value |
|---|---|
| Reference design viewport | `1152x648` |
| Minimum supported viewport | `960x540` |
| Required viewport matrix | `960x540`, `1152x648`, `1280x720`, `1920x1080`, `2560x1080` |
| Required UI scale matrix | `100%`, `150%`, `200%` |
| Screen-space safe margin | `12 px` at 100% scale |
| Minimum overlay separation | `8 px` at 100% scale |
| World scaling | uniform scale; centered reference region; surplus axis expands visible world/background |
| Tuning region | top-right |
| Recording/playback region | bottom-right |
| Diagnostics region | bounded left region with wrapping and scroll/collapse |
| Recording shortcut | `training_record_toggle`, default `Ctrl+Shift+R` |
| Play-once shortcut | `training_play_once`, default `Ctrl+Shift+P` |
| Loop shortcut | `training_loop_toggle`, default `Ctrl+Shift+L` |
| Stop shortcut | `training_playback_stop`, default `Ctrl+Shift+X` |

## Risk Matrix

Risk dimensions: lifecycle epoch; ownership; generation; event order; immutability; invalid input; failure atomicity; concurrency; test isolation; runtime/scaffold parity; determinism.

| Story | Risk dimension | Disposition and rationale | Specific AC | Evidence ID |
|---|---|---|---|---|
| Story 2.1 | lifecycle epoch | Applicable: S2.1-AC10 requires plugin disable/reload to dispose adapter state exactly once and reconstruct from authoritative Data. | S2.1-AC10 | E2.1-L |
| Story 2.1 | ownership | Applicable: Data owns validation/persistence; Godot is an adapter. | S2.1-AC01 | E2.1-D |
| Story 2.1 | generation | N/A: authoring creates no lifecycle-scoped gameplay generation. | N/A | N/A |
| Story 2.1 | event order | N/A: authoring commits through Data and publishes no EventBus gameplay sequence. | N/A | N/A |
| Story 2.1 | immutability | Applicable: a complete candidate/loaded model round-trips without hidden mutation. | S2.1-AC03 | E2.1-R |
| Story 2.1 | invalid input | Applicable: invalid fields, references, and paths reject before mutation. | S2.1-AC04 | E2.1-U |
| Story 2.1 | failure atomicity | Applicable: staging/replacement failure preserves prior bytes and dataset. | S2.1-AC08 | E2.1-F |
| Story 2.1 | concurrency | Applicable: stale editor versions and UndoRedo conflicts cannot overwrite unseen data. | S2.1-AC09 | E2.1-C |
| Story 2.1 | test isolation | Applicable: pure services execute without Godot singleton/editor state. | S2.1-AC10 | E2.1-U |
| Story 2.1 | runtime/scaffold parity | Applicable: authored JSON must load through runtime in a generated scaffold. | S2.1-AC12 | E2.1-S |
| Story 2.1 | determinism | Applicable: canonical JSON and runtime model round-trip to one semantic value. | S2.1-AC03 | E2.1-R |
| Story 2.2 | lifecycle epoch | Applicable: exit/restart/restore/replay cancels stale candidates and subscriptions. | S2.2-AC10 | E2.2-L |
| Story 2.2 | ownership | Applicable: Data transaction owns validation and commit; ViewModel owns staging only. | S2.2-AC03 | E2.2-D |
| Story 2.2 | generation | N/A: tuning allocates no gameplay generation. | N/A | N/A |
| Story 2.2 | event order | Applicable: FileWatcher observation follows the committed Data version without a second swap. | S2.2-AC03 | E2.2-B |
| Story 2.2 | immutability | Applicable: in-flight actions retain initiation snapshots after tuning. | S2.2-AC04 | E2.2-L |
| Story 2.2 | invalid input | Applicable: invalid candidates preserve file, DataStore, and active snapshots. | S2.2-AC07 | E2.2-U |
| Story 2.2 | failure atomicity | Applicable: every validation/I/O seam preserves prior bytes and dataset. | S2.2-AC07 | E2.2-F |
| Story 2.2 | concurrency | Applicable: exactly one optimistic writer wins. | S2.2-AC08 | E2.2-C |
| Story 2.2 | test isolation | Applicable: watcher/EventBus integration uses the PREP-2.2 isolation boundary. | S2.2-AC11 | E2.2-B |
| Story 2.2 | runtime/scaffold parity | N/A: Story 2.2 changes the installed runtime training panel only and does not change generated-scaffold inventory. | N/A | N/A |
| Story 2.2 | determinism | Applicable: restart loads the same canonical committed value. | S2.2-AC09 | E2.2-R |
| Story 2.3 | lifecycle epoch | Applicable: match/restore/replay/scene transitions reset transient display state. | S2.3-AC09 | E2.3-L |
| Story 2.3 | ownership | Applicable: Engine outcomes own damage; ViewModel owns display aggregation only. | S2.3-AC02 | E2.3-B |
| Story 2.3 | generation | N/A: display observes and allocates no generation. | N/A | N/A |
| Story 2.3 | event order | Applicable: final display follows AD-12 and within-phase sequence. | S2.3-AC06 | E2.3-B |
| Story 2.3 | immutability | Applicable: authoritative event damage is used without DataStore reread. | S2.3-AC02 | E2.3-U |
| Story 2.3 | invalid input | Applicable: malformed events preserve every bucket. | S2.3-AC07 | E2.3-U |
| Story 2.3 | failure atomicity | N/A: no persistence/transaction; invalid-event value preservation is covered as invalid input. | N/A | N/A |
| Story 2.3 | concurrency | Applicable: interleaved attacker and same-frame events remain isolated and ordered. | S2.3-AC03 | E2.3-C |
| Story 2.3 | test isolation | Applicable: subscription disposal prevents cross-test and re-entry duplication. | S2.3-AC10 | E2.3-L |
| Story 2.3 | runtime/scaffold parity | N/A: Story 2.3 adds training-scene UI but does not alter generated-scaffold inventory. | N/A | N/A |
| Story 2.3 | determinism | Applicable: repeated identical envelopes yield identical display values. | S2.3-AC06 | E2.3-E |
| Story 2.4 | lifecycle epoch | Applicable: stale schedules and ownership are discarded across lifecycle changes. | S2.4-AC10 | E2.4-L |
| Story 2.4 | ownership | Applicable: playback exclusively owns dummy input and conflicts with capture/replay are rejected. | S2.4-AC07 | E2.4-C |
| Story 2.4 | generation | Applicable: recording/playback identity is lifecycle-scoped and cannot reactivate old scheduled work. | S2.4-AC10 | E2.4-L |
| Story 2.4 | event order | Applicable: same-frame entries preserve explicit within-frame sequence. | S2.4-AC02 | E2.4-B |
| Story 2.4 | immutability | Applicable: finalized recording cannot change with capture-buffer mutation. | S2.4-AC03 | E2.4-U |
| Story 2.4 | invalid input | Applicable: full invalid matrix rejects before assigning/scheduling. | S2.4-AC04 | E2.4-U |
| Story 2.4 | failure atomicity | Applicable: confirmed same-name replacement installs the new immutable recording atomically; cancel/failure preserves the old recording, selection, assignment, and schedule. | S2.4-AC14 | E2.4-U, E2.4-E |
| Story 2.4 | concurrency | Applicable: capture, training playback, live input, and Replay ownership cannot overlap illegally. | S2.4-AC07 | E2.4-C |
| Story 2.4 | test isolation | Applicable: playback/EventBus tests use PREP-2.2 scopes and release ownership. | S2.4-AC13 | E2.4-B |
| Story 2.4 / CORR-2 | runtime/scaffold parity | Applicable: responsive presentation, layout regions, and InputMap actions must remain equivalent in repository and generated-scaffold scenes. | S2.4-AC13, C2-AC07 | E2.4-S |
| Story 2.4 | determinism | Applicable: rebased executions match across starts/seeds. | S2.4-AC11 | E2.4-R |
| Story 2.5 | lifecycle epoch | Applicable: Prepare/Commit reserves and activates one epoch and resumes at F+1. | S2.5-AC11 | E2.5-L |
| Story 2.5 | ownership | Applicable: component codecs and the existing Core coordinator retain singular ownership. | S2.5-AC01 | E2.5-U |
| Story 2.5 | generation | Applicable: restored high-water identities keep new launches above restored generations. | S2.5-AC13 | E2.5-R |
| Story 2.5 | event order | Applicable: exactly one phase-7 StateRestored precedes resume at F+1. | S2.5-AC11 | E2.5-B |
| Story 2.5 | immutability | Applicable: capture uses value-only versioned snapshots. | S2.5-AC02 | E2.5-U |
| Story 2.5 | invalid input | Applicable: schema/integrity/graph/resource failures reject before mutation. | S2.5-AC06 | E2.5-U |
| Story 2.5 | failure atomicity | Applicable: file and Prepare seams preserve prior state and bytes. | S2.5-AC09 | E2.5-F |
| Story 2.5 | concurrency | Applicable: capture/load and quiescence exclude competing mutation. | S2.5-AC08 | E2.5-C |
| Story 2.5 | test isolation | Applicable: fault and EventBus tests must restore singleton/producer state. | S2.5-AC15 | E2.5-B |
| Story 2.5 | runtime/scaffold parity | Applicable: the snapshot codec/persistence contract must pass once in a clean generated scaffold under P-SAVE. | S2.5-AC14 | E2.5-S |
| Story 2.5 | determinism | Applicable: original/restored runs match for the fixed multi-frame window. | S2.5-AC13 | E2.5-E |

## Evidence Matrix

Every artifact lives under `_bmad-output/implementation-artifacts/evidence/v2-2-N/` and records command/scenario, OS, .NET SDK, Godot version, reviewed commit/hash, pass/fail/skip counts, exit code, duration/seed where relevant, reviewer, acceptance state, and SHA-256 inventory. Tests never write into tracked evidence folders.

| Story | Unit | Data/service integration | EventBus integration | Fault injection | Concurrency | Lifecycle | Godot editor/runtime | Scaffold | Round trip | End-to-end |
|---|---|---|---|---|---|---|---|---|---|---|
| Story 2.1 | E2.1-U required | E2.1-D required | N/A: no EventBus contract | E2.1-F required: every pre-commit failure preserves prior bytes/DataStore and returns error; post-commit cleanup failure preserves the new commit, returns success, and emits a diagnostic | E2.1-C required | E2.1-L plugin disable/reload required | E2.1-G editor + UndoRedo required | E2.1-S required | E2.1-R JSON/runtime load required | E2.1-E author-save-runtime-load required |
| Story 2.2 | E2.2-U required | E2.2-D required | E2.2-B reload observation required | E2.2-F every persistence seam required | E2.2-C editor/tuner race required | E2.2-L initiation snapshot/restart required | E2.2-G runtime panel required | N/A: no generated-scaffold inventory change in Story 2.2 | E2.2-R restart required | E2.2-E tune-write-reload required |
| Story 2.3 | E2.3-U required | N/A: no Data mutation | E2.3-B required | N/A: no fallible persistence; invalid-event preservation in unit tests | E2.3-C same-frame order required | E2.3-L required | E2.3-G runtime display required | N/A: no generated-scaffold inventory change in Story 2.3 | N/A: transient observer | E2.3-E hit/block/end/restore required |
| Story 2.4 / CORR-2 | E2.4-U codec/ViewModel, stop-derived duration, toggle idempotency, atomic overwrite + layout/shortcut routing required | E2.4-D presentation/simulation isolation required | E2.4-B injection/order and gapless-loop timing required | E2.4-U/E overwrite cancel/failure preservation required; CORR-2 layout adds no persistence boundary | E2.4-C playback ownership required; layout applies on the Godot main thread | E2.4-L required | E2.4-G dummy playback + viewport/UI-scale matrix required | E2.4-S responsive scene/InputMap parity required | E2.4-R codec + pre/post-resize deterministic hash required | E2.4-E dynamic capture, one-toggle, gapless loop, same-name overwrite + responsive interaction required |
| Story 2.5 | E2.5-U codecs/ViewModel required | E2.5-D persistence required | E2.5-B restore notification/queues required | E2.5-F every Prepare/file seam required | E2.5-C capture/load exclusion required | E2.5-L epoch/frame required | E2.5-G mid-combo restore required | E2.5-S clean generated-scaffold save/load required | E2.5-R restart + recording required | E2.5-E multi-frame hash required |

## Bounded Vertical-Slice Plans

Slice IDs are stable execution/evidence keys. Parent stories remain FR/user-outcome keys and become done only after every required slice plus final parent E2E acceptance passes. Slice statuses are created only when the parent story file is created; this checklist does not pre-create sprint status. Every documented/configurable parameter must have a concrete value or fixture before `ready-for-dev`.

- **S2.1-A:** developer can load/edit a move and receives complete inline validation/conflict results; **S2.1-B:** developer saves and reloads one path-confined move with byte-preserving failure proof; **S2.1-C:** developer performs editor selection, save, UndoRedo, scaffold, and runtime-load flow in Godot.
- **S2.2-A:** developer stages edits while active gameplay remains unchanged; **S2.2-B:** one writer commits and a stale concurrent writer receives a recoverable conflict with fault-equivalence proof; **S2.2-C:** developer observes committed tuning after watcher processing and restart in Godot.
- **S2.3-A:** developer observes isolated per-attacker count/damage for authoritative hits, including invalid/overflow preservation; **S2.3-B:** developer observes deterministic hit/block/end and lifecycle resets without duplicate subscriptions; **S2.3-C:** rendered Godot display matches the ViewModel through exit/re-entry.
- **S2.4-A:** developer records, names, lists, and reloads a validated immutable recording through the ViewModel/codec boundary; **S2.4-B:** developer plays the recording once from different absolute frames with identical relative outcomes; **S2.4-C:** developer starts/stops/loops while capture/live-input/Replay conflicts and lifecycle cancellation are visibly enforced; **S2.4-D:** Godot dummy playback produces the accepted deterministic hash window after CORR-2 proves responsive layout, shortcuts, resize/fullscreen state retention, and repository/scaffold parity; **S2.4-E:** developer captures until the single toggle stops, observes a gapless loop boundary, and explicitly overwrites a same-name recording with failure-atomic preservation evidence.
- **S2.5-A:** developer saves a complete inspectable training snapshot containing every required participant and recording metadata; **S2.5-B:** developer atomically overwrites/loads a save while injected file/codec failures preserve the prior save and live state; **S2.5-C:** developer restores a mid-combo state through full Prepare/no-fail Commit and observes exactly one StateRestored/F+1 continuation; **S2.5-D:** developer restores assigned/mid-playback Story 2.4 recording state without old-epoch work; **S2.5-E:** Godot ViewModel flow reproduces the accepted multi-frame state hash.

## Approval Record

### CORR-2 Evidence Classification

| Work item | Primary risks | Required evidence |
|---|---|---|
| CORR-2-A | resize mutates authoritative state; aspect distortion; center drift; scaffold divergence | UNIT layout calculations; INT presentation/simulation isolation; GODOT viewport/fullscreen matrix; SCAFFOLD parity; E2E real-character framing |
| CORR-2-B | overlay collision; clipped/unreachable controls; lost focus/edit state; duplicated resize ownership | UNIT region calculations; LIFE rebuild/scene-exit behavior; GODOT 100%-200% scale, scrolling, focus, non-overlap; E2E tuning + playback coexistence |
| CORR-2-C | shortcut/gameplay conflicts; text input interception; duplicate commands; stale lifecycle ownership | UNIT command routing; INT InputMap/ownership; LIFE replay/restore/scene transitions; GODOT keyboard/controller; SCAFFOLD action parity; E2E Story 2.4 interaction |

`FAULT` is N/A because CORR-2 adds no persistence commit boundary; existing Story 2.2 and Story 2.4 failure semantics receive regression coverage. `CONC` is N/A because layout is applied on the Godot main thread; background callbacks may not mutate Controls directly. Product Owner, Architect, QA, and Q1625 acceptance are required for CORR-2 completion.

The PREP-2.4 binding decisions are stored in `_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/role-approvals.md` and remain valid historical evidence for the reviewed inventory. The approved 2026-08-01 planning reconciliation and its passing readiness rerun authorize the source, vocabulary, and stable-slice amendments recorded here; they do not make the prior SHA-256 inventory describe the amended files. Before Epic 2 kickoff, Product Owner, Architect, and QA must approve fresh hashes for the current planning set. Any rejection reopens the affected row.
