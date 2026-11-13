# Build Astro - Portal 0.6.0

Review [third-party obligations](docs/LICENSING_REVIEW.md) before SDK downloads or distribution. Windows x64, .NET 10 SDK (self-contained runtime 10.0.8), Java JDK, Python 3 and Windows Hypervisor Platform are required.

1. Run `python work/download_container_runtime.py` to obtain verified runtime components, subject to upstream terms.
2. Run `python work/fetch_android_build.py`, then `python work/build_container_home.py` to build the original layout helper.
3. Publish and package:

```powershell
dotnet publish work/container-host/AsiairContainer.csproj -c Release -r win-x64 --self-contained true -o work/portal-060
python work/build_portal_060.py
```

The builder produces the full setup and five-file patch in `outputs`, with SHA-256 sidecars. It requires .NET and WPF LICENSE/THIRD-PARTY-NOTICES from the runtime NuGet packages (or existing locally prepared license files). The WPF notice is available from the official dotnet/wpf v10.0.8 source. The full setup contains the launcher runtime, original helper and scripts; Android and vendor APKs are separate downloads. No user profiles, accounts or vendor signing keys belong in releases.

The patch supports self-contained 0.5.0 and 0.5.1 installations. It checks manifest hashes, running processes and file locks; backs up originals and attempts rollback after failure. It does not replace configuration or private-data. Apply into the existing installation, not an empty folder.

## Verification

- `dotnet run --project work/container-checks -c Release`: options, updates, downloads, encrypted notes, phone GPS, clone state and pane layout. DPAPI requires the Windows user profile. Vendor APK identity tests additionally use locally obtained official APKs; APKs are never published.
- `AsiairContainer.exe --verify-ui --root <isolated-empty-root>`: dashboard, clone controls, Notes, Advanced and font boundaries. Set AutoStartAndroid, AutomaticUpdateChecks and UseWindowsLocation false.
- `--verify-contrast`: existing theme contrast checks.
- `--verify-displays --root <isolated-runtime-root>`: independent app displays.
- `--verify-multi --root <isolated-runtime-root>`: four instances, isolated persistent profile markers, two/three/four/five selected panes, hidden-session preservation, pane zoom/restore, rejected overlapping actions, font 130% in both profiles, simulated shared GPS and per-instance termination.

Runtime checks require a separate runtime-backed root with both official apps installed and dedicated console/ADB ports. Do not use an observing session. Clone first boot needs substantial RAM/disk space. Hardware camera/mount/firmware operations are outside these checks.

The older `--verify-portal` diagnostic exercises the legacy Android split-task layout, which the new pane workspace replaces; use `--verify-multi` for 0.6.0 acceptance.

