from pathlib import Path
import hashlib, json, shutil, zipfile

root = Path(__file__).resolve().parent.parent
folder = root / 'outputs/Astro-Portal-Patch-0.5.1'
payload = folder / 'payload'
payload.mkdir(parents=True, exist_ok=True)
files = ['AsiairContainer.exe', 'AsiairContainer.dll', 'AsiairContainer.deps.json', 'AsiairContainer.runtimeconfig.json']
for name in files:
    shutil.copy2(root / 'work/portal-051' / name, payload / name)
shutil.copy2(payload / 'AsiairContainer.exe', payload / 'Astro-Portal.exe')
manifest = [{'path': p.name, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sorted(payload.iterdir()) if p.is_file()]
(folder / 'patch-sha256.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
for name in ['Apply-Patch-0.5.1.ps1', 'Apply-Patch.cmd']:
    shutil.copy2(root / 'work/portal-installer' / name, folder / name)
license_file = root / 'LICENSE'
if not license_file.exists(): license_file = root / 'outputs/Astro-Portal-GitHub/LICENSE'
shutil.copy2(license_file, folder / 'LICENSE')
(folder / 'README.md').write_text('''# Astro - Portal 0.5.1 patch

For an existing self-contained Windows x64 Astro - Portal 0.5.0 installation. No Android reinstall, runtime download, or separate .NET installation is needed.

1. Close Astro - Portal completely and allow Android to stop.
2. Extract this patch ZIP to a separate folder.
3. Run Apply-Patch.cmd.
4. Paste your existing installation folder (the folder containing Astro-Portal.exe and AsiairContainer.dll), then answer Yes.
5. Open Astro-Portal.exe from your existing installation normally.

The dashboard now has Android font A− / A+ controls in 10% increments, a current percentage and Reset to 100%. Supported range is 80%–200%. A running Android system updates immediately; otherwise the choice is saved for the next start. Both apps share the Android setting. Text drawn at a fixed size by an app may not follow it. Font changes may cause Android activities to refresh.

The patch verifies SHA-256 hashes, changes only five launcher files, and keeps old launcher files in a patch-backup-0.5.0-... folder inside the installation. Android runtime, private-data, apps, accounts, notes and existing configuration are not replaced. No selects cancellation before any writes. It rejects incompatible/newer versions and running processes. Replacement failure attempts to restore the original launcher files.

Rollback: close the launcher/Android, then copy the backed-up launcher files back to the installation folder. Preserve the backup until satisfied. This patch does not install onto an empty PC and does not fix missing runtime files.

Original patch code is MIT-licensed. Existing runtime/vendor licenses and the repository's unresolved third-party permitted-use caveats still apply: https://github.com/Xee-Astro/Astro-Portal/blob/main/docs/LICENSING_REVIEW.md
''', encoding='utf-8')
target = root / 'outputs/Astro-Portal-Patch-0.5.1.zip'
with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED) as archive:
    for p in sorted(folder.rglob('*')):
        if p.is_file():
            archive.write(p, Path(folder.name) / p.relative_to(folder))
checksum = hashlib.sha256(target.read_bytes()).hexdigest()
target.with_suffix('.zip.sha256').write_text(checksum + '  ' + target.name + '\n', encoding='utf-8')
print(target.name, target.stat().st_size, 'bytes', checksum)
