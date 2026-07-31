# Generated-Project Godot Smoke Test

This harness verifies the scaffolded project's automatic character-select flow,
P1/P2 state labels, visible P1 input history, and the P1 `U`/`5LP` attack path.
It is staged into a generated project because its `res://` paths must resolve
inside that project.

From the repository root:

```powershell
$output = Join-Path ([System.IO.Path]::GetTempPath()) "ftg-scaffold-smoke"
dotnet run --project Scaffold/ftg-cli -- new MyFighter --output $output
Copy-Item Scaffold/verification/ScaffoldSmokeTest.cs "$output/MyFighter/Scripts/"
Copy-Item Scaffold/verification/scaffold_smoke.tscn "$output/MyFighter/"
dotnet build "$output/MyFighter/MyFighter.csproj" --no-restore
& "D:\path\to\Godot_mono_console.exe" --headless --path "$output/MyFighter" res://scaffold_smoke.tscn
```

On Unix-like systems, use the equivalent `cp` commands and invoke the installed
.NET-enabled Godot console binary. Success prints `[ScaffoldSmoke] PASS` and exits
zero. The harness waits up to 600 rendered frames for training initialization,
then runs at a 1280×720 root viewport.
