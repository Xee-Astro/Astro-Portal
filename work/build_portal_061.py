from pathlib import Path
import hashlib, json, shutil, zipfile
r=Path(__file__).resolve().parent.parent
version='0.6.1'
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
patch=r/'outputs/Astro-Portal-Patch-0.6.1'
payload=patch/'payload'; payload.mkdir(parents=True,exist_ok=True)
for name in ['AsiairContainer.exe','AsiairContainer.dll','AsiairContainer.deps.json','AsiairContainer.runtimeconfig.json']: copy(r/'work/portal-061'/name,payload/name)
copy(payload/'AsiairContainer.exe',payload/'Astro-Portal.exe')
manifest=[{'path':p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sorted(payload.iterdir()) if p.is_file()]
(patch/'patch-sha256.json').write_text(json.dumps(manifest,indent=2))
copy(r/'work/portal-installer/Apply-Patch-0.6.1.ps1',patch/'Apply-Patch-0.6.1.ps1')
copy(r/'work/portal-installer/Apply-Patch-0.6.1.cmd',patch/'Apply-Patch.cmd')
copy(license,patch/'LICENSE')
(patch/'README.md').write_text("""# Astro - Portal 0.6.1 patch

For existing self-contained Windows x64 versions 0.5.0, 0.5.1 or 0.6.0.

1. Close Astro - Portal completely and allow Android to stop.
2. Extract this patch ZIP into a separate folder.
3. Run Apply-Patch.cmd, paste your existing installation folder, and answer Yes.
4. Start Astro-Portal.exe from the original installation.

Changes: the supplied ASTRO/PORTAL artwork is static while Android is stopped or starting. Portal rotation and ASTRO consumption animations are removed. Clone checkboxes, numbered launch buttons and second-profile launching are removed. Existing second-profile files remain untouched on disk.

Split screen keeps toggles for ASIAIR, Stellavita and Other (main Android). Hidden panes keep running; double-click a pane badge to zoom fullscreen and exit to restore the selected panes. The original blue executable/taskbar icon and red/blue app identities remain.

Android font controls now support 80%-130%, the range accepted by this Android image. Higher saved values are reduced to 130%. Fixed-size vendor app text may ignore this setting. Font and location settings apply to the single active Android profile.

The patch changes only five launcher files, makes a backup, and verifies hashes. It never removes private-data or reinstalls Android. No additional runtime or APK download is needed to apply it. Physical equipment control remains outside automated validation.

To roll back, close the launcher/Android and copy files from patch-backup-... back over the launcher files. Keep private-data intact. The second profile is private-data/profiles/second; do not delete it unless you intend to erase all cloned app data. This patch cannot install on an empty PC or repair missing runtime files.

Original source and generated portal icon are provided under the project MIT license. The static splash uses artwork supplied for this project. Third-party software/vendor terms remain separate. See https://github.com/Xee-Astro/Astro-Portal/blob/main/docs/LICENSING_REVIEW.md
""",encoding='utf-8')
archive(patch)
setup=r/'outputs/Astro-Portal-Setup-0.6.1'; setup.mkdir(parents=True,exist_ok=True)
shutil.copytree(r/'work/portal-061',setup/'app',dirs_exist_ok=True)
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
