from pathlib import Path
import hashlib,json,shutil,zipfile
r=Path.cwd();out=r/'outputs/Astro-Portal-Setup';out.mkdir(parents=True,exist_ok=True)
shutil.copytree(r/'work/portal-selfcontained',out/'app',dirs_exist_ok=True)
shutil.copy2(out/'app/AsiairContainer.exe',out/'app/Astro-Portal.exe')
for name in ['Install-Astro-Portal.ps1','Install.cmd','README.md']:
 shutil.copy2(r/'work/portal-installer'/name,out/name)
shutil.copy2(r/'work/android-container/dependencies.json',out/'dependencies.json')
shutil.copy2(r/'outputs/ASIAIR-Container/runtime/ASIAIR-Home.apk',out/'ASIAIR-Home.apk')
manifest=[]
for p in sorted((out/'app').rglob('*')):
 if p.is_file():manifest.append({'path':p.relative_to(out).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()})
for name in ['ASIAIR-Home.apk','dependencies.json']:
 p=out/name;manifest.append({'path':name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()})
(out/'payload-sha256.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
target=r/'outputs/Astro-Portal-Setup.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as z:
 for p in sorted(out.rglob('*')):
  if p.is_file():z.write(p,Path('Astro-Portal-Setup')/p.relative_to(out))
print(target.name,target.stat().st_size,flush=True)
