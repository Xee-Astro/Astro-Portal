using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    public async Task VerifyPortalUiAsync()
    {
        string result = Path.Combine(root, "portal-ui-verification.json");
        try
        {
            await Task.Delay(200); UpdateLayout();
            if (!Equals(start.Content, "Install ASIAIR") || !Equals(stellaStart.Content, "Install Stellavita")) throw new IOException("Fresh install labels were not displayed.");
            if (updateAsiair.IsEnabled || updateStella.IsEnabled || splitScreen.IsEnabled) throw new IOException("Unavailable controls were enabled.");
            void Capture(Window window, string name)
            {
                window.UpdateLayout();
                void Visit(DependencyObject obj)
                {
                    if (obj is TextBox box && box.IsVisible)
                    {
                        if (box.ActualHeight < 34) throw new IOException("Text input is too short: " + box.ActualHeight);
                        if (box.Text.Length == 0) box.Text = box.AcceptsReturn ? "Sample notes: Aa Gg 0123456789\nSecond line" : "192.168.1.20";
                    }
                    if (obj is PasswordBox password && password.Password.Length == 0) password.Password = "SamplePassword123";
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) Visit(VisualTreeHelper.GetChild(obj, i));
                }
                Visit(window); window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(root, name + ".png")); png.Save(stream);
            }
            if (androidFontPercent.Text != "100%") throw new IOException("Default Android font label is incorrect.");
            await ChangeAndroidFontAsync(1.1);
            if (androidFontPercent.Text != "110%" || ContainerOptions.Load(root).AndroidFontScale != 1.1) throw new IOException("Font control did not persist.");
            SetBusy(true); await ChangeAndroidFontAsync(1.2);
            if (settings.AndroidFontScale != 1.1 || smallerAndroidFont.IsEnabled || largerAndroidFont.IsEnabled) throw new IOException("Font changes were accepted while busy.");
            SetBusy(false); await ChangeAndroidFontAsync(0.8);
            if (smallerAndroidFont.IsEnabled) throw new IOException("Font minimum was not enforced.");
            await ChangeAndroidFontAsync(2.0);
            if (largerAndroidFont.IsEnabled) throw new IOException("Font maximum was not enforced.");
            await ChangeAndroidFontAsync(1.0);
            if (resetAndroidFont.IsEnabled) throw new IOException("Font reset state was not cleared.");
            Capture(this, "Astro-Portal-dashboard");
            async Task Inspect(Action show, string name)
            {
                Exception? failure = null;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                timer.Tick += (_, _) => { timer.Stop(); try { if (secondary is null) throw new IOException("Secondary window did not open."); Capture(secondary, name); } catch (Exception e) { failure = e; } finally { secondary?.Close(); } };
                timer.Start(); show(); if (failure is not null) throw failure; await Task.Delay(50);
            }
            await Inspect(ShowNotes, "Astro-Portal-notes"); await Inspect(ShowAdvanced, "Astro-Portal-advanced");
            File.WriteAllText(result, "{\"passed\":true,\"freshInstallLabels\":true,\"unavailableControlsDisabled\":true,\"notesWindow\":true,\"advancedWindow\":true,\"textFieldHeight\":true}");
        }
        catch (Exception error) { Environment.ExitCode = 1; File.WriteAllText(result, JsonSerializer.Serialize(new { passed = false, error = error.ToString() })); }
        finally { Close(); }
    }
    public async Task VerifyPortalAsync()
    {
        verificationMode = true;
        string result = Path.Combine(root, "private-data", "portal-verification.json");
        try
        {
            if (Title != "Astro - Portal") throw new IOException("Branding mismatch.");
            if (!asiairInstalled && !Equals(start.Content, "Install ASIAIR")) throw new IOException("Missing app installation state was not shown.");
            if (updateAsiair.IsEnabled || updateStella.IsEnabled) throw new IOException("An update button was enabled without a newer release.");
            await StartAsync(); if (!runtime.IsRunning) throw new IOException(status.Text);
            if (loadingOverlay.Visibility != Visibility.Collapsed) throw new IOException("Boot animation was not cleared.");
            string[] args = Environment.GetCommandLineArgs();
            foreach (var item in new[] { ("--install-asiair", false), ("--install-stellavita", true) })
            {
                int index = Array.IndexOf(args, item.Item1);
                if (index >= 0 && index + 1 < args.Length) await RunWorkspaceActionAsync(() => runtime.InstallOfficialApkAsync(args[index + 1], item.Item2, lifetime.Token), false);
            }
            if (!runtime.HasLiveDisplay || !view.IsAttached || runtime.DisplayNeedsRestart) throw new IOException("The display/input session was not restored after installation.");
            await RefreshAppStateAsync();
            if (!asiairInstalled || !stellaInstalled) throw new IOException("Both test apps must be installed.");
            await runtime.VerifyLandscapeAsync(lifetime.Token);
            var completion = new TaskCompletionSource(); int unexpected = 0;
            var first = RunWorkspaceActionAsync(() => completion.Task, false);
            await Task.Delay(50);
            await RunWorkspaceActionAsync(() => { unexpected++; return Task.CompletedTask; }, false);
            await ChangePresentationAsync(true);
            if (unexpected != 0 || presenting || !busy) throw new IOException("Commands were accepted during an existing transition.");
            completion.SetResult(); await first; await Task.Delay(100);
            if (unexpected != 0) throw new IOException("A blocked command was queued.");
            if (!Environment.GetCommandLineArgs().Contains("--existing-profile")) await runtime.SetLocationAsync(12.3456, 45.6789, lifetime.Token);
            await RunWorkspaceActionAsync(() => runtime.ShowAstroAppAsync(RuntimeController.AsiairPackage, lifetime.Token), false);
            await runtime.WaitForAppScreenAsync(lifetime.Token);
            await runtime.CaptureAsync(Path.Combine(root, "ASIAIR-landscape.png"), lifetime.Token);
            await RefreshAppStateAsync();
            if (!asiairRunning || start.BorderThickness.Left != 3 || start.BorderBrush.ToString() != "#FFFFDF56") throw new IOException("ASIAIR running indicator was not shown.");
            await runtime.LaunchAppAsync(RuntimeController.StellavitaPackage, lifetime.Token); await Task.Delay(6000);
            await runtime.VerifyWorkspaceAsync(lifetime.Token);
            await runtime.VerifyLandscapeAsync(lifetime.Token);
            await RefreshWorkspaceAsync();
            if (!Equals(splitScreen.Tag, "Active")) throw new IOException("Split highlight was not shown.");
            EnterFullScreen(); await Task.Delay(400); view.VerifySize(); ExitFullScreen();
            await runtime.TerminateAstroAppAsync(RuntimeController.StellavitaPackage, lifetime.Token); await RefreshAppStateAsync();
            if (stellaRunning || !asiairRunning) throw new IOException("App termination affected the wrong app.");
            await runtime.ShowOthersAsync(lifetime.Token);
            // Keyboard verification explicitly enables the native input surface.
            view.SetInputEnabled(true);
            await runtime.VerifyKeyboardAsync(view.ClickGuest, view.TypeForVerification, view.BackspaceForVerification, lifetime.Token);
            await runtime.ShowOthersAsync(lifetime.Token);
            ExitFullScreen(); view.Visibility = Visibility.Hidden; placeholder.Visibility = Visibility.Visible; UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(root, "Astro-Portal.png"))) png.Save(file);
            await runtime.RestoreConfiguredLocationAsync(lifetime.Token);
            File.WriteAllText(result, JsonSerializer.Serialize(new { passed = true, build = "0.5.1", branding = true, nativeX64Install = true, landscapeTablet = true, adaptiveLandscapeResolution = true, singleResolution = "2560x1600", splitResolution = "2560x800", noCommandQueuing = true, runningIndicator = true, perAppTermination = true, splitScreen = true, preservedAppProcesses = true, fullScreen = true, keyboard = true, time = DateTimeOffset.UtcNow }));
        }
        catch (Exception error) { Environment.ExitCode = 1; Directory.CreateDirectory(Path.GetDirectoryName(result)!); File.WriteAllText(result, JsonSerializer.Serialize(new { passed = false, error = error.ToString() })); }
        finally { Close(); }
    }
}
