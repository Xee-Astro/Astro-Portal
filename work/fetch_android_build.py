from pathlib import Path
import urllib.request, xml.etree.ElementTree as E, json,hashlib,zipfile
out=Path('work/android-build'); out.mkdir(exist_ok=True)
x=E.fromstring(urllib.request.urlopen('https://dl.google.com/android/repository/repository2-3.xml',timeout=45).read())
for p in x.findall('{*}remotePackage'):
 if p.get('path') not in ['platforms;android-30','build-tools;36.0.0']: continue
 for a in p.findall('{*}archives/{*}archive'):
  if a.findtext('{*}host-os') not in [None,'windows']:continue
  c=a.find('{*}complete'); u=c.findtext('{*}url'); f=out/u.rsplit('/',1)[-1]
  if not f.exists():
   with urllib.request.urlopen('https://dl.google.com/android/repository/'+u,timeout=120) as src, f.open('wb') as dest:
    while data:=src.read(1024*1024): dest.write(data)
  expected=c.findtext('{*}checksum').lower().zfill(40)
  with f.open('rb') as z: actual=hashlib.file_digest(z,'sha1').hexdigest()
  if actual!=expected: raise RuntimeError('Checksum mismatch')
  with zipfile.ZipFile(f) as z: z.extractall(out)
  print(p.get('path'),f.stat().st_size,flush=True)
