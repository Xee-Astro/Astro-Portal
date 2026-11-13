[CmdletBinding()]
param([string]$Destination)
$ErrorActionPreference = 'Stop'
function Get-PatchHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
    finally { $stream.Dispose(); $sha.Dispose() }
}
if (-not $Destination) { $Destination = Read-Host 'Paste the folder containing your installed Astro-Portal.exe' }
if ([string]::IsNullOrWhiteSpace($Destination)) { throw 'No installation folder supplied.' }
$Destination = [IO.Path]::GetFullPath($Destination.Trim().Trim('"'))
$prefix = $Destination.TrimEnd('\') + '\'
$assembly = Join-Path $Destination 'AsiairContainer.dll'
if (-not (Test-Path -LiteralPath $assembly) -or -not (Test-Path -LiteralPath (Join-Path $Destination 'coreclr.dll'))) {
    throw 'Choose an existing self-contained Astro - Portal installation folder. This patch is not a full installer.'
}
$versionText = [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly).FileVersion
$version = [version]$versionText
if ($version -ge [version]'0.5.1.0') { Write-Host "Already version $versionText or newer. No changes made."; exit 0 }
if ($version.Major -ne 0 -or $version.Minor -ne 5) { throw 'This patch supports version 0.5.0 only.' }
$running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { try { $file = $_.Path; $file -and $file.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) } catch { $false } })
if ($running) { throw 'Close Astro - Portal and its Android environment completely, then run the patch again.' }
$allowed = @('AsiairContainer.exe','Astro-Portal.exe','AsiairContainer.dll','AsiairContainer.deps.json','AsiairContainer.runtimeconfig.json')
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'patch-sha256.json') -Raw | ConvertFrom-Json
if ($manifest.Count -ne $allowed.Count) { throw 'Incomplete patch manifest.' }
foreach ($name in $allowed) {
    $entries = @($manifest | Where-Object { $_.path -ceq $name })
    if ($entries.Count -ne 1) { throw "Invalid patch entry: $name" }
    $source = Join-Path (Join-Path $PSScriptRoot 'payload') $name
    if ((Get-PatchHash $source) -ne $entries[0].sha256) { throw "Patch checksum failed: $name" }
    $target = [IO.Path]::GetFullPath((Join-Path $Destination $name))
    if (-not $target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid patch destination.' }
    if (Test-Path -LiteralPath $target) {
        $probe = [IO.File]::Open($target,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $probe.Dispose()
    }
}
Write-Host "Update Astro - Portal $versionText to 0.5.1 in: $Destination"
Write-Host 'Adds Android font controls (80%-200%, reset to 100%). No downloads. Only five launcher files change.'
Write-Host 'Your runtime, Android apps, accounts, notes, settings and private-data remain in place. The old launcher files are backed up for rollback.'
do { $answer = Read-Host 'Apply patch? [Yes/No]' } while ($answer -notmatch '^(?i:y|yes|n|no)$')
if ($answer -match '^(?i:n|no)$') { Write-Host 'Cancelled. No changes made.'; exit 0 }
$backup = Join-Path $Destination ('patch-backup-0.5.0-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup | Out-Null
$originals = @()
foreach ($name in $allowed) {
    $target = Join-Path $Destination $name
    if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination (Join-Path $backup $name); $originals += $name }
}
try {
    foreach ($name in $allowed) { Copy-Item -LiteralPath (Join-Path (Join-Path $PSScriptRoot 'payload') $name) -Destination (Join-Path $Destination $name) -Force }
    foreach ($entry in $manifest) {
        if ((Get-PatchHash (Join-Path $Destination $entry.path)) -ne $entry.sha256) { throw 'Installed patch verification failed.' }
    }
} catch {
    foreach ($name in $allowed) {
        $target = Join-Path $Destination $name
        if ($name -in $originals) { Copy-Item -LiteralPath (Join-Path $backup $name) -Destination $target -Force }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
    }
    throw
}
Write-Host 'Updated to 0.5.1. Open Astro-Portal.exe normally.' -ForegroundColor Green
Write-Host "Old launcher backup: $backup"
