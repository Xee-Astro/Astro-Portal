> Before any SDK download or binary distribution, review [the licensing review](../docs/LICENSING_REVIEW.md). This source snapshot is not a vendor-approved end-user distribution.

# Build Astro - Portal 0.5

Windows x64, .NET 10 SDK, Java JDK (tested with 25), Python 3 and Windows Hypervisor Platform are required. Runtime dependencies and notices are pinned in `work/android-container/dependencies.json`.

1. Run `python work/download_container_runtime.py` to download and verify the official Android emulator, Android 11 Google Play image, platform tools and scrcpy into `outputs/ASIAIR-Container/runtime`.
2. Run `python work/fetch_android_build.py`, then `python work/build_container_home.py` to build the shell-classpath layout helper. It is pushed to Android as a classpath APK, not installed as an Android application.
3. Build the self-contained portable launcher:
   `dotnet publish work/container-host/AsiairContainer.csproj -c Release -r win-x64 --self-contained true -o outputs/ASIAIR-Container`
4. Build the self-contained clean-installer payload:
   `dotnet publish work/container-host/AsiairContainer.csproj -c Release -r win-x64 --self-contained true -o work/portal-selfcontained`
5. Run `python work/build_portal_installer.py` to create `outputs/Astro-Portal-Setup.zip`. It includes only the self-contained launcher, helper, dependency manifest and installer scripts. Android dependencies and official app APKs are downloaded on the target PC. No private-data, accounts, passwords, signing keys or original APK set is packaged.
6. Run `dotnet run --project work/container-checks -c Release` for offline checks. Windows DPAPI checks need the actual Windows user profile. Official APK identity checks run additionally when the two downloaded vendor APKs exist in `work/android-container/downloads/`.

UI checks: `AsiairContainer.exe --verify-ui --root <empty-test-root>` and `--verify-contrast` render the dashboard, Notes, Advanced and theme states. Set `AutomaticUpdateChecks` and `AutoStartAndroid` false in that test root's container.json.

Runtime checks: `--verify-portal --install-asiair <official-ASIAIR.apk> --install-stellavita <official-Stellavita.apk>` performs native installation, tablet rotation/display checks, discarded overlapping commands, split/full-screen switching, PID preservation, running indicators, specific-app termination and Windows keyboard input. Use a separate fresh test root with the runtime folder, not a real observing session. `work/verify_portal_fresh.py` creates that isolated root using hard links to immutable runtime dependencies and copies the launcher. No app data is copied.

Architecture: WPF desktop shell + owned native scrcpy surface + isolated Android emulator. App mode changes use a shell-only Java helper. A non-waiting layout lock rejects concurrent transitions. A named per-installation session prevents duplicate launchers; repeat launches activate the existing window. Single apps use 2560×1600 (16:10); split screen uses 2560×800. Display height changes inside the non-queued layout transition while rotation remains locked. Native x86-64 vendor APKs replace ARM-only packages with Android's data-preserving update mechanism. APK downloads use official endpoints, a constrained HTTPS redirect list and pinned vendor signing certificates; Android verifies the full signatures during installation.

Notes persist with atomic JSON replacement; passwords use Windows DPAPI CurrentUser. Never log note contents or package private-data with the installer. Physical camera/mount/firmware actions remain outside automated checks.
