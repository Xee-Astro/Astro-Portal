# Astro - Portal

Experimental Windows launcher for the **original ASIAIR and Stellavita Android apps**, using a dedicated Android emulator and an embedded scrcpy display. This project is independent of ZWO, ToupTek, Google, Microsoft, and Genymobile.

**Source and experimental installer.** [Download the Windows installer ZIP](https://github.com/Xee-Astro/Astro-Portal/releases/tag/v0.6.0-preview). No vendor APKs, Google Play images, Android emulator runtime, personal profiles, or private signing keys are bundled in the installer. The launcher includes its own .NET runtime. The Android SDK has restrictions on redistribution and permitted use. This project has **not** received vendor approval or a legal compliance certification. Read [third-party notices](THIRD_PARTY_NOTICES.md) and the [licensing review](docs/LICENSING_REVIEW.md) before building, downloading dependencies, or distributing a package.

## Version 0.6.0

Download the [0.6.0 patch or full installer](https://github.com/Xee-Astro/Astro-Portal/releases/tag/v0.6.0-preview). Existing 0.5.0/0.5.1 users: close the launcher and Android, extract the patch elsewhere, run `Apply-Patch.cmd`, and select your existing installation folder. It backs up the five replaced launcher files and preserves Android apps, accounts, settings and notes.

Clone checkboxes reveal **ASIAIR 2** and **Stellavita 2**, using an independent persistent second Android profile. Split screen offers toggles for four astronomy instances plus **Other** (main Android). Hidden panes keep running. Double-click a pane badge to zoom full screen; exit restores the chosen panes. The executable and taskbar use an original generated blue portal icon. The supplied ASTRO/PORTAL splash appears while stopped, with both portals rotating within their own projected planes; during boot the ASTRO lettering is drawn into the ring and is completely consumed only when Android is ready. Failed or cancelled startup restores it. General accents match the ice-blue/navy artwork, while astronomy app colors stay red/blue.

Font controls now support **80%–130%**, the range accepted by this Android image. Unsupported higher saved values migrate to 130%. Both profiles share font and location settings. Vendor text drawn at fixed sizes may ignore Android font settings.

## Features

- Red ASIAIR and blue Stellavita launch/install controls; yellow running indicators and per-app termination.
- Independent 1600 × 1000 landscape app displays with aspect-preserving desktop panes; selectable one-to-five-pane workspace.
- Caption-free ASIAIR main screen, avoiding obstruction of its camera/mount toolbar. Portrait-only connection screens use a compatibility window.
- Windows keyboard/mouse input; overlapping layout commands are rejected rather than queued.
- Official-source app installation/update checks, Google Play access, and manual APK installation.
- Location from Windows, manual coordinates, or a TCP/NMEA phone GPS source; shared with both apps.
- Persistent Android data and local notes; passwords encrypted for the current Windows account.

## Requirements

| Item | Requirement |
| --- | --- |
| Operating system | Windows 10/11, Intel/AMD x64; Windows on ARM is not validated |
| Virtualization | Hardware virtualization enabled in BIOS/UEFI and Windows Hypervisor Platform enabled; restart after enabling |
| Disk space | At least 16 GiB free for initial setup; allow 8–12 GiB extra for the second profile/apps |
| Setup location | Writable, with roughly 2 GiB available for the largest temporary download |
| Internet | About 1.75 GiB runtime downloads, plus app downloads; sizes vary by version |
| Memory | Main guest: 4 GiB; second profile: up to 3 GiB additional. 16 GiB PC RAM recommended for four apps |
| Equipment | A reachable ASIAIR/Stellavita device for actual equipment control |

The built launcher includes .NET. End users do not need to separately install Java, Python, Android Studio, or .NET. Building from source requires additional tools listed in [BUILDING.md](BUILDING.md).

## Get started

1. Review the [licensing review](docs/LICENSING_REVIEW.md), [prerequisites and setup](docs/INSTALLATION.md), and vendor terms.
2. Download this repository using **Code → Download ZIP**, or clone it.
3. Download `Astro-Portal-Setup-0.6.0.zip` from the [experimental release](https://github.com/Xee-Astro/Astro-Portal/releases/tag/v0.5.0-preview), or follow [BUILDING.md](BUILDING.md) to build it. The installer downloads Android separately; third-party permitted-use questions remain unresolved.
4. Extract the setup ZIP and run `Install.cmd`. Setup shows its plan and asks Yes/No for each stage.
5. Choose an **empty installation destination**. Setup files and installation destination are separate locations.
6. Use **Install ASIAIR** / **Install Stellavita** in the launcher, subject to vendor terms. Google Play and Install APK are under Advanced.

Temporary setup downloads use a uniquely named folder beside `Install.cmd`, removed after successful installation. Failed installations may leave partial downloads and files for inspection. Setup does not enable BIOS virtualization or Windows features automatically.

## Documentation

- [Installation, downloads, cleanup and troubleshooting](docs/INSTALLATION.md)
- [Build and developer verification](BUILDING.md)
- [Architecture and project handoff](docs/ARCHITECTURE.md)
- [Privacy and local data](PRIVACY.md)
- [Security reporting](SECURITY.md)
- [Third-party licenses, trademarks and official links](THIRD_PARTY_NOTICES.md)
- [Copyright/licensing review and unresolved questions](docs/LICENSING_REVIEW.md)
- [Contributing](CONTRIBUTING.md)

## Known limitations

Use one launcher installation at a time. Clone mode allocates distinct emulator/ADB ports and shares immutable runtime files. The two main apps share the original profile; the two second copies share the independent second profile. App settings/accounts remain isolated between first and second copies. Clone checkboxes hide/show launch controls without erasing data. Launching clones copies installed APKs from the main profile and may take several minutes initially.

Physical camera capture, guiding, mount movement, firmware updates, and physical phone GPS have not been validated. Simulated GPS forwarding and Windows input were tested. A larger desktop display does not guarantee every vendor screen scales correctly. Non-Latin text entry depends on the Android input method. No promise of compatibility with future app releases is made.

The 0.6.0 development environment passed 81 offline checks. Runtime verification covers isolated clone state, five panes, selection, fullscreen restore, command blocking, shared font/location and individual termination. Earlier releases also passed 39 contrast checks and Windows input/ASIAIR toolbar checks. Some offline checks require locally obtained vendor APKs and are skipped when those APKs are absent. These counts are historical, not claims about every PC or a public CI run.

## License

Original project code and the original AI-generated portal icon are licensed under [MIT](LICENSE). That license does **not** cover third-party software, trademarks, vendor apps, Google services or Android images. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

