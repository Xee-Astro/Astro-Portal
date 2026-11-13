using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    private bool commandActive, asiairInstalled, stellaInstalled, asiairRunning, stellaRunning;
    private readonly Button updateAsiair = ToolButton("Update ASIAIR"), updateStella = ToolButton("Update Stellavita");
    private readonly List<(Button Launch, Button Kill, bool Stella)> appTiles = [];
    private Button? presentationExit;
    private Border loadingOverlay = null!;
    private PortalSplash loadingSplash = null!;
    private Window? secondary;
    private UIElement AppTile(Button launch, bool stella)
    {
        var grid = new Grid { Margin = new(0, 0, 8, 0) };
        launch.Margin = new(0); launch.MinWidth = 140; launch.MinHeight = 44; launch.Padding = new(15, 9, 32, 9);
        grid.Children.Add(launch);
        var kill = ToolButton("Ã—"); kill.ToolTip = "Close " + (stella ? "Stellavita" : "ASIAIR");
        kill.Tag = stella ? "Stellavita" : "ASIAIR"; kill.Padding = new(3, 0, 3, 0); kill.FontSize = 17;
        kill.Width = 24; kill.Height = 24; kill.Margin = new(0, 3, 3, 0); kill.HorizontalAlignment = HorizontalAlignment.Right;
        kill.VerticalAlignment = VerticalAlignment.Top; kill.Visibility = Visibility.Collapsed;
        kill.Click += async (_, _) => await RunWorkspaceActionAsync(() => StopPaneAsync(stella ? WorkspacePane.Stellavita : WorkspacePane.Asiair), false);
        grid.Children.Add(kill); appTiles.Add((launch, kill, stella)); return grid;
    }
    private async Task RefreshAppStateAsync()
    {
        if (runtime.IsRunning)
        {
            await runtime.RefreshInstalledAppInfoAsync(lifetime.Token);
            asiairRunning = await runtime.IsAppRunningAsync(RuntimeController.AsiairPackage, lifetime.Token);
            stellaRunning = await runtime.IsAppRunningAsync(RuntimeController.StellavitaPackage, lifetime.Token);
        }
        else asiairRunning = stellaRunning = false;
        asiairInstalled = UpdateChecker.InstalledVersion(root) is not null;
        stellaInstalled = UpdateChecker.InstalledVersion(root, true) is not null;
        UpdateAppButtons();
    }
    private void UpdateAppButtons()
    {
        if (updates is null) return;
        asiairInstalled = UpdateChecker.InstalledVersion(root) is not null;
        stellaInstalled = UpdateChecker.InstalledVersion(root, true) is not null;
        start.Content = asiairInstalled ? "ASIAIR" : "Install ASIAIR";
        stellaStart.Content = stellaInstalled ? "Stellavita" : "Install Stellavita";
        start.IsEnabled = stellaStart.IsEnabled = !busy && !closing;
        foreach (var tile in appTiles)
        {
            bool running = runtime.IsRunning && (tile.Stella ? stellaRunning : asiairRunning);
            tile.Launch.BorderBrush = Brush(running ? "#FFDF56" : tile.Stella ? "#79B8FF" : "#FFA0A0");
            tile.Launch.BorderThickness = new(running ? 3 : 1);
            tile.Kill.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            tile.Kill.IsEnabled = !busy && !closing;
        }
        updateAsiair.IsEnabled = !busy && !closing && UpdateAvailable(false);
        updateStella.IsEnabled = !busy && !closing && UpdateAvailable(true);
    }
    private bool UpdateAvailable(bool stella)
    {
        var snapshot = (stella ? stellavitaUpdates : updates).Snapshot;
        string? installed = UpdateChecker.InstalledVersion(root, stella);
        return UpdateChecker.IsUpdateAvailable(installed, snapshot);
    }
    private Task OpenOrInstallAsync(bool stella) => (stella ? stellaInstalled : asiairInstalled)
        ? RunWorkspaceActionAsync(() => OpenPaneAsync(stella ? WorkspacePane.Stellavita : WorkspacePane.Asiair))
        : InstallOfficialAsync(stella, false);
    private Task InstallOfficialAsync(bool stella, bool update)
    {
        if (update && !UpdateAvailable(stella)) return Task.CompletedTask;
        return RunWorkspaceActionAsync(async () =>
        {
            await runtime.ShowOthersAsync(lifetime.Token);
            using var downloader = new OfficialApkInstaller(root);
            string file = await downloader.DownloadAsync(stella, message => Dispatcher.InvokeAsync(() => status.Text = message), lifetime.Token);
            await runtime.InstallOfficialApkAsync(file, stella, lifetime.Token);
            await RefreshAppStateAsync(); RenderUpdateStatus();
        }, false);
    }
    private async Task ChangePresentationAsync(bool enter)
    {
        if (busy || closing || commandActive) return;
        commandActive = true; SetBusy(true);
        try { if (enter) EnterFullScreen(); else ExitFullScreen(); UpdateLayout(); await Task.Delay(250); view.Fit(); }
        finally { commandActive = false; SetBusy(false); }
    }
    private void BuildLoadingOverlay(Grid screen)
    {
        loadingSplash = new PortalSplash();
        var panel = new Grid(); panel.Children.Add(loadingSplash);
        var caption = new Border { Background = Brush("#10151E"), Padding = new(14,7,14,7), CornerRadius = new(6), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0,0,16,16), Child = new TextBlock { Text = "Starting Android…", FontSize = 17, Foreground = Brush("#EEF3FA") } };
        panel.Children.Add(caption);
        loadingOverlay = new() { Background = Brush("#10151E"), Child = panel, Visibility = Visibility.Collapsed };
        screen.Children.Add(loadingOverlay);
    }
    private void SetLoading(bool show)
    {
        SetWorkspaceLoading(show);
        loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show) loadingSplash.BeginBoot(); else loadingSplash.Reset();
    }
    private Window SecondaryWindow(string name, UIElement body, double width = 760, double height = 600)
    {
        var window = new Window { Owner = this, Icon = Icon, Title = "Astro - Portal Â· " + name, Width = width, Height = height, MinWidth = 520, MinHeight = 400, Background = Background, Foreground = Foreground, FontFamily = FontFamily, FontSize = FontSize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/AsiairContainer;component/Theme.xaml", UriKind.Relative) });
        var dock = new DockPanel { Margin = new(20) };
        var header = new DockPanel { Margin = new(0, 0, 0, 18) };
        var close = ToolButton("Ã—"); close.FontSize = 22; close.ToolTip = "Close " + name; DockPanel.SetDock(close, Dock.Right); close.Click += (_, _) => window.Close(); header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = name, FontSize = 25, VerticalAlignment = VerticalAlignment.Center }); DockPanel.SetDock(header, Dock.Top); dock.Children.Add(header);
        dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); window.Content = dock; return window;
    }
    private void OpenSecondary(Window window)
    {
        if (busy || closing) return;
        if (secondary is not null) { secondary.Activate(); return; }
        secondary = window; var previous = view.Visibility; SetWorkspaceLoading(true); view.Visibility = Visibility.Hidden;
        try { window.ShowDialog(); }
        finally { secondary = null; if (!closing) { SetWorkspaceLoading(false); view.Visibility = previous; view.Fit(); } }
    }
    private void ShowAdvanced()
    {
        var panel = new StackPanel();
        var tools = new WrapPanel();
        var play = ToolButton("Google Play"); play.Click += async (_, _) => { secondary?.Close(); await RunWorkspaceActionAsync(async () => { await OpenPaneAsync(WorkspacePane.Other); await runtime.LaunchAppAsync("com.android.vending", lifetime.Token); }); }; tools.Children.Add(play);
        var install = ToolButton("Install APKâ€¦"); install.Click += async (_, _) => { secondary?.Close(); await InstallApksAsync(); }; tools.Children.Add(install);
        if (stop.Parent is Panel old) old.Children.Remove(stop);
        tools.Children.Add(stop); panel.Children.Add(tools);
        panel.Children.Add(new TextBlock { Text = "Use Install APK to select a single APK or every file in a matching split APK set. Android uses a fixed landscape tablet display. App data stays in private-data. Location settings on the dashboard apply to all Android apps.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#B8C5D6"), Margin = new(0, 24, 0, 0) });
        var window = SecondaryWindow("Advanced", panel); window.Closing += (_, _) => SaveToolOptions(); OpenSecondary(window);
    }
    private void ShowNotes()
    {
        NotesData data;
        try { data = NotesStore.Load(root); }
        catch (Exception error) { status.Text = "Could not open notes: " + error.Message; return; }
        var grid = new Grid(); grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
        var saveStatus = new TextBlock { Foreground = Brush("#B8C5D6"), Margin = new(0, 14, 0, 0), Text = "Saved locally. Passwords are protected by your Windows account." };
        var fields = new List<(TextBox Ip, PasswordBox Password, TextBox Notes)>();
        void Section(string name, AppNotes entry, int column, string color)
        {
            var panel = new StackPanel { Margin = new(column == 0 ? 0 : 14, 0, column == 0 ? 14 : 0, 0) };
            panel.Children.Add(new TextBlock { Text = name, FontSize = 22, Foreground = Brush(color), Margin = new(0, 0, 0, 18) });
            var address = new TextBox { Text = entry.Ip, MinHeight = 36 };
            var password = new PasswordBox { Password = entry.Password, MinHeight = 36, Padding = new(8, 2, 8, 2), VerticalContentAlignment = VerticalAlignment.Center, Background = Brush("#202C3D"), Foreground = Brush("#EEF3FA"), BorderBrush = Brush("#8298B3") };
            var notes = new TextBox { Text = entry.Notes, Height = 230, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
            address.BorderBrush = notes.BorderBrush = password.BorderBrush = Brush(color);
            address.SelectionBrush = notes.SelectionBrush = Brush(color);
            password.SelectionBrush = Brush(column == 0 ? "#862B36" : "#164E87");
            panel.Children.Add(Field("IP address", address)); panel.Children.Add(new TextBlock { Text = "Password", Margin = new(0, 14, 0, 7) }); panel.Children.Add(password); panel.Children.Add(new TextBlock { Text = "Notes", Margin = new(0, 14, 0, 7) }); panel.Children.Add(notes);
            fields.Add((address, password, notes)); Grid.SetColumn(panel, column); grid.Children.Add(panel);
        }
        Section("ASIAIR", data.Asiair, 0, "#FFA0A0"); Section("Stellavita", data.Stellavita, 1, "#79B8FF");
        var content = new StackPanel(); content.Children.Add(grid); content.Children.Add(saveStatus);
        var window = SecondaryWindow("Notes", content, 850, 650);
        bool Save()
        {
            try { AppNotes Entry(int i) => new(fields[i].Ip.Text, fields[i].Password.Password, fields[i].Notes.Text); NotesStore.Save(root, new(Entry(0), Entry(1))); saveStatus.Text = "Saved locally. Passwords are protected by your Windows account."; return true; }
            catch { saveStatus.Text = "Could not save notes. Check folder access and free space."; return false; }
        }
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => { timer.Stop(); Save(); };
        foreach (var f in fields) { void Changed() { timer.Stop(); timer.Start(); } f.Ip.TextChanged += (_, _) => Changed(); f.Notes.TextChanged += (_, _) => Changed(); f.Password.PasswordChanged += (_, _) => Changed(); }
        window.Closing += (_, e) => { timer.Stop(); if (!Save() && !closing) e.Cancel = true; }; OpenSecondary(window);
    }
}
