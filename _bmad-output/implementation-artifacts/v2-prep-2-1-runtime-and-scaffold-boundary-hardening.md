---
story_key: v2-prep-2-1-runtime-and-scaffold-boundary-hardening
story_id: PREP-2.1
date_created: 2026-08-01
baseline_commit: ad1c202d35173114caee74219bd052c3cb6b3e9d
---

# PREP-2.1: Runtime and Scaffold Boundary Hardening

Status: done

## Story

As a senior framework developer preparing Epic 2,
I want the known knockback-ownership, player-validation, and scaffold-path defects corrected with regression evidence,
so that Epic 2 builds on safe runtime and generated-project boundaries.

## Acceptance Criteria

1. **P2.1-AC01 — Generation-owned Hitstun.**  
   **Given** an older knockback generation G1 completes after a newer generation G2 has bound Hitstun for the same player and epoch  
   **When** StateMachine handles the G1 completion  
   **Then** it rejects the non-matching tuple without clearing or replacing G2 occupancy  
   **And** unit and EventBus regression tests prove the G2 state remains unchanged.

2. **P2.1-AC02 — Validate player before access.**  
   **Given** a knockback outcome contains an invalid player ID  
   **When** StateMachine receives it  
   **Then** the ID is rejected before any player-state lookup, profile query, or stack mutation  
   **And** tests cover every invalid boundary value supported by the player-ID representation.

3. **P2.1-AC03 — Exact tuple lifecycle.**  
   **Given** a valid player receives `Started`, zero or more `Progressed`, and `Completed` for one exact epoch/generation tuple  
   **When** the sequence is processed  
   **Then** only the matching completion clears its Hitstun occupancy  
   **And** exact duplicates are idempotent while all other out-of-order sequences are rejected.

4. **P2.1-AC04 — Scaffold confinement.**  
   **Given** a scaffold source-manifest entry is rooted, contains parent traversal, uses forbidden separators, resolves through normalization or links outside its approved root, or targets outside the validated destination  
   **When** manifest validation runs  
   **Then** the entire scaffold request is rejected before copying  
   **And** no external source or destination file is read, created, modified, or deleted.

5. **P2.1-AC05 — Scaffold parity.**  
   **Given** a valid repository-confined manifest  
   **When** the scaffold is generated  
   **Then** its file set and runnable behavior remain equivalent to the accepted Story 1.4 scaffold  
   **And** build plus Godot smoke evidence confirms no regression.

6. **P2.1-AC06 — Closure evidence.**  
   **Given** PREP-2.1 implementation is ready for closure  
   **When** its evidence is reviewed  
   **Then** targeted unit tests, EventBus integration tests, scaffold path-boundary tests, generated-project build, and Godot runtime proof all pass  
   **And** evidence is indexed under this implementation record with links from the three corresponding `v2-1` retrospective actions.

7. **P2.1-AC07 — Tracking isolation.**  
   **Given** every PREP-2.1 criterion has linked passing evidence  
   **When** sprint tracking is updated  
   **Then** `v2-prep-2-1-runtime-and-scaffold-boundary-hardening` and only its three satisfied retrospective actions may move to `done`  
   **And** `v2-epic-1` remains `done` while `v2-epic-2` remains `backlog`.

## Tasks / Subtasks

- [x] Implement exact knockback occupancy ownership in StateMachine (AC: 1, 3)
  - [x] Replace the loose latest/completed-generation filtering with explicit per-player Hitstun occupancy and per-tuple phase progress. Occupancy must identify the generation that actually entered/bound Hitstun; do not infer ownership merely from the numerically latest event.
  - [x] Represent `Started`, `Progressed`, and `Completed` explicitly. Accept exactly one `Started`, zero or more `Progressed`, then one `Completed`. `ContactFrame` is constant for the tuple and each newly accepted phase has a strictly increasing `FrameNumber`.
  - [x] Define idempotence centrally: only an event identical to the last accepted event across every payload field (`PlayerId`, `GenerationId`, `Phase`, forces, position, `ContactFrame`, and `FrameNumber`) is an exact duplicate and is ignored. Reject an older previously accepted event replayed after later progress, same-generation/same-frame conflicting data, and every post-terminal event.
  - [x] Bind occupancy only when an accepted `Started` corresponds to the player's Hitstun entry. A stale/non-matching completion must not call `ResetToIdle`, refresh a profile snapshot, publish state events, or alter the newer tuple's tracking state.
  - [x] Store the exact `(PlayerId, LifecycleEpoch, GenerationId)` supplied by the EventBus dispatch envelope. Clear tuple/occupancy tracking symmetrically on shutdown; lifecycle activation invalidates stale envelopes through EventBus rather than pretending a generation-only reset is an epoch.
  - [x] Preserve AD-10/AD-11 ownership: Physics publishes immutable outcomes; StateMachine alone mutates the stack; neither module directly controls the other.

