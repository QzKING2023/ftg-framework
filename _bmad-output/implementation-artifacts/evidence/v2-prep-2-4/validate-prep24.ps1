param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
)

$ErrorActionPreference = "Stop"
$failures = [System.Collections.Generic.List[string]]::new()

function Read-RepoFile([string]$RelativePath) {
    $path = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $failures.Add("Missing file: $RelativePath")
        return ""
    }
    Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

function Require-Match([string]$Text, [string]$Pattern, [string]$Message) {
    if ($Text -notmatch $Pattern) { $failures.Add($Message) }
}

function Get-Anchor([string]$Heading) {
    $slug = $Heading.Trim().ToLowerInvariant()
    $slug = [regex]::Replace($slug, '[^\p{L}\p{Nd}\s-]', '')
    $slug = [regex]::Replace($slug, '\s+', '-')
    [regex]::Replace($slug, '-+', '-').Trim('-')
}

function Validate-Links([string]$RelativePath, [string]$Text) {
    $sourceDirectory = Split-Path -Parent (Join-Path $RepoRoot $RelativePath)
    foreach ($match in [regex]::Matches($Text, '\[[^\]]+\]\(([^)]+)\)')) {
        $reference = $match.Groups[1].Value
        if ($reference -match '^(https?://|mailto:)') { continue }
        $parts = $reference.Split('#', 2)
        $targetPath = if ([string]::IsNullOrWhiteSpace($parts[0])) {
            Join-Path $RepoRoot $RelativePath
        } else {
            [IO.Path]::GetFullPath((Join-Path $sourceDirectory $parts[0]))
        }
        if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
            $failures.Add("Broken link in ${RelativePath}: $reference")
            continue
        }
        if ($parts.Count -eq 2 -and -not [string]::IsNullOrWhiteSpace($parts[1])) {
            $anchors = Get-Content -LiteralPath $targetPath -Encoding UTF8 |
                Where-Object { $_ -match '^#{1,6}\s+(.+)$' } |
                ForEach-Object { Get-Anchor $matches[1] }
            if ($anchors -notcontains $parts[1].ToLowerInvariant()) {
                $failures.Add("Broken anchor in ${RelativePath}: $reference")
            }
        }
    }
}

$epicPath = "_bmad-output/planning-artifacts/epics/epic-2-move-authoring-training-suite.md"
$checklistPath = "_bmad-output/planning-artifacts/epic-2-risk-and-evidence-checklist.md"
$uxPath = "_bmad-output/planning-artifacts/epic-2-ux-contract.md"
$storyPath = "_bmad-output/implementation-artifacts/v2-prep-2-4-epic-2-planning-and-evidence-review.md"
$story13Path = "_bmad-output/implementation-artifacts/v2-1-3-character-scene-template.md"
$manualPath = "_bmad-output/implementation-artifacts/v2-epic-1-manual-verification-guide.md"
$retroPath = "_bmad-output/implementation-artifacts/epic-v2-1-retro-2026-07-31.md"
$sprintPath = "_bmad-output/implementation-artifacts/sprint-status.yaml"
$approvalsPath = "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/role-approvals.md"
$traceabilityPath = "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/traceability.md"
$inventoryPath = "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/sha256.txt"

$epic = Read-RepoFile $epicPath
$checklist = Read-RepoFile $checklistPath
$ux = Read-RepoFile $uxPath
$story = Read-RepoFile $storyPath
$story13 = Read-RepoFile $story13Path
$manual = Read-RepoFile $manualPath
$retro = Read-RepoFile $retroPath
$sprint = Read-RepoFile $sprintPath
$approvals = Read-RepoFile $approvalsPath
$traceability = Read-RepoFile $traceabilityPath
$inventory = Read-RepoFile $inventoryPath

$expectedAcCounts = @{ 1 = 12; 2 = 11; 3 = 12; 4 = 13; 5 = 15 }
$allAcIds = [regex]::Matches($epic, '\*\*(S2\.[1-5]-AC\d{2})\*\*') | ForEach-Object { $_.Groups[1].Value }
foreach ($storyNumber in 1..5) {
    Require-Match $epic "(?m)^## Story 2\.${storyNumber}:" "Epic package missing Story 2.$storyNumber."
    $storyAcIds = $allAcIds | Where-Object { $_ -like "S2.$storyNumber-*" }
    if ($storyAcIds.Count -ne $expectedAcCounts[$storyNumber]) {
        $failures.Add("Story 2.$storyNumber AC count is $($storyAcIds.Count); expected $($expectedAcCounts[$storyNumber]).")
    }
    $expectedAcIds = 1..$expectedAcCounts[$storyNumber] | ForEach-Object { "S2.$storyNumber-AC$($_.ToString('00'))" }
    $identifierDelta = @(Compare-Object -ReferenceObject $expectedAcIds -DifferenceObject $storyAcIds)
    if ($identifierDelta.Count -gt 0) {
        $failures.Add("Story 2.$storyNumber acceptance-criterion identifiers are not the exact contiguous set AC01-AC$($expectedAcCounts[$storyNumber].ToString('00')).")
    }
}
if (($allAcIds | Sort-Object -Unique).Count -ne $allAcIds.Count) {
    $failures.Add("Epic 2 contains duplicate acceptance-criterion identifiers.")
}

