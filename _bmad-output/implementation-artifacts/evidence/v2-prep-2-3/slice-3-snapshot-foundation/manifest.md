# PREP-2.3 Slice 3 Evidence Manifest

- Scope: AD-20 versioned failure-atomic snapshot foundation
- Entry baseline: `cddba608b099b00e7a0f25cc5d0e635286ff00a4`
- State: accepted
- Reviewer: Codex implementation validator
- Review date: 2026-08-01

## Accepted contracts

- Stable required discriminators: `frame_data`, `input`, `physics_motion`, `recording`, `state_machine`; ordinal registration order.
- Canonical deterministic UTF-8 value-only container with versioned component codecs and duplicate/invalid rejection.
- Quiescent capture, quarantined publication, reserved epoch cancellation, and immutable prepared replacements.
- Normal restore emits exactly one observe-only `StateRestored`; replay bootstrap/handoff emits none; continuation is captured frame + 1.
- Queue diagnostics compare payload value, frame, epoch, and monotonic sequence for current/next/pending/quarantined queues.
- Graph-validator hook owns shared identity and generation high-water validation before Commit.

## Results

| Command | Result |
|---|---|
| `dotnet test ... --filter FullyQualifiedName~StateSnapshot --no-restore` | 13 passed, 0 failed, 0 skipped |
| PREP-2.2 deterministic stress harness, 20 iterations/seeds 2202-2206 | 20 passed, 0 timeout |
| `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore` | 844 passed, 0 failed, 0 skipped |

## Stable fault catalog

| ID | Location | Equivalence comparator |
|---|---|---|
| `ReserveEpoch` | before epoch reservation | active/reserved epoch; all queue payloads and metadata |
| `DecodeComponent` | before component lookup/version decode | component graph; epoch; all queues |
| `PrepareParticipant` | before participant Prepare | component graph; epoch; all queues |
| `ValidateGraph` | before cross-component validation | graph; epoch/high-water inputs; all queues |

All injected failures occur before Commit and preserve live values, active epoch, and complete queue ordering metadata.
