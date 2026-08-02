# V2 Story 2.1-B Evidence Manifest

Date: 2026-08-02
Story: `v2-2-1-b-path-confined-atomic-persistence`
Baseline commit: `f4a0aee8807787a87fab87e8c951ff4c92d1d6e0`
Environment: Windows x64 `10.0.26200`; .NET SDK `10.0.201`; target `net8.0`
Godot: project pin `4.5.1`; CLI not available on `PATH` during this Data-only slice run
Reviewer: parallel Blind Hunter, Edge Case Hunter, and Acceptance Auditor; accepted after remediation

## Accepted Automated Evidence

| Evidence | Command / scenario | Result | Duration / seed | AC |
|---|---|---|---|---|
| E2.1-U/D/F/C/R focused | `dotnet test Tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore --filter "FullyQualifiedName~MoveDatasetPersistenceTests|FullyQualifiedName~MoveAuthoringUndoServiceTests" --verbosity minimal` | 45 passed, 0 failed, 0 skipped; exit 0 | 160 ms; seed 2202 | 1-9 |
| Full regression | `dotnet test FTG_Framework.sln --no-restore --verbosity minimal` | 920 passed, 0 failed, 0 skipped; exit 0 | 1m 2s; seed 2202 | 1-9 |
| Framework quality | `dotnet build FTG_Framework.csproj --no-restore --verbosity minimal` | 0 warnings, 0 errors; exit 0 | 0.42s | 1-9 |
| Path boundary | Strict identifier matrix plus deterministic destination, staging, and configured-root reparse probes before initial read, coordinated commit, final identity check, and replacement | Prior bytes and committed dataset preserved; no platform silently bypasses the boundary probes | deterministic fault seams | 2-3, 5, 7 |
| Atomicity | Serialization/staging/flush/staged-validation/coordination/containment/replacement fault matrix | Prior bytes reloadable; DataStore value/version unchanged; owned staging cleaned unless cleanup itself was injected | deterministic fault seams | 1, 4-6 |
| UndoRedo concurrency | External write after save followed by Undo | conflict returned; external bytes preserved; committed in-memory version not silently overwritten | deterministic | 7-8 |
| Runtime round trip | Successful save loaded with ordinary `MoveDataLoader` and identity recomputed from committed bytes | semantic value, UTF-8 no-BOM bytes, content identity, and DataStore version agree | deterministic | 4, 9 |

## Reviewed File SHA-256

```text
3f44d4ffcec52abdcd07a602d8e8885780679d8dd464a10f85bff8f9422263f7  Scripts/Framework/Data/MoveDatasetPersistence.cs
b4977a0c3b4f25a3d5cc380154a3399f01065a6aed1d25fdea288259e659d1f9  Tests/FTG_Framework.Tests/Data/MoveDatasetPersistenceTests.cs
3be9eb79e7aeddde5c270b11079e7cbe3e90ed47f1bf3220a42999b089fdf629  Tests/FTG_Framework.Tests/Editor/MoveAuthoringUndoServiceTests.cs
```

## Scope Note

This slice changes pure-C# Data persistence and tests only. Godot editor interaction, plugin lifecycle, packaging, and scaffold acceptance remain Story 2.1-C/parent evidence and were not rerun here.