- [x] Reject invalid knockback player IDs before all keyed access (AC: 2, 3)
  - [x] Validate `PlayerId` at the first line of knockback outcome handling, before dictionary lookup/insertion, `GetCurrentState`, effective-profile access, or stack mutation.
  - [x] Define event-input rejection behavior consistently (no partial mutation and an English `[StateMachine]` diagnostic); do not let malformed EventBus input escape as a query-method exception.
  - [x] Cover the complete `int` boundary partition: `int.MinValue`, representative negative, `0`, valid `1` and `2`, `3`, representative positive above range, and `int.MaxValue`.

- [x] Implement the minimal EventBus lifecycle-epoch substrate required by PREP-2.1 (AC: 1, 3)
  - [x] Replace raw queued payload objects with internal immutable envelopes stamped at publication with the active non-wrapping `ulong LifecycleEpoch`, frame, and payload. Event payloads remain epoch-free.
  - [x] Expose the currently dispatched envelope epoch to internal subscribers through a read-only dispatch context valid only during the callback; StateMachine reads that context when processing knockback outcomes. Do not add epoch to `KnockbackAppliedEvent`.
  - [x] On `MatchInitialized`, `ReplayStarted`, and `ReplayEnded` dispatch (including immediate dispatch), reserve and activate the next epoch before lifecycle subscribers run, reject exhaustion before state/queue mutation, restamp the lifecycle envelope to the new epoch, and discard all queued old-epoch work. Events published by lifecycle subscribers are stamped with the new epoch.
  - [x] Reject any envelope whose epoch differs from the active epoch before recorder or subscriber observation. Preserve non-reentrant next-frame queuing, LIFO same-type order, AD-12 phase order, recorder behavior, reload queue handling, pause/step behavior, and frame rewind semantics.
  - [x] Add focused EventBus tests for monotonic activation, stale current/next/reload work rejection, lifecycle-subscriber publication, immediate lifecycle dispatch, recorder visibility, and `ulong.MaxValue` reservation failure through an internal test seam.

- [x] Add runtime unit and EventBus regression evidence (AC: 1-3, 6)
  - [x] Extend `StateMachineTests` with HitConnected-G1-start, a second HitConnected that supersedes the first occupancy, G2-start, then stale G1-complete; matching completion; last-event duplicates; old-duplicate-after-progress; same-frame conflicts; completion-before-start; progress-before-start; start-after-complete; mismatched generation; both players; lifecycle reset; and invalid-player cases.
  - [x] Assert rejected sequences preserve stack contents, effective-profile snapshot identity/value, occupancy owner, and emitted `StateChangedEvent`/`StateStackChangedEvent` count.
  - [x] Exercise queued EventBus dispatch, not only direct/private handler behavior, and dispose subscriptions in every test path.
  - [x] Extend Physics/StateMachine integration coverage so real accepted launches emit the explicit phase sequence with monotonically increasing per-player generations and preserve an active newer trajectory.
  - [x] Extend `CharacterKnockbackEventOrderTests` for the explicit Started/Progressed/Completed sequence, last-event duplicates, same-frame conflicts, post-completion rejection, lifecycle epoch reset, and final Completed absolute position/non-airborne behavior.

