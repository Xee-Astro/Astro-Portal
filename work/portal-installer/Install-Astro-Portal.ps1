[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Astro-Portal'),
    [string]$RuntimeSource,
    [switch]$CompactImages,
    [switch]$NoShortcut,
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'
function Format-Size([long]$Bytes) {
    if ($Bytes -ge 1GB) { return ('{0:N2} GiB' -f ($Bytes / 1GB)) }
    return ('{0:N1} MiB' -f ($Bytes / 1MB))
}
function Get-FreeSpace([string]$Path) {
    $existing = [IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $existing)) {
        $parent = [IO.Path]::GetDirectoryName($existing.TrimEnd('\'))
        if (-not $parent -or $parent -eq $existing) { throw "Cannot access installation drive: $Path" }
        $existing = $parent
    }
    if (-not ('AstroPortalInstaller.DiskSpace' -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace AstroPortalInstaller {
 public static class DiskSpace {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern bool GetDiskFreeSpaceEx(string path, out ulong available, out ulong total, out ulong free);
 }
}
"@
    }
    [UInt64]$available=0; [UInt64]$total=0; [UInt64]$free=0
    if (-not [AstroPortalInstaller.DiskSpace]::GetDiskFreeSpaceEx($existing,[ref]$available,[ref]$total,[ref]$free)) {
        throw "Could not check free space for $Path. Choose an accessible installation folder."
    }
    return [long]$available
}
function Assert-FreeSpace([string]$Path, [long]$Required) {
    $available = Get-FreeSpace $Path
    if ($available -lt $Required) {
        throw "Not enough space for $Path. Available: $(Format-Size $available); required: $(Format-Size $Required). Choose a different empty installation folder on a drive with more space. Running setup from another drive does not change its destination."
    }
    return $available
}
$script:StepNumber = 0
function Confirm-Step([string]$Title, [string]$Details, [switch]$Optional) {
    $script:StepNumber++
    Write-Host "`nStep $($script:StepNumber): $Title" -ForegroundColor Cyan
    Write-Host $Details
    while ($true) {
        $answer = Read-Host 'Continue? [Yes/No]'
        if ($answer -match '^(?i:y|yes)$') { return $true }
        if ($answer -match '^(?i:n|no)$') {
            if ($Optional) { Write-Host 'Skipped.'; return $false }
            throw 'Installation cancelled. Completed steps remain in the installation folder. No further steps will run. Use a new empty folder for a fresh attempt.'
        }
        Write-Host 'Enter Yes or No. Enter alone does not give permission.'
    }
}
$packages = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Raw | ConvertFrom-Json
$payloadBytes = (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'app') -File -Recurse | Measure-Object Length -Sum).Sum
$downloadBytes = ($packages | Measure-Object bytes -Sum).Sum
Write-Host 'Astro - Portal installation plan' -ForegroundColor Cyan
$plan = @('Review requirements and installation location', 'Review third-party license obligations', 'Verify and copy the bundled launcher (no download)')
if ($RuntimeSource) {
    $plan += 'Copy a clean Android runtime from your selected folder (no download)'
} else {
    foreach ($package in $packages) { $plan += "Download and install $($package.package): $(Format-Size $package.bytes) download" }
}
if ($CompactImages) { $plan += 'Optionally compress Android images (no download)' }
$plan += 'Create fresh Android settings (no download)'
if (-not $NoShortcut) { $plan += 'Optionally create a desktop shortcut (no download)' }
if (-not $NoLaunch) { $plan += 'Optionally open the launcher (no download)' }
for ($i = 0; $i -lt $plan.Count; $i++) { Write-Host "$($i + 1). $($plan[$i])" }
Write-Host 'The launcher later offers Install ASIAIR and Install Stellavita; those APKs are separate downloads, roughly 1.2 GiB and 0.6 GiB respectively. Vendor sizes can change.'
Write-Host 'Required: Windows 10/11 Intel/AMD x64, BIOS virtualization, Windows Hypervisor Platform, and at least 16 GiB free disk space during setup.'
Write-Host "Downloads use a temporary folder beside this setup script: $PSScriptRoot. It is removed after successful installation; interrupted installs may leave it behind."
Write-Host 'This installer does not change BIOS settings, enable Windows features, or restart Windows. Enable Windows Hypervisor Platform in Windows Features and restart if needed.'
if (-not $PSBoundParameters.ContainsKey('Destination')) {
    Write-Host "Default installation folder: $Destination"
    $chosen = Read-Host 'Enter a different installation folder, or press Enter to use the default'
    if (-not [string]::IsNullOrWhiteSpace($chosen)) { $Destination = $chosen.Trim().Trim('"') }
}
$Destination = [IO.Path]::GetFullPath($Destination)
$available = Assert-FreeSpace $Destination 16GB
if (-not $RuntimeSource) {
    $largestArchive = [long](($packages | Measure-Object bytes -Maximum).Maximum)
    [void](Assert-FreeSpace $PSScriptRoot ($largestArchive + 512MB))
}
[void](Confirm-Step 'Review requirements and installation location' "Install into: $Destination`nAvailable space on destination: $(Format-Size $available). Minimum required: 16 GiB.`nDownload: $(if ($RuntimeSource) {'0 bytes (local runtime copy)'} else {Format-Size $downloadBytes}). No files or settings have been changed yet.")
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Astro - Portal requires 64-bit Windows.' }
$Destination = [IO.Path]::GetFullPath($Destination)
$packagePrefix = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\'
if ($Destination.StartsWith($packagePrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Choose an installation folder outside the extracted setup package.' }
if ($RuntimeSource) {
    $sourcePrefix = [IO.Path]::GetFullPath($RuntimeSource).TrimEnd('\') + '\'
    if (($Destination.TrimEnd('\') + '\').StartsWith($sourcePrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'The installation folder must be outside the runtime source.' }
}
if (Test-Path -LiteralPath $Destination) {
    if (@(Get-ChildItem -LiteralPath $Destination -Force).Count -gt 0) { throw 'Choose an empty installation folder. Existing installations and private data are never overwritten.' }
}
[void](Confirm-Step 'Review third-party license obligations' 'Read https://developer.android.com/studio/terms and the repository THIRD_PARTY_NOTICES.md / docs/LICENSING_REVIEW.md. Google SDK/Play image permitted use remains unresolved for general end-user distribution. Proceed only if you have reviewed the applicable terms and are authorized to use these components. This project does not grant third-party licenses or accept terms on your behalf. Download: 0 bytes.')
[void](Confirm-Step 'Verify and install the launcher' "Download: 0 bytes. Bundled launcher files: $(Format-Size $payloadBytes). Verify their checksums, create the destination folder, and copy the launcher. Existing Android data is never copied.")
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
# Compress newly deployed files on NTFS; unsupported filesystems simply ignore this.
& (Join-Path $env:SystemRoot 'System32\compact.exe') /C /I /Q $Destination | Out-Null
$payload = Join-Path $PSScriptRoot 'app'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'payload-sha256.json') -Raw | ConvertFrom-Json
foreach ($item in $manifest) {
    $file = Join-Path $PSScriptRoot $item.path
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $item.sha256) { throw "Installer payload verification failed: $($item.path)" }
}
Copy-Item -LiteralPath $payload -Destination (Join-Path $Destination 'app-staging') -Recurse
Get-ChildItem -LiteralPath (Join-Path $Destination 'app-staging') | Move-Item -Destination $Destination
Remove-Item -LiteralPath (Join-Path $Destination 'app-staging')
$runtime = Join-Path $Destination 'runtime'
$cache = $null
if ($RuntimeSource) {
    $RuntimeSource = [IO.Path]::GetFullPath($RuntimeSource)
    foreach ($required in @('sdk\emulator\emulator.exe','sdk\platform-tools\adb.exe','sdk\system-images\android-30\google_apis_playstore\x86_64\system.img')) {
        if (-not (Test-Path -LiteralPath (Join-Path $RuntimeSource $required))) { throw "Incomplete runtime source: $required" }
    }
    $copyBytes = 0L
    foreach ($folder in @('sdk','screen')) { $copyBytes += (Get-ChildItem -LiteralPath (Join-Path $RuntimeSource $folder) -File -Recurse | Measure-Object Length -Sum).Sum }
    [void](Confirm-Step 'Copy clean Android runtime' "Download: 0 bytes. Files to copy: $(Format-Size $copyBytes), before filesystem compression. Only SDK and display components are copied; no accounts or app data.")
    Write-Host 'Copying clean runtime components (no Android user data)…' 
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    foreach ($folder in @('sdk','screen')) { Copy-Item -LiteralPath (Join-Path $RuntimeSource $folder) -Destination (Join-Path $runtime $folder) -Recurse }
} else {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $cache = Join-Path $PSScriptRoot ('Astro-Portal-temp-' + [Guid]::NewGuid().ToString('N'))
    Write-Host "Temporary downloads: $cache (removed when installation succeeds)."
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $packages = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Raw | ConvertFrom-Json
    foreach ($package in $packages) {
        $url = [Uri]$package.url
        if ($url.Scheme -ne 'https' -or $url.Host -notin @('dl.google.com','github.com')) { throw 'Unsupported dependency source.' }
        $archive = Join-Path $cache ([IO.Path]::GetFileName($url.AbsolutePath))
        $installedEstimate = switch ($package.package) {
            'platform-tools' { 'about 40 MiB' }
            'emulator' { 'about 1.2 GiB' }
            'scrcpy' { 'about 35 MiB' }
            default { 'about 4 GiB, plus several GiB of user data created on first boot' }
        }
        [void](Confirm-Step "Download and install $($package.package)" "Source: $($url.Host)`nDownload: $(Format-Size $package.bytes). Installed space estimate: $installedEstimate (actual filesystem usage varies). The archive is checksum-verified, extracted, then deleted. This is a required component; No stops installation.")
        Write-Host "Downloading $($package.package)…"
        [void](Assert-FreeSpace $cache ([long]$package.bytes + 512MB))
        $client = New-Object Net.WebClient
        try { $client.DownloadFile($url, $archive) } finally { $client.Dispose() }
        if ((Get-Item -LiteralPath $archive).Length -ne $package.bytes) { throw 'Incomplete runtime download.' }
        if ($package.digest) { $algorithm,$expected = $package.digest.Split(':',2) } else { $algorithm = 'SHA1'; $expected = $package.checksum }
        $expected = $expected.PadLeft($(if ($algorithm -eq 'SHA1') {40} else {64}), '0')
        if ((Get-FileHash -LiteralPath $archive -Algorithm $algorithm).Hash -ne $expected) { throw 'Runtime archive checksum mismatch.' }
        if ($package.package.StartsWith('system-images;')) {
            $parts = $package.package.Split(';'); $extract = Join-Path $runtime "sdk\system-images\$($parts[1])\$($parts[2])"
        } elseif ($package.package -eq 'scrcpy') { $extract = Join-Path $runtime 'screen' }
        else { $extract = Join-Path $runtime 'sdk' }
        New-Item -ItemType Directory -Path $extract -Force | Out-Null
        $prefix = [IO.Path]::GetFullPath($extract).TrimEnd('\') + '\'
        $zip = [IO.Compression.ZipFile]::OpenRead($archive)
        try {
            $expandedBytes = [long](($zip.Entries | Measure-Object Length -Sum).Sum)
            [void](Assert-FreeSpace $Destination ($expandedBytes + 512MB))
            Write-Host "Unpacked files: $(Format-Size $expandedBytes). Destination: $extract"
            foreach ($entry in $zip.Entries) {
                $full = [IO.Path]::GetFullPath((Join-Path $extract $entry.FullName))
                if (-not $full.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path.' }
            }
        } finally { $zip.Dispose() }
        try { [IO.Compression.ZipFile]::ExtractToDirectory($archive,$extract) }
        catch { throw "Could not extract $($package.package) into $extract. Available space: $(Format-Size (Get-FreeSpace $Destination)). Completed files remain in $Destination; temporary downloads remain in $cache. Use a new empty destination if retrying. Details: $($_.Exception.Message)" }
        Remove-Item -LiteralPath $archive
    }
}
if ($CompactImages -and (Confirm-Step 'Compress Android images' 'Download: 0 bytes. Compress existing system/vendor images to reduce disk use. This may take several minutes; space savings vary.' -Optional)) {
    Write-Host 'Optimizing immutable Android image storage (may take several minutes)…'
    foreach ($image in @('system.img','vendor.img')) {
        $imagePath = Join-Path $runtime "sdk\system-images\android-30\google_apis_playstore\x86_64\$image"
        if (Test-Path -LiteralPath $imagePath) { & (Join-Path $env:SystemRoot 'System32\compact.exe') /C /F /EXE:LZX $imagePath | Out-Null }
    }
}
[void](Confirm-Step 'Create fresh Android settings' 'Download: 0 bytes. Copy the bundled small Android home helper and write fresh local settings. Android app data will be created on first boot. No accounts, passwords or personal data are bundled.')
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ASIAIR-Home.apk') -Destination (Join-Path $runtime 'ASIAIR-Home.apk')
# Fresh settings only. No profile, accounts, credentials, location or app APKs are copied.
@{
    ApkFolder=''; DeviceIp='192.168.4.1'; EnablePlayStore=$true; AndroidApi=30;
    AutoStartAndroid=$false; AutomaticUpdateChecks=$true; UseWindowsLocation=$true;
    MemoryMb=4096; CpuCores=4; ConsolePort=5560; AdbPort=5039
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Destination 'container.json') -Encoding UTF8
if (-not $NoShortcut -and (Confirm-Step 'Create desktop shortcut' 'Download: 0 bytes. Create or replace the Astro - Portal shortcut on your desktop (a few KiB).' -Optional)) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Astro - Portal.lnk'))
    $shortcut.TargetPath = Join-Path $Destination 'Astro-Portal.exe'; $shortcut.WorkingDirectory = $Destination; $shortcut.Save()
}
if ($cache -and (Test-Path -LiteralPath $cache)) {
    # Only remove our own empty download directory; never delete the setup folder.
    if (@(Get-ChildItem -LiteralPath $cache -Force).Count -eq 0) {
        Remove-Item -LiteralPath $cache
        Write-Host 'Temporary download folder removed.'
    } else {
        Write-Host "Temporary files remain in $cache; installation did not remove unexpected files."
    }
}
Write-Host "Installed Astro - Portal to $Destination"
Write-Host 'Windows Hypervisor Platform and hardware virtualization must be enabled. A restart is required after first enabling the Windows feature.'
Write-Host 'Open the launcher and select Install ASIAIR / Install Stellavita. Google Play is under Advanced.'
if (-not $NoLaunch -and (Confirm-Step 'Open Astro - Portal' 'Download: 0 bytes at this step. Open the launcher with Android auto-start disabled. App installation downloads happen later when you choose Install ASIAIR or Install Stellavita.' -Optional)) { Start-Process -FilePath (Join-Path $Destination 'Astro-Portal.exe') -WorkingDirectory $Destination -WindowStyle Normal }
