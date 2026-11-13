using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    private readonly Button stellaStart = ToolButton("Stellavita");
    private readonly Button others = ToolButton("Other");
    private readonly Button splitScreen = ToolButton("Split screen");
    private readonly CheckBox autoStartAndroid = new() { Content = "Start Android automatically", VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 0, 12, 0) };
    private readonly TextBox phoneIp = new() { Width = 145, Height = 30, Margin = new(0, 0, 8, 0) };
    private readonly TextBox phonePort = new() { Width = 70, Height = 30, Margin = new(0, 0, 8, 0) };
    private readonly DispatcherTimer workspaceTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool hasAstroApp, refreshingWorkspace, feedingLocation;
    private readonly DispatcherTimer gpsFeedTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private async Task ReplayLocationAsync()
    {
        if (feedingLocation || busy || closing || verificationMode || !runtime.IsRunning || settings.Latitude is not double lat || settings.Longitude is not double lon) return;
        feedingLocation = true;
        try { await runtime.FeedLocationAsync(lat, lon, lifetime.Token); if (cloneRuntime is { IsRunning: true }) await cloneRuntime.FeedLocationAsync(lat,lon,lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (runtime.IsRunning && !closing) Trace("GPS forwarding will retry: " + error.Message); }
        finally { feedingLocation = false; }
    }
    private async Task RefreshWorkspaceAsync()
    {
        if (refreshingWorkspace || busy || closing) return;
        refreshingWorkspace = true;
        try
        {
            hasAstroApp = paneSurfaces.Keys.Any(id => id != WorkspacePane.Other && PaneRunning(id));
            if (runtime.IsRunning)
            {
                await RefreshAppStateAsync();
                if (await runtime.NeedsAppDisplayNormalizationAsync(lifetime.Token) || cloneRuntime is { IsRunning: true } && await cloneRuntime.NeedsAppDisplayNormalizationAsync(lifetime.Token)) await RunWorkspaceActionAsync(async () => { await runtime.NormalizeAppDisplaysAsync(lifetime.Token); if (cloneRuntime is { IsRunning: true }) await cloneRuntime.NormalizeAppDisplaysAsync(lifetime.Token); }, false);
            }
        }
        catch { hasAstroApp = false; }
        finally { refreshingWorkspace = false; UpdatePresentationButtons(); UpdateMultiWorkspaceControls(); }
    }
    private async Task RunWorkspaceActionAsync(Func<Task> action, bool presentFullScreen = true)
    {
        if (commandActive || busy || closing) return;
        commandActive = true; SetBusy(true);
        try
        {
            if (!runtime.IsRunning) await StartCoreAsync();
            if (!runtime.IsRunning || closing) return;
            appDrawer.Visibility = Visibility.Collapsed; view.Visibility = Visibility.Visible;
            await action();
            await EnsureDisplaySessionAsync();
            if (presentFullScreen && !verificationMode) EnterFullScreen();
            UpdateLayout(); await Task.Delay(250, lifetime.Token); view.Fit();
            await RefreshAppStateAsync();
            status.Text = runtime.SplitScreenActive ? "ASIAIR and Stellavita · split screen" : "Ready.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { status.Text = error.Message; Trace(error.ToString()); }
        finally
        {
            if (!closing && runtime.IsRunning && runtime.DisplayNeedsRestart)
                try { await EnsureDisplaySessionAsync(); } catch (Exception error) { status.Text = error.Message; }
            commandActive = false; SetBusy(false); await RefreshWorkspaceAsync();
            if (appDrawer.Visibility != Visibility.Visible && !closing) view.FocusAndroid();
        }
    }
    private async Task EnsureDisplaySessionAsync()
    {
        if (!runtime.DisplayNeedsRestart && runtime.HasLiveDisplay && view.IsAttached) return;
        view.Detach(); view.Visibility = Visibility.Visible; UpdateLayout();
        nint window = await runtime.RestartDisplayAsync(lifetime.Token);
        view.Attach(window); view.SetInputEnabled(false);
    }
    private Task OpenOthersDrawerAsync() => OpenPaneAsync(WorkspacePane.Other);
    private async Task ReadPhoneLocationAsync()
    {
        if (gettingLocation || closing) return;
        gettingLocation = true; locationRequest = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = locationRequest.Token;
        try
        {
            // Persist the address even when the phone cannot currently be reached.
            settings = CurrentOptions(); settings.Save(root);
            locationStatus.Text = "Getting GPS from phone…";
            var fix = await PhoneLocation.GetAsync(settings.PhoneGpsIp, settings.PhoneGpsPort, token); token.ThrowIfCancellationRequested();
            if (runtime.IsRunning) await BroadcastLocationAsync(fix.Latitude, fix.Longitude, token);
            windowsLocation.IsChecked = false;
            latitude.Text = fix.Latitude.ToString("0.######", CultureInfo.InvariantCulture); longitude.Text = fix.Longitude.ToString("0.######", CultureInfo.InvariantCulture);
            settings = CurrentOptions() with { Latitude = fix.Latitude, Longitude = fix.Longitude, LocationSource = "Phone GPS", UsePhoneLocation = true, UseWindowsLocation = false }; settings.Save(root);
            locationStatus.Text = runtime.IsRunning ? "Phone GPS sent to Android for both apps. Refreshes every minute." : "Phone GPS saved for both apps. It will be sent when Android starts.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { locationStatus.Text = "Phone GPS unavailable: " + error.Message + " Keeping saved coordinates."; }
        finally { locationRequest.Dispose(); locationRequest = null; gettingLocation = false; }
    }
}
