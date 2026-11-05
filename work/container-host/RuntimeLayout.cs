using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AsiairContainer;

public sealed record AndroidApp(string Package, string Label);
public sealed record AndroidTask(int Id, int Stack, string Package, string Component, string Mode, bool Visible);
public sealed partial class RuntimeController
{
    public const string AsiairPackage = "com.zwoasi.asiair", StellavitaPackage = "com.touptek.astrostation";
    private readonly SemaphoreSlim layoutLock = new(1, 1);
    public bool SplitScreenActive { get; private set; }
    public int DisplayHeight { get; private set; } = 1600;
    private async Task SetDisplayHeightAsync(int height, CancellationToken token)
    {
        if (DisplayHeight == height) return;
        await ShellAsync(token, "wm", "size", $"2560x{height}");
        DisplayHeight = height;
        displayNeedsRestart = true;
        await Task.Delay(600, token);
    }
    public static IReadOnlyList<AndroidTask> ParseTasks(string listing)
    {
        var tasks = new List<AndroidTask>(); int stack = -1; string mode = "";
        foreach (string line in listing.Split('\n'))
        {
            var header = Regex.Match(line, @"^Stack id=(\d+)");
            if (header.Success) { stack = int.Parse(header.Groups[1].Value); mode = ""; }
            var config = Regex.Match(line, @"mWindowingMode=([\w-]+)"); if (config.Success) mode = config.Groups[1].Value;
            var task = Regex.Match(line, @"^\s+taskId=(\d+):\s+([\w.]+)/[^\s]+.*?visible=(true|false).*?topActivity=ComponentInfo\{([^}]+)\}");
            if (task.Success && stack >= 0) tasks.Add(new(int.Parse(task.Groups[1].Value), stack, task.Groups[2].Value, task.Groups[4].Value, mode, task.Groups[3].Value == "true"));
        }
        return tasks;
    }
    private async Task<IReadOnlyList<AndroidTask>> ReadTasksAsync(CancellationToken token) => ParseTasks(await ShellAsync(token, "am", "stack", "list"));
    public async Task<bool> HasRunningAstroAppAsync(CancellationToken token)
    {
        if (!IsRunning) return false;
        var tasks = await ReadTasksAsync(token);
        foreach (string package in new[] { AsiairPackage, StellavitaPackage })
            if (tasks.Any(t => t.Package == package) && !string.IsNullOrWhiteSpace(await AdbAsync(["-s", Serial, "shell", "pidof", package], token, 10, false))) return true;
        return false;
    }
    public async Task RefreshSplitScreenStateAsync(CancellationToken token)
    {
        var tasks = await ReadTasksAsync(token);
        SplitScreenActive = tasks.Any(t => t.Package == AsiairPackage && t.Visible && t.Mode == "split-screen-primary") && tasks.Any(t => t.Package == StellavitaPackage && t.Visible && t.Mode == "split-screen-secondary");
    }
    private async Task SetFullscreenAsync(AndroidTask task, CancellationToken token)
    {
        // ASIAIR declares its connection activities portrait-only. A full-size
        // freeform task ignores that request without modifying the signed APK.
        string mode = task.Package == AsiairPackage && !IsAsiairControlScreen(task) ? "5" : "1";
        await ShellAsync(token, "am", "task", "resizeable", task.Id.ToString(), "2");
        string result = await ShellAsync(token, "env", "CLASSPATH=/data/local/tmp/astro-layout.apk", "app_process", "/", "local.asiair.home.LayoutControl", task.Id.ToString(), mode);
        if (mode == "5") await ShellAsync(token, "am", "task", "resize", task.Id.ToString(), "0", "24", "2560", (DisplayHeight - 48).ToString());
        if (!result.Contains("Layout applied:")) throw new IOException("Android could not restore the full-screen app layout.");
    }
    public static bool IsAsiairControlScreen(AndroidTask task) => task.Package == AsiairPackage &&
        task.Component.EndsWith("/com.zwoasi.asiair.activity.AirActivity", StringComparison.Ordinal);
    public async Task<bool> NeedsAsiairCaptionRepairAsync(CancellationToken token)
    {
        if (!IsRunning || SplitScreenActive) return false;
        return (await ReadTasksAsync(token)).Any(t => t.Visible && IsAsiairControlScreen(t) && t.Mode == "freeform");
    }
    public async Task RepairAsiairCaptionAsync(CancellationToken token)
    {
        if (!await layoutLock.WaitAsync(0, token)) return;
        try
        {
            var task = (await ReadTasksAsync(token)).FirstOrDefault(t => t.Visible && IsAsiairControlScreen(t) && t.Mode == "freeform");
            if (task is null) return;
            await SetFullscreenAsync(task, token);
            await WaitForLayoutAsync(AsiairPackage, false, token);
        }
        finally { layoutLock.Release(); }
    }
    private async Task ExitSplitCoreAsync(CancellationToken token)
    {
        // Leave both tasks as ordinary fullscreen background tasks first. Only the
        // chosen foreground ASIAIR task receives the landscape compatibility mode.
        foreach (var task in (await ReadTasksAsync(token)).Where(t => t.Package is AsiairPackage or StellavitaPackage && (t.Mode.StartsWith("split-screen") || t.Mode == "freeform")))
            await ShellAsync(token, "env", "CLASSPATH=/data/local/tmp/astro-layout.apk", "app_process", "/", "local.asiair.home.LayoutControl", task.Id.ToString(), "1");
        await SetDisplayHeightAsync(1600, token);
        SplitScreenActive = false;
    }
    public async Task ShowAstroAppAsync(string package, CancellationToken token)
    {
        if (package is not (AsiairPackage or StellavitaPackage)) throw new ArgumentException("Choose an astronomy app.");
        if (!await layoutLock.WaitAsync(0, token)) throw new InvalidOperationException("A layout change is already in progress. Wait for it to finish.");
        try
        {
            await ExitSplitCoreAsync(token);
            await ShellAsync(token, "input", "keyevent", "3");
            if (package == AsiairPackage) await EnsureNativeAsiairAsync(token);
            var existing = (await ReadTasksAsync(token)).FirstOrDefault(t => t.Package == package);
            if (existing is null) { await LaunchAppAsync(package, token); existing = (await ReadTasksAsync(token)).FirstOrDefault(t => t.Package == package); }
            if (existing is not null) await SetFullscreenAsync(existing, token);
            await WaitForLayoutAsync(package, false, token);
        }
        finally { layoutLock.Release(); }
    }
    public async Task ShowOthersAsync(CancellationToken token)
    {
        if (!await layoutLock.WaitAsync(0, token)) throw new InvalidOperationException("A layout change is already in progress. Wait for it to finish.");
        try { await ExitSplitCoreAsync(token); await ShellAsync(token, "input", "keyevent", "3"); await WaitForLayoutAsync(null, false, token); }
        finally { layoutLock.Release(); }
    }
    public async Task<IReadOnlyList<AndroidApp>> ListOtherAppsAsync(CancellationToken token)
    {
        string result = await ShellAsync(token, "env", "CLASSPATH=/data/local/tmp/astro-layout.apk", "app_process", "/", "local.asiair.home.LayoutControl", "apps");
        string? json = result.Split('\n').FirstOrDefault(line => line.StartsWith("ASTRO_APPS="))?[11..];
        if (json is null) throw new IOException("Android could not list installed apps.");
        return (JsonSerializer.Deserialize<List<AndroidApp>>(json) ?? []).Where(app => app.Package is not (AsiairPackage or StellavitaPackage or "local.asiair.home")).ToArray();
    }
    private async Task<AndroidTask> EnsureAstroTaskAsync(string package, CancellationToken token)
    {
        string installed = await ShellAsync(token, "pm", "path", package);
        if (!installed.Contains(".apk")) throw new InvalidOperationException("Install both ASIAIR and Stellavita before using split screen.");
        var existing = (await ReadTasksAsync(token)).FirstOrDefault(t => t.Package == package);
        if (existing is not null && !existing.Component.Contains("Splash", StringComparison.OrdinalIgnoreCase)) return existing;
        await LaunchAppAsync(package, token);
        for (int i = 0; i < 75; i++)
        {
            var task = (await ReadTasksAsync(token)).FirstOrDefault(t => t.Package == package);
            if (task is not null && !task.Component.Contains("Splash", StringComparison.OrdinalIgnoreCase)) return task;
            await Task.Delay(1000, token);
        }
        throw new TimeoutException("The app is still starting. Finish its welcome screen, then try split screen again.");
    }
    public async Task ShowSplitScreenAsync(CancellationToken token)
    {
        if (!await layoutLock.WaitAsync(0, token)) throw new InvalidOperationException("A layout change is already in progress. Wait for it to finish.");
        try
        {
            if (!await HasRunningAstroAppAsync(token)) throw new InvalidOperationException("Start ASIAIR or Stellavita first.");
            // Check both installations before changing the existing layout.
            foreach (string package in new[] { AsiairPackage, StellavitaPackage })
                if (!(await ShellAsync(token, "pm", "path", package)).Contains(".apk")) throw new InvalidOperationException("Install both ASIAIR and Stellavita before using split screen.");
            await EnsureNativeAsiairAsync(token);
            var asiair = await EnsureAstroTaskAsync(AsiairPackage, token);
            var stella = await EnsureAstroTaskAsync(StellavitaPackage, token);
            foreach (var task in new[] { asiair, stella }) await ShellAsync(token, "am", "task", "resizeable", task.Id.ToString(), "2");
            await SetDisplayHeightAsync(800, token);
            string listing = await ShellAsync(token, "am", "stack", "list");
            int StackFor(string mode)
            {
                var match = Regex.Match(listing, @"Stack id=(\d+)[^\r\n]*\r?\n[^\r\n]*mWindowingMode=" + mode + @"\b");
                if (!match.Success) throw new IOException("Android split-screen stacks are unavailable. Stop Android and start it again.");
                return int.Parse(match.Groups[1].Value);
            }
            await ShellAsync(token, "am", "stack", "move-task", asiair.Id.ToString(), StackFor("split-screen-primary").ToString(), "true");
            await ShellAsync(token, "am", "stack", "move-task", stella.Id.ToString(), StackFor("split-screen-secondary").ToString(), "true");
            await Task.Delay(1500, token);
            var result = await ReadTasksAsync(token);
            if (!result.Any(t => t.Id == asiair.Id && t.Visible && t.Mode == "split-screen-primary") || !result.Any(t => t.Id == stella.Id && t.Visible && t.Mode == "split-screen-secondary")) throw new IOException("Both apps did not enter split screen. Try opening each app first.");
            SplitScreenActive = true;
            await WaitForLayoutAsync(null, true, token);
        }
        catch { await ExitSplitCoreAsync(CancellationToken.None); throw; }
        finally { layoutLock.Release(); }
    }
}
