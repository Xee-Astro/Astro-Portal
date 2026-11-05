# Copyright and licensing review

Review date: 2026-11-05. This is an engineering review of the files prepared for publication, not a legal opinion or a guarantee of non-infringement.

## Publication scope and changes

- Publish original C#/Java/Python/PowerShell source, synthetic fixtures and documentation only.
- Exclude vendor APKs, Google/Android images, downloaded runtimes, Microsoft runtime binaries, account data, device profiles, credentials, private keys, logs, screenshots and extracted/decompiled vendor code.
- Replace the captured vendor HTML download page and copied Stellavita changelog with minimal synthetic fixtures.
- Keep vendor names only for compatibility identification; do not include their logos or product artwork.
- The original source has an MIT license. It does not sublicense third-party applications, SDKs, services, images or marks.

## Important unresolved issues

The [Android SDK License Agreement](https://developer.android.com/studio/terms), sections 3.1–3.4, restricts permitted purposes and redistribution; section 3.5 treats open-source components separately. Its use limitations are relevant to presenting a development emulator as a general-purpose end-user app container. Downloading components directly from Google instead of bundling them does **not** by itself resolve permitted-use restrictions. We have not obtained Google approval for this use or concluded that it is covered.

The [Google Play image documentation](https://developer.android.com/studio/run/managing-avds) explains emulator images with Google Play access; it does not grant this project an independent license to redistribute Google's proprietary components or claim certification.

[ZWO's public download page](https://www.zwoastro.com/software/asiair/) and [ToupTek's download center](https://www.touptek-astro.com/downloads/) establish official sources. They do not establish permission to redistribute APKs or blanket permission for emulator usage. Applicable app agreements and vendor clarification must be reviewed before commercial/general distribution. No public APK mirroring is supplied.

## Before publishing any binary installer/release

1. Clarify Google SDK/Google Play image permitted use and any vendor emulator restrictions; obtain permission or replace affected components with a suitable, independently licensed approach.
2. Preserve all .NET/WPF binary LICENSE and third-party notices. A self-contained build does not remove those obligations.
3. Preserve scrcpy and bundled-dependency licenses/notices, including applicable FFmpeg/SDL obligations of the exact upstream binary release. Do not assume Apache-2.0 applies to every bundled dependency.
4. Do not include vendor APKs, operating-system images, account state or private keys.
5. Recheck the exact release artifact, upstream versions and terms. Do not describe this review as vendor approval or legal clearance.

A source-only repository reduces redistribution exposure; it does not certify that every future use, build or distribution is permitted. No prebuilt installer is published by this source snapshot.
