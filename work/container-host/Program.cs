using System.IO;
using System.Text.Json;
using System.Windows;

namespace AsiairContainer;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        string root = AppContext.BaseDirectory;
        int rootIndex = Array.IndexOf(args, "--root"); if (rootIndex >= 0 && rootIndex + 1 < args.Length) root = Path.GetFullPath(args[rootIndex + 1]);
        bool runtimeSession = !args.Any(a => a is "--verify-ui" or "--verify-contrast" or "--diagnose" or "--verify-location");
        using var session = new LauncherSession(root, runtimeSession); if (!session.Acquired) return;
        var app = new Application(); session.Observe(app);
        if (args.Contains("--verify-contrast")) app.Startup += (_, _) => { var window = ContrastVerification.Create(root); app.MainWindow = window; window.Show(); };
        else if (args.Contains("--verify-app") || args.Contains("--diagnose") || args.Contains("--verify-location") || args.Contains("--probe-layout") || args.Contains("--verify-landscape"))
        {
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Startup += async (_, _) =>
            {
                int code = 0;
                await using var runtime = new RuntimeController(root, ContainerOptions.Load(root));
                runtime.Status += Console.WriteLine;
                try
                {
                    runtime.VerifyFiles();
                    if (args.Contains("--verify-landscape")) { using var token = new CancellationTokenSource(TimeSpan.FromMinutes(5)); await runtime.StartAsync(token.Token); await runtime.VerifyLandscapeAsync(token.Token); }
                    else if (args.Contains("--probe-layout")) { using var token = new CancellationTokenSource(TimeSpan.FromMinutes(5)); await runtime.StartAsync(token.Token); await runtime.ProbeLandscapeAsync(token.Token); }
                    else if (args.Contains("--verify-location"))
                    {
                        object result;
                        try { using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(40)); var fix = await WindowsLocation.GetAsync(cancellation.Token); result = new { available = true, accuracyMetres = fix.AccuracyMetres }; }
                        catch (Exception error) { result = new { available = false, manualFallback = true, message = error.Message }; }
                        Directory.CreateDirectory(Path.Combine(root, "private-data"));
                        await File.WriteAllTextAsync(Path.Combine(root, "private-data", "windows-location-verification.json"), JsonSerializer.Serialize(result));
                    }
                    else if (args.Contains("--verify-app"))
                    {
                        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(12));
                        await runtime.StartAsync(cancellation.Token);
                        await Task.Delay(10000, cancellation.Token);
                        string screenshot = Path.Combine(root, "ASIAIR-original-app.png");
                        await runtime.CaptureAsync(screenshot, cancellation.Token);
                        Console.WriteLine("Original app screenshot: " + screenshot);
                    }
                    else Console.WriteLine("Runtime files and configuration verified.");
                }
                catch (Exception error) { code = 1; Console.Error.WriteLine(error); }
                finally { await runtime.StopAsync(); app.Shutdown(code); }
            };
        }
        else app.Startup += (_, _) =>
        {
            var window = new MainWindow(root); app.MainWindow = window;
            bool verificationStarted = false;
            if (args.Contains("--verify-window")) window.Loaded += async (_, _) => { if (verificationStarted) return; verificationStarted = true; await window.VerifyWindowAsync(); };
            if (args.Contains("--verify-keyboard")) window.Loaded += async (_, _) => { if (verificationStarted) return; verificationStarted = true; await window.VerifyKeyboardWindowAsync(); };
            if (args.Contains("--verify-portal")) window.Loaded += async (_, _) => { if (verificationStarted) return; verificationStarted = true; await window.VerifyPortalAsync(); };
            if (args.Contains("--verify-toolbar")) window.Loaded += async (_, _) => { if (verificationStarted) return; verificationStarted = true; await window.VerifyToolbarAsync(); };
            if (args.Contains("--verify-ui")) window.Loaded += async (_, _) => { if (verificationStarted) return; verificationStarted = true; await window.VerifyPortalUiAsync(); };
            window.Show();
        };
        app.Run();
    }
}
