using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AsiairContainer;

public sealed class AppDisplaySession : IDisposable
{
    internal Process Process { get; }
    public string Package { get; }
    public int DisplayId { get; internal set; }
    public nint Window => IsAlive ? Process.MainWindowHandle : 0;
    public bool IsAlive { get { try { return !Process.HasExited; } catch (InvalidOperationException) { return false; } } }
    internal AppDisplaySession(Process process, string package) { Process = process; Package = package; }
    public void Dispose() { try { if (!Process.HasExited) Process.Kill(); } finally { Process.Dispose(); } }
}

public sealed partial class RuntimeController
{
    private readonly Dictionary<string, AppDisplaySession> appDisplays = [];
    public async Task<AppDisplaySession> OpenAppDisplayAsync(string package, CancellationToken token)
    {
        if (package is not (AsiairPackage or StellavitaPackage)) throw new ArgumentException("Unsupported astronomy app.");
        if (!IsRunning) throw new InvalidOperationException("Start Android first.");
        if (appDisplays.TryGetValue(package, out var current) && current.IsAlive)
        {
            string resolved = await ShellAsync(token,"cmd","package","resolve-activity","--brief","-a","android.intent.action.MAIN","-c","android.intent.category.LAUNCHER","-p",package);
            string component = resolved.Split('\n').Select(s=>s.Trim()).Last(s=>s.StartsWith(package+"/",StringComparison.Ordinal));
            await ShellAsync(token,"am","start","-W","--display",current.DisplayId.ToString(),"-n",component);
            await NormalizeAppDisplaysAsync(token); return current;
        }
        current?.Dispose(); appDisplays.Remove(package);
        if (package == AsiairPackage) await EnsureNativeAsiairAsync(token);
        if (!(await ShellAsync(token, "pm", "path", package)).Contains(".apk")) throw new IOException("Install this app in the main Android profile first.");
        string executable = Directory.EnumerateFiles(Path.Combine(root, "runtime", "screen"), "scrcpy.exe", SearchOption.AllDirectories).Single();
        var process = new Process { StartInfo = Info(executable, ["--serial=" + Serial, "--new-display=1600x1000/160", "--start-app=" + package, "--no-vd-system-decorations", "--no-vd-destroy-content", "--no-audio", "--no-clipboard-autosync", "--keyboard=sdk", "--prefer-text", "--shortcut-mod=rsuper", "--max-fps=25", "--video-bit-rate=6M", "--window-borderless", "--window-title=Astro - Portal · " + package, "--window-x=-20000", "--window-y=-20000"]) };
        process.OutputDataReceived += EngineOutput; process.ErrorDataReceived += EngineOutput;
        var session = new AppDisplaySession(process, package);
        try
        {
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            for (int i = 0; i < 180; i++)
            {
                token.ThrowIfCancellationRequested(); process.Refresh();
                if (process.HasExited) throw new IOException("The app display stopped. See the Android engine log.");
                if (process.MainWindowHandle != 0)
                {
                    var task = (await ReadTasksAsync(token)).FirstOrDefault(t => t.Package == package && t.Display > 0);
                    if (task is not null)
                    {
                        session.DisplayId = task.Display;
                        await ShellAsync(token, "wm", "set-user-rotation", "lock", "-d", task.Display.ToString(CultureInfo.InvariantCulture), "0");
                        await ShellAsync(token, "wm", "set-fix-to-user-rotation", "-d", task.Display.ToString(CultureInfo.InvariantCulture), "enabled");
                        appDisplays.Add(package, session);
                        await NormalizeAppDisplaysAsync(token);
                        return session;
                    }
                }
                await Task.Delay(250, token);
            }
            throw new TimeoutException("The independent app display did not finish starting.");
        }
        catch { session.Dispose(); throw; }
    }
    public async Task<bool> NeedsAppDisplayNormalizationAsync(CancellationToken token)
    {
        var tasks = await ReadTasksAsync(token);
        return appDisplays.Values.Where(s => s.IsAlive).Any(s => tasks.Any(t => t.Package == s.Package && t.Display == s.DisplayId && (t.Package == AsiairPackage && !IsAsiairControlScreen(t) ? t.Mode != "freeform" : t.Mode != "fullscreen")));
    }
    public async Task NormalizeAppDisplaysAsync(CancellationToken token)
    {
        var tasks = await ReadTasksAsync(token);
        foreach (var session in appDisplays.Values.Where(s => s.IsAlive))
        {
            var task = tasks.FirstOrDefault(t => t.Package == session.Package && t.Display == session.DisplayId);
            if (task is null) continue;
            string mode = task.Package == AsiairPackage && !IsAsiairControlScreen(task) ? "5" : "1";
            if ((mode == "5" && task.Mode == "freeform") || (mode == "1" && task.Mode == "fullscreen")) continue;
            await ShellAsync(token, "am", "task", "resizeable", task.Id.ToString(), "2");
            await ShellAsync(token, "env", "CLASSPATH=/data/local/tmp/astro-layout.apk", "app_process", "/", "local.asiair.home.LayoutControl", task.Id.ToString(), mode);
            if (mode == "5") await ShellAsync(token, "am", "task", "resize", task.Id.ToString(), "0", "0", "1600", "1000");
        }
    }
    public async Task CloseAppDisplayAsync(string package, CancellationToken token)
    {
        await ShellAsync(token, "am", "force-stop", package);
        if (appDisplays.Remove(package, out var display)) display.Dispose();
    }
    public async Task VerifyAppDisplaysAsync(CancellationToken token)
    {
        var first = await OpenAppDisplayAsync(AsiairPackage, token);
        var second = await OpenAppDisplayAsync(StellavitaPackage, token);
        if (first.DisplayId == second.DisplayId || !first.IsAlive || !second.IsAlive) throw new IOException("Apps did not get independent displays.");
        File.WriteAllText(Path.Combine(state,"virtual-display-tasks.txt"),await ShellAsync(token,"am","stack","list"));
        File.WriteAllText(Path.Combine(state,"virtual-display-verification.json"),System.Text.Json.JsonSerializer.Serialize(new { passed = true, asiairDisplay = first.DisplayId, stellavitaDisplay = second.DisplayId }));
    }
    private void CloseAllAppDisplays()
    {
        foreach (var display in appDisplays.Values) display.Dispose();
        appDisplays.Clear();
    }
    public async Task CopyInstalledAppToAsync(RuntimeController destination, string package, CancellationToken token)
    {
        if (package is not (AsiairPackage or StellavitaPackage)) throw new ArgumentException("Unsupported app.");
        if (!IsRunning || !destination.IsRunning) throw new InvalidOperationException("Both Android profiles must be ready.");
        if (package == AsiairPackage) await EnsureNativeAsiairAsync(token);
        string sourceVersion = await ShellAsync(token, "dumpsys", "package", package);
        string targetVersion = await destination.ShellAsync(token, "dumpsys", "package", package);
        string Version(string detail) => System.Text.RegularExpressions.Regex.Match(detail, @"\bversionCode=(\d+)").Groups[1].Value;
        if (Version(sourceVersion).Length > 0 && Version(sourceVersion) == Version(targetVersion) && targetVersion.Contains("primaryCpuAbi=x86_64")) return;
        string listing = await ShellAsync(token, "pm", "path", package);
        string transfer = Path.Combine(state, "clone-transfer"); Directory.CreateDirectory(transfer);
        var paths = new List<string>();
        try
        {
            foreach (string line in listing.Split('\n').Where(l => l.StartsWith("package:")))
            {
                string path = Path.Combine(transfer, "part-" + paths.Count + ".apk"); paths.Add(path);
                await AdbAsync(["-s", Serial, "pull", line[8..].Trim(), path], token, 180);
            }
            if (paths.Count == 0) throw new IOException("Install the main app before starting its clone.");
            await destination.AdbAsync(new[] { "-s",destination.Serial, paths.Count == 1 ? "install" : "install-multiple", "-r","-g","--abi","x86_64" }.Concat(paths),token,600,false);
            for (int i=0;i<60;i++)
            {
                try
                {
                    string detail = await destination.ShellAsync(token,"dumpsys","package",package);
                    if (Version(sourceVersion) == Version(detail) && detail.Contains("primaryCpuAbi=x86_64")) { await destination.RecordInstalledVersionAsync(token); return; }
                } catch (IOException) { }
                await Task.Delay(1000,token);
            }
            throw new IOException("Android did not confirm the cloned x86-64 app installation. Retry its launch button.");
        }
        finally { foreach (string path in paths) if (File.Exists(path)) File.Delete(path); }
    }
}
