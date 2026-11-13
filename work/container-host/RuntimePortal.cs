using System.IO;
using System.Text.RegularExpressions;

namespace AsiairContainer;

public sealed partial class RuntimeController
{
    public async Task<bool> IsAppRunningAsync(string package, CancellationToken token)
    {
        if (!IsRunning || package is not (AsiairPackage or StellavitaPackage)) return false;
        return !string.IsNullOrWhiteSpace(await AdbAsync(["-s", Serial, "shell", "pidof", package], token, 10, false));
    }
    public async Task TerminateAstroAppAsync(string package, CancellationToken token)
    {
        if (package is not (AsiairPackage or StellavitaPackage)) throw new ArgumentException("Choose an astronomy app.");
        await ShowOthersAsync(token);
        await ShellAsync(token, "am", "force-stop", package);
        if (await IsAppRunningAsync(package, token)) throw new IOException("The app has not finished closing.");
        string other = package == AsiairPackage ? StellavitaPackage : AsiairPackage;
        if (await IsAppRunningAsync(other, token)) await ShowAstroAppAsync(other, token);
    }
    private async Task EnsureNativeAsiairAsync(CancellationToken token)
    {
        string detail = await ShellAsync(token, "dumpsys", "package", AsiairPackage);
        if (!detail.Contains("versionName=")) return;
        if (Regex.IsMatch(detail, @"primaryCpuAbi=x86_64\b")) return;
        Stage("Replacing the ARM-only ASIAIR package with the official x86-64 build. App data will be retained…");
        using var installer = new OfficialApkInstaller(root);
        string apk = await installer.DownloadAsync(false, Stage, token);
        await InstallOfficialApkAsync(apk, false, token);
    }
    public async Task InstallOfficialApkAsync(string path, bool stella, CancellationToken token)
    {
        OfficialApkInstaller.VerifyApk(path, stella);
        string package = stella ? StellavitaPackage : AsiairPackage;
        var expected = ApkIdentity.Read(path);
        Stage("Installing " + (stella ? "Stellavita" : "ASIAIR") + " for the tablet…");
        string before = await ShellAsync(token, "dumpsys", "package", package);
        bool Matches(string detail) => detail.Contains("primaryCpuAbi=x86_64") && Regex.Match(detail, @"\bversionCode=(\d+)") is { Success: true } version && long.Parse(version.Groups[1].Value) == expected.VersionCode;
        if (Matches(before)) { await RecordInstalledVersionAsync(token); DeleteOwnedDownload(path); Stage("The native app is already up to date."); return; }
        await ShellAsync(token, "am", "force-stop", package);
        displayNeedsRestart = true;
        string result = await AdbAsync(["-s", Serial, "install", "-r", "-g", "--abi", "x86_64", Path.GetFullPath(path)], token, 600, false);
        if (!result.Contains("Success", StringComparison.OrdinalIgnoreCase)) Stage("Waiting for Android to confirm the native app after installation…");
        // Existing Android profiles can restart their system process after a package
        // change. Wait for the service before updating cached installation state.
        string previousSystem = ""; int stableSystem = 0;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                string detail = await ShellAsync(token, "dumpsys", "package", package);
                if (Matches(detail))
                {
                    string system = await ShellAsync(token, "pidof", "system_server");
                    stableSystem = system.Length > 0 && system == previousSystem ? stableSystem + 1 : 0; previousSystem = system;
                    if (stableSystem < 3) { await Task.Delay(1000, token); continue; }
                    await RecordInstalledVersionAsync(token); Stage("Installation complete.");
                    DeleteOwnedDownload(path);
                    return;
                }
            }
            catch (IOException) { stableSystem = 0; previousSystem = ""; }
            await Task.Delay(1000, token);
        }
        throw new IOException("Android has not confirmed the x86-64 app installation. Stop Android and restart it.");
    }
    private void DeleteOwnedDownload(string path)
    {
        if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(Path.Combine(state, "downloads")), StringComparison.OrdinalIgnoreCase))
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private async Task WaitForLayoutAsync(string? foreground, bool split, CancellationToken token)
    {
        string previous = ""; int stable = 0;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            var tasks = await ReadTasksAsync(token);
            bool ready = split
                ? tasks.Any(t => t.Package == AsiairPackage && t.Visible && t.Mode == "split-screen-primary") && tasks.Any(t => t.Package == StellavitaPackage && t.Visible && t.Mode == "split-screen-secondary") && await IsAppRunningAsync(AsiairPackage, token) && await IsAppRunningAsync(StellavitaPackage, token)
                : foreground is null ? !tasks.Any(t => t.Visible && t.Package is AsiairPackage or StellavitaPackage)
                : tasks.Any(t => t.Package == foreground && t.Visible && (t.Mode == "fullscreen" || foreground == AsiairPackage && t.Mode == "freeform")) && await IsAppRunningAsync(foreground, token);
            string current = string.Join(";", tasks.Where(t => t.Visible).Select(t => $"{t.Id}/{t.Package}/{t.Component}/{t.Mode}"));
            stable = ready && current == previous ? stable + 1 : 0; previous = current;
            if (stable >= 3)
            {
                string processes = await ShellAsync(token, "dumpsys", "activity", "processes");
                if (Regex.IsMatch(processes, @"\bnotResponding=true\b", RegexOptions.IgnoreCase)) throw new IOException("Android reports an app is not responding. Close the affected app with × and reopen it.");
                return;
            }
            await Task.Delay(250, token);
        }
        throw new IOException("The Android layout did not settle. Wait for the app to finish opening, then try again.");
    }
    public async Task ProbeLandscapeAsync(CancellationToken token)
    {
        await SetLocationAsync(12.3456,45.6789,token);
        await ShowAstroAppAsync(AsiairPackage,token); await Task.Delay(2000,token);
        var task = (await ReadTasksAsync(token)).First(t=>t.Package==AsiairPackage);
        await ShellAsync(token,"settings","put","global","enable_freeform_support","1");
        await ShellAsync(token,"am","task","resizeable",task.Id.ToString(),"2");
        Stage(await ShellAsync(token,"env","CLASSPATH=/data/local/tmp/astro-layout.apk","app_process","/","local.asiair.home.LayoutControl",task.Id.ToString(),"5"));
        Stage(await ShellAsync(token,"am","task","resize",task.Id.ToString(),"0","0","2560","800"));
        await Task.Delay(1500,token);
        File.WriteAllText(Path.Combine(state,"landscape-probe.txt"),await ShellAsync(token,"am","stack","list"));
        await CaptureAsync(Path.Combine(root,"landscape-probe.png"),token);
    }
    public async Task VerifyLandscapeAsync(CancellationToken token)
    {
        string size = await ShellAsync(token, "wm", "size");
        string display = await ShellAsync(token, "dumpsys", "input");
        string policy = await ShellAsync(token, "dumpsys", "window", "displays");
        // Apps may toggle accelerometer_rotation themselves. The system's fixed-to-user
        // policy takes precedence, so verify effective rotation and policy instead.
        if (!size.TrimEnd().EndsWith($"2560x{DisplayHeight}") || !display.Contains("SurfaceOrientation: 0") || !policy.Contains("mFixedToUserRotation=true") || !policy.Contains("mUserRotation=ROTATION_0"))
        { File.WriteAllText(Path.Combine(state, "landscape-diagnostic.txt"), size + "\n" + display + "\n" + policy); throw new IOException("The landscape tablet policy was not retained. See landscape-diagnostic.txt."); }
        foreach (string package in new[] { AsiairPackage, StellavitaPackage })
            if (!(await ShellAsync(token, "dumpsys", "package", package)).Contains("primaryCpuAbi=x86_64")) throw new IOException("An astronomy app still uses ARM translation.");
    }
    private async Task SlimGuestAsync(CancellationToken token)
    {
        // Only optional entertainment/demo packages. Keep Play, WebView, keyboard,
        // network, location, permissions, backup and all system services intact.
        foreach (string package in new[] { "com.google.android.youtube", "com.google.android.apps.maps", "com.google.android.music", "com.google.android.videos" })
            await AdbAsync(["-s", Serial, "shell", "pm", "disable-user", "--user", "0", package], token, 15, false);
    }
}
