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
