using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AsiairContainer;

public sealed partial class RuntimeController : IAsyncDisposable
{
    public static readonly string[] ApkNames = ["base.apk", "split_asset_pack_0.apk", "split_config.arm64_v8a.apk", "split_config.en.apk", "split_config.xhdpi.apk"];
    private const string Package = "com.zwoasi.asiair";
    private readonly string root, sdk, state, adb;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private Process? emulator, screen;
    private readonly object logLock = new();
    private bool engineStarted, screenStarted, ownsAdb;
    private ContainerOptions options;
    private bool stopping, displayRestarting;
    private volatile bool displayNeedsRestart;
    public bool DisplayNeedsRestart => displayNeedsRestart;
    public bool IsRunning { get; private set; }
    public bool HasLiveDisplay => screenStarted && screen is { HasExited: false };
    public event Action<string>? Status;
    public event Action<string>? Log;
    public event Action? DisplayExited;
    private string Serial => $"127.0.0.1:{options.ConsolePort + 1}";
    private string ProfileName => options.ProfileName;
    private int ImageApi => options.EnablePlayStore ? options.AndroidApi : 30;
    private string ImageTag => options.EnablePlayStore ? "google_apis_playstore" : "google_apis";
    public string StateDirectory => state;
    public RuntimeController(string root, ContainerOptions options, bool secondProfile = false)
    {
        this.root = Path.GetFullPath(root); this.options = options;
        sdk = Path.Combine(root, "runtime", "sdk"); state = secondProfile ? Path.Combine(root, "private-data", "profiles", "second") : Path.Combine(root, "private-data"); adb = Path.Combine(sdk, "platform-tools", "adb.exe");
    }
    public void SetOptions(ContainerOptions value) { if (IsRunning) throw new InvalidOperationException("Stop the runtime before changing settings."); value.Validate(); options = value; }
    private void Stage(string value) { Status?.Invoke(value); Log?.Invoke(value); }
    public void VerifyFiles()
    {
        options.Validate();
        foreach (string path in new[] { adb, Path.Combine(root, "runtime", "ASIAIR-Home.apk"), Path.Combine(sdk, "emulator", "emulator.exe"), Path.Combine(sdk, "system-images", "android-" + ImageApi, ImageTag, "x86_64", "system.img") })
            if (!File.Exists(path)) throw new FileNotFoundException("A runtime component is missing. Restore the runtime folder next to this launcher.", path);
        if (!Directory.Exists(Path.Combine(root, "runtime", "screen"))) throw new DirectoryNotFoundException("The display component is missing.");
    }
    private Dictionary<string, string> EnvironmentValues() => new()
    {
        ["ANDROID_SDK_ROOT"] = sdk, ["ANDROID_HOME"] = sdk,
        ["ANDROID_AVD_HOME"] = Path.Combine(state, "avd"), ["ANDROID_EMULATOR_HOME"] = Path.Combine(state, "emulator-home"),
        ["ANDROID_USER_HOME"] = Path.Combine(state, "android-home"), ["ANDROID_ADB_SERVER_PORT"] = options.AdbPort.ToString(),
        ["ADB_SERVER_SOCKET"] = $"tcp:127.0.0.1:{options.AdbPort}", ["ADB"] = adb, ["ANDROID_SERIAL"] = Serial
    };
    private ProcessStartInfo Info(string executable, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        foreach (var pair in EnvironmentValues()) info.Environment[pair.Key] = pair.Value;
        return info;
    }
    private async Task<string> AdbAsync(IEnumerable<string> arguments, CancellationToken token, int seconds = 30, bool required = true)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        using var process = new Process { StartInfo = Info(adb, new[] { "-P", options.AdbPort.ToString() }.Concat(arguments)) };
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token); string stdout = await output; string stderr = await error;
            if (required && process.ExitCode != 0) throw new IOException((stderr + "\n" + stdout).Trim());
            return stdout.Trim();
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
            throw;
        }
    }
    private Task<string> ShellAsync(CancellationToken token, params string[] arguments) => AdbAsync(new[] { "-s", Serial, "shell" }.Concat(arguments), token);
    private static bool PortOccupied(int port)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try { return socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port)).Wait(200) && socket.Connected; } catch { return false; }
    }
    public void ConfigureAvd()
    {
        foreach (string folder in new[] { state, Path.Combine(state, "logs"), Path.Combine(state, "emulator-home"), Path.Combine(state, "android-home"), Path.Combine(state, "avd") }) Directory.CreateDirectory(folder);
        string device = Path.Combine(state, "avd", ProfileName + ".avd"); Directory.CreateDirectory(device);
        // The stock userdata partition is 6 GB of mostly empty space. NTFS folder
        // compression keeps newly created baseline files compact without changing Android.
        if (!File.Exists(Path.Combine(device, "userdata-qemu.img")))
        {
            try { using var compact = Process.Start(Info(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.System), "compact.exe"), ["/C", "/I", "/Q", device])); compact?.WaitForExit(5000); } catch (System.ComponentModel.Win32Exception) { }
        }
        File.WriteAllText(Path.Combine(state, "avd", ProfileName + ".ini"), $"avd.ini.encoding=UTF-8\npath={device}\ntarget=android-{ImageApi}\n", new UTF8Encoding(false));
        var values = new Dictionary<string, string>
        {
            ["AvdId"]=ProfileName, ["avd.ini.displayname"]="Astro - Portal", ["avd.ini.encoding"]="UTF-8", ["abi.type"]="x86_64", ["hw.cpu.arch"]="x86_64",
            ["image.sysdir.1"]=$"system-images/android-{ImageApi}/{ImageTag}/x86_64/", ["tag.id"]=ImageTag, ["tag.display"]=options.EnablePlayStore ? "Google Play" : "Google APIs", ["target"]="android-" + ImageApi,
            ["PlayStore.enabled"]=options.EnablePlayStore ? "true" : "false", ["hw.cpu.ncore"]=options.CpuCores.ToString(), ["hw.ramSize"]=options.MemoryMb.ToString(), ["vm.heapSize"]="256",
            ["disk.dataPartition.size"]="4G", ["hw.lcd.width"]="2560", ["hw.lcd.height"]="800", ["hw.lcd.density"]="160", ["hw.keyboard"]="yes",
            ["hw.camera.back"]="none", ["hw.camera.front"]="none", ["hw.audioInput"]="no", ["hw.sdCard"]="no", ["hw.gps"]="yes", ["hw.gpu.enabled"]="yes", ["hw.gpu.mode"]="software",
            ["showDeviceFrame"]="no", ["skin.dynamic"]="no", ["skin.name"]="2560x800", ["hw.initialOrientation"]="landscape", ["hw.device.name"]="Astro_Portal_Tablet", ["hw.device.manufacturer"]="Astro - Portal", ["hw.mainKeys"]="no", ["hw.gsmModem"]="no", ["hw.accelerometer"]="no", ["hw.sensors.orientation"]="no", ["fastboot.forceColdBoot"]="yes", ["fastboot.forceFastBoot"]="no"
        };
        File.WriteAllText(Path.Combine(device, "config.ini"), string.Join("\n", values.Select(pair => pair.Key + "=" + pair.Value)) + "\n", new UTF8Encoding(false));
    }
    public async Task StartAsync(CancellationToken token)
    {
        await lifecycle.WaitAsync(token);
        try
        {
            if (IsRunning) return;
            VerifyFiles(); ConfigureAvd();
            foreach (int port in new[] { options.AdbPort, options.ConsolePort, options.ConsolePort + 1 })
                if (PortOccupied(port)) throw new IOException($"Private runtime port {port} is already in use. Stop the earlier ASIAIR runtime before starting another one.");
            Stage("Starting private Android environment…");
            await AdbAsync(["start-server"], token);
            ownsAdb = true;
            emulator = new Process { StartInfo = Info(Path.Combine(sdk, "emulator", "emulator.exe"),
                ["-avd", ProfileName, "-port", options.ConsolePort.ToString(), "-no-window", "-no-audio", "-no-boot-anim", "-no-snapshot", "-no-snapstorage", "-feature", "-QuickbootFileBacked", "-gpu", "software", "-memory", options.MemoryMb.ToString(), "-cores", options.CpuCores.ToString(), "-camera-back", "none", "-camera-front", "none", "-netdelay", "none", "-netspeed", "full"]) };
            emulator.OutputDataReceived += EngineOutput; emulator.ErrorDataReceived += EngineOutput;
            emulator.Start(); engineStarted = true; emulator.BeginOutputReadLine(); emulator.BeginErrorReadLine();
            File.WriteAllText(Path.Combine(state, "host-session.json"), JsonSerializer.Serialize(new { pid = emulator.Id, started = emulator.StartTime.ToUniversalTime(), root }));
            var timer = Stopwatch.StartNew(); bool booted = false;
            while (timer.Elapsed < TimeSpan.FromMinutes(4))
            {
                token.ThrowIfCancellationRequested();
                if (emulator.HasExited) throw new IOException("The Android engine stopped before boot completed. See private-data/logs/engine.log.");
                try
                {
                    await AdbAsync(["connect", Serial], token, 6, false);
                    booted = await AdbAsync(["-s", Serial, "shell", "getprop", "sys.boot_completed"], token, 6, false) == "1";
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                if (booted) break;
                await Task.Delay(2000, token);
            }
            if (!booted) throw new TimeoutException("Android did not finish starting within four minutes.");
            string abis = await ShellAsync(token, "getprop", "ro.product.cpu.abilist");
            if (!abis.Split(',').Contains("arm64-v8a")) throw new IOException("The Android image does not support the ARM64 libraries required by ASIAIR.");
            Stage("Android ready. Configuring the landscape tablet…");
            await ShellAsync(token, "wm", "size", "2560x1600"); DisplayHeight = 1600; SplitScreenActive = false;
            await ConfigureGuestAsync(token);
            await AdbAsync(["-s", Serial, "push", Path.Combine(root, "runtime", "ASIAIR-Home.apk"), "/data/local/tmp/astro-layout.apk"], token);

            // App installation is explicit through Install ASIAIR / Install Stellavita.
            await SlimGuestAsync(token);
            await RecordInstalledVersionAsync(token);
            await ShellAsync(token, "input", "keyevent", "3");
            IsRunning = true;
            if (options.Latitude is double latitude && options.Longitude is double longitude) await SetLocationAsync(latitude, longitude, token);
            Stage("Android is ready. Open ASIAIR, Stellavita, or Google Play.");
        }
        catch
        {
            if (engineStarted)
            {
                try { string system = await AdbAsync(["-s", Serial, "shell", "logcat", "-d", "-t", "2500"], CancellationToken.None, 8, false); File.WriteAllText(Path.Combine(state, "logs", "startup-system.log"), system); } catch { }
                try { string crash = await AdbAsync(["-s", Serial, "shell", "logcat", "-d", "-b", "crash", "-t", "120"], CancellationToken.None, 8, false); File.WriteAllText(Path.Combine(state, "logs", "startup-crash.log"), crash); } catch { }
            }
            await StopCoreAsync(); throw;
        }
        finally { lifecycle.Release(); }
    }
    private void EngineOutput(object sender, DataReceivedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.Data)) return;
        if (eventArgs.Data.Contains("DeadSystemException") || eventArgs.Data.Contains("DeadObjectException")) displayNeedsRestart = true;
        string path = Path.Combine(state, "logs", "engine.log");
        lock (logLock)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, eventArgs.Data + "\n");
            }
            catch { }
        }
    }
    private async Task ConfigureGuestAsync(CancellationToken token)
    {
        foreach (var item in new (string Area, string Key, string Value)[]
        {
            ("global","device_provisioned","1"),("secure","user_setup_complete","1"),("global","window_animation_scale","0"),
            ("global","transition_animation_scale","0"),("global","animator_duration_scale","0"),("system","screen_off_timeout","2147483647"),
            ("secure","location_mode",options.Latitude.HasValue ? "3" : "0"),
            ("system","accelerometer_rotation","0"),("system","user_rotation","0"),
            ("global","force_resizable_activities","1"),("global","enable_freeform_support","1"),("global","policy_control","immersive.full=com.zwoasi.asiair,com.touptek.astrostation")
        }) await ShellAsync(token, "settings", "put", item.Area, item.Key, item.Value);
        await ApplyAndroidFontScaleCoreAsync(options.AndroidFontScale, token);
        await ShellAsync(token, "wm", "set-user-rotation", "lock", "0");
        await ShellAsync(token, "wm", "set-fix-to-user-rotation", "enabled");
    }
    public async Task<nint> OpenDisplayAsync(CancellationToken token)
    {
        if (!IsRunning) throw new InvalidOperationException("Start Android first.");
        if (screen is { HasExited: true }) { screen.Dispose(); screen = null; screenStarted = false; }
        string path = Directory.EnumerateFiles(Path.Combine(root, "runtime", "screen"), "scrcpy.exe", SearchOption.AllDirectories).Single();
        screen = new Process { StartInfo = Info(path, ["--serial=" + Serial, "--no-audio", "--no-clipboard-autosync", "--keyboard=sdk", "--prefer-text", "--shortcut-mod=rsuper", "--max-fps=30", "--video-bit-rate=8M", "--window-borderless", "--window-title=Astro - Portal", "--window-x=-20000", "--window-y=-20000"]), EnableRaisingEvents = true };
        screen.OutputDataReceived += EngineOutput; screen.ErrorDataReceived += EngineOutput;
        screen.Exited += (_, _) => { if (!stopping && !displayRestarting) DisplayExited?.Invoke(); };
        screen.Start(); screenStarted = true; screen.BeginOutputReadLine(); screen.BeginErrorReadLine();
        for (int i = 0; i < 150; i++)
        {
            token.ThrowIfCancellationRequested(); screen.Refresh();
            if (screen.HasExited) throw new IOException("The Android display stopped. See the runtime log for details.");
            if (screen.MainWindowHandle != 0) { displayNeedsRestart = false; return screen.MainWindowHandle; }
            await Task.Delay(100, token);
        }
        throw new TimeoutException("The Android display window did not become ready.");
    }
    public async Task<nint> RestartDisplayAsync(CancellationToken token)
    {
        displayRestarting = true;
        try
        {
            if (screenStarted && screen is { HasExited: false }) { screen.Kill(); await screen.WaitForExitAsync(token); }
            screen?.Dispose(); screen = null; screenStarted = false;
            return await OpenDisplayAsync(token);
        }
        finally { displayRestarting = false; }
    }
    public async Task CaptureAsync(string destination, CancellationToken token)
    {
        using var process = new Process { StartInfo = Info(adb, ["-P", options.AdbPort.ToString(), "-s", Serial, "exec-out", "screencap", "-p"]) };
        process.Start(); var error = process.StandardError.ReadToEndAsync(token);
        await using (var file = File.Create(destination)) await process.StandardOutput.BaseStream.CopyToAsync(file, token);
        await process.WaitForExitAsync(token); if (process.ExitCode != 0) throw new IOException(await error);
    }
    public async Task StopAsync()
    {
        await lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { lifecycle.Release(); }
    }
    private async Task StopCoreAsync()
    {
        stopping = true;
        try
        {
            CloseAllAppDisplays();
            if (screenStarted && screen is { HasExited: false }) { screen.Kill(); await screen.WaitForExitAsync(); }
            screen?.Dispose(); screen = null; screenStarted = false;
            if (emulator is not null && engineStarted)
            {
                try { await AdbAsync(["-s", $"emulator-{options.ConsolePort}", "emu", "kill"], CancellationToken.None, 8, false); } catch { }
                try { await emulator.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (TimeoutException) { if (!emulator.HasExited) { emulator.Kill(entireProcessTree: true); await emulator.WaitForExitAsync(); } }
                for (int i = 0; i < 100 && (PortOccupied(options.ConsolePort) || PortOccupied(options.ConsolePort + 1)); i++) await Task.Delay(100);
                if (PortOccupied(options.ConsolePort) || PortOccupied(options.ConsolePort + 1)) throw new IOException("Android is still closing. Wait before starting it again.");
            }
            emulator?.Dispose(); emulator = null; engineStarted = false;
            if (ownsAdb)
            {
                string unusedRam = Path.Combine(state, "avd", ProfileName + ".avd", "snapshots", "default_boot", "ram.img");
                try { File.Delete(unusedRam); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            if (ownsAdb)
            {
                try { await AdbAsync(["kill-server"], CancellationToken.None, 5, false); } catch { }
                ownsAdb = false;
            }
            IsRunning = false; Stage("Stopped. Your private Android app data is preserved.");
        }
        finally { stopping = false; }
    }
    public async ValueTask DisposeAsync() => await StopAsync();
}

