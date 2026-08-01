# PREP-2.2 Evidence Index

Captured on 2026-08-01 for the PREP-2.2 implementation worktree based on commit `e139032416e1ef674702fc3ccf00bae630f765af`.

## Results

- Targeted stress: 20/20 processes passed; seeds `2202` through `2206` each ran four times; no 90-second outer timeout fired. See `targeted-stress.log` and the per-run stdout/stderr files.
- Same-process order: every targeted stdout log contains `[SeededOrder]`, the resolved cross-class order, seed, and exact replay command.
- Isolation, watcher, and runner configuration: focused tests passed (18/18). See `isolation-parallelism.log`.
- Fixed-delay scan: no prohibited wait exists; the only match is `FileWatcherObservation.DeadlineBackoffAsync`, the named bounded polling primitive. See `source-scan.log`.
- Full regression: 813/813 tests passed with `--blame-hang --blame-hang-timeout 60s`. See `full-suite.log`.
- Environment and tool versions: see `environment-metadata.log`.
- SHA-256 inventory: see `sha256.txt`; it covers every evidence artifact other than this index and the manifest itself.

## Commands

```powershell
& tests/FTG_Framework.Tests/run-prep22-stress.ps1
$env:FTG_TEST_SEED='2202'
dotnet test tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj --no-restore --blame-hang --blame-hang-timeout 60s
```

The evidence records the base repository commit and dirty-worktree state because implementation is handed to review before a commit is created. The final evidence-commit equality gate remains open until these artifacts and the reviewed implementation are committed together.
