# Architecture and handoff

The WPF launcher controls one private Android 11 profile. Each astronomy app owns a scrcpy virtual display (1600 × 1000 landscape); the main Android display supplies Other. Three owned native Windows surfaces retain keyboard/mouse handling and aspect ratio. The grid supports one, two or three selected panes. Hiding panes keeps apps running. Badge double-click zooms one pane; leaving fullscreen restores the prior selection.

0.6.1 removes the 0.6.0 clone checkboxes, numbered app buttons, clone port allocation and second-profile controller/installation paths. Existing `private-data/profiles/second` is left untouched. Legacy CloneAsiair/CloneStellavita config fields are ignored; supported settings continue to load normally. There is no automatic second profile creation or migration of its accounts into the main profile.

`PortalSplash.cs` draws the supplied static `Assets/Splash.png` with aspect ratio preserved. It has no timer, rotation, progress animation or consumption layers. Both stopped and boot states display identical artwork; the boot overlay adds a separate readable status caption. The art is hidden when Android is ready and restored after shutdown or startup failure.

`MainWindow.MultiWorkspace.cs` manages selection and per-app display lifecycle; `WorkspacePanes.cs` handles grid placement. Non-waiting command guards reject overlapping transitions and block native input during operations. Home explicitly targets Android display 0. Back targets the selected/last-focused pane. Font and GPS apply to the single Android profile. Font range remains 80%–130%.

ASIAIR main AirActivity uses caption-free fullscreen in its virtual display. Connection screens use landscape compatibility mode. Read-only periodic checks normalize layout only when needed. `RuntimeVirtualDisplay.cs` owns display processes; the second-profile APK-copy path was removed.

The original Java layout helper is under `work/container-home`; offline checks are under `work/container-checks`. `--verify-multi` now verifies two apps and three panes. `--verify-ui` checks that old clone flags cannot create UI controls and that splash pixels stay unchanged over time and across stopped/loading states.

Notes use local atomic JSON; passwords use Windows DPAPI CurrentUser. Runtime logs may contain device information. Never package user profiles, accounts, vendor APKs or signing keys. Hardware equipment validation and unresolved upstream permitted-use questions remain separate from launcher checks.

