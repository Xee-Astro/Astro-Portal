# Architecture and handoff

The WPF shell controls a private Android 11 emulator. An owned native scrcpy window displays Android and handles keyboard/mouse input. ADB and a small original Java helper manage Android tasks, layouts and location.

`work/container-host/` contains the C# launcher. `work/container-home/` contains the Java helper/test activities. `work/container-checks/` contains offline checks and small synthetic fixtures. `work/portal-installer/` contains interactive setup. `work/android-container/dependencies.json` pins upstream download metadata. Python scripts build the helper and assemble setup.

Single apps use 2560×1600. Split screen uses 2560×800. Landscape orientation stays locked while display height changes inside guarded transitions. Layout and lifecycle locks reject overlapping requests. A per-installation session prevents duplicate controllers for the same profile, but different installations still share default ports.

ASIAIR's main `AirActivity` uses true fullscreen to remove Android's freeform caption and unblock its top toolbar. Portrait-only connection screens use freeform compatibility. The dashboard periodically detects entry into the main screen and normalizes it through the guarded transition path.

The launcher uses original, signed vendor APKs with native x86-64 libraries. It does not patch APK signatures or use extracted vendor authentication keys. Android's package update mechanism preserves app data during native-ABI migration. Display/input reconnect after APK installation handles observed Android service restarts.

Data is kept locally. Notes use atomic JSON writes; passwords use Windows DPAPI CurrentUser. Android accounts remain in the private guest profile. Runtime logs are diagnostic data and may contain device information.

Next priorities: independent profile/port allocation for multiple simultaneous ASIAIR instances; equipment testing; safer in-place upgrades; resolution/layout compatibility across vendor releases; and clarification of SDK/image/vendor permissions before general end-user distribution.