- [x] Implement the AD-18 generation and event contract without compatibility ambiguity (AC: 1, 3)
  - [x] Physics owns one non-wrapping `ulong` generation counter per player. Reserve the next value with checked logic before accepting or mutating a launch; P1 and P2 start and advance independently. At `ulong.MaxValue`, reject before trajectory/state mutation and emit a fatal `[Physics]` diagnostic.
  - [x] Define `KnockbackPhase : byte { Started = 1, Progressed = 2, Completed = 3 }`. Change `KnockbackAppliedEvent.GenerationId` to `ulong`, add `KnockbackPhase Phase`, and remove the public `Completed` Boolean. Update every production/test constructor in the same change; old constructor source compatibility is intentionally not retained.
  - [x] Preserve the existing replay JSON property naming policy (currently CLR/PascalCase), write `GenerationId` as an unsigned number and `Phase` as the explicitly configured representation used by the codec, and add golden JSON to lock that shape. Do not introduce an unrelated global naming-policy change.
  - [x] Raise the current replay data version to 3 while retaining explicit container migrations for versions 1 and 2. A v1/v2 replay without knockback events may migrate normally; a v1/v2 replay containing legacy `KnockbackAppliedEvent` payloads without a valid phase must fail during `ReplayPlayer.Load` with `[Replay]` context. Never deserialize missing phase as enum zero and continue. Add v3 round-trip/golden tests and v1/v2 legacy-knockback rejection fixtures.
  - [x] Emit `Started` exactly once when a validated hit becomes the accepted `_launchCandidates[playerId]` trajectory, after generation reservation and before its first advance. It carries launch position, contact frame, and current EventBus frame. On the next Physics update, publish either `Progressed` after the first non-terminal advance or `Completed` if that advance terminates; never publish both for one advance.
  - [x] StateMachine records the causally preceding `HitConnected` Hitstun as awaiting a launch for that defender in the active lifecycle namespace; the next valid `Started` binds that exact occupancy. A later accepted `HitConnected` supersedes/cancels the prior awaiting or bound ownership before its new `Started`. Reject a `Started` with no awaiting Hitstun, or after another state mutation displaced it, without rebinding unrelated Hitstun.

- [x] Preflight and confine the complete scaffold request before mutation (AC: 4)
  - [x] Refactor `ProjectScaffolder.ValidateInputs`/manifest loading into one pure validation phase that builds an immutable, exhaustive copy/mutation plan before `CopyDirectory` or `EnsureDirectory` can write. The plan enumerates every source file and destination from the template tree, every manifest tree, `Scripts/FrameRateManager.cs`, `Characters/`, the renamed `.csproj`, and placeholder-written files; execution must not rediscover directory children.
  - [x] Validate `projectName` as one filename component: reject empty/whitespace, rooted/drive/UNC/device forms, either separator, `.`/`..`, invalid filename characters, and reserved platform names. Canonicalize the renamed `.csproj` and every project-name-derived destination under the validated target root.
  - [x] Accept only normalized repository-relative manifest directory entries. Reject empty effective entries, rooted/drive/UNC/device paths, `.`/`..` segments, alternate or forbidden separators, malformed paths, and any canonical source outside the approved repository root.
  - [x] Apply separator-aware containment checks (`candidate == root` or starts with `root + directory separator`); never use a raw string-prefix test. Use the platform's appropriate path comparison semantics.
  - [x] Inspect every existing file and directory component for reparse points/symbolic links; reject broken, cyclic, unresolvable, or out-of-root links. For a not-yet-created target, validate the nearest existing parent and every ancestor through the target root.
  - [x] Reject duplicate/aliasing manifest entries and source-to-destination collisions after canonicalization. Validate the configured target itself and every destination beneath it.
  - [x] Preserve all-or-nothing preflight: one invalid entry rejects the entire request before any template file is copied, target directory is created, source outside the root is opened, or cleanup is needed.
  - [x] Route manifest reads, source reads, destination creates, placeholder reads/writes, `.csproj` move, and restore working-directory selection through validated-plan helpers that revalidate the relevant parent/link chain immediately before each access. Preserve `Program.cs` cleanup ownership; change it only if a public CLI regression proves the existing artifact tracker cannot preserve pre-existing content.
  - [x] Threat model: guarantee rejection of pre-existing malicious manifest entries and link/reparse layouts, including link replacement detected by access-boundary revalidation. Portable `System.IO` cannot guarantee a race-free sandbox against a privileged actor replacing a path between final check and open; handle-based no-follow traversal is out of scope. Document this limit and test deterministic pre-existing and injected pre-access swaps.

