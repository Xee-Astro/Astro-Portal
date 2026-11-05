from pathlib import Path
import os,shutil,json,subprocess
src=Path('outputs/ASIAIR-Container/runtime');root=Path('work/portal-test').resolve();root.mkdir(exist_ok=True);(root/'runtime').mkdir(exist_ok=True)
for p in src.rglob('*'):
 d=root/'runtime'/p.relative_to(src)
 if p.is_dir():d.mkdir(parents=True,exist_ok=True)
 elif not d.exists():os.link(p,d)
for p in Path('work/portal-selfcontained').iterdir():
 if p.is_file():shutil.copy2(p,root/p.name)
(root/'container.json').write_text(json.dumps({'ApkFolder':'','DeviceIp':'192.168.4.1','AutoStartAndroid':False,'UseWindowsLocation':False,'AutomaticUpdateChecks':False}))
args=[str(root/'AsiairContainer.exe'),'--verify-portal','--install-asiair',str(Path('work/android-container/downloads/ASIAIR-official.apk').resolve()),'--install-stellavita',str(Path('work/android-container/downloads/Stellavita-official.apk').resolve())]
p=subprocess.Popen(args,cwd=root,creationflags=subprocess.CREATE_NO_WINDOW);print('Fresh profile verification',p.pid,flush=True)
code=p.wait(timeout=900);print('Exit',code,flush=True)
f=root/'private-data/portal-verification.json';print(f.read_text() if f.exists() else 'No verification result',flush=True)

