from pathlib import Path
import hashlib, json, shutil, zipfile
r=Path(__file__).resolve().parent.parent
version='0.6.0'
def copy(source,target):
 target.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(source,target)
def archive(folder):
 target=folder.parent/(folder.name+'.zip')
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as z:
  for p in sorted(folder.rglob('*')):
   if p.is_file(): z.write(p,Path(folder.name)/p.relative_to(folder))
 digest=hashlib.sha256(target.read_bytes()).hexdigest()
 target.with_suffix('.zip.sha256').write_text(digest+'  '+target.name+'\n')
 print(target.name,target.stat().st_size,digest)
license=r/'LICENSE'
if not license.exists(): license=r/'outputs/Astro-Portal-GitHub/LICENSE'
patch=r/'outputs/Astro-Portal-Patch-0.6.0'
payload=patch/'payload'; payload.mkdir(parents=True,exist_ok=True)
for name in ['AsiairContainer.exe','AsiairContainer.dll','AsiairContainer.deps.json','AsiairContainer.runtimeconfig.json']: copy(r/'work/portal-060'/name,payload/name)
copy(payload/'AsiairContainer.exe',payload/'Astro-Portal.exe')
manifest=[{'path':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sorted(payload.iterdir()) if p.is_file()]
(patch/'patch-sha256.json').write_text(json.dumps(manifest,indent=2))
copy(r/'work/portal-installer/Apply-Patch-0.6.0.ps1',patch/'Apply-Patch-0.6.0.ps1')
copy(r/'work/portal-installer/Apply-Patch-0.6.0.cmd',patch/'Apply-Patch.cmd')
copy(license,patch/'LICENSE')
(patch/'README.md').write_text("""# Astro - Portal 0.6.0 patch

For existing self-contained Windows x64 versions 0.5.0 or 0.5.1.

1. Close Astro - Portal completely and allow Android to stop.
2. Extract this patch ZIP into a separate folder.
3. Run Apply-Patch.cmd, paste your existing installation folder, and answer Yes.
4. Start Astro-Portal.exe from the original installation.

Changes: Clone checkboxes reveal ASIAIR 2 and Stellavita 2. The second copies use a fresh persistent Android profile and independent app accounts/settings. Only APKs are copied from the first profile. Original app data stays in place. Split screen offers toggles for up to four astronomy apps plus Other (main Android). Hidden panes keep running. Double-click a pane badge to zoom full screen; exit restores your selection. A new original blue portal icon is embedded in the executable/taskbar. The supplied splash appears when Android is stopped; both portals rotate in their projected planes and ASTRO is consumed during boot, completing only when Android is ready. Failed startup restores the artwork. Motion follows the Windows animation preference.

Android font controls now support 80%-130%, the range accepted by this Android image. Higher saved values are reduced to 130%. Fixed-size vendor app text may ignore this setting. Font and location settings are shared across both Android profiles.

The patch changes only five launcher files, makes a backup, and verifies hashes. No Android reinstall or download is needed to apply it. First clone launch creates another profile: allow 8-12 GiB additional free disk space and up to 3 GiB additional guest RAM, plus Windows overhead. 16 GiB PC RAM is recommended for all four apps. Vendor APK installation and first boot take time; wait for completion. No physical device control/capture validation is implied.

To roll back, close the launcher/Android and copy files from patch-backup-... back over the launcher files. Keep private-data intact. The second profile is private-data/profiles/second; do not delete it unless you intend to erase all cloned app data. This patch cannot install on an empty PC or repair missing runtime files.

Original source and generated portal icon are provided under the project MIT license. The splash uses artwork supplied for this project, with AI-prepared animation layers. Third-party software/vendor terms remain separate. See https://github.com/Xee-Astro/Astro-Portal/blob/main/docs/LICENSING_REVIEW.md
""",encoding='utf-8')
archive(patch)
setup=r/'outputs/Astro-Portal-Setup-0.6.0'; setup.mkdir(parents=True,exist_ok=True)
shutil.copytree(r/'work/portal-060',setup/'app',dirs_exist_ok=True)
copy(setup/'app/AsiairContainer.exe',setup/'app/Astro-Portal.exe')
for name in ['Install-Astro-Portal.ps1','Install.cmd','README.md']:
 copy(r/'work/portal-installer'/name,setup/name)
copy(r/'outputs/ASIAIR-Container/runtime/ASIAIR-Home.apk',setup/'ASIAIR-Home.apk')
copy(r/'work/android-container/dependencies.json',setup/'dependencies.json')
copy(license,setup/'LICENSE')
public=r if (r/'THIRD_PARTY_NOTICES.md').exists() else r/'outputs/Astro-Portal-GitHub'
copy(public/'THIRD_PARTY_NOTICES.md',setup/'THIRD_PARTY_NOTICES.md')
for name in ['LICENSING_REVIEW.md','INSTALLATION.md']:
 copy(public/'docs'/name,setup/'docs'/name)
# Retain the runtime's upstream license material. Build on Windows .NET 10.0.8.
nuget=Path(__import__('os').environ.get('NUGET_PACKAGES',str(Path.home()/'.nuget/packages')))
if not nuget.exists(): nuget=r/'work/nuget-packages'
for package,label in [('microsoft.netcore.app.runtime.win-x64','DOTNET'),('microsoft.windowsdesktop.app.runtime.win-x64','WPF')]:
 for name in ['LICENSE.TXT','THIRD-PARTY-NOTICES.TXT']:
  source=nuget/package/'10.0.8'/name
  if not source.exists():
   source=r/'outputs/Astro-Portal-Setup/licenses'/(label+'-'+name)
  if not source.exists() and label=='WPF' and name=='THIRD-PARTY-NOTICES.TXT': source=r/'work/publish/licenses/WPF-THIRD-PARTY-NOTICES.TXT'
  if not source.exists(): raise FileNotFoundError('Missing upstream runtime license: '+str(source))
  copy(source,setup/'licenses'/(label+'-'+name))
manifest=[]
for p in sorted((setup/'app').rglob('*')):
 if p.is_file(): manifest.append({'path':p.relative_to(setup).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()})
for name in ['ASIAIR-Home.apk','dependencies.json']:
 p=setup/name; manifest.append({'path':name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()})
(setup/'payload-sha256.json').write_text(json.dumps(manifest,indent=2))
archive(setup)