- [x] Add scaffold boundary and parity evidence (AC: 4-6)
  - [x] Extend `FtgCliTests` with rooted Windows and Unix forms, drive-relative/UNC/device forms where supported, `../` and `..\\`, forbidden mixed separators, normalization escape, prefix-sibling roots, duplicate aliases, source symlink/junction escape, destination link escape, and a valid nested repository-relative entry.
  - [x] Exercise both `ProjectScaffolder` and the public CLI path. Cover malicious `projectName`, renamed-project path escape, a later-invalid manifest entry, template/manifest file links, source/destination links, and restore/placeholder access boundaries.
  - [x] Use an injectable filesystem/access seam to prove forbidden external sources are never opened. For every rejection, snapshot names, bytes, and relevant metadata outside both roots and in a pre-existing non-empty target; assert exact preservation. For a new target, assert it remains absent.
  - [x] Keep existing inventory/parity, placeholder, cleanup, restore-failure, SDK-resolution, package, and generated-build tests green.
  - [x] Generate a project from the real manifest, run `dotnet build --no-restore` after scaffold restore, and execute the documented `Scaffold/verification` Godot smoke scenario.

- [x] Index evidence and close only the owned gate rows (AC: 6, 7)
  - [x] Store checked-in logs and manifests under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-1/`; terminal-only transcripts are not durable evidence. Complete the Evidence Index with exact test names, commands, OS, .NET SDK, Godot version, exit codes, pass/fail/skip counts, and repo-relative artifact paths.
  - [x] Add an `Evidence` column to the V2 Epic 1 retrospective Action Items table. Rows 1-3 link to this story's Evidence Index anchors; rows 4-9 remain textually and semantically unchanged.
  - [x] Follow normal story transitions (`ready-for-dev` -> `in-progress` -> `review` -> `done`). At accepted closure, update the three top-level `action_items` rows whose `epic` is `v2-1` and whose exact `action` text matches knockback ownership, invalid-player rejection, and scaffold-manifest hardening. Change only those rows' `status`, this PREP `development_status` entry, and `last_updated`; preserve comments/order, every non-owned action/status row, `v2-epic-1: done`, and `v2-epic-2: backlog`.

### Review Findings

- [x] [Review][Patch] Stop dispatching an outer lifecycle envelope after a nested lifecycle subscriber advances the epoch [Scripts/Framework/Core/EventBus.cs:159]
- [x] [Review][Patch] Reject queued lifecycle publication at epoch exhaustion without clearing unrelated queued work [Scripts/Framework/Core/EventBus.cs:121]
- [x] [Review][Patch] Synchronize background reload epoch/frame stamping with lifecycle activation [Scripts/Framework/Core/EventBus.cs:94]
- [x] [Review][Patch] Clear or invalidate knockback occupancy whenever its bound Hitstun is displaced [Scripts/Framework/Engine/StateMachine/StateMachine.cs:159]
- [x] [Review][Patch] Preserve current occupancy until a superseding Started event is accepted [Scripts/Framework/Engine/StateMachine/StateMachine.cs:275]
- [x] [Review][Patch] Reject replayed or decreasing Started generations within an epoch [Scripts/Framework/Engine/StateMachine/StateMachine.cs:313]
- [x] [Review][Patch] Accept Completed as the first legal advance after Started [Scripts/Framework/Characters/CharacterController.cs:165]
- [x] [Review][Patch] Require temporal monotonicity when accepting a higher-generation Started event [Scripts/Framework/Characters/CharacterController.cs:160]
- [x] [Review][Patch] Publish effective gravity and friction consistently in the Started phase [Scripts/Framework/Engine/Physics/PhysicsEngine.cs:100]
- [x] [Review][Patch] Reserve all same-update launch generations before publishing any hit event [Scripts/Framework/Engine/Physics/PhysicsEngine.cs:96]
- [x] [Review][Patch] Fully deserialize and validate knockback payloads atomically during ReplayPlayer.Load [Scripts/Framework/Core/Replay/ReplayVersionValidator.cs:51]
- [x] [Review][Patch] Apply the complete reserved-name and C# identifier policy in the direct ProjectScaffolder API [Scaffold/ftg-cli/ProjectScaffolder.cs:86]
- [x] [Review][Patch] Revalidate renamed csproj destinations after deriving the final path [Scaffold/ftg-cli/ProjectScaffolder.cs:139]
- [x] [Review][Patch] Move deterministic swap hooks before the final access-boundary validation [Scaffold/ftg-cli/ProjectScaffolder.cs:193]
- [x] [Review][Patch] Validate all target descendants immediately before restore [Scaffold/ftg-cli/ProjectScaffolder.cs:71]
- [x] [Review][Patch] Preserve planned empty source directories in generated projects [Scaffold/ftg-cli/ProjectScaffolder.cs:113]
- [x] [Review][Patch] Use platform-appropriate path comparison semantics [Scaffold/ftg-cli/ProjectScaffolder.cs:106]
- [x] [Review][Patch] Add the mandated queued ownership, lifecycle, recorder, phase-sequence, and invariant-preservation regressions [Tests/FTG_Framework.Tests/Core/EventBusEpochTests.cs:26]
- [x] [Review][Patch] Make the story and evidence artifacts eligible for checked-in durable evidence [/.gitignore:25]
- [x] [Review][Defer] Define EventBus frame-number overflow behavior for long-running sessions [Scripts/Framework/Core/EventBus.cs:201] — deferred, pre-existing

## Dev Notes

### Scope and Dependency Boundary

- This is non-epic corrective work with no FR coverage. It neither reopens V2 Epic 1 nor starts V2 Epic 2.
- PREP-2.1 owns retrospective actions 1-3 only: knockback/Hitstun generation ownership, invalid-player rejection, and scaffold path hardening.
- PREP-2.1 owns the bounded EventBus envelope/epoch and knockback event-schema slices required to make P2.1-AC01/03 executable. PREP-2.3 must consume these foundations and owns the remaining AD-18 work (transitively immutable state payloads and completed-runtime audit), broader event/lifecycle enforcement, transactional data, and snapshot contracts; it must not reimplement or replace the accepted PREP-2.1 semantics.
- Do not absorb PREP-2.2 test-infrastructure work (fixed FileWatcher waits/EventBus global isolation), PREP-2.3 data/event/snapshot contracts, or PREP-2.4 planning approvals.
- Findings outside these three defects enter the normal backlog unless they violate an approved architecture invariant; expanding the readiness gate requires a newly approved course correction.

### Current State and Required Preservation

#### `Scripts/Framework/Core/EventBus.cs` — UPDATE

- **Current:** queues raw payload objects, has no lifecycle epoch/envelope identity, and exposes only `CurrentFrame`; lifecycle events are ordinary phase-7 payloads.
- **Change:** add the minimal immutable envelope, active-epoch allocation/activation, stale-envelope rejection, and internal dispatch context specified above.
- **Preserve:** singleton API surface for ordinary publishers/subscribers, eight-phase order, same-type LIFO behavior, non-reentrant `_nextQueue`, immediate-dispatch use cases, replay recorder hooks, phase-0 reload draining, and rewind queue purge.

#### `Scripts/Framework/Engine/StateMachine/StateMachine.cs` — UPDATE

- **Current:** subscribes to `KnockbackAppliedEvent`; tracks `_latestTrajectoryGenerations` and `_completedTrajectoryGenerations`; accepts events with positive generation and positions; resets Hitstun on a completed latest generation. Replay start/end and match initialization clear generation dictionaries.
- **Defect:** Hitstun occupancy is not bound to the generation that created it. A numerically accepted completion can clear a later logical occupancy. `PlayerId` is used as a dictionary key before validation and can reach `GetCurrentState`, which throws for invalid IDs.
- **Preserve:** stack semantics, cached effective physics profiles, event publication through `CommitStateChange`, subscription symmetry, replay/match reset behavior, and the StateMachine/Physics peer boundary.

#### `Scripts/Framework/Engine/Physics/PhysicsEngine.cs` — UPDATE

- **Current:** owns per-player active trajectories, uses a process-wide increasing `long` generation, advances trajectories once per update, publishes `KnockbackAppliedEvent`, then removes completed trajectories.
- **Change:** replace the global counter with mandatory per-player non-wrapping `ulong` counters, reserve before mutation, reject exhaustion, and publish the exact explicit phase sequence defined above.
- **Preserve:** snapshot-on-hit profiles, Physics ownership of calculation/position, one active trajectory per player, deterministic participant order, and EventBus-only communication to StateMachine.

#### `Scripts/Framework/Core/Events/KnockbackAppliedEvent.cs` — UPDATE

- **Current:** public `readonly record struct` with nullable position, signed `long GenerationId`, frame fields, and a `Completed` Boolean.
- **Change:** remove `Completed`, change generation to `ulong`, and add the required `KnockbackPhase : byte`. Reject legacy replay payloads without a versioned migration; update all constructors, codecs, consumers, and tests together.
- **Preserve:** plain immutable Core payload with no Godot type or mutable container. Lifecycle epoch belongs to the EventBus envelope, not this payload.

#### `Scripts/Framework/Core/Events/KnockbackPhase.cs` — NEW

- Add only the explicit byte-backed phase enum. Keep it in Core beside the event; do not put lifecycle or transition logic in the enum.

#### `Scripts/Framework/Characters/CharacterController.cs` — UPDATE

- **Current:** filters knockback outcomes by generation/frame, applies absolute world positions, and resets ordering at replay/match lifecycle boundaries.
- **Change:** consume the explicit phase contract and preserve the final completed-motion behavior; reject malformed ordering consistently without creating a second ownership state machine.
- **Preserve:** absolute-position application, player filtering, lifecycle resets, and view/controller separation.

#### Replay registration and tests — UPDATE

- Update `ReplayVersionValidator.cs`, `ReplayEventJsonContext.cs`, and the playback validation path. Keep `KnockbackAppliedEvent` in `EventTypeRegistry` with its discriminator and playback policy unchanged. Implement the exact v1/v2/v3 policy above rather than relying on enum defaults.

#### `Scaffold/ftg-cli/ProjectScaffolder.cs` — UPDATE

- **Current:** preflights required files and manifest directories, but `LoadFrameworkSourceDirs` trims lines, replaces `\\` with `/`, and returns them for direct `Path.Combine(repoRoot, srcDir)` / `Path.Combine(targetDir, srcDir)`. Copying begins only after `ValidateInputs`, but confinement and link checks are absent.
- **Change:** produce a fully validated immutable copy plan before the first filesystem mutation and use only the canonical paths from that plan during copying.
- **Preserve:** excluded `.uid` files and `bin`/`obj`/`.godot` directories, artifact tracking/cleanup behavior, placeholder replacement, required resource inventory, restore timeout/error handling, and byte-for-byte manifest parity for valid inputs.

#### `Scaffold/ftg-cli/Program.cs` — PRESERVE UNLESS PROVEN NECESSARY

- Keep public argument handling and invocation-owned cleanup semantics. If the validated-plan design requires a change here, first add a failing public-CLI regression that proves why `ProjectScaffolder` and the existing artifact tracker cannot enforce the boundary alone.

#### Tests — UPDATE

- `Tests/FTG_Framework.Tests/Engine/StateMachine/StateMachineTests.cs`: retain existing lifecycle draining and shutdown pattern; strengthen current generation tests rather than adding a second test harness.
- `Tests/FTG_Framework.Tests/Engine/Physics/PhysicsEngineTests.cs`: prove real publisher phase/generation behavior and snapshot isolation.
- `Tests/FTG_Framework.Tests/Characters/CharacterKnockbackEventOrderTests.cs`: align controller ordering, duplicate/conflict rejection, lifecycle reset, and final-position behavior with the public event contract.
- `Tests/FTG_Framework.Tests/Scaffold/FtgCliTests.cs`: extend `CreateMinimalScaffoldRepo`, preflight atomicity, inventory/parity, generated-build, and smoke-harness coverage.
- `Tests/FTG_Framework.Tests/Replay/KnockbackReplayTests.cs`, `EventTypeRegistryTests.cs`, and `ReplayVersionValidatorTests.cs`: lock discriminator/policy stability, v3 golden payload, `ulong.MaxValue` round-trip, valid phase representation, and v1/v2 missing-phase rejection.

### Architecture Compliance

- Follow AD-9 snapshot-on-initiation, AD-10 Physics ownership, AD-11 peer communication, AD-12 deterministic phase-4 dispatch, and AD-18 exact generation ownership.
- Event duplicates may be ignored only when the complete tuple and phase/data are exact duplicates. A same-generation event with conflicting phase/data is malformed, not idempotent.
- No new runtime NuGet dependency. Use built-in `System.IO` and existing xUnit infrastructure.
- C# naming follows project conventions; errors are English and module-prefixed.
- Scaffold containment is a security boundary: validation must be fail-closed, canonical, separator-aware, link-aware, and repeated at the actual access boundary.

### Library / Framework Requirements

- Keep checked-in pins: Godot .NET SDK 4.5.1, runtime `net8.0` (Android `net9.0`), and .NET SDK 10.x scaffold toolchain.
- `Path.GetFullPath(path, basePath)` provides canonical resolution but is not itself a containment or symlink guarantee; combine it with explicit root-boundary and reparse/link validation.
- `FileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)` may be used for existing links; still validate each component and handle unsupported/broken/cyclic links by rejecting the request.
- No EditorPlugin work is required in this preparation story.

### Testing Requirements

- Run focused StateMachine/Physics tests first, then focused scaffold tests, then the complete repository suite.
- Because EventBus is a singleton, every added test must unsubscribe/shutdown in `finally`/`Dispose`; PREP-2.2 owns broader isolation infrastructure.
- Path tests must be platform-aware without skipping the portable semantic cases. Windows-only syntaxes may be conditional, but traversal, canonical escape, sibling-prefix, and valid-contained cases must run everywhere.
- Counter tests must prove independent P1/P2 sequences, checked reservation before mutation, and `ulong.MaxValue` exhaustion through an internal test seam.
- Build evidence is not a substitute for Godot runtime proof. Record both the generated-project build and the documented smoke scenario.

Minimum command evidence (adapt filter syntax only if the runner requires it):

```powershell
dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --filter "FullyQualifiedName~StateMachine|FullyQualifiedName~PhysicsEngine|FullyQualifiedName~CharacterKnockback|FullyQualifiedName~KnockbackReplay|FullyQualifiedName~EventBus"
dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --filter "FullyQualifiedName~FtgCli"
dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj
dotnet run --project Scaffold/ftg-cli -- new MyFighter --output <isolated-output-root>
dotnet build <isolated-output-root>/MyFighter/MyFighter.csproj --no-restore
<Godot-4.5.1-mono-console> --headless --path <isolated-output-root>/MyFighter res://scaffold_smoke.tscn
```

The Godot run must exit zero and contain `[ScaffoldSmoke] PASS`.

### Git Intelligence

- Recent implementation sequence: StateMachine (`6e221fa`), character/scaffold flow (`2feedf1`), collision/scaffold verification (`2ddaac9`), then knockback/hot reload (`ad1c202`). The defects span those established boundaries; amend them in place rather than creating parallel modules.
- Existing tests already cover a loose “latest generation” case, lifecycle generation resets, legacy completion rejection, scaffold preflight, file-set parity, generated build, and smoke documentation. Convert these into exact-contract regression evidence and retain the broader guarantees.

### Latest Technical Notes

- Official .NET 10 API references: `Path.GetFullPath` and `FileSystemInfo.ResolveLinkTarget`. Use them as primitives, not as a complete sandbox implementation.
- Godot 4.5.1 remains the repository pin; this story changes pure C#/CLI boundaries and should not upgrade Godot or .NET packages.

### Evidence Index (complete during implementation)

| AC | Required evidence | Durable location / result |
|---|---|---|
| P2.1-AC01 | StateMachine + queued EventBus G1/G2/epoch regression | `evidence/v2-prep-2-1/runtime-focused.log` |
| P2.1-AC02 | Exhaustive invalid `int` player-ID partition | `evidence/v2-prep-2-1/runtime-focused.log` |
| P2.1-AC03 | Phase/order/duplicate matrix, controller, replay v3 | `evidence/v2-prep-2-1/runtime-focused.log`; `replay-contract.log` |
| P2.1-AC04 | Access-spy path/link tests and filesystem snapshots | `evidence/v2-prep-2-1/scaffold-boundary.log`; `scaffold-snapshots.md` |
| P2.1-AC05 | Real-manifest parity, generated build, Godot smoke | `evidence/v2-prep-2-1/generated-build.log`; `godot-smoke.log` |
| P2.1-AC06 | Focused/full suite and environment manifest | `evidence/v2-prep-2-1/full-suite.log`; `environment.md` |
| P2.1-AC07 | Pre/post tracking diff proving all non-owned rows unchanged | `evidence/v2-prep-2-1/tracking-diff.patch` |

### References

- [Source: `_bmad-output/planning-artifacts/epics/v2-epic-2-readiness-gate-non-epic.md` — PREP-2.1]
- [Source: `_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md` — AD-9 through AD-12, AD-18, Adoption Gate, Stack]
- [Source: `_bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md` — Critical Preparation Gates and Action Items 1-3]
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-31.md` — Sections 4.1, 5]
- [Source: `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01.md` — Recommended Next Steps]
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md`]
- [Source: `Scripts/Framework/Engine/StateMachine/StateMachine.cs`]
- [Source: `Scripts/Framework/Engine/Physics/PhysicsEngine.cs`]
- [Source: `Scripts/Framework/Core/Events/KnockbackAppliedEvent.cs`]
- [Source: `Scaffold/ftg-cli/ProjectScaffolder.cs`]
- [Source: `Tests/FTG_Framework.Tests/Engine/StateMachine/StateMachineTests.cs`]
- [Source: `Tests/FTG_Framework.Tests/Scaffold/FtgCliTests.cs`]
- [Official .NET `Path.GetFullPath`](https://learn.microsoft.com/en-us/dotnet/api/system.io.path.getfullpath?view=net-10.0)
- [Official .NET `FileSystemInfo.ResolveLinkTarget`](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.resolvelinktarget?view=net-10.0)
- [Godot 4.5.1 maintenance release](https://godotengine.org/article/maintenance-release-godot-4-5-1/)

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `evidence/v2-prep-2-1/runtime-focused.log`
- `evidence/v2-prep-2-1/replay-contract.log`
- `evidence/v2-prep-2-1/scaffold-boundary.log`
- `evidence/v2-prep-2-1/generated-build.log`
- `evidence/v2-prep-2-1/godot-smoke.log`
- `evidence/v2-prep-2-1/full-suite.log`
- `evidence/v2-prep-2-1/tracking-diff.patch`

### Completion Notes List

- Added lifecycle-epoch EventBus envelopes and exact generation-owned knockback phase handling across Physics, StateMachine, and Character boundaries.
- Added replay v3 phase validation while preserving v1/v2 container compatibility and stable event registration policy.
- Hardened scaffold planning and access boundaries with canonical containment, reparse/link rejection, immutable plans, and pre-access hooks.
- Added runtime, replay, scaffold, generated-project build, and Godot 4.5.1 smoke evidence; full suite passes 786 tests.
- Addressed all 19 code-review patches; post-review full suite passes 803 tests and generated-project Godot smoke remains green.

### File List

- `Scaffold/ftg-cli/ProjectScaffolder.cs`
- `Scaffold/verification/ScaffoldSmokeTest.cs`
- `Scripts/Framework/Characters/CharacterController.cs`
- `Scripts/Framework/Core/EventBus.cs`
- `Scripts/Framework/Core/Events/KnockbackAppliedEvent.cs`
- `Scripts/Framework/Core/Replay/ReplayPlayer.cs`
- `Scripts/Framework/Core/Replay/ReplayVersionValidator.cs`
- `Scripts/Framework/Engine/Physics/ForceCalculator.cs`
- `Scripts/Framework/Engine/Physics/PhysicsEngine.cs`
- `Scripts/Framework/Engine/Physics/TrajectoryState.cs`
- `Scripts/Framework/Engine/StateMachine/StateMachine.cs`
- `Tests/FTG_Framework.Tests/Characters/CharacterKnockbackEventOrderTests.cs`
- `Tests/FTG_Framework.Tests/Core/EventBusEpochTests.cs`
- `Tests/FTG_Framework.Tests/Engine/Physics/ForceCalculatorTests.cs`
- `Tests/FTG_Framework.Tests/Engine/Physics/PhysicsEngineTests.cs`
- `Tests/FTG_Framework.Tests/Engine/StateMachine/StateMachineTests.cs`
- `Tests/FTG_Framework.Tests/Replay/CollisionReplayCompatibilityTests.cs`
- `Tests/FTG_Framework.Tests/Replay/KnockbackReplayTests.cs`
- `Tests/FTG_Framework.Tests/Scaffold/FtgCliTests.cs`
- `_bmad-output/implementation-artifacts/evidence/v2-prep-2-1/*`
- `_bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/v2-prep-2-1-runtime-and-scaffold-boundary-hardening.md`

## Change Log

- 2026-08-01: Story created and set to `ready-for-dev`.
- 2026-08-01: Implemented runtime/replay/scaffold hardening, captured verification evidence, and set story to `review`.
- 2026-08-01: Applied all adversarial code-review fixes and set story to `done` after 803-test regression and Godot smoke verification.
