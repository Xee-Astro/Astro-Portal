# Installation and troubleshooting

## Before setup

Use a supported Windows x64 PC with virtualization enabled. Enable **Windows Hypervisor Platform** under **Turn Windows features on or off**, then restart when Windows requests it. Check the official [Android emulator acceleration guide](https://developer.android.com/studio/run/emulator-acceleration).

Review applicable Google, Microsoft, scrcpy and vendor licenses first. The project's MIT license does not grant permission to use or redistribute those dependencies.

## Setup behavior

`Install.cmd` launches Windows PowerShell 5 with a process-only execution-policy override. It does not change the system execution policy. It asks for the destination, shows a numbered plan, and requires an explicit Yes/No before installation stages. No cancels a required stage; optional shortcuts, compression and launch can be skipped.

Defaults: installation under `%LOCALAPPDATA%\Astro-Portal`, Android auto-start disabled, private ADB port 5039 and emulator ports 5560/5561. The destination must be empty; existing data is never overwritten. Use a local drive suitable for emulator storage.

Downloads are checked against pinned sizes/checksums. Runtime archive sources are Google and the official Genymobile GitHub releases. The Google image contains Google Play services and is not a freely sublicensable project asset.

## Storage and temporary files

Setup checks at least 16 GiB on the **destination**, not the drive where setup was opened. It also checks room for temporary downloads beside the setup script. A unique `Astro-Portal-temp-...` folder is created there; downloaded ZIPs are removed after successful extraction and the empty temporary folder is removed after setup succeeds. The extracted setup package remains.

Archive unpacked sizes are checked before extraction. Compression may reduce actual disk use but is not relied on to bypass space checks. Extra Android user data is created on first boot and grows with apps/data.

If interrupted or cancelled, completed files and temporary downloads may remain. Setup does not roll back or delete user data. Inspect the reported paths, preserve any personal profile, then retry into a new empty destination.

## Not enough disk space

Opening setup on Z: does not imply installation on Z:. Choose a destination on a drive with sufficient free space. The installer reports destination, available space and needed space. Keep enough space on both the setup drive and destination drive.

## Apps and location

Use the launcher's install controls for vendor-signed apps from their official sources. App sizes change; the previous tested downloads were approximately 1.17 GiB for ASIAIR and 0.59 GiB for Stellavita. App-install downloads are separate from runtime downloads. Google Play sign-in is performed by the user inside Android.

Windows location depends on the PC's location permission/service and may be coarse. Manual coordinates are the fallback. Phone location uses the supplied TCP address/port and NMEA GGA/RMC data. Keep phone GPS on a trusted network.

## Data, backup and removal

Back up `private-data` with the launcher fully closed. It contains Android data, accounts and local configuration. Passwords in Notes are protected for the current Windows account and must be entered again on another PC. Do not publish profiles or logs without reviewing them.

There is no in-place upgrade/repair installer yet. Do not overwrite an active installation with a fresh setup. To remove a copy, close it and remove only its own installation folder and shortcut after backing up wanted data.
