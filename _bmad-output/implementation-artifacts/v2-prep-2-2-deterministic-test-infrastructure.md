---
story_key: v2-prep-2-2-deterministic-test-infrastructure
story_id: PREP-2.2
date_created: 2026-08-01
baseline_commit: e139032416e1ef674702fc3ccf00bae630f765af
owner: Dana (QA Engineer)
---

# PREP-2.2: Deterministic Test Infrastructure

Status: done

## Story

As a QA engineer preparing reliable Epic 2 verification,
I want FileWatcher synchronization and EventBus test isolation to be deterministic,
so that failures represent product defects rather than timing or shared-state leakage.

## Acceptance Criteria

1. **P2.2-AC01 — Condition-based watcher completion.**  
   **Given** a FileWatcher test triggers a file change  
   **When** it waits for observation and frame-boundary processing  
   **Then** it uses an explicit condition, signal, or bounded polling abstraction tied to the expected event/content version  
   **And** it contains no fixed delay used as proof that processing completed.

2. **P2.2-AC02 — Actionable bounded timeout.**  
   **Given** an expected watcher condition never occurs  
   **When** the configured test timeout expires  
   **Then** the test fails with the expected canonical path, content/version identity, observed notifications, and EventBus frame state  
   **And** the timeout is bounded so the suite cannot hang indefinitely.

3. **P2.2-AC03 — OS-notification independence.**  
   **Given** a watcher emits duplicate or reordered OS notifications  
   **When** the test fixture processes them  
   **Then** assertions target the committed canonical-path/content-version outcome rather than the raw notification count  
   **And** the test remains deterministic across supported operating-system notification patterns.

4. **P2.2-AC04 — Clean EventBus baseline.**  
   **Given** an EventBus test starts  
   **When** its fixture initializes  
   **Then** subscribers, current/next queues, pending reloads, frame counters, lifecycle epoch state, pause/step flags, replay hooks, and test-owned service registrations begin from a documented clean baseline  
   **And** no prior test can influence the result.

5. **P2.2-AC05 — Exception-safe idempotent teardown.**  
   **Given** an EventBus test completes successfully or throws during setup, execution, or assertion  
   **When** fixture teardown runs  
   **Then** all test-owned subscriptions and pending work are removed and singleton state is restored through the supported test boundary  
   **And** teardown is idempotent.

6. **P2.2-AC06 — Scoped serialization.**  
   **Given** tests that mutate EventBus singleton state could run concurrently  
   **When** the test runner schedules them  
   **Then** they are placed in one explicitly non-parallel xUnit collection (the current singleton cannot be independently instantiated)  
   **And** unrelated pure tests remain eligible for parallel execution.

7. **P2.2-AC07 — Repeat/order stress evidence.**  
   **Given** the targeted FileWatcher and EventBus suites are executed repeatedly in seeded randomized orders that can be replayed in the same test process  
   **When** the documented stress matrix completes  
   **Then** every run produces the same pass/fail result with no leaked subscribers, stale envelopes, or timing-only flakes  
   **And** evidence records run count, order/seed, platform, SDK, and duration.

8. **P2.2-AC08 — Closure evidence.**  
   **Given** PREP-2.2 is ready for closure  
   **When** repository tests and evidence are reviewed  
   **Then** a source scan finds no prohibited fixed-delay completion checks in scoped FileWatcher tests, isolation tests demonstrate clean before/after state, and the full regression suite passes  
   **And** evidence is indexed under PREP-2.2 and linked from the corresponding `v2-1` retrospective action.

9. **P2.2-AC09 — Tracking isolation.**  
   **Given** all PREP-2.2 evidence is accepted  
   **When** sprint tracking is updated  
   **Then** `v2-prep-2-2-deterministic-test-infrastructure` and only its associated retrospective action may move to `done`  
   **And** `v2-epic-2` remains `backlog` while any other start-gate condition is unsatisfied.

## Tasks / Subtasks

