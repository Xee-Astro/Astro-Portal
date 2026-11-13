# Astro - Portal 0.5 â€” clean Windows installer

Extract the ZIP to a folder and run **Install.cmd**. Setup shows a numbered plan, then requires an explicit **Yes** or **No** before every installation stage and each component download. Download sizes come from the pinned dependency manifest; installed disk sizes are estimates. **No** on a required stage stops setup, leaving already completed files in place; use a new empty folder when restarting. **No** on shortcut, compression or launch skips that optional action. Pressing Enter never grants permission. BIOS virtualization and Windows Hypervisor Platform remain manual prerequisites. The installer downloads verified official Android and scrcpy runtime archives (about 1.9 GB), deploys a fresh profile, and creates a desktop shortcut. Setup asks which installation folder to use and checks that its drive has at least 16 GiB free before copying files or downloading anything. Launching setup from Z: does not automatically install onto Z:. Each archive is checked against available space again before extraction, using its actual unpacked size. An interrupted attempt leaves partial files in its chosen destination; retry into a new empty folder. Windows 10/11 x64, hardware virtualization and **Windows Hypervisor Platform** are required. Enable that Windows feature and restart if needed. The launcher includes its own .NET runtime.

Your existing Android accounts, notes, passwords, location and app data are never included. The installer refuses to overwrite a populated destination. Choose **Install ASIAIR** and **Install Stellavita** in the launcher to download and install vendor-signed x86-64 APKs. Internet access is required for those installations. Google Play sign-in is optional and stays inside Android.

The default destination is `%LOCALAPPDATA%\Astro-Portal`. Advanced installation from PowerShell:

```powershell
.\Install-Astro-Portal.ps1 -Destination 'D:\Astro-Portal' -RuntimeSource 'D:\ASIAIR-Container\runtime'
```

`-RuntimeSource` copies only `sdk` and `screen` from an existing trusted runtime folder, avoiding dependency downloads. It never copies `private-data` or an existing configuration. The launcher/helper payload is checked against its SHA-256 manifest. Downloaded dependency archives are checked against the pinned official manifest. Keep vendor license/NOTICE files with the runtime. Review the [Android SDK terms](https://developer.android.com/studio/terms) and the bundled scrcpy license before use.

Android keeps Google Play, WebView, location, keyboard, permission handling and backup services. Only optional Maps/YouTube/music/video packages, if present, are disabled. That reduces background activity; it does not remove bytes from the read-only system image. NTFS compression of fresh userdata saves physical storage without deleting system components. Add `-CompactImages` for stronger compression of immutable images; this optional step can take several minutes and should finish before running Android. Cold boot, no snapshots, no camera/audio/sd-card emulation, and automatic download cleanup reduce working storage and startup overhead. The image still requires several GB; claiming a tiny Android runtime would be misleading.

Notes are saved locally. Passwords are encrypted for the current Windows account; copying notes.json to another PC will not transfer password access. Re-enter passwords there. App settings remain in `private-data`. To remove an installation, close it and remove its folder and desktop shortcut; back up private-data first if you want to keep it.

Temporary downloads use a uniquely named `Astro-Portal-temp-â€¦` folder beside `Install.cmd`, on the drive where setup was extracted. That folder is deleted after successful installation. The setup location must be writable and have enough space for the largest download (about 1.3 GiB plus a safety margin). The destination drive is checked separately for installation/extraction space. Interrupted installations may retain their temporary folder for inspection. The extracted setup package itself is kept.

## Clones and workspace

Clone checkboxes reveal ASIAIR 2 / Stellavita 2. A second persistent Android profile starts only when a clone is launched. It copies installed APKs, without copying accounts or app data. Allow 8-12 GiB additional free disk space and up to 3 GiB additional guest RAM; 16 GiB PC RAM is recommended for four apps. Split screen lets you toggle the four astronomy panes and Other (main Android); hidden panes keep running. Double-click a pane badge for fullscreen; exit restores your selection. Android font size is limited to 80%-130%, shared across both profiles with location.

Existing 0.5.0/0.5.1 users should use the small 0.6.0 patch instead of installing over their populated folder. The original blue portal icon is included under the project MIT license.

The supplied splash remains visible while Android is stopped. Both portals rotate within their projected planes. During boot, ASTRO is drawn into the upper ring, completing only when Android is ready. Failed or cancelled starts restore the splash. Windows animation preferences are respected.
