# PREP-2.2 Evidence Index

Captured on 2026-08-01 for the reviewed PREP-2.2 implementation commit `3b4d1b35e31532c27610abe9b29212fa8260c85f`.

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

The evidence was generated from the exact implementation committed as `3b4d1b35e31532c27610abe9b29212fa8260c85f`; this closure-only metadata update does not alter runtime or test implementation.
