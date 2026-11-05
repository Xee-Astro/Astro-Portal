using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    private readonly List<Button> appButtons = [];
    private readonly TextBox latitude = new() { Width = 110, Height = 30, Margin = new(0, 0, 8, 0) };
    private readonly TextBox longitude = new() { Width = 110, Height = 30, Margin = new(0, 0, 8, 0) };
    private readonly CheckBox windowsLocation = new() { Content = "Use Windows location", VerticalAlignment = VerticalAlignment.Center, Margin = new(12, 0, 12, 0) };
    private readonly CheckBox automaticUpdates = new() { Content = "Automatic checks", VerticalAlignment = VerticalAlignment.Center, Margin = new(12, 0, 12, 0) };
    private readonly TextBlock locationStatus = new() { Foreground = Brush("#A4B6CD"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 5, 0, 0) };
    private readonly TextBlock updateStatus = new() { Foreground = Brush("#A4B6CD"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock stellavitaUpdateStatus = new() { Foreground = Brush("#A4B6CD"), TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 0) };
    private UpdateChecker stellavitaUpdates = null!;
    private readonly Button checkUpdates = ToolButton("Check updates");
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromMinutes(30) };
    private readonly DispatcherTimer locationTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private UpdateChecker updates = null!;
    private bool toolsStarted, checkingUpdates, gettingLocation;
    private CancellationTokenSource? locationRequest;
    private void BuildTools(Grid content)
    {
        updates = new(root); stellavitaUpdates = new(root, stellavita: true);
        updateStatus.Foreground = Brush("#FFA0A0"); stellavitaUpdateStatus.Foreground = Brush("#79B8FF");
        autoStartAndroid.IsChecked = settings.AutoStartAndroid; phoneIp.Text = settings.PhoneGpsIp; phonePort.Text = settings.PhoneGpsPort.ToString(CultureInfo.InvariantCulture);
        windowsLocation.IsChecked = settings.UseWindowsLocation; automaticUpdates.IsChecked = settings.AutomaticUpdateChecks;
        latitude.Text = settings.Latitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? "";
        longitude.Text = settings.Longitude?.ToString("0.######", CultureInfo.InvariantCulture) ?? "";
        foreach (var field in new[] { latitude, longitude }) { field.Height = 36; field.Padding = new(8, 2, 8, 2); }
        locationStatus.Text = settings.Latitude.HasValue ? "Saved location: " + settings.LocationSource : "Location not set. Use PC location or enter latitude and longitude.";
        var apps = new WrapPanel { Margin = new(0, 0, 0, 12) };
        start.Margin = new(0, 0, 8, 0); apps.Children.Add(AppTile(start, false)); apps.Children.Add(AppTile(stellaStart, true)); apps.Children.Add(others); apps.Children.Add(splitScreen); apps.Children.Add(fullScreenButton); apps.Children.Add(autoStartAndroid); var notes = ToolButton("Notes"); notes.Click += (_, _) => ShowNotes(); apps.Children.Add(notes); var advanced = ToolButton("Advanced"); advanced.Click += (_, _) => ShowAdvanced(); apps.Children.Add(advanced);
        stellaStart.Click += async (_, _) => await OpenOrInstallAsync(true);
        others.Click += async (_, _) => await RunWorkspaceActionAsync(OpenOthersDrawerAsync);
        splitScreen.Click += async (_, _) => await RunWorkspaceActionAsync(() => runtime.ShowSplitScreenAsync(lifetime.Token), false);
        fullScreenButton.Click += async (_, _) => await ChangePresentationAsync(true);
        autoStartAndroid.Click += (_, _) => SaveToolOptions();
        AddAppButton(apps, "Back", () => runtime.NavigateAsync(false, lifetime.Token));
        
        
        Grid.SetRow(apps, 2); content.Children.Add(apps);
        var locations = new StackPanel { Margin = new(0, 10, 0, 10) };
        var line = new WrapPanel(); line.Children.Add(new TextBlock { Text = "Latitude", VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) }); line.Children.Add(latitude);
        line.Children.Add(new TextBlock { Text = "Longitude", VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) }); line.Children.Add(longitude);
        var manual = ToolButton("Use coordinates"); manual.Click += async (_, _) => await ApplyManualLocationAsync(); line.Children.Add(manual); line.Children.Add(windowsLocation);
        var locate = ToolButton("Use PC location"); locate.Click += async (_, _) => await ReadWindowsLocationAsync(); line.Children.Add(locate);
        locations.Children.Add(line);
        var phoneLine = new WrapPanel { Margin = new(0, 7, 0, 0) };
        phoneLine.Children.Add(new TextBlock { Text = "Phone IP", VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) }); phoneLine.Children.Add(phoneIp);
        phoneLine.Children.Add(new TextBlock { Text = "Port", VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) }); phoneLine.Children.Add(phonePort);
        var getPhone = ToolButton("Get from phone"); getPhone.Click += async (_, _) => await ReadPhoneLocationAsync(); phoneLine.Children.Add(getPhone);
        phoneLine.Children.Add(new TextBlock { Text = "GPS2IP: NMEA · Socket/TCP server", Foreground = Brush("#A4B6CD"), VerticalAlignment = VerticalAlignment.Center });
        foreach (var field in new[] { phoneIp, phonePort }) { field.Height = 36; field.Padding = latitude.Padding; field.LostKeyboardFocus += (_, _) => SaveToolOptions(); }
        locations.Children.Add(phoneLine); locations.Children.Add(locationStatus); Grid.SetRow(locations, 4); content.Children.Add(locations);
        var updateLine = new WrapPanel(); updateLine.Children.Add(checkUpdates); updateLine.Children.Add(automaticUpdates);
        updateAsiair.Tag = "ASIAIR"; updateStella.Tag = "Stellavita";
        updateAsiair.Click += async (_, _) => await InstallOfficialAsync(false, true);
        updateStella.Click += async (_, _) => await InstallOfficialAsync(true, true);
        updateLine.Children.Add(updateAsiair); updateLine.Children.Add(updateStella);
        var updatePanel = new StackPanel(); updatePanel.Children.Add(updateLine); updatePanel.Children.Add(updateStatus); updatePanel.Children.Add(stellavitaUpdateStatus);
        checkUpdates.Click += async (_, _) => await CheckUpdatesAsync(true);
        automaticUpdates.Click += async (_, _) => { SaveToolOptions(); if (automaticUpdates.IsChecked == true) await CheckUpdatesAsync(false); else RenderUpdateStatus(); };
        windowsLocation.Click += async (_, _) => { if (windowsLocation.IsChecked == true) settings = settings with { UsePhoneLocation = false }; SaveToolOptions(); if (windowsLocation.IsChecked != true) locationRequest?.Cancel(); else if (runtime.IsRunning) await ReadWindowsLocationAsync(); };
        Grid.SetRow(updatePanel, 5); content.Children.Add(updatePanel);
        updateTimer.Tick += async (_, _) => { if (automaticUpdates.IsChecked == true) await CheckUpdatesAsync(false); };
        locationTimer.Tick += async (_, _) => { if (!runtime.IsRunning || verificationMode) return; if (settings.UsePhoneLocation) await ReadPhoneLocationAsync(); else if (windowsLocation.IsChecked == true) await ReadWindowsLocationAsync(); };
        gpsFeedTimer.Tick += async (_, _) => await ReplayLocationAsync(); gpsFeedTimer.Start();
        workspaceTimer.Tick += async (_, _) => { if (runtime.IsRunning && runtime.DisplayNeedsRestart && !busy && !closing) await RunWorkspaceActionAsync(() => Task.CompletedTask, false); else await RefreshWorkspaceAsync(); }; workspaceTimer.Start();
        Loaded += async (_, _) => { if (toolsStarted) return; toolsStarted = true; RenderUpdateStatus(); updateTimer.Start(); locationTimer.Start(); var startup = autoStartAndroid.IsChecked == true && !verificationMode ? StartAsync() : Task.CompletedTask; if (automaticUpdates.IsChecked == true) await CheckUpdatesAsync(false); await startup; UpdateAppButtons(); };
    }
    private static Button ToolButton(string text) => new() { Content = text, Padding = new(12, 6, 12, 6), Margin = new(0, 0, 8, 0) };
    private void AddAppButton(Panel panel, string text, Func<Task> action)
    {
        var button = ToolButton(text); button.IsEnabled = false; button.Click += async (_, _) => await RunAppActionAsync(action); panel.Children.Add(button); appButtons.Add(button);
    }
    private Task RunAppActionAsync(Func<Task> action) => RunWorkspaceActionAsync(action, false);
    private async Task InstallApksAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Choose an APK, or all files in a matching split APK set", Filter = "Android packages (*.apk)|*.apk", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        await RunWorkspaceActionAsync(async () => { status.Text = "Installing the selected app…"; await runtime.ShowOthersAsync(lifetime.Token); await runtime.InstallApksAsync(dialog.FileNames, lifetime.Token); RenderUpdateStatus(); }, false);
    }
    private ContainerOptions CurrentOptions() => settings with { AutomaticUpdateChecks = automaticUpdates.IsChecked == true, UseWindowsLocation = windowsLocation.IsChecked == true, AutoStartAndroid = autoStartAndroid.IsChecked == true, PhoneGpsIp = phoneIp.Text.Trim(), PhoneGpsPort = int.TryParse(phonePort.Text, out int parsedPort) ? parsedPort : 0 };
    private void SaveToolOptions()
    {
        try { var changed = CurrentOptions(); changed.Save(root); settings = changed; } catch (Exception error) { status.Text = error.Message; }
    }
    private async Task InitializeLocationAsync()
    {
        if (verificationMode) return;
        if (settings.UsePhoneLocation) await ReadPhoneLocationAsync();
        else if (windowsLocation.IsChecked == true) await ReadWindowsLocationAsync();
        else if (settings.Latitude.HasValue) locationStatus.Text = "Using saved " + settings.LocationSource.ToLowerInvariant() + " location.";
    }
    private async Task ReadWindowsLocationAsync()
    {
        if (gettingLocation || closing) return; gettingLocation = true; locationStatus.Text = "Requesting Windows location…";
        windowsLocation.IsChecked = true; settings = settings with { UsePhoneLocation = false };
        locationRequest = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = locationRequest.Token;
        try
        {
            var fix = await WindowsLocation.GetAsync(token); token.ThrowIfCancellationRequested();
            if (!latitude.IsKeyboardFocusWithin && !longitude.IsKeyboardFocusWithin) { latitude.Text = fix.Latitude.ToString("0.######", CultureInfo.InvariantCulture); longitude.Text = fix.Longitude.ToString("0.######", CultureInfo.InvariantCulture); }
            windowsLocation.IsChecked = true;
            settings = CurrentOptions() with { Latitude = fix.Latitude, Longitude = fix.Longitude, LocationSource = "Windows", UsePhoneLocation = false }; settings.Save(root);
            if (runtime.IsRunning) await runtime.SetLocationAsync(fix.Latitude, fix.Longitude, token);
            locationStatus.Text = $"Windows location {(runtime.IsRunning ? "sent to Android" : "saved")}; estimated accuracy {fix.AccuracyMetres:0} m.";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { locationStatus.Text = settings.Latitude.HasValue ? "Windows location unavailable. Keeping saved coordinates; enter new coordinates if needed." : "Windows location unavailable. Enable Windows location services, or enter coordinates manually."; }
        finally { locationRequest.Dispose(); locationRequest = null; gettingLocation = false; }
    }
    private async Task ApplyManualLocationAsync()
    {
        try
        {
            if (!double.TryParse(latitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) || !double.TryParse(longitude.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)) throw new ArgumentException("Enter latitude and longitude using decimal degrees, for example 30.0444 and 31.2357.");
            var changed = CurrentOptions() with { Latitude = lat, Longitude = lon, LocationSource = "Manual", UseWindowsLocation = false, UsePhoneLocation = false }; changed.Validate();
            locationRequest?.Cancel();
            if (runtime.IsRunning) await runtime.SetLocationAsync(lat, lon, lifetime.Token);
            windowsLocation.IsChecked = false; settings = changed; settings.Save(root);
            locationStatus.Text = runtime.IsRunning ? "Manual location sent to Android. Windows location is off until you enable it again." : "Manual location saved. It will be sent when Android starts.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { locationStatus.Text = error.Message; }
    }
    private async Task CheckUpdatesAsync(bool force)
    {
        if (checkingUpdates || closing) return; checkingUpdates = true; checkUpdates.IsEnabled = false; updateStatus.Text = "Checking official app releases…"; stellavitaUpdateStatus.Text = "Checking Stellavita…";
        try { await Task.WhenAll(updates.CheckAsync(force, lifetime.Token), stellavitaUpdates.CheckAsync(force, lifetime.Token)); if (runtime.IsRunning && !busy) await runtime.RefreshInstalledAppInfoAsync(lifetime.Token); RenderUpdateStatus(); }
        catch (OperationCanceledException) { }
        catch (Exception) { RenderUpdateStatus(); status.Text = "Could not complete or save an update check. Try again later."; }
        finally { checkingUpdates = false; checkUpdates.IsEnabled = true; }
    }
    private void RenderUpdateStatus()
    {
        RenderAppUpdateStatus(updates, updateStatus, "ASIAIR", false);
        RenderAppUpdateStatus(stellavitaUpdates, stellavitaUpdateStatus, "Stellavita", true);
        UpdateAppButtons();
    }
    private void RenderAppUpdateStatus(UpdateChecker checker, TextBlock target, string name, bool stellavita)
    {
        var snapshot = checker.Snapshot;
        if (snapshot is null) { string? version = UpdateChecker.InstalledVersion(root, stellavita); target.Text = version is null ? name + ": not installed." : name + " " + version + (automaticUpdates.IsChecked == true ? ": checking official releases…" : ": automatic checks off."); return; }
        if (snapshot.Error is not null) { target.Text = name + ": " + snapshot.Error; return; }
        string? installed = UpdateChecker.InstalledVersion(root, stellavita);
        string latest = snapshot.Release!.Version;
        try
        {
            target.Text = installed is null ? $"Latest {name}: {latest}. Start Android to read the installed version." : UpdateChecker.ParseVersion(latest) > UpdateChecker.ParseVersion(installed) ? $"{name} {latest} is available (installed {installed}). Use Update {name}." : $"{name} {installed}: no newer official release listed. Checked {snapshot.LastSuccess?.ToLocalTime():g}.";
        }
        catch (FormatException) { target.Text = $"Latest {name}: {latest}; installed version could not be compared."; }
    }
}
