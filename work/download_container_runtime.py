from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib, json, urllib.request, zipfile, os

root = Path(__file__).resolve().parent.parent
task = root/'work'/'android-container'
downloads = task/'downloads'
runtime = root/'outputs'/'ASIAIR-Container'/'runtime'
downloads.mkdir(parents=True,exist_ok=True)
runtime.mkdir(parents=True,exist_ok=True)
packages=json.loads((task/'dependencies.json').read_text())

def verified(path, package):
    if not path.exists() or path.stat().st_size != package['bytes']: return False
    algorithm, expected = package.get('digest','sha1:'+package.get('checksum','')).split(':',1)
    expected=expected.zfill(hashlib.new(algorithm).digest_size*2)
    digest=hashlib.new(algorithm)
    with path.open('rb') as source:
        for chunk in iter(lambda:source.read(4*1024*1024),b''): digest.update(chunk)
    return digest.hexdigest()==expected

def install(package):
    name=package['url'].rsplit('/',1)[1]
    destination=downloads/name
    if not verified(destination,package):
        temporary=destination.with_suffix('.zip.part')
        request=urllib.request.Request(package['url'],headers={'User-Agent':'ASIAIR-Local-Runtime-Prototype'})
        total=0; reported=0
        print('Downloading '+package['package'],flush=True)
        with urllib.request.urlopen(request,timeout=90) as response, temporary.open('wb') as output:
            while True:
                chunk=response.read(4*1024*1024)
                if not chunk: break
                output.write(chunk); total+=len(chunk)
                if total-reported >= 128*1024*1024:
                    print(package['package']+f': {total*100/package["bytes"]:.0f}%',flush=True); reported=total
        temporary.replace(destination)
        if not verified(destination,package): raise RuntimeError('Archive verification failed for '+name)
    print('Verified '+package['package'],flush=True)
    if package['package'].startswith('system-images;'): target=runtime/'sdk'/'system-images'/package['package'].split(';')[1]/package['package'].split(';')[2]
    elif package['package']=='scrcpy': target=runtime/'screen'
    else: target=runtime/'sdk'
    marker=target/('.'+name+'.installed')
    if not marker.exists():
        target.mkdir(parents=True,exist_ok=True)
        with zipfile.ZipFile(destination) as archive:
            prefix=str(target.resolve()).casefold()+os.sep
            for entry in archive.infolist():
                resolved=(target/entry.filename).resolve()
                if not str(resolved).casefold().startswith(prefix): raise RuntimeError('Unsafe archive path')
            archive.extractall(target)
        marker.write_text('Verified official archive: '+name,encoding='utf-8')
    print('Ready '+package['package'],flush=True)

with ThreadPoolExecutor(max_workers=2) as pool:
    list(pool.map(install,packages))
print('All runtime components ready.',flush=True)
