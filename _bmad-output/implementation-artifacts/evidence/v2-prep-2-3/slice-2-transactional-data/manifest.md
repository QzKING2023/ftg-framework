# PREP-2.3 Slice 2 Evidence Manifest

- Scope: AD-15/AD-19 presence-aware physics data and failure-atomic transactions
- Baseline: `cddba608b099b00e7a0f25cc5d0e635286ff00a4`
- Depends on: accepted Slice 1 manifest
- State: accepted
- Reviewer: Codex implementation validator
- Review date: 2026-08-01

## Results

| Command | Result |
|---|---|
| Focused Data loader/store/hot-reload/persistence test filter | 59 passed, 0 failed, 0 skipped |
| `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore` | 821 passed, 0 failed, 0 skipped |

## Contract Checks

- Current schema is version 1; missing/unsupported schema, missing/null/type-coerced/duplicate fields, invalid magnitudes, duplicate IDs, and dangling references reject before swap.
- DataStore publishes a monotonic physics dataset version and rejects stale optimistic commits.
- Watcher reload validates against current move/state-profile references and retains the last valid candidate.
- Explicit migrations require a complete ordered registry and persist only the validated current version.
- Atomic write uses a same-directory temporary file, durable pre-commit flush, an explicit replacement commit point, and cleanup on injected pre-commit failure.

Hashes are recorded in the umbrella evidence index after all slices are complete.