- [x] Add one supported EventBus test-state boundary (AC: 4, 5)
  - [x] Extend `EventBus` with an `internal` test-only reset/scope API accessible through the existing `InternalsVisibleTo` grant; do not expose a new public runtime API and do not use reflection to mutate private fields.
  - [x] Define the canonical baseline explicitly: no subscriber dictionary entries; empty `_currentQueue`, `_nextQueue`, and `_pendingReloads`; `_dispatching == false`; `_frameNumber` and `_dispatchFrame` reset; no dispatch context; `Paused`, `StepRequested`, and `SuppressFrameAdvanced` false; `Recorder` null. Assign a fresh monotonically advancing test isolation generation/epoch and expose it in diagnostics; never recycle a fixed epoch such as `1`, which could make late work appear current.
  - [x] Constrain begin/reset/end operations to the owning test thread while EventBus is not dispatching. Do not claim `_epochSync` makes `ProcessFrame()` and reset mutually exclusive: stop all producers first, then enter the synchronized reset boundary and drain all queues.
  - [x] Make cleanup safe to call more than once and after partial fixture setup. Add an immutable diagnostic snapshot containing subscriber type/count, current/next/reload queue counts, frame and dispatch frame, lifecycle/test generation, dispatch state/context, pause/step/suppression flags, and recorder presence; never expose mutable collections.
  - [x] Add focused tests proving dirty state is cleared, a second cleanup is harmless, teardown runs after a thrown test action, and baseline assertions identify every residual-state category.

- [x] Replace ad-hoc EventBus cleanup with a shared fixture and collection (AC: 4-6)
  - [x] Use an xUnit collection definition only to serialize singleton-mutating tests. Establish isolation per test through a composed scope acquired in each test-class constructor and disposed last from that class's `Dispose`; xUnit `ICollectionFixture` is collection-lifetime and must not be used as if it ran before/after every test.
  - [x] Standardize existing `IDisposable` test classes so domain resources are disposed first and the per-test EventBus scope is disposed last. The scope must verify leaks before restoring the baseline and must still restore in `finally` after setup, execution, assertion, or partial-disposal failures.
  - [x] Maintain an explicit inventory of every direct and indirect EventBus-owning test class, including constructors that subscribe through GameLoop, SceneManager, FileWatcher, hot-reload services, StateMachine/engine modules, replay components, and EventBus-backed ViewModels. Put them in the serialized collection; a text scan for `EventBus.Instance` alone is not sufficient.
  - [x] Migrate `EventBusTestHelper` to the supported boundary. Preserve useful typed collection helpers, but remove the assumption that one `ProcessFrame()` drain can erase subscribers, recorder configuration, lifecycle state, flags, or events published during dispatch.
  - [x] Remove class-local reset fragments once the shared boundary covers them. Keep domain-specific disposal (services, engines, watchers) where those objects own subscriptions or background resources.
  - [x] Change `xunit.runner.json` from assembly-wide serialization to collection-level parallelism. Mark the EventBus collection with `DisableParallelization = true`; verify configuration and collection annotations statically, and use an explicit maintained inventory plus runtime isolation tests to prevent indirect users from escaping.

- [x] Build deterministic FileWatcher observation utilities (AC: 1-3)
  - [x] Replace every completion-proving `Thread.Sleep` in `FileWatcherTests` with one reusable bounded wait that repeatedly processes the EventBus frame boundary and evaluates an outcome predicate.
  - [x] Subscribe before the filesystem mutation to avoid the observe-after-act race and capture the observation sequence before acting, so only later observations qualify. Normalize paths with `Path.GetFullPath`; compare with `OrdinalIgnoreCase` on Windows and `Ordinal` on Unix-family targets. Do not use `EndsWith` or plain string equality as a canonical-path oracle.
  - [x] Define the observable contract honestly: success requires a matching canonical-path notification observed after the test mutation plus an independently verified final filesystem state/version. `DataReloadedEvent` carries only a path, so do not claim that the event itself identifies content or a version.
  - [x] For create/modify, subscribe before mutation, record a mutation-specific token/version in the written content, require a post-mutation matching path observation, and independently verify final content. For delete, assert non-existence plus a post-mutation deleted-path observation; for rename, assert the final old/new filesystem state plus both canonical path observations. Never require an exact raw callback count or order.
  - [x] For rapid writes, use distinct content tokens and wait for the final committed state plus a post-mutation canonical-path observation. Unit-test the observation/aggregation helper with injected duplicate and reordered inputs; retain real FileSystemWatcher integration coverage without relying on the OS to produce every pattern.
  - [x] Implement the negative/disposal case by racing an unexpected-event `TaskCompletionSource` against a cancellable monotonic deadline. Deadline expiry is success; observing the matching event is immediate failure. Record the quiet-window duration and observations; do not use an unconditional delay.
  - [x] On timeout, report expected canonical path, expected mutation token/final state, all observed canonical paths/notifications, the complete immutable EventBus diagnostic snapshot, elapsed time, and platform. Add a forced-timeout test that asserts these diagnostic fields and values.
  - [x] Make FileWatcher shutdown a producer-quiescence boundary: `Dispose` (or a narrowly scoped test seam) must prevent/await in-flight callbacks before EventBus cleanup. Teardown order is watcher/domain resource disposal, quiescence confirmation, leak snapshot, then EventBus restoration; only afterward delete the temporary directory. Surface cleanup failures in diagnostics.