$riskDimensions = @(
    "lifecycle epoch", "ownership", "generation", "event order", "immutability",
    "invalid input", "failure atomicity", "concurrency", "test isolation",
    "runtime/scaffold parity", "determinism"
)
foreach ($storyNumber in 1..5) {
    foreach ($risk in $riskDimensions) {
        $escapedRisk = [regex]::Escape($risk)
        $rows = [regex]::Matches($checklist, "(?m)^\| Story 2\.$storyNumber \| $escapedRisk \| ([^|]+) \| ([^|]+) \| ([^|]+) \|$")
        if ($rows.Count -ne 1) {
            $failures.Add("Story 2.$storyNumber risk '$risk' must have exactly one mapping row.")
            continue
        }
        $disposition = $rows[0].Groups[1].Value.Trim()
        $ac = $rows[0].Groups[2].Value.Trim()
        $evidence = $rows[0].Groups[3].Value.Trim()
        $isNa = $ac -eq "N/A" -and $evidence -eq "N/A"
        if ($isNa -and $disposition -notmatch '^N/A:\s+\S') {
            $failures.Add("Story 2.$storyNumber risk '$risk' has N/A without rationale.")
        } elseif (-not $isNa) {
            if ($ac -notmatch "^S2\.$storyNumber-AC\d{2}$" -or $allAcIds -notcontains $ac) {
                $failures.Add("Story 2.$storyNumber risk '$risk' references invalid AC '$ac'.")
            }
            if ($evidence -notmatch "^E2\.$storyNumber-[A-Z]$") {
                $failures.Add("Story 2.$storyNumber risk '$risk' references invalid evidence '$evidence'.")
            }
        }
    }
}

foreach ($storyNumber in 1..5) {
    $line = [regex]::Match($checklist, "(?m)^\| Story 2\.$storyNumber \|.*$").Value
    $evidenceLines = @([regex]::Matches($checklist, "(?m)^\| Story 2\.$storyNumber \|.*$") |
        ForEach-Object { $_.Value } |
        Where-Object { ($_.Trim([char]'|').Split('|')).Count -eq 11 })
    if ($evidenceLines.Count -ne 1) {
        $failures.Add("Story 2.$storyNumber must have exactly one 10-layer evidence row.")
        continue
    }
    $cells = $evidenceLines[0].Trim([char]'|').Split('|') | ForEach-Object { $_.Trim() }
    foreach ($cell in $cells[1..10]) {
        if ($cell -notmatch '(required|^N/A:\s+\S)') {
            $failures.Add("Story 2.$storyNumber has unresolved evidence cell '$cell'.")
        }
    }
}
if ($checklist -match 'N/A unless|unless distributed|unless controls ship') {
    $failures.Add("Checklist contains conditional evidence applicability.")
}

foreach ($decision in "D-UX-01", "D-UJ6-01", "D-DATA-01", "D-COMBO-01", "D-REC-01", "D-SAVE-01", "D-BASE-01") {
    Require-Match $checklist "(?m)^\| $decision \|.*\| Resolved \|" "Decision $decision is not resolved."
}
Require-Match $ux "(?m)^Status: Approved" "UX contract is not approved."
Require-Match $story13 "(?m)^  - \[x\] 3\.2 " "Story 1.3 Task 3.2 is not complete."
Require-Match $story13 "(?m)^- \[x\] Task 10:" "Story 1.3 Task 10 is not complete."
Require-Match $manual "(?m)^> \*\*Status\*\*: Complete" "Manual guide is not marked complete."
Require-Match $manual "747/747" "Manual guide lacks the accepted 747/747 baseline."
foreach ($acGroup in "AC01-AC03", "AC04-AC05", "AC06-AC07", "AC08-AC09", "AC10") {
    Require-Match $story "(?m)^\| $acGroup \|.*\| Accepted\b" "Story evidence row $acGroup is not accepted."
}

