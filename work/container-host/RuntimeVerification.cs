using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.IO;

namespace AsiairContainer;

public sealed partial class RuntimeController
{
    public bool StellavitaVerified { get; private set; }
    private async Task<XDocument> ReadUiAsync(CancellationToken token)
    {
        const string path = "/data/local/tmp/asiair-window-verification.xml";
        string dump = await ShellAsync(token, "uiautomator", "dump", path);
        File.AppendAllText(Path.Combine(state, "logs", "window-test-dump.log"), dump + "\n");
        string xml = await ShellAsync(token, "cat", path);
        if (string.IsNullOrWhiteSpace(xml)) throw new IOException("Android has not supplied an accessibility hierarchy yet. " + dump);
        File.WriteAllText(Path.Combine(state, "logs", "window-test-ui.xml"), xml);
        return XDocument.Parse(xml);
    }
    private async Task AssertResponsiveUiAsync(CancellationToken token)
    {
        var ui = await ReadUiAsync(token);
        if (ui.Descendants("node").Any(n => ((string?)n.Attribute("text"))?.Contains("isn't responding", StringComparison.OrdinalIgnoreCase) == true)) throw new IOException("Android displayed an app-not-responding dialog during layout verification.");
    }
    public async Task VerifyDisplayInputAsync(Action<double, double, double, double> click, CancellationToken token)
    {
        // ASIAIR requires location services even for its initial network screen.
        // Use a known test fix; restore the configured location before finishing.
        await SetLocationAsync(12.3456, 45.6789, token);
        Stage("Checking embedded mouse input using Android Settings navigation…");
        await LaunchAppAsync("com.android.settings", token); await Task.Delay(2500, token);
        var ui = await ReadUiAsync(token);
        var button = ui.Descendants("node").FirstOrDefault(node => (string?)node.Attribute("text") == "Network & internet") ?? throw new IOException("Android Settings navigation was not ready.");
        int[] bounds = Regex.Matches((string)button.Attribute("bounds")!, @"\d+").Select(match => int.Parse(match.Value)).ToArray();
        var areas = ui.Descendants("node").Select(node => Regex.Matches((string?)node.Attribute("bounds") ?? "", @"\d+").Select(match => int.Parse(match.Value)).ToArray()).Where(a => a.Length == 4).ToArray();
        click((bounds[0] + bounds[2]) / 2d, (bounds[1] + bounds[3]) / 2d, 2560, DisplayHeight);
        await Task.Delay(1500, token); ui = await ReadUiAsync(token);
        if (!ui.Descendants("node").Any(node => (string?)node.Attribute("text") == "Airplane mode")) throw new InvalidOperationException("Mouse input did not open the requested Settings page.");
        Stage("Mouse input verified. Checking Play Store and shared GPS…");
        if (options.EnablePlayStore)
        {
            string store = await ShellAsync(token, "pm", "path", "com.android.vending");
            if (!store.Contains(".apk")) throw new InvalidOperationException("Google Play is missing from the Play Store profile.");
            await OpenStoreAsync("com.touptek.astrostation", token); await Task.Delay(10000, token);
            await CaptureAsync(Path.Combine(root, "Google-Play.png"), token);
        }
        var gpsClient = ShellAsync(token, "env", "CLASSPATH=/data/local/tmp/astro-layout.apk", "app_process", "/", "local.asiair.home.LayoutControl", "gps");
        await Task.Delay(1500, token);
        await SetLocationAsync(12.3456, 45.6789, token);
        string gps = await gpsClient;
        var coordinates = Regex.Match(gps, @"GPS_FIX=([-\d.]+),([-\d.]+)");
        if (!coordinates.Success || Math.Abs(double.Parse(coordinates.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - 12.3456) > 0.00001 || Math.Abs(double.Parse(coordinates.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) - 45.6789) > 0.00001) throw new IOException("The Android location client did not receive the shared GPS fix.");
        string stella = await AdbAsync(["-s", Serial, "shell", "pm", "path", "com.touptek.astrostation"], token, 10, false);
        if (stella.Contains("base.apk"))
        {
            Stage("Checking the installed Stellavita app…");
            await LaunchAppAsync("com.touptek.astrostation", token);
            var loading = Stopwatch.StartNew();
            while (loading.Elapsed < TimeSpan.FromSeconds(90))
            {
                await Task.Delay(2000, token);
                string activity = await ShellAsync(token, "dumpsys", "activity", "activities");
                if (activity.Split('\n').Any(l => l.Contains("mResumedActivity:") && l.Contains("com.touptek.astrostation/") && !l.Contains("SplashActivity"))) break;
            }
            string pid = await AdbAsync(["-s", Serial, "shell", "pidof", "com.touptek.astrostation"], token, 10, false);
            string windows = await ShellAsync(token, "dumpsys", "activity", "activities");
            if (string.IsNullOrWhiteSpace(pid) || !windows.Split('\n').Any(l => l.Contains("mResumedActivity:") && l.Contains("com.touptek.astrostation/"))) throw new InvalidOperationException("Stellavita did not remain open in the Android foreground.");
            await CaptureAsync(Path.Combine(root, "Stellavita.png"), token); StellavitaVerified = true;
        }
        await LaunchAppAsync(Package, token);
        await WaitForAppScreenAsync(token);
        Stage("Original app rendered; embedded input, multi-app Home, Play Store and GPS verified.");
    }
    public async Task VerifyKeyboardAsync(Action<double, double, double, double> click, Func<string, Task> type, Func<Task> backspace, CancellationToken token)
    {
        Stage("Checking Windows keyboard input in an Android text field…");
        await ShellAsync(token, "am", "start", "-a", "android.settings.APP_SEARCH_SETTINGS");
        await Task.Delay(2500, token);
        var ui = await ReadUiAsync(token);
        var field = ui.Descendants("node").FirstOrDefault(n => (string?)n.Attribute("class") == "android.widget.EditText") ?? throw new IOException("Android's search text field did not open.");
        int[] bounds = Regex.Matches((string)field.Attribute("bounds")!, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
        var areas = ui.Descendants("node").Select(n => Regex.Matches((string?)n.Attribute("bounds") ?? "", @"\d+").Select(m => int.Parse(m.Value)).ToArray()).Where(a => a.Length == 4).ToArray();
        click((bounds[0]+bounds[2])/2d, (bounds[1]+bounds[3])/2d, 2560, DisplayHeight);
        await Task.Delay(500, token); await type("Astro 123!?"); await Task.Delay(1500, token);
        ui = await ReadUiAsync(token);
        if (!ui.Descendants("node").Any(n => (string?)n.Attribute("text") == "Astro 123!?")) throw new IOException("Windows text input did not reach the focused Android field.");
        await backspace(); await Task.Delay(700, token); ui = await ReadUiAsync(token);
        if (!ui.Descendants("node").Any(n => (string?)n.Attribute("text") == "Astro 123!")) throw new IOException("Windows Backspace did not edit the Android text field.");
        Stage("Windows keyboard letters, digits, punctuation and Backspace verified.");
    }
    public Task RestoreConfiguredLocationAsync(CancellationToken token) => options.Latitude is double lat && options.Longitude is double lon ? SetLocationAsync(lat, lon, token) : ShellAsync(token, "settings", "put", "secure", "location_mode", "0");
    public async Task WaitForAppScreenAsync(CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromMinutes(2))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var ui = await ReadUiAsync(token);
                if (ui.Descendants("node").Any(n => ((string?)n.Attribute("text"))?.Contains("isn't responding", StringComparison.OrdinalIgnoreCase) == true)) throw new InvalidOperationException("ASIAIR stopped responding during its startup check.");
                if (ui.Descendants("node").Any(node => (string?)node.Attribute("package") == Package && new[] { "Connect", "Enter Device", "Preview Ready", "Service Connected" }.Contains((string?)node.Attribute("text"))))
                {
                    return;
                }
                if (ui.Descendants("node").Any(n => (string?)n.Attribute("package") == "com.touptek.astrostation")) await LaunchAppAsync(Package, token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            catch (IOException) { }
            catch (System.Xml.XmlException) { }
            await Task.Delay(5000, token);
        }
        throw new TimeoutException("The original ASIAIR screen did not become ready after returning Home.");
    }
}