- [x] Prove isolation and runner behavior (AC: 4-7)
  - [x] Within one test, dirty and dispose an explicit scope, create a second scope, and assert its baseline; cover every EventBus state category, exception-path teardown, subscription leakage, and idempotence without depending on test execution order.
  - [x] Verify runner parallelism and the EventBus collection's non-parallel marker statically. If runtime parallel evidence is retained, place it in an opt-in harness with a barrier and bounded timeout; a normal regression test must not require nondeterministic scheduler overlap or wall-clock speedup.
  - [x] Provide a checked-in xUnit 2.9.3-compatible same-process seeded randomization mechanism. It must randomize singleton-mutating test cases across their classes, accept a recorded seed (for example `FTG_TEST_SEED`), log the resolved order, and provide the exact replay command. A collection orderer that only reorders collections, or separate-process invocation of one test at a time, does not prove shared-state order independence.
  - [x] Define the stress matrix unambiguously: run 20 total targeted suite processes, cover each of 5 fixed recorded seeds at least once, and run the complete FileWatcher/EventBus selection in every process; then run one full regression suite.
  - [x] Bound both individual hangs (`dotnet test --blame-hang --blame-hang-timeout 60s`) and the outer stress process. The harness must terminate an overrun and record its seed, resolved order, timeout, stdout/stderr, duration, and exit code.

- [x] Record evidence and update only earned tracking state (AC: 7-9)
  - [x] Create `_bmad-output/implementation-artifacts/evidence/v2-prep-2-2/` with an index, source-scan output, targeted stress log, isolation/parallelism log, full-suite log, and environment metadata.
  - [x] Define the fixed-delay scan precisely: scan `FileWatcherTests.cs` and every watcher observation helper for `Thread.Sleep`, unconditional `Task.Delay`, aliases, and equivalent completion waits; exempt only the named deadline-bound wait primitive. Store the exact command/patterns, exemptions, output, and exit code.
  - [x] Record repository commit, OS/platform, .NET SDK, xUnit/adapter versions, commands, run count, seeds/orders, durations, outer timeout status, and exit codes. Logs must be captured from actual executions, not reconstructed summaries; index each artifact with its SHA-256 and require the evidence commit to match the reviewed implementation.
  - [x] Link accepted evidence from this story and from action 4 in `epic-v2-1-retro-2026-07-31.md`.
  - [x] Only after all ACs pass, mark the story and the single combined sprint action `Remove fixed-delay FileWatcher testing and isolate EventBus singleton state between tests` done. Preserve `v2-epic-1: done`, all unrelated actions, and `v2-epic-2: backlog`.

### Review Findings

