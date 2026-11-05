using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AsiairContainer;

public sealed partial class MainWindow : Window
{
    private Grid mainContent = null!;
    private Border screenBorder = null!;
    private readonly string root;
    private readonly RuntimeController runtime;
    private readonly Button start = new() { Content = "Start ASIAIR", Padding = new(22, 9, 22, 9), Margin = new(18, 0, 10, 0) };
    private readonly Button stop = new() { Content = "Stop", Padding = new(18, 9, 18, 9), IsEnabled = false };
    private readonly TextBlock status = new() { Text = "Ready to start your private Android environment.", Foreground = Brush("#A4B6CD"), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock placeholder = new() { Text = "Astro - Portal\n\nYour Android apps will appear here.", FontSize = 24, Foreground = Brush("#6D829C"), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border appDrawer = new() { Visibility = Visibility.Collapsed, Background = Brush("#10151E"), Padding = new(22) };
    private readonly EmbeddedView view = new() { Visibility = Visibility.Collapsed };
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private bool busy, closing, closeAllowed;
    private ContainerOptions settings;
    private bool verificationMode;
    private readonly System.Windows.Threading.DispatcherTimer displayFit = new() { Interval = TimeSpan.FromMilliseconds(500) };

    public MainWindow(string root)
    {
        verificationMode = (Environment.GetCommandLineArgs().Contains("--verify-window") || Environment.GetCommandLineArgs().Contains("--verify-keyboard") || Environment.GetCommandLineArgs().Contains("--verify-portal") || Environment.GetCommandLineArgs().Contains("--verify-ui") || Environment.GetCommandLineArgs().Contains("--verify-toolbar"));
        view.SuppressActivation = verificationMode; ShowActivated = !verificationMode;
        this.root = root; settings = ContainerOptions.Load(root); runtime = new(root, settings);
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/AsiairContainer;component/Theme.xaml", UriKind.Relative) });
        Title = "Astro - Portal"; Width = Math.Min(1380, SystemParameters.WorkArea.Width); Height = Math.Min(1000, SystemParameters.WorkArea.Height); MinWidth = Math.Min(900, SystemParameters.WorkArea.Width); MinHeight = Math.Min(640, SystemParameters.WorkArea.Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = Brush("#10151E"); Foreground = Brush("#EEF3FA"); FontFamily = new("Segoe UI"); FontSize = 13;
        start.Tag = "ASIAIR"; stellaStart.Tag = "Stellavita";
        var content = new Grid { Margin = new(20) }; mainContent = content;
        for (int i = 0; i < 7; i++) content.RowDefinitions.Add(new() { Height = i == 3 ? new(1, GridUnitType.Star) : GridLength.Auto });
        var title = new Grid { Margin = new(0, 0, 0, 18) };
        title.Children.Add(new TextBlock { Text = "Astro - Portal", FontSize = 25, FontWeight = FontWeights.SemiBold });
        
        content.Children.Add(title);
        var screen = new Grid { Background = Brushes.Black }; screen.Children.Add(placeholder); screen.Children.Add(view); screen.Children.Add(appDrawer); BuildLoadingOverlay(screen);
        var border = new Border { Background = Brushes.Black, BorderBrush = Brush("#30415A"), BorderThickness = new(1), Child = screen };
        screenBorder = border;
        Grid.SetRow(border, 3); content.Children.Add(border); status.Margin = new(0, 13, 0, 0); Grid.SetRow(status, 6); content.Children.Add(status); Content = BuildPresentationShell(content);
        BuildTools(content);
        runtime.Status += message => Dispatcher.InvokeAsync(() => { status.Text = message; Trace(message); });
        runtime.DisplayExited += () => Dispatcher.InvokeAsync(async () => { if (!busy && !closing) await StopAsync(); });
        start.Click += async (_, _) => await OpenOrInstallAsync(false); stop.Click += async (_, _) => await StopAsync(); Closing += OnClosing;
        displayFit.Tick += (_, _) => { if (view.IsAttached) view.Fit(); }; displayFit.Start(); SetBusy(false);
    }
    private Task StartAsync() => RunWorkspaceActionAsync(() => Task.CompletedTask, false);
    private async Task StartCoreAsync()
    {
        if (runtime.IsRunning) return; SetBusy(true); SetLoading(true);
        try
        {
            var options = CurrentOptions(); options.Validate(); options.Save(root); settings = options; runtime.SetOptions(options);
            operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            await runtime.StartAsync(operation.Token);
            await InitializeLocationAsync();
            status.Text = "Opening Android…";
            nint window = await runtime.OpenDisplayAsync(operation.Token);
            view.Visibility = Visibility.Visible; view.UpdateLayout(); view.Attach(window); view.SetInputEnabled(!busy); placeholder.Visibility = Visibility.Collapsed;
            status.Text = "Android is ready."; await RefreshAppStateAsync();
            RenderUpdateStatus();
        }
        catch (Exception error)
        {
            Trace(error.ToString());
            view.Detach(); view.Visibility = Visibility.Collapsed; placeholder.Visibility = Visibility.Visible;
            try { await runtime.StopAsync(); } catch { }
            status.Text = error is OperationCanceledException ? "Startup cancelled." : error.Message;
        }
        finally { operation?.Dispose(); operation = null; SetLoading(false); SetBusy(false); }
    }
    private async Task StopAsync()
    {
        operation?.Cancel(); locationRequest?.Cancel(); SetBusy(true); appDrawer.Visibility = Visibility.Collapsed; view.Detach(); view.Visibility = Visibility.Collapsed; placeholder.Visibility = Visibility.Visible;
        ExitFullScreen(); hasAstroApp = false;
        try { await runtime.StopAsync(); } catch (Exception error) { status.Text = error.Message; }
        finally { SetBusy(false); UpdatePresentationButtons(); }
    }
    public async Task VerifyWindowAsync()
    {
        verificationMode = true;
        string result = Path.Combine(root, "private-data", "window-verification.json");
        try
        {
            if (splitScreen.IsEnabled) throw new InvalidOperationException("Split screen must be disabled before Android/app startup.");
            await RunWorkspaceActionAsync(OpenOthersDrawerAsync);
            if (!runtime.IsRunning) throw new InvalidOperationException(status.Text);
            string[] args = Environment.GetCommandLineArgs(); int installer = Array.IndexOf(args, "--install-stellavita");
            if (installer >= 0 && installer + 1 < args.Length) { status.Text = "Installing the official Stellavita APK for verification…"; await runtime.InstallApksAsync([args[installer + 1]], lifetime.Token); }
            if (appDrawer.Visibility != Visibility.Visible) throw new InvalidOperationException("Others did not show its full-screen app list. " + status.Text);
            var image = new System.Windows.Media.Imaging.RenderTargetBitmap((int)appDrawer.ActualWidth, (int)appDrawer.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(appDrawer);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image)); using (var output = File.Create(Path.Combine(root, "Others.png"))) encoder.Save(output);
            appDrawer.Visibility = Visibility.Collapsed; view.Visibility = Visibility.Visible; UpdateLayout(); await Task.Delay(600); view.Fit();
            view.VerifySize();
            Width = 1200; Height = 840; UpdateLayout();
            await Task.Delay(3000); view.VerifySize();
            EnterFullScreen(); await Task.Delay(500);
            if (!presenting || presentationBar.Visibility != Visibility.Visible || WindowStyle != WindowStyle.None) throw new InvalidOperationException("Full-screen mode or its exit bar was not available.");
            ExitFullScreen();
            if (presenting || presentationBar.Visibility != Visibility.Collapsed) throw new InvalidOperationException("Full-screen exit did not restore the launcher.");
            await runtime.VerifyDisplayInputAsync(view.ClickGuest, lifetime.Token);
            await runtime.VerifyKeyboardAsync(view.ClickGuest, view.TypeForVerification, view.BackspaceForVerification, lifetime.Token);
            await runtime.ShowAstroAppAsync(RuntimeController.AsiairPackage, lifetime.Token);
            await runtime.CaptureAsync(Path.Combine(root, "ASIAIR-original-app.png"), lifetime.Token);
            await runtime.VerifyWorkspaceAsync(lifetime.Token);
            await RefreshWorkspaceAsync(); UpdatePresentationButtons();
            if (!Equals(splitScreen.Tag, "Active")) throw new InvalidOperationException("Split-screen button did not show its active color.");
            EnterFullScreen(); await Task.Delay(500); view.VerifySize(); ExitFullScreen();
            await runtime.ShowOthersAsync(lifetime.Token); await RefreshWorkspaceAsync();
            if (splitScreen.Tag is not null) throw new InvalidOperationException("Split-screen active color was not cleared after switching apps.");
            await runtime.RestoreConfiguredLocationAsync(lifetime.Token);
            File.WriteAllText(result, System.Text.Json.JsonSerializer.Serialize(new { passed = true, embedded = view.IsAttached, resized = true, mouseInput = true, appScreenReady = true, multiAppHome = true, playStore = settings.EnablePlayStore, gpsForwarding = true, stellavita = runtime.StellavitaVerified, workspaceSplitScreen = true, othersFullscreen = true, appProcessesPreserved = true, splitScreenInitiallyDisabled = true, keyboardInput = true, fullscreenEnterExit = true, splitActiveColor = true, time = DateTimeOffset.UtcNow }));
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(result)!);
            File.WriteAllText(result, System.Text.Json.JsonSerializer.Serialize(new { passed = false, error = error.ToString() }));
            Environment.ExitCode = 1;
        }
        finally { Close(); }
    }
    public async Task VerifyKeyboardWindowAsync()
    {
        verificationMode = true;
        try
        {
            await StartAsync(); if (!runtime.IsRunning) throw new InvalidOperationException(status.Text);
            EnterFullScreen(); await Task.Delay(500);
            if (!presenting || MinWidth != 0 || MinHeight != 0 || presentationBar.Visibility != Visibility.Visible) throw new InvalidOperationException("Full-screen controls did not adapt to the monitor.");
            ExitFullScreen(); await Task.Delay(300); view.VerifySize();
            await runtime.VerifyKeyboardAsync(view.ClickGuest, view.TypeForVerification, view.BackspaceForVerification, lifetime.Token);
            File.WriteAllText(Path.Combine(root, "private-data", "keyboard-verification.json"), "{\"passed\":true,\"fullscreenEnterExit\":true}");
        }
        catch (Exception error) { Environment.ExitCode = 1; File.WriteAllText(Path.Combine(root, "private-data", "keyboard-verification.json"), System.Text.Json.JsonSerializer.Serialize(new { passed = false, error = error.ToString() })); }
        finally { Close(); }
    }
    private void SetBusy(bool value)
    {
        if (!value && commandActive) return;
        busy = value;
        foreach (var button in appButtons) button.IsEnabled = !busy && runtime.IsRunning;
        others.IsEnabled = !busy; stop.IsEnabled = !busy && runtime.IsRunning;
        splitScreen.IsEnabled = !busy && runtime.IsRunning && hasAstroApp && asiairInstalled && stellaInstalled;
        if (view.IsAttached) view.SetInputEnabled(!busy);
        UpdateAppButtons(); UpdatePresentationButtons();
    }
    private async void OnClosing(object? sender, CancelEventArgs args)
    {
        if (closeAllowed) return;
        args.Cancel = true; if (closing) return;
        closing = true; lifetime.Cancel(); IsEnabled = false;
        displayFit.Stop();
        updateTimer.Stop(); locationTimer.Stop(); workspaceTimer.Stop(); gpsFeedTimer.Stop();
        await Task.Yield();
        try { await StopAsync(); await runtime.DisposeAsync(); }
        finally { updates.Dispose(); stellavitaUpdates.Dispose(); closeAllowed = true; _ = Dispatcher.BeginInvoke(Close); }
    }
    private void Trace(string message)
    {
        string folder = Path.Combine(root, "private-data", "logs"); Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "host.log"), DateTimeOffset.UtcNow.ToString("O") + " " + message + "\n");
    }
    private static StackPanel Field(string name, TextBox box)
    {
        var panel = new StackPanel { Margin = new(0, 0, 18, 0) }; panel.Children.Add(new TextBlock { Text = name, FontSize = 11, Foreground = Brush("#A4B6CD"), Margin = new(0, 0, 0, 7) }); panel.Children.Add(box); return panel;
    }
    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
}