foreach ($role in "Product Owner", "System Architect", "QA Engineer") {
    Require-Match $approvals "(?ms)^## $([regex]::Escape($role)).*?Decision: Approved" "$role approval is missing."
}

$inventoryHashes = @{}
foreach ($line in $inventory -split "`r?`n") {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -match '^([0-9a-f]{64})  (.+)$') {
        $expected = $matches[1]
        $relativePath = $matches[2]
        if ($inventoryHashes.ContainsKey($relativePath)) {
            $failures.Add("Duplicate inventory target: $relativePath")
            continue
        }
        $absolutePath = Join-Path $RepoRoot $relativePath
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
            $failures.Add("Inventory target missing: $relativePath")
            continue
        }
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $absolutePath).Hash.ToLowerInvariant()
        if ($actual -ne $expected) { $failures.Add("Inventory hash mismatch: $relativePath") }
        $inventoryHashes[$relativePath] = $expected
    } else {
        $failures.Add("Malformed inventory row: $line")
    }
}
$requiredInventoryPaths = @(
    ".gitignore",
    $epicPath,
    $checklistPath,
    $uxPath,
    "_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md",
    "_bmad-output/planning-artifacts/epics/v2-epic-2-readiness-gate-non-epic.md",
    "_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-prep24-closure.md",
    $story13Path,
    $manualPath,
    $retroPath,
    $sprintPath,
    $storyPath,
    "_bmad-output/implementation-artifacts/deferred-work.md",
    "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/index.md",
    $approvalsPath,
    $traceabilityPath,
    "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/review-history.md",
    "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/tracking-diff.patch",
    "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/full-suite.log",
    "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/validate-prep24.ps1"
)
foreach ($requiredPath in $requiredInventoryPaths) {
    if (-not $inventoryHashes.ContainsKey($requiredPath)) {
        $failures.Add("Required inventory target missing: $requiredPath")
    }
}
foreach ($binding in @{
    "Epic 2 stories" = $epicPath
    "Risk/evidence checklist" = $checklistPath
    "UX contract" = $uxPath
    "Architecture Spine" = "_bmad-output/planning-artifacts/architecture/architecture-ftg-framework-2026-07-26/ARCHITECTURE-SPINE.md"
    "PREP readiness gate" = "_bmad-output/planning-artifacts/epics/v2-epic-2-readiness-gate-non-epic.md"
}.GetEnumerator()) {
    $approvalMatch = [regex]::Match($approvals, "(?m)^\| $([regex]::Escape($binding.Key)) \| ``([0-9a-f]{64})`` \|$")
    if (-not $approvalMatch.Success -or $inventoryHashes[$binding.Value] -ne $approvalMatch.Groups[1].Value) {
        $failures.Add("Approval hash is stale or missing for $($binding.Key).")
    }
}

foreach ($path in $epicPath, $checklistPath, $uxPath, $storyPath, $story13Path, $manualPath,
    "_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-01-prep24-closure.md",
    "_bmad-output/implementation-artifacts/evidence/v2-prep-2-4/index.md") {
    & git -C $RepoRoot check-ignore -q -- $path
    if ($LASTEXITCODE -eq 0) { $failures.Add("Required deliverable is ignored by Git: $path") }
}

foreach ($pair in @{
    $epicPath = $epic
    $checklistPath = $checklist
    $retroPath = $retro
}.GetEnumerator()) { Validate-Links $pair.Key $pair.Value }

Require-Match $traceability "P2\.4-AC10" "Traceability does not cover P2.4-AC10."
foreach ($action in 6..9) {
    Require-Match $retro "(?m)^\| $action \|.*\| \[[^]]+\]\([^)]*v2-prep-2-4[^)]*\) \|" "Retrospective action $action lacks PREP-2.4 evidence."
}
Require-Match $sprint "all nine `?v2-1`? actions" "Sprint start gate does not name all nine v2-1 actions."
Require-Match $sprint "every Architecture\s*#?\s*Adoption Gate row" "Sprint start gate omits Architecture Adoption Gate rows."
Require-Match $sprint "risk/evidence checklist" "Sprint start gate omits the risk/evidence checklist."
Require-Match $sprint "Product Owner, Architect, and QA approvals" "Sprint start gate omits all three approvals."
Require-Match $sprint "(?m)^  v2-epic-2: backlog$" "Epic 2 must remain backlog during PREP-2.4."

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    Write-Host "PREP-2.4 validation failed with $($failures.Count) issue(s)."
    exit 1
}

Write-Host "PREP-2.4 validation passed. Stories: 5; risks: 55; evidence cells: 50; approvals: 3; ACs traced: 10."