- [x] [Review][Patch] Enforce strict physical-thread ownership for EventBus test scopes; validate the recorded thread ID and keep scope teardown on its creating thread, including watcher tests. [Scripts/Framework/Core/EventBus.cs:410]
- [x] [Review][Patch] Make FileWatcher callback admission and disposal atomic; concurrent callbacks/disposers can signal quiescence early, access a disposed wait handle, or leave timeout cleanup permanently half-disposed. [Scripts/Framework/Core/FileWatcher.cs:66]
- [x] [Review][Patch] Fail or explicitly classify dirty EventBus residual state before reset instead of silently erasing leaked subscribers, queues, flags, recorder, and pending reloads. [Tests/FTG_Framework.Tests/EventBusTestScope.cs:19]
- [x] [Review][Patch] Stress the complete maintained singleton-mutating inventory rather than filtering only class names containing `FileWatcher` or `EventBus`. [Tests/FTG_Framework.Tests/run-prep22-stress.ps1:8]
- [x] [Review][Patch] Make PREP-2.2 evidence reviewable by adding the required `.gitignore` exceptions and regenerate/re-hash evidence against the reviewed implementation commit. [.gitignore:33]
- [x] [Review][Patch] Allow EventBus scope teardown recovery when `EndTestScope` throws; the wrapper currently marks itself disposed while EventBus ownership remains latched. [Tests/FTG_Framework.Tests/EventBusTestScope.cs:24]
- [x] [Review][Patch] Reopen the retrospective action until all review findings and tracked closure evidence are accepted. [_bmad-output/implementation-artifacts/sprint-status.yaml:150]
- [x] [Review][Patch] Preserve and restore the caller's `FTG_TEST_SEED` environment value in the stress harness. [Tests/FTG_Framework.Tests/run-prep22-stress.ps1:18]
- [x] [Review][Patch] Replace random-key sorting with deterministic Fisher-Yates shuffling so collisions cannot reintroduce discovery-order dependence. [Tests/FTG_Framework.Tests/SeededTestFramework.cs:34]

## Dev Notes

### Developer Context and Guardrails

- This is non-epic readiness work with no FR coverage. It closes one V2 Epic 1 retrospective hazard and must not start Epic 2 or implement Epic 2 product behavior.
- `EventBus.Instance` is process-global, privately constructed, and single-threaded for ordinary publication/dispatch. `FileWatcher` is the sanctioned background-thread producer through `_pendingReloads`. Preserve this ownership boundary.
- Preserve all EventBus runtime semantics: eight dispatch phases, per-type LIFO dispatch, subscriber snapshot iteration, next-frame queuing for subscriber publications, lifecycle-epoch rejection, recorder behavior, pause/step state, and FileWatcher phase-0 drain. This story changes test control and diagnostics, not event semantics.
- A frame drain is not a reset. The current helper cannot remove leaked subscribers, queued next-frame work, pending reloads racing from watcher threads, recorder state, flags, frame state, or lifecycle state. Use one authoritative boundary.
- Reset is not producer shutdown. A test must dispose and quiesce all background/domain producers before leak verification and restoration; clearing their subscriptions or queues first would hide a lifecycle defect.
- Do not solve isolation by making the production singleton constructor public, replacing it with service-location indirection, globally disabling the whole test assembly, or scattering new manual cleanup blocks across tests.
- Do not test `FileSystemWatcher` callback cardinality or order. The platform may emit duplicate, reordered, or coalesced notifications; canonical committed filesystem state plus the corresponding canonical path is the stable contract.
- No new runtime NuGet dependency is required. Stay on the checked-in `net8.0`, xUnit `2.9.3`, `Microsoft.NET.Test.Sdk` `17.12.0`, and VS runner `3.0.1` unless a separate dependency-change decision is approved.

### Existing Files to Update

- `Scripts/Framework/Core/EventBus.cs`
  - **Current state:** owns subscribers; current/next queues; concurrent reload queue; frame and lifecycle state; pause/step flags; recorder and replay suppression. Only `SetLifecycleEpochForTesting` is a dedicated test hook.
  - **Change:** add one internal, synchronized, comprehensive test reset/diagnostic boundary.
  - **Preserve:** all public API behavior and dispatch/lifecycle semantics listed above.
- `tests/FTG_Framework.Tests/Core/FileWatcherTests.cs`
  - **Current state:** five behavior tests use `Thread.Sleep(50/200)` as synchronization and local frame-drain helpers; rapid-change assertions are notification-oriented; cleanup swallows directory-delete failures.
  - **Change:** predicate-based bounded observation, canonical outcome assertions, rich timeout diagnostics, shared EventBus isolation.
  - **Preserve:** create, modify, rapid change, delete, rename, dispose, and constructor-validation coverage.
- `tests/FTG_Framework.Tests/EventBusTestHelper.cs`
  - **Current state:** calls `ProcessFrame()` before scenarios and unsubscribes only handlers it creates.
  - **Change:** delegate baseline/reset responsibility to the supported fixture while retaining concise typed capture helpers where useful.
