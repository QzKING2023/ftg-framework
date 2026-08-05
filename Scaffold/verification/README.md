# Generated-Project Godot Smoke Test

This harness verifies the scaffolded project's automatic character-select flow,
P1/P2 state labels, visible P1 input history, and the P1 `U`/`5LP` attack path.
It is staged into a generated project because its `res://` paths must resolve inside that project.

The smoke workflow uses production P1 movement to enter range, a real `5LP` hit, and real P2 facing-relative Back input for a block. It must not assign `CharacterController.GlobalPosition` to manufacture range.

From the repository root:

```powershell
$output = Join-Path ([System.IO.Path]::GetTempPath()) "ftg-scaffold-smoke"
dotnet run --project Scaffold/ftg-cli -- new MyFighter --output $output
Copy-Item Scaffold/verification/ScaffoldSmokeTest.cs "$output/MyFighter/Scripts/"
Copy-Item Scaffold/verification/scaffold_smoke.tscn "$output/MyFighter/"
dotnet build "$output/MyFighter/MyFighter.csproj" --no-restore
& "D:\path\to\Godot_mono_console.exe" --headless --path "$output/MyFighter" res://scaffold_smoke.tscn
```

Run the CORR-2 responsive presentation and shortcut-parity harness against the
same generated project:

```powershell
Copy-Item Scaffold/verification/Corr2ResponsiveSmokeTest.cs "$output/MyFighter/Scripts/"
Copy-Item Scaffold/verification/corr2_responsive_smoke.tscn "$output/MyFighter/"
dotnet build "$output/MyFighter/MyFighter.csproj" --no-restore
& "D:\path\to\Godot_mono_console.exe" --headless --path "$output/MyFighter" res://corr2_responsive_smoke.tscn
```

Success prints `[Corr2ResponsiveSmoke] PASS` and exits zero. Headless runs skip
the platform fullscreen transition while still covering the viewport/UI-scale
matrix, visible-world bounds, ultra-wide expansion, authoritative-state
preservation, focus reachability, and keyboard/controller InputMap parity.

On Unix-like systems, use the equivalent `cp` commands and invoke the installed
.NET-enabled Godot console binary. Success prints `[ScaffoldSmoke] PASS` and exits
zero. The harness waits up to 600 rendered frames for training initialization,
then runs at a 1280×720 root viewport.

## Story 4.1 balance trial smoke (E4.1-G)

The balance trial harness proves the deterministic balance testbed end to end in
a generated project: it saves a training snapshot containing a real recording,
prepares and starts a trial through the runtime `BalanceTrialService`, and waits
for the observation window (120 frames) to complete with per-frame hashes,
initiations, and metrics.

```powershell
Copy-Item Scaffold/verification/BalanceTrialSmokeTest.cs "$output/MyFighter/Scripts/"
Copy-Item Scaffold/verification/balance_trial_smoke.tscn "$output/MyFighter/"
dotnet build "$output/MyFighter/MyFighter.csproj" --no-restore
& "D:\path\to\Godot_mono_console.exe" --headless --path "$output/MyFighter" res://balance_trial_smoke.tscn
```

Success prints `[BalanceTrialSmoke] PASS` (with window, damage, combo, initiation
count, terminal move, position, and committed data versions) and exits zero.

## Story 4.2 state-scoped replay smoke (E4.2-G, E4.2-S)

The state-scoped replay harness proves Story 2.5 snapshot bootstrap end to end in
a generated project. Flow B saves a mid-move snapshot, records 30 frames, and
replays them with byte-identical per-frame hashes. Flow A saves mid-combo after
real walking (3px/frame, stop at 45px), records 40 frames, replays, and verifies
zero `StateRestored` publications, replay-end rebind into live play, and live
continuation.

```powershell
Copy-Item Scaffold/verification/StateScopedReplaySmokeTest.cs "$output/MyFighter/Scripts/"
Copy-Item Scaffold/verification/state_scoped_replay_smoke.tscn "$output/MyFighter/"
dotnet build "$output/MyFighter/MyFighter.csproj" --no-restore
& "D:\path\to\Godot_mono_console.exe" --headless --path "$output/MyFighter" res://state_scoped_replay_smoke.tscn
```

Success prints `[StateScopedReplaySmoke] PASS` and exits zero. The harness
persists trails to `user://s42-recorded-trail.json` / `s42-replayed-trail.json`.

Restart portability (E4.2-S): replay the same file in a clean second process.
With the same generated project still present, pass `--replay-verify` as a user
argument; the harness loads `user://s42-replay-b.json`, replays it against the
persisted recorded trail, asserts zero `StateRestored` publications, and prints
`[StateScopedReplaySmoke] PASS (restart verify)`:

```powershell
& "D:\path\to\Godot_mono_console.exe" --headless --path "$output/MyFighter" res://state_scoped_replay_smoke.tscn -- --replay-verify
```

The main flow and the restart verify flow each exit with code 0 only on PASS.
