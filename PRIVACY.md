# Privacy and local data

The launcher stores settings and Android app data locally. Notes may contain IP addresses, free text and passwords. Passwords use Windows DPAPI for the current account. Other notes/settings and Android disk data are not encrypted by the launcher.

Windows location, manual coordinates and phone GPS can be passed to Android apps. Phone GPS uses TCP/NMEA on the configured network endpoint. Do not expose it to untrusted networks. Diagnostic logs can contain app/device/network information; inspect before sharing.

Update checks contact official vendor sources. Installing dependencies/apps contacts Google, Genymobile/GitHub and vendor download services. Google Play and vendor applications may have their own account, privacy and network behavior; this project cannot make privacy guarantees for them. No project-hosted analytics backend is configured.

Keep `private-data`, notes, credentials and logs out of public repositories. Close Android before making backups. Password encryption is not portable to another Windows account/PC.