- `tests/FTG_Framework.Tests/xunit.runner.json`
  - **Current state:** `parallelizeTestCollections: false` and `maxParallelThreads: 1` serialize the entire assembly.
  - **Change:** enable normal collection parallelism; singleton-mutating tests are serialized by their explicit collection.
- Every test class discovered by an `EventBus.Instance`/indirect-owner inventory may require an explicit collection annotation or base fixture. Treat that inventory as UPDATE scope, not as permission to change domain assertions.
- `Scripts/Framework/Core/FileWatcher.cs` may require a narrowly bounded shutdown/quiescence change if current `Dispose` cannot prove that callbacks have finished before EventBus cleanup. Preserve its thin detection-only role and all runtime notification semantics.

### Expected New Files (names may follow the repository's final convention)

- `tests/FTG_Framework.Tests/EventBusTestCollection.cs` — serialization-only collection definition and non-parallel marker.
- `tests/FTG_Framework.Tests/EventBusTestScope.cs` — composed per-test clean-baseline/leak-verification/restoration scope; this is not an `ICollectionFixture`.
- `tests/FTG_Framework.Tests/Core/EventBusTestIsolationTests.cs` — reset, leak, exception, idempotence, and diagnostics tests.
- A reusable watcher wait/diagnostic helper colocated with `FileWatcherTests` or under a test-infrastructure folder; do not add it to runtime code unless production behavior actually needs it.
- PREP-2.2 evidence files under `_bmad-output/implementation-artifacts/evidence/v2-prep-2-2/`.

### Testing Requirements

- Targeted tests must cover every AC, including negative timeout diagnostics and teardown after exceptions—not only happy-path watcher delivery.
- The full suite command remains `dotnet test tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj`.
- Fixed-delay scan scope includes `FileWatcherTests.cs` and every new watcher observation helper. `Thread.Sleep`, unconditional `Task.Delay`, aliases, or equivalent waits cannot be used as proof of completion. Backoff is allowed only inside the single named deadline-bound primitive.
- Stress evidence must be repeatable. A failure report must identify the exact iteration and seed/order so the same run can be replayed.
- Keep evidence generation separate from ordinary tests; tests must not write into tracked evidence folders.

### Definition of Done Matrix

| Area | Implementation complete when | Evidence complete when |
|---|---|---|
| Watcher outcomes | Post-mutation canonical-path observation is paired with independently verified final filesystem state; duplicates/reordering and negative deadline paths are covered | Scoped delay scan is clean; helper unit tests and real watcher integration tests pass with forced-timeout diagnostics |
| EventBus baseline | Per-test scope verifies leaks and restores every documented state field only after all producers are quiescent | Dirty-scope/second-scope, exception, idempotence, queue, subscriber, reload, flags, recorder, frame, epoch/generation tests pass |
| Stress/order | Same-process seeded randomization logs and replays cross-class singleton test order; pure collections remain enabled while EventBus collection is disabled from parallel execution | 20 complete targeted processes cover 5 fixed seeds, outer/per-test bounds hold, then the full suite passes; indexed logs include hashes and environment |

### Latest Technical Notes

