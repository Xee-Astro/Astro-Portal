# Astro - Portal

Experimental Windows launcher for the **original ASIAIR and Stellavita Android apps**, using a dedicated Android emulator and an embedded scrcpy display. This project is independent of ZWO, ToupTek, Google, Microsoft, and Genymobile.

**Source and experimental installer.** [Download the Windows installer ZIP](https://github.com/Xee-Astro/Astro-Portal/releases/tag/v0.5.0-preview). No vendor APKs, Google Play images, Android emulator runtime, personal profiles, or private signing keys are bundled in the installer. The launcher includes its own .NET runtime. The Android SDK has restrictions on redistribution and permitted use. This project has **not** received vendor approval or a legal compliance certification. Read [third-party notices](THIRD_PARTY_NOTICES.md) and the [licensing review](docs/LICENSING_REVIEW.md) before building, downloading dependencies, or distributing a package.

## Version 0.5.1 patch

Already running 0.5.0? Download the [small 0.5.1 patch](https://github.com/Xee-Astro/Astro-Portal/releases/tag/v0.5.1-preview), close the launcher and Android, extract the patch elsewhere, run `Apply-Patch.cmd`, and choose your existing installation folder. It backs up the old launcher and preserves Android, apps, accounts, settings and notes. No Android download is required.

New: dashboard Android font **A− / A+**, percentage and Reset controls (80%–200%, 10% steps), applied live or saved for the next boot. Both apps share the setting; app text drawn at a fixed size may not follow it.

## Features

- Red ASIAIR and blue Stellavita launch/install controls; yellow running indicators and per-app termination.
- Single-app 2560 × 1600 landscape view; 2560 × 800 split-screen for both astronomy apps.
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
| Disk space | At least 16 GiB free on the destination at setup; additional space as Android data grows |
| Setup location | Writable, with roughly 2 GiB available for the largest temporary download |
| Internet | About 1.75 GiB runtime downloads, plus app downloads; sizes vary by version |
| Memory | Guest configured for 4 GiB and four virtual CPU cores; allow additional memory for Windows |
| Equipment | A reachable ASIAIR/Stellavita device for actual equipment control |

The built launcher includes .NET. End users do not need to separately install Java, Python, Android Studio, or .NET. Building from source requires additional tools listed in [BUILDING.md](BUILDING.md).

## Get started

1. Review the [licensing review](docs/LICENSING_REVIEW.md), [prerequisites and setup](docs/INSTALLATION.md), and vendor terms.
2. Download this repository using **Code → Download ZIP**, or clone it.
3. Download `Astro-Portal-Setup.zip` from the [experimental release](https://github.com/Xee-Astro/Astro-Portal/releases/tag/v0.5.0-preview), or follow [BUILDING.md](BUILDING.md) to build it. The installer downloads Android separately; third-party permitted-use questions remain unresolved.
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

Only one installation should run at a time: default ADB/emulator ports are shared. Two simultaneous ASIAIR copies need independent profiles and distinct ports; automatic multi-instance support is not implemented.

Physical camera capture, guiding, mount movement, firmware updates, and physical phone GPS have not been validated. Simulated GPS forwarding and Windows input were tested. A larger desktop display does not guarantee every vendor screen scales correctly. Non-Latin text entry depends on the Android input method. No promise of compatibility with future app releases is made.

The original development environment passed 63 offline checks and 39 contrast checks, plus runtime tests for switching, process preservation, keyboard input and the ASIAIR toolbar. Some offline checks require locally obtained vendor APKs and are skipped when those APKs are absent. These counts are historical, not claims about every PC or a public CI run.

## License

Original project code is licensed under [MIT](LICENSE). That license does **not** cover third-party software, trademarks, vendor apps, Google services or Android images. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
