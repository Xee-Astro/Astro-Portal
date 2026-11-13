# Architecture and handoff

The WPF shell controls a private Android 11 main profile and, on first clone launch, a second independent profile. Immutable Android SDK/scrcpy files are shared. Mutable second-profile state is stored under `private-data/profiles/second`; original state stays in place. Separate console and ADB ports prevent collisions. The second profile uses up to 3 GiB additional guest RAM.

Each astronomy app owns a scrcpy virtual display (1600 × 1000 landscape). The main Android display supplies the Other pane. Five owned native Windows surfaces preserve keyboard/mouse handling and aspect ratio. WPF arranges selected panes in a grid. Hiding a pane hides its surface without terminating the Android app; × force-stops only that package in its own profile. The app launch buttons select a single fullscreen pane. Split toggles choose the visible set. Badge double-click zooms a pane; leaving fullscreen restores the previous set.

`RuntimeVirtualDisplay.cs` manages sessions and signed APK transfer to the clone profile. APK copying never copies app data or accounts. Each app's first and second copies run in different Android systems; ASIAIR and Stellavita within one system share its Android account/services. Font and GPS preferences are broadcast to both profiles. Higher historical font scales migrate to 130%, the Android image's accepted maximum.

`MainWindow.MultiWorkspace.cs` owns pane selection and clone lifecycle. `WorkspacePanes.cs` supplies pure grid placement. Existing non-waiting transition guards reject concurrent requests; native input and controls are disabled during transitions. A per-installation session blocks duplicate launchers.

ASIAIR's main AirActivity uses caption-free fullscreen within its display. Portrait-only connection screens use landscape compatibility mode. Periodic read-only checks request normalization only when needed, avoiding continuous focus changes. Rotation remains locked. Vendor app lifecycle behavior still requires real equipment testing.

`work/container-home` is the original shell-only Java layout helper; `work/container-checks` contains offline checks. Interactive installer/patch scripts live in `work/portal-installer`.

Notes use local atomic JSON; passwords are encrypted with Windows DPAPI CurrentUser. Never package private-data, downloaded vendor APKs, personal configuration or keys. Runtime logs may contain device information. Equipment validation and clarification of third-party permitted use remain outstanding.

