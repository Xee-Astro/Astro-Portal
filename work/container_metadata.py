from pathlib import Path
import json, urllib.request, xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent / 'android-container'
root.mkdir(exist_ok=True)
def get(url):
    request = urllib.request.Request(url, headers={'User-Agent':'ASIAIR-Local-Runtime-Prototype'})
    return urllib.request.urlopen(request, timeout=45).read()
sources = [
    ('https://dl.google.com/android/repository/repository2-3.xml', ['emulator','platform-tools']),
    ('https://dl.google.com/android/repository/sys-img/google_apis_playstore/sys-img2-3.xml', ['system-images;android-30;google_apis_playstore;x86_64'])
]
packages=[]
for url, wanted in sources:
    xml = ET.fromstring(get(url))
    for package in xml.findall('{*}remotePackage'):
        if package.get('path') not in wanted: continue
        channel = package.find('{*}channelRef')
        if channel is not None and channel.get('ref') != 'channel-0': continue
        for archive in package.findall('{*}archives/{*}archive'):
            host = archive.findtext('{*}host-os')
            if host not in (None,'windows'): continue
            complete=archive.find('{*}complete')
            if complete is None: continue
            location=complete.findtext('{*}url')
            base=url.rsplit('/',1)[0]+'/'
            packages.append({'package':package.get('path'),'url':location if location.startswith('https:') else base+location,
                'bytes':int(complete.findtext('{*}size')),'checksum':complete.findtext('{*}checksum'),
                'checksum_type':complete.find('{*}checksum').get('type','sha1'),
                'revision':{part.tag.split('}')[-1]:part.text for part in package.find('{*}revision')}})
request = urllib.request.Request('https://api.github.com/repos/Genymobile/scrcpy/releases/latest',headers={'User-Agent':'ASIAIR-Local-Runtime-Prototype'})
release=json.load(urllib.request.urlopen(request,timeout=30))
for asset in release['assets']:
    if asset['name'].startswith('scrcpy-win64-') and asset['name'].endswith('.zip'):
        packages.append({'package':'scrcpy','url':asset['browser_download_url'],'bytes':asset['size'],'digest':asset.get('digest'),'revision':release['tag_name']})
if len(packages)!=4: raise RuntimeError('Unexpected dependency metadata: '+str([p['package'] for p in packages]))
(root/'dependencies.json').write_text(json.dumps(packages,indent=2),encoding='utf-8')
print(json.dumps(packages,indent=2))
