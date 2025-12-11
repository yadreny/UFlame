Param(
    [int]$MaxFileMB   = 95,
    [int]$MaxCommitMB = 150
)

$ErrorActionPreference = "Stop"

# --- Force UTF-8 console & pipeline for correct Cyrillic handling ---
try {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [Console]::InputEncoding  = $utf8NoBom
    [Console]::OutputEncoding = $utf8NoBom
    $global:OutputEncoding    = $utf8NoBom
} catch {}

# Report under .git (UTF-8 file)
$GitDir = (& git rev-parse --git-dir 2>$null)
if (-not $GitDir) { $GitDir = ".git" }
$ReportPath = Join-Path $GitDir "also_filtered_report.txt"

function HR([long]$bytes) {
    if ($bytes -ge 1GB) { return ("{0:N2} GB" -f ($bytes / 1GB)) }
    elseif ($bytes -ge 1MB) { return ("{0:N2} MB" -f ($bytes / 1MB)) }
    elseif ($bytes -ge 1KB) { return ("{0:N2} KB" -f ($bytes / 1KB)) }
    else { return ("{0} B" -f $bytes) }
}

function Get-StagedEntries {
    # staged paths (A,C,M,R), with raw UTF-8 names
    $paths = (& git -c core.quotepath=false diff --cached --name-only --diff-filter=ACMR) -split "`n" | Where-Object { $_ -ne "" }
    $result = @()

    foreach ($p in $paths) {
        # "mode sha stage<TAB>path"
        $line = (& git -c core.quotepath=false ls-files -s -- "$p")
        if (-not $line) { continue }

        $parts = $line -split "\s+"
        $mode = $parts[0]; $sha = $parts[1]

        # Skip submodules (mode 160000)
        if ($mode -eq "160000") { continue }

        $size = 0
        try { $size = [int64](& git cat-file -s $sha 2>$null) } catch {
            try { $size = (Get-Item -LiteralPath $p).Length } catch { $size = 0 }
        }

        $result += [pscustomobject]@{ Path=$p; Sha=$sha; Size=$size }
    }
    return $result
}

function Unstage([string]$path) {
    & git reset -q HEAD -- "$path" | Out-Null
}

function Add-Box([System.Text.StringBuilder]$sb, [string[]]$lines) {
    $border = "########################################################################"
    $null = $sb.AppendLine($border)
    foreach ($ln in $lines) {
        if ($ln -eq "") {
            $null = $sb.AppendLine("###")
        } else {
            $null = $sb.AppendLine(("###  {0}" -f $ln))
        }
    }
    $null = $sb.AppendLine($border)
    $null = $sb.AppendLine("")  # blank after box
}

$MaxFileBytes   = [int64]$MaxFileMB * 1MB
$MaxCommitBytes = [int64]$MaxCommitMB * 1MB

$removedTooBigFiles = New-Object System.Collections.Generic.List[object]
$removedToFitTotal  = New-Object System.Collections.Generic.List[object]

# === STEP 1: per-file limit (> MaxFileMB => unstage) ===
$entries = Get-StagedEntries
foreach ($e in $entries) {
    if ($e.Size -gt $MaxFileBytes) {
        Unstage $e.Path
        $removedTooBigFiles.Add($e) | Out-Null
    }
}

# === STEP 2: total size limit (remove largest until <= MaxCommitBytes) ===
$entries = Get-StagedEntries
$beforeTotal = (($entries | Measure-Object -Property Size -Sum).Sum) ; if (-not $beforeTotal) { $beforeTotal = 0 }
if ($beforeTotal -gt $MaxCommitBytes -and $entries.Count -gt 0) {
    $sorted = $entries | Sort-Object Size -Descending
    $running = $beforeTotal
    foreach ($e in $sorted) {
        if ($running -le $MaxCommitBytes) { break }
        Unstage $e.Path
        $removedToFitTotal.Add($e) | Out-Null
        $running -= $e.Size
    }
}

# === Compute final INCLUDED set (actual commit content) ===
$included = Get-StagedEntries
$afterTotal = (($included | Measure-Object -Property Size -Sum).Sum) ; if (-not $afterTotal) { $afterTotal = 0 }

# === Build conditional boxes ===
$sb = New-Object System.Text.StringBuilder
$printedAnything = $false

# Box A: SKIPPED (>95 MB) вЂ” ONLY if any
if ($removedTooBigFiles.Count -gt 0) {
    $printedAnything = $true
    $lines = @(
        "",
        ("SKIPPED (> {0} MB)" -f $MaxFileMB),
        ""
    ) + ($removedTooBigFiles | Sort-Object Size -Descending | ForEach-Object {
        " - {0}  ({1})" -f $_.Path, (HR $_.Size)
    }) + @(
        ""
    )
    Add-Box $sb $lines
}

# Box B: TOTAL (>150 MB) вЂ” ONE box with INCLUDED + EXCLUDED (ONLY if exceeded)
if ($beforeTotal -gt $MaxCommitBytes) {
    $printedAnything = $true

    $excluded = $removedToFitTotal
    $excludedTotal = (($excluded | Measure-Object -Property Size -Sum).Sum) ; if (-not $excludedTotal) { $excludedTotal = 0 }

    $totalBoxLines = @(
        "",
        ("TOTAL STAGED SIZE EXCEEDED (> {0} MB)" -f $MaxCommitMB),
        "",
        "INCLUDED (will be committed)",
        ""
    ) + ($included | Sort-Object Size -Descending | ForEach-Object {
        " - {0}  ({1})" -f $_.Path, (HR $_.Size)
    }) + @(
        "",
        "EXCLUDED (removed to fit total limit)",
        ""
    ) + ($excluded | Sort-Object Size -Descending | ForEach-Object {
        " - {0}  ({1})" -f $_.Path, (HR $_.Size)
    }) + @(
        ("Total excluded: {0} in {1} file(s)" -f (HR $excludedTotal), $excluded.Count),
        ""
    )

    Add-Box $sb $totalBoxLines
}

# Print/save only if something was reported; else clear previous report
if ($printedAnything) {
    try {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [IO.File]::WriteAllText($ReportPath, $sb.ToString(), $utf8NoBom)
    } catch {}
    Write-Host ""
    Write-Host $sb.ToString()
} else {
    try { if (Test-Path -LiteralPath $ReportPath) { Remove-Item -LiteralPath $ReportPath -Force } } catch {}
}

exit 0