using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AsiairContainer;

public sealed record InstalledAppInfo(string Package, string Version, string Profile, DateTimeOffset CheckedAt);
public sealed partial class RuntimeController
{
    public Task RefreshInstalledAppInfoAsync(CancellationToken token) => RecordInstalledVersionAsync(token);
    public async Task InstallApksAsync(IReadOnlyList<string> paths, CancellationToken token)
    {
        if (!IsRunning) throw new InvalidOperationException("Start Android first.");
        if (paths.Count == 0 || paths.Any(p => !File.Exists(p) || !string.Equals(Path.GetExtension(p), ".apk", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Choose APK files to install.");
        displayNeedsRestart = true;
        string result = await AdbAsync(new[] { "-s", Serial, paths.Count == 1 ? "install" : "install-multiple", "-r", "-g" }.Concat(paths.Select(Path.GetFullPath)), token, 420);
        if (!result.Contains("Success", StringComparison.OrdinalIgnoreCase)) throw new IOException("Android did not confirm installation: " + result);
        await RecordInstalledVersionAsync(token);
    }
    private async Task RecordInstalledVersionAsync(CancellationToken token)
    {
        foreach (var app in new[] { (Package, "installed-app-info.json"), ("com.touptek.astrostation", "installed-stellavita-info.json") })
        {
            string detail = await ShellAsync(token, "dumpsys", "package", app.Item1);
            var version = Regex.Match(detail, @"\bversionName=([^\s]+)");
            string path = Path.Combine(state, app.Item2);
            if (version.Success) File.WriteAllText(path, JsonSerializer.Serialize(new InstalledAppInfo(app.Item1, version.Groups[1].Value, ProfileName, DateTimeOffset.UtcNow)));
            else if (File.Exists(path)) File.Delete(path);
        }
    }
    public async Task LaunchAppAsync(string package, CancellationToken token)
    {
        if (!IsRunning) throw new InvalidOperationException("Start Android first.");
        if (!Regex.IsMatch(package, @"^[a-zA-Z][a-zA-Z0-9_]*(?:\.[a-zA-Z0-9_]+)+$")) throw new ArgumentException("Invalid app package.");
        string resolved = await ShellAsync(token, "cmd", "package", "resolve-activity", "--brief", "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER", "-p", package);
        string? component = resolved.Split('\n').Select(x => x.Trim()).LastOrDefault(x => x.StartsWith(package + "/", StringComparison.Ordinal));
        if (component is null) { await OpenStoreAsync(package, token); return; }
        string result = package == AsiairPackage
            ? await ShellAsync(token, "am", "start", "-W", "--windowingMode", "5", "-n", component)
            : await ShellAsync(token, "am", "start", "-W", "-n", component);
        if (result.Contains("Error:", StringComparison.Ordinal)) throw new IOException(result);
    }
    public async Task OpenStoreAsync(string? package, CancellationToken token)
    {
        if (!IsRunning) throw new InvalidOperationException("Start Android first.");
        if (!options.EnablePlayStore) throw new InvalidOperationException("Google Play is available in the Play Store profile.");
        string uri = package is null ? "market://details?id=com.touptek.astrostation" : "market://details?id=" + Uri.EscapeDataString(package);
        string result = await ShellAsync(token, "am", "start", "-a", "android.intent.action.VIEW", "-d", uri, "-p", "com.android.vending");
        if (result.Contains("Error:", StringComparison.Ordinal)) throw new IOException(result);
    }
    public Task NavigateAsync(bool home, CancellationToken token) => ShellAsync(token, "input", "keyevent", home ? "3" : "4");
    public async Task SetLocationAsync(double latitude, double longitude, CancellationToken token)
    {
        if (!IsRunning) throw new InvalidOperationException("Start Android before sending location.");
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 || !double.IsFinite(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(latitude), "Invalid location coordinates.");
        await ShellAsync(token, "settings", "put", "secure", "location_mode", "3");
        await FeedLocationAsync(latitude, longitude, token);
    }
    public async Task FeedLocationAsync(double latitude, double longitude, CancellationToken token)
    {
        if (!IsRunning) throw new InvalidOperationException("Start Android before sending location.");
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 || !double.IsFinite(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(latitude));
        string result = await AdbAsync(["-s", $"emulator-{options.ConsolePort}", "emu", "geo", "fix", longitude.ToString("R", CultureInfo.InvariantCulture), latitude.ToString("R", CultureInfo.InvariantCulture)], token, 10);
        if (result.Contains("KO:", StringComparison.Ordinal)) throw new IOException("Android did not accept the location: " + result);
    }
}
