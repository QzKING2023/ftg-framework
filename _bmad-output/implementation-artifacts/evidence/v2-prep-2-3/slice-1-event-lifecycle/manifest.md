# PREP-2.3 Slice 1 Evidence Manifest

- Scope: AD-12 phase-3 ordering and AD-18 immutable state-event boundaries
- Baseline: `cddba608b099b00e7a0f25cc5d0e635286ff00a4`
- State: accepted
- Reviewer: Codex implementation validator
- Review date: 2026-08-01

## Results

| Command | Result |
|---|---|
| `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore --filter "FullyQualifiedName~FrameDataEngineTests\|FullyQualifiedName~StateMachineTests\|FullyQualifiedName~EventBusEpochTests"` | 86 passed, 0 failed, 0 skipped |
| `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore` | 815 passed, 0 failed, 0 skipped |

## Contract Checks

- `MoveStartedEvent` precedes the first `MoveFrameChangedEvent` in phase 3.
- Re-entrant publication remains next-frame queued.
- `StateStackSnapshot` copies its source and exposes no mutable container.
- `StateChangedEvent` carries old/new top values plus the new snapshot only when the top changes.
- `StateStackChangedEvent` carries old/new snapshots for every structural mutation.
- Existing lifecycle epoch and knockback tuple regression suites remain green.

Hashes are recorded in the umbrella evidence index after all slices are complete.
