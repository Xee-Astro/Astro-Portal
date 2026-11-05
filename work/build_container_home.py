from pathlib import Path
import subprocess,zipfile
root=Path.cwd(); src=root/'work/container-home'; build=src/'build'; build.mkdir(exist_ok=True)
tools=root/'work/android-build/android-16'; android=root/'work/android-build/android-11/android.jar'
def run(args): subprocess.run([str(a) for a in args],check=True)
classes=build/'classes';classes.mkdir(exist_ok=True)
run(['javac','--release','8','-classpath',android,'-d',classes,*src.glob('src/local/asiair/home/*.java')])
run(['java','-cp',tools/'lib/d8.jar','com.android.tools.r8.D8','--lib',android,'--output',build,*classes.rglob('*.class')])
unsigned=build/'home-unsigned.apk'
run([tools/'aapt.exe','package','-f','-M',src/'AndroidManifest.xml','-I',android,'-F',unsigned])
with zipfile.ZipFile(unsigned,'a',zipfile.ZIP_DEFLATED) as z: z.write(build/'classes.dex','classes.dex')
key=build/'local-development.p12'
if not key.exists(): run(['keytool','-genkeypair','-keystore',key,'-storepass','android','-keypass','android','-alias','local-home','-dname','CN=ASIAIR Local Development','-keyalg','RSA','-keysize','2048','-validity','3650'])
aligned=build/'home-aligned.apk';run([tools/'zipalign.exe','-f','4',unsigned,aligned])
out=root/'outputs/ASIAIR-Container/runtime/ASIAIR-Home.apk'
run(['java','-jar',tools/'lib/apksigner.jar','sign','--ks',key,'--ks-pass','pass:android','--out',out,aligned])
run(['java','-jar',tools/'lib/apksigner.jar','verify',out]);print(out.stat().st_size)
