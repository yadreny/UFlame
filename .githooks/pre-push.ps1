$ErrorActionPreference = "Stop"
try {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [Console]::InputEncoding  = $utf8NoBom
    [Console]::OutputEncoding = $utf8NoBom
    $global:OutputEncoding    = $utf8NoBom
} catch {}

$GitDir = (& git rev-parse --git-dir 2>$null)
if (-not $GitDir) { $GitDir = ".git" }
$ReportPath = Join-Path $GitDir "also_filtered_report.txt"

if (Test-Path -LiteralPath $ReportPath) {
    try {
        $content = Get-Content -LiteralPath $ReportPath -Raw -Encoding UTF8 -ErrorAction Stop
        if ($content -and $content.Trim().Length -gt 0) {
            Write-Host ""
            Write-Host $content
        }
    } catch {}
    try { Remove-Item -LiteralPath $ReportPath -Force } catch {}
}

exit 0