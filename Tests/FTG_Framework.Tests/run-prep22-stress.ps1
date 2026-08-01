param(
    [string]$EvidenceDirectory = "_bmad-output/implementation-artifacts/evidence/v2-prep-2-2",
    [int]$OuterTimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"
$project = "tests/FTG_Framework.Tests/FTG_Framework.Tests.csproj"
$inventoryPath = "Tests/FTG_Framework.Tests/eventbus-test-inventory.txt"
$filter = (Get-Content -LiteralPath $inventoryPath | ForEach-Object { "FullyQualifiedName~$_" }) -join "|"
$seeds = @(2202, 2203, 2204, 2205, 2206)
$resolvedEvidence = [IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($resolvedEvidence) | Out-Null
$summary = New-Object System.Collections.Generic.List[string]
$previousSeed = [Environment]::GetEnvironmentVariable("FTG_TEST_SEED", "Process")

try {
for ($iteration = 1; $iteration -le 20; $iteration++) {
    $seed = $seeds[($iteration - 1) % $seeds.Count]
    $stdout = Join-Path $resolvedEvidence ("targeted-{0:D2}-seed-{1}.stdout.log" -f $iteration, $seed)
    $stderr = Join-Path $resolvedEvidence ("targeted-{0:D2}-seed-{1}.stderr.log" -f $iteration, $seed)
    $env:FTG_TEST_SEED = $seed.ToString()
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = "dotnet"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.Arguments = "test `"$project`" --no-restore --filter `"$filter`" --blame-hang --blame-hang-timeout 60s --logger `"console;verbosity=detailed`""
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    $process.Start() | Out-Null
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()

    $timedOut = -not $process.WaitForExit($OuterTimeoutSeconds * 1000)
    if ($timedOut) {
        $process.Kill($true)
        $process.WaitForExit()
    }
    [IO.File]::WriteAllText($stdout, $stdoutTask.GetAwaiter().GetResult())
    [IO.File]::WriteAllText($stderr, $stderrTask.GetAwaiter().GetResult())
    $stopwatch.Stop()
    $exitCode = if ($timedOut) { 124 } else { $process.ExitCode }
    $summary.Add("iteration=$iteration seed=$seed duration_ms=$($stopwatch.ElapsedMilliseconds) outer_timeout=$timedOut exit_code=$exitCode stdout=$([IO.Path]::GetFileName($stdout)) stderr=$([IO.Path]::GetFileName($stderr))")
    if ($exitCode -ne 0) {
        [IO.File]::WriteAllLines((Join-Path $resolvedEvidence "targeted-stress.log"), $summary)
        exit $exitCode
    }
}

[IO.File]::WriteAllLines((Join-Path $resolvedEvidence "targeted-stress.log"), $summary)
}
finally {
    [Environment]::SetEnvironmentVariable("FTG_TEST_SEED", $previousSeed, "Process")
}