- Microsoft documents that common filesystem operations may raise multiple `FileSystemWatcher` events and that moves can surface as several lower-level notifications. Outcome-based assertions are therefore mandatory, not merely a flake workaround. [Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher]
- xUnit test collections are the unit of parallelization: tests in one collection do not run in parallel with sibling tests, while other collections may. Use that scope instead of assembly-wide serialization. [Source: https://xunit.net/docs/shared-context]
- An xUnit collection fixture lives for the complete collection, not one test. Use it only for shared collection state; PREP-2.2 requires a separate per-test lifetime boundary. [Source: https://xunit.net/docs/shared-context]
- `dotnet test --blame-hang --blame-hang-timeout <timespan>` provides a bounded per-test hang diagnostic path and is suitable for stress evidence. [Source: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test]

### Previous Story and Git Intelligence

- PREP-2.1 established the evidence convention used here: a dedicated evidence directory, environment metadata, focused/full-suite logs, explicit tracking isolation, and links from the retrospective action. Reuse that structure.
- Baseline commit `e1390324` completed PREP-2.1 and expanded EventBus lifecycle/epoch behavior. Isolation must cover the newly added lifecycle epoch, dispatch epoch, pending reload, recorder, and queue state rather than reverting or bypassing it.
- Recent commits consistently pair Core changes with focused tests and retain `v2-epic-2: backlog`; follow the same review boundary.

### Project Structure Notes

- Runtime code remains under `Scripts/Framework/Core/`; test-only fixtures stay under `tests/FTG_Framework.Tests/`.
- Production concrete classes remain `internal`; tests already receive internals through `FTG_Framework.csproj`.
- Use `#nullable enable`, C# naming conventions, and English `[EventBus]` / `[FileWatcher]` diagnostics.
- No UX, Godot scene, data schema, scaffold, or gameplay changes are in scope.

### References

- [Source: _bmad-output/planning-artifacts/epics/v2-epic-2-readiness-gate-non-epic.md#PREP-22-Deterministic-Test-Infrastructure]
- [Source: _bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md#Preparation-for-V2-Epic-2]
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-31.md#Recommended-Approach]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#Stack]
- [Source: _bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md#Adoption-Gate]
- [Source: Scripts/Framework/Core/EventBus.cs]
- [Source: Scripts/Framework/Core/FileWatcher.cs]
- [Source: tests/FTG_Framework.Tests/Core/FileWatcherTests.cs]
- [Source: tests/FTG_Framework.Tests/EventBusTestHelper.cs]
- [Source: tests/FTG_Framework.Tests/xunit.runner.json]
- [Source: docs/development-guide.md#Test]

## Evidence Index (Complete During Implementation)

| AC | Required evidence | Status / link |
|---|---|---|
| AC01-AC03 | Fixed-delay scan; watcher outcome, duplicate/reorder, timeout-diagnostic tests | Pending |
| AC04-AC06 | Baseline/reset, exception teardown, idempotence, collection serialization and pure parallel eligibility | Pending |
| AC07 | 20 complete targeted processes covering 5 fixed recorded seeds, same-process resolved orders, replay commands, platform and duration | Pending |
| AC08 | Source scan, targeted suite, full regression suite, environment metadata | Pending |
| AC09 | Story/retrospective/sprint tracking diff preserving Epic states | Pending |

## Dev Agent Record

### Agent Model Used

OpenAI GPT-5

### Debug Log References

- RED: `EventBusTestIsolationTests` initially failed to compile because the supported boundary and fixture did not exist.
- GREEN: focused EventBus isolation tests passed (4/4), then watcher/isolation/configuration tests passed (16/16).
- Parallel regression exposed `ReplayRecorderTests.Save_PublishesFrameworkLog` sharing `FrameworkLog`; the non-pure class was added to the serialized collection.
- Removing scenario capture drains exposed five stale-within-test assertions; `EventBusTestHelper` now documents and retains capture-boundary drains while per-test scopes own isolation.
- Final stress: 20/20 targeted processes passed across seeds 2202-2206, with no outer timeout.
- Final regression: 811/811 passed with `--blame-hang --blame-hang-timeout 60s`.

### Implementation Plan

- Add an internal EventBus scope/reset/diagnostic boundary with a monotonic isolation generation.
- Compose that scope into every maintained singleton-owning test class and serialize only the shared-state collection.
- Replace FileWatcher sleeps with canonical-path, committed-state observation and bounded diagnostics.
- Add same-process seeded ordering, bounded stress execution, and immutable evidence artifacts.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- Added a complete immutable EventBus diagnostic and comprehensive idempotent test reset boundary without changing the public runtime API.
- Migrated the maintained direct/indirect EventBus inventory to per-test scopes disposed after domain cleanup; unrelated test collections remain parallel.
- Replaced fixed-delay FileWatcher assertions with bounded outcome waits, duplicate/reorder coverage, forced-timeout diagnostics, and callback quiescence on disposal.
- Added seeded cross-class test-case ordering and a replayable 20-process stress harness with per-test and outer timeout protection.
- Evidence is indexed at `_bmad-output/implementation-artifacts/evidence/v2-prep-2-2/index.md`; SHA-256 inventory is in `sha256.txt`.

### File List

- Scripts/Framework/Core/EventBus.cs
- Scripts/Framework/Core/FileWatcher.cs
- Tests/FTG_Framework.Tests/EventBusTestCollection.cs
- Tests/FTG_Framework.Tests/EventBusTestScope.cs
- Tests/FTG_Framework.Tests/EventBusTestHelper.cs
- Tests/FTG_Framework.Tests/SeededTestFramework.cs
- Tests/FTG_Framework.Tests/run-prep22-stress.ps1
- Tests/FTG_Framework.Tests/xunit.runner.json
- Tests/FTG_Framework.Tests/Core/EventBusRunnerConfigurationTests.cs
- Tests/FTG_Framework.Tests/Core/EventBusTestIsolationTests.cs
- Tests/FTG_Framework.Tests/Core/FileWatcherObservation.cs
- Tests/FTG_Framework.Tests/Core/FileWatcherTests.cs
- Tests/FTG_Framework.Tests/Core/EventBusDebugServiceTests.cs
- Tests/FTG_Framework.Tests/Core/EventBusEpochTests.cs
- Tests/FTG_Framework.Tests/Core/GameLoopPauseTests.cs
- Tests/FTG_Framework.Tests/Core/SceneManagerTests.cs
- Tests/FTG_Framework.Tests/Data/PhysicsProfileHotReloadServiceTests.cs
- Tests/FTG_Framework.Tests/Engine/Combo/CancelWindowConsumptionIntegrationTests.cs
- Tests/FTG_Framework.Tests/Engine/Combo/ChainValidatorTests.cs
- Tests/FTG_Framework.Tests/Engine/Combo/ComboExecutorCancelTests.cs
- Tests/FTG_Framework.Tests/Engine/Combo/ComboStateTrackerTests.cs
- Tests/FTG_Framework.Tests/Engine/FrameData/CancelWindowTrackerTests.cs
- Tests/FTG_Framework.Tests/Engine/FrameData/EvaluatedMoveFrameTests.cs
- Tests/FTG_Framework.Tests/Engine/FrameData/FrameDataEngineCancelTests.cs
- Tests/FTG_Framework.Tests/Engine/FrameData/FrameDataEngineSnapshotTests.cs
- Tests/FTG_Framework.Tests/Engine/FrameData/FrameDataEngineTests.cs
- Tests/FTG_Framework.Tests/Engine/FrameData/FrameDataStateLifecycleTests.cs
- Tests/FTG_Framework.Tests/Engine/Physics/PhysicsEngineTests.cs
- Tests/FTG_Framework.Tests/Engine/StateMachine/StateMachineTests.cs
- Tests/FTG_Framework.Tests/Input/DefaultInputPipelineTests.cs
- Tests/FTG_Framework.Tests/Input/InputBufferTests.cs
- Tests/FTG_Framework.Tests/Input/InputHistoryRewindTests.cs
- Tests/FTG_Framework.Tests/Replay/EventBusRecordingTests.cs
- Tests/FTG_Framework.Tests/Replay/FrameDataEngineReplayTests.cs
- Tests/FTG_Framework.Tests/Replay/KnockbackReplayTests.cs
- Tests/FTG_Framework.Tests/Replay/ReplayIntegrationTests.cs
- Tests/FTG_Framework.Tests/Replay/ReplayPlayerTests.cs
- Tests/FTG_Framework.Tests/Replay/ReplayRecorderTests.cs
- Tests/FTG_Framework.Tests/Replay/Spike/ReferenceReplayTests.cs
- Tests/FTG_Framework.Tests/Replay/Spike/SerializationValidationTests.cs
- Tests/FTG_Framework.Tests/UI/CharacterSelectViewModelTests.cs
- Tests/FTG_Framework.Tests/UI/EventBusDebugViewModelTests.cs
- Tests/FTG_Framework.Tests/UI/Training/ViewModels/PlaybackControlsViewModelTests.cs
- _bmad-output/implementation-artifacts/evidence/v2-prep-2-2/ (index, manifest, metadata, scans, stress logs, and regression logs)
- _bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/v2-prep-2-2-deterministic-test-infrastructure.md

## Change Log

- 2026-08-01: Implemented deterministic EventBus isolation, FileWatcher observation, scoped xUnit parallelism, seeded stress ordering, and PREP-2.2 closure evidence; moved story to review.
