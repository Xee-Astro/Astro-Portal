using AsiairContainer;
using System.IO;
public static class PortalChecks
{
    public static void Run(Action<bool,string> assert, string root)
    {
        var link = OfficialApkInstaller.ResolveAsiairDownload(File.ReadAllText("work/container-checks/official-download-fixture.html"));
        assert(link.Host == "drive.usercontent.google.com" && link.Query.Contains("1hUV0Jr-Z4WLtgqaX0iCIRTXUM2qk5h0I"), "latest stable Android card resolves vendor-linked APK");
        foreach (string url in new[] { "http://www.zwoastro.com/a.apk", "https://www.zwoastro.com.evil.test/a.apk", "https://evil.test/a.apk", "https://www.zwoastro.com:8443/a.apk" })
            assert(!OfficialApkInstaller.AllowedDownload(new(url)), "APK source rejects insecure or unrelated host");
        if (File.Exists("work/android-container/downloads/ASIAIR-official.apk") && File.Exists("work/android-container/downloads/Stellavita-official.apk"))
        {
        OfficialApkInstaller.VerifyApk("work/android-container/downloads/ASIAIR-official.apk", false);
        OfficialApkInstaller.VerifyApk("work/android-container/downloads/Stellavita-official.apk", true);
        assert(true, "both official APKs match pinned vendor certificates and contain x86-64 libraries");
        var asiairIdentity = ApkIdentity.Read("work/android-container/downloads/ASIAIR-official.apk");
        var stellaIdentity = ApkIdentity.Read("work/android-container/downloads/Stellavita-official.apk");
        assert(asiairIdentity.Package == RuntimeController.AsiairPackage && asiairIdentity.VersionCode == 143, "binary Android manifest identifies the precise ASIAIR package/version code");
        assert(stellaIdentity.Package == RuntimeController.StellavitaPackage && stellaIdentity.VersionCode > 0, "binary Android manifest identifies the precise Stellavita package/version code");
        try { OfficialApkInstaller.VerifyApk("work/android-container/downloads/ASIAIR-official.apk", true); throw new Exception("Wrong signer accepted"); }
        catch (IOException) { assert(true, "wrong vendor certificate is blocked before installation"); }
        }
        string malformed = Path.Combine(root,"bad.apk"); File.WriteAllText(malformed,"not an APK");
        try { OfficialApkInstaller.ReadSignerHash(malformed); throw new Exception("Malformed APK accepted"); }
        catch (IOException) { assert(true, "malformed APK is blocked"); }
        var now = DateTimeOffset.UtcNow;
        UpdateSnapshot Snapshot(string version, string? error = null) => new(now,now,new(version,"https://www.zwoastro.com/software/asiair/"),error);
        assert(UpdateChecker.IsUpdateAvailable("3.0",Snapshot("3.1")), "update enabled only for a detected newer release");
        assert(!UpdateChecker.IsUpdateAvailable("3.0.0",Snapshot("3.0")), "equal normalized versions keep update disabled");
        assert(!UpdateChecker.IsUpdateAvailable("3.1",Snapshot("3.0")), "older release cannot trigger a downgrade");
        assert(!UpdateChecker.IsUpdateAvailable(null,Snapshot("3.1")), "missing app uses Install rather than Update");
        assert(!UpdateChecker.IsUpdateAvailable("3.0",Snapshot("3.1","Offline")), "failed update check keeps update disabled");
        assert(!UpdateChecker.IsUpdateAvailable("3.0",null) && !UpdateChecker.IsUpdateAvailable("bad",Snapshot("3.1")), "unknown or invalid versions keep update disabled");
        var data = new NotesData(new("192.0.2.64", "Test-Password-123!", "ASIAIR notes\nsecond line"), new("192.168.4.1", "Another secret", "Stellavita notes"));
        NotesStore.Save(root,data); var loaded = NotesStore.Load(root);
        assert(loaded == data, "both apps' notes, IPs and passwords persist across reload");
        string stored = File.ReadAllText(Path.Combine(root,"private-data","notes.json"));
        assert(!stored.Contains("Test-Password-123!") && !stored.Contains("Another secret"), "saved passwords are encrypted with Windows DPAPI");
        assert(RuntimeController.IsAsiairControlScreen(new(1,1,RuntimeController.AsiairPackage,"com.zwoasi.asiair/com.zwoasi.asiair.activity.AirActivity","freeform",true)), "ASIAIR control screen uses caption-free fullscreen");
        assert(!RuntimeController.IsAsiairControlScreen(new(1,1,RuntimeController.AsiairPackage,"com.zwoasi.asiair/com.zwoasi.asiair.activity_pre.AirPartActivity","freeform",true)), "ASIAIR connection screen retains landscape compatibility");
        var runtime = new RuntimeController(root,new ContainerOptions()); runtime.ConfigureAvd();
        string config = File.ReadAllText(Path.Combine(root,"private-data","avd","Astro_Play.avd","config.ini"));
        assert(config.Contains("hw.lcd.width=2560") && config.Contains("hw.lcd.height=800") && config.Contains("hw.accelerometer=no") && config.Contains("hw.initialOrientation=landscape"), "tablet uses one landscape framebuffer without rotation sensor");
    }
}
