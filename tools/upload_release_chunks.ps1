param(
    [Parameter(Mandatory=$true)][int]$Worker,
    [int]$Workers = 8,
    [string]$Repository = 'ShrinkShi/OpenHearthstone',
    [string]$Tag = 'openh-assets-20261105'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $root 'work\release-staging'
$chunks = Join-Path $stage 'chunks'
$manifest = Get-Content -Raw -LiteralPath (Join-Path $chunks 'full-chunks.json') | ConvertFrom-Json
if ($Worker -lt 0 -or $Worker -ge $Workers) { throw 'Invalid worker index' }
Set-Location -LiteralPath $root
for ($i = $Worker; $i -lt $manifest.Count; $i += $Workers) {
    $item = $manifest[$i]
    $path = Join-Path $chunks $item.name
    if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -ne $item.size) {
        throw "Archive volume missing or changed: $path"
    }
    $success = $false
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        Write-Host "Uploading $($item.name), attempt $attempt"
        & gh.exe release upload $Tag $path --repo $Repository
        if ($LASTEXITCODE -eq 0) { $success = $true; break }
        Start-Sleep -Seconds ([Math]::Min(60, 5 * $attempt))
    }
    if (-not $success) { throw "Upload failed: $($item.name)" }
}
Write-Host "Worker $Worker completed."
