using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    private sealed class PaneSurface
    {
        public required WorkspacePane Id;
        public required EmbeddedView View;
        public required Border Frame;
        public required Button Toggle;
        public AppDisplaySession? Session;
    }
    private readonly Dictionary<WorkspacePane, PaneSurface> paneSurfaces = [];
    private readonly HashSet<WorkspacePane> selectedPanes = [WorkspacePane.Other];
    private HashSet<WorkspacePane>? zoomRestore;
    private Grid paneGrid = null!;
    private Grid workspaceSurface = null!;
    private WrapPanel paneChoices = null!;
    private bool workspaceSplit;
    private RuntimeController? cloneRuntime;
    private readonly CheckBox cloneAsiair = new() { Content = "Clone", Margin = new(3, 5, 0, 0) };
    private readonly CheckBox cloneStellavita = new() { Content = "Clone", Margin = new(3, 5, 0, 0) };
    private readonly Button asiair2Start = ToolButton("ASIAIR 2"), stellavita2Start = ToolButton("Stellavita 2");
    private UIElement? asiair2Tile, stellavita2Tile;
    private UIElement DashboardAppTile(Button button, bool stella)
    {
        var panel = new StackPanel { Margin = new(0, 0, 8, 0) };
        panel.Children.Add(AppTile(button, stella));
        var check = stella ? cloneStellavita : cloneAsiair;
        check.IsChecked = stella ? settings.CloneStellavita : settings.CloneAsiair;
        check.ToolTip = "Show a second app button with independent Android app data. Hiding the button does not erase or stop a running clone.";
        check.Click += (_, _) => { SaveToolOptions(); UpdateMultiWorkspaceControls(); };
        panel.Children.Add(check); return panel;
    }
    private UIElement CloneTile(Button button, WorkspacePane id)
    {
        button.Tag = id == WorkspacePane.Asiair2 ? "ASIAIR" : "Stellavita";
        var grid = new Grid { Margin = new(0, 0, 8, 0) };
        button.Padding = new(15, 9, 32, 9); button.MinHeight = 44; button.Margin = new(0); grid.Children.Add(button);
        var close = ToolButton("×"); close.Width = close.Height = 24; close.Padding = new(3,0,3,0); close.Margin = new(0,3,3,0); close.HorizontalAlignment = HorizontalAlignment.Right; close.VerticalAlignment = VerticalAlignment.Top;
        close.ToolTip = "Close " + WorkspacePanes.Label(id); close.Click += async (_, _) => await RunWorkspaceActionAsync(() => StopPaneAsync(id), false);
        close.Tag = button.Tag; grid.Children.Add(close);
        button.Click += async (_, _) => await RunWorkspaceActionAsync(() => OpenPaneAsync(id));
        cloneCloseButtons[id] = close; return grid;
    }
    private readonly Dictionary<WorkspacePane, Button> cloneCloseButtons = [];
    private void AddCloneLaunchControls(Panel apps)
    {
        asiair2Tile = CloneTile(asiair2Start, WorkspacePane.Asiair2); stellavita2Tile = CloneTile(stellavita2Start, WorkspacePane.Stellavita2);
        apps.Children.Add(asiair2Tile); apps.Children.Add(stellavita2Tile); UpdateMultiWorkspaceControls();
    }
    private void BuildWorkspaceSurface(Grid screen)
    {
        var surface = new Grid { Visibility = Visibility.Collapsed }; workspaceSurface = surface; surface.RowDefinitions.Add(new() { Height = GridLength.Auto }); surface.RowDefinitions.Add(new());
        paneChoices = new WrapPanel { Margin = new(6), Visibility = Visibility.Collapsed };
        paneChoices.Children.Add(new TextBlock { Text = "Show:", Margin = new(4, 5, 12, 0), VerticalAlignment = VerticalAlignment.Center });
        paneGrid = new Grid(); Grid.SetRow(paneGrid,1); surface.Children.Add(paneChoices); surface.Children.Add(paneGrid);
        foreach (var id in Enum.GetValues<WorkspacePane>())
        {
            var embedded = id == WorkspacePane.Other ? view : new EmbeddedView { SuppressActivation = verificationMode };
            var badge = new Border { Background = Brush(id is WorkspacePane.Asiair or WorkspacePane.Asiair2 ? "#862B36" : id is WorkspacePane.Stellavita or WorkspacePane.Stellavita2 ? "#164E87" : "#26354A"), Padding = new(10,6,10,6), Cursor = Cursors.Hand };
            badge.Child = new TextBlock { Text = WorkspacePanes.Label(id), Foreground = Brush("#FFFFFF"), FontWeight = FontWeights.SemiBold };
            badge.ToolTip = "Double-click to show this screen full screen";
            badge.MouseLeftButtonDown += async (_, e) => { if (e.ClickCount == 2) { e.Handled = true; await RunWorkspaceActionAsync(() => ZoomPaneAsync(id)); } };
            var body = new Grid(); body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new()); body.Children.Add(badge); Grid.SetRow(embedded,1); body.Children.Add(embedded);
            var frame = new Border { Child = body, BorderThickness = new(1), BorderBrush = Brush("#8298B3"), Margin = new(3), Background = Brush("#000000"), Visibility = id == WorkspacePane.Other ? Visibility.Visible : Visibility.Collapsed };
            var toggle = ToolButton(WorkspacePanes.Label(id)); toggle.MinHeight = 34;
            toggle.Click += async (_, _) => { bool wanted = !selectedPanes.Contains(id); await RunWorkspaceActionAsync(() => TogglePaneAsync(id,wanted), false); UpdateMultiWorkspaceControls(); };
            paneChoices.Children.Add(toggle); paneGrid.Children.Add(frame); paneSurfaces[id] = new() { Id = id, View = embedded, Frame = frame, Toggle = toggle };
        }
        screen.Children.Add(surface); ReflowWorkspace();
    }
    private bool PaneRunning(WorkspacePane id) => id == WorkspacePane.Other ? runtime.IsRunning : paneSurfaces.TryGetValue(id, out var p) && p.Session is { IsAlive: true };
    private RuntimeController ControllerFor(WorkspacePane id) => WorkspacePanes.IsClone(id) ? cloneRuntime ?? throw new InvalidOperationException("Second profile has not started.") : runtime;
    private void ReflowWorkspace()
    {
        if (paneGrid is null) return;
        var placements = WorkspacePanes.Arrange(selectedPanes);
        paneGrid.RowDefinitions.Clear(); paneGrid.ColumnDefinitions.Clear();
        int columns = placements.Count > 1 ? 2 : 1, rows = Math.Max(1,(placements.Count+1)/2);
        for (int i=0;i<columns;i++) paneGrid.ColumnDefinitions.Add(new());
        for (int i=0;i<rows;i++) paneGrid.RowDefinitions.Add(new());
        foreach (var pane in paneSurfaces.Values) pane.Frame.Visibility = Visibility.Collapsed;
        foreach (var p in placements) { var frame = paneSurfaces[p.Pane].Frame; Grid.SetRow(frame,p.Row); Grid.SetColumn(frame,p.Column); Grid.SetColumnSpan(frame,p.ColumnSpan); frame.Visibility = Visibility.Visible; }
        paneChoices.Visibility = workspaceSplit ? Visibility.Visible : Visibility.Collapsed;
        UpdateLayout(); foreach (var pane in paneSurfaces.Values) pane.View.Fit(); UpdateMultiWorkspaceControls();
    }
    private void UpdateMultiWorkspaceControls()
    {
        if (asiair2Tile is not null) asiair2Tile.Visibility = cloneAsiair.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (stellavita2Tile is not null) stellavita2Tile.Visibility = cloneStellavita.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        cloneAsiair.IsEnabled = cloneStellavita.IsEnabled = !busy && !closing;
        foreach (var item in new[] { (WorkspacePane.Asiair2,asiair2Start,asiairInstalled),(WorkspacePane.Stellavita2,stellavita2Start,stellaInstalled) })
        {
            bool running = PaneRunning(item.Item1); item.Item2.IsEnabled = !busy && !closing && item.Item3;
            item.Item2.BorderBrush = Brush(running ? "#FFDF56" : item.Item1 == WorkspacePane.Asiair2 ? "#FFA0A0" : "#79B8FF"); item.Item2.BorderThickness = new(running ? 3 : 1);
            if (cloneCloseButtons.TryGetValue(item.Item1,out var close)) { close.Visibility = running ? Visibility.Visible : Visibility.Collapsed; close.IsEnabled = !busy && !closing; }
        }
        foreach (var pane in paneSurfaces.Values)
        {
            pane.Toggle.IsEnabled = !busy && !closing && PaneRunning(pane.Id);
            pane.Toggle.Tag = selectedPanes.Contains(pane.Id) ? "Active" : null;
            pane.Toggle.ToolTip = selectedPanes.Contains(pane.Id) ? "Hide this pane while keeping its app running" : "Show this pane alongside the other selected screens";
            if (pane.View.IsAttached) pane.View.SetInputEnabled(!busy && !closing);
        }
    }
    private async Task EnsureCloneRuntimeAsync()
    {
        if (cloneRuntime is { IsRunning: true }) return;
        int port = FindCloneConsolePort(settings.ConsolePort);
        using var socket = new TcpListener(IPAddress.Loopback,0); socket.Start(); int adbPort = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        var options = CurrentOptions() with { ConsolePort = port, AdbPort = adbPort, MemoryMb = Math.Min(settings.MemoryMb,3072) };
        cloneRuntime = new(root,options,true); cloneRuntime.Status += message => Dispatcher.InvokeAsync(() => { status.Text = "Second profile: " + message; Trace(message); });
        SetLoading(true); try { await cloneRuntime.StartAsync(lifetime.Token); await loadingSplash.CompleteBootAsync(); } finally { SetLoading(false); }
    }
    private static int FindCloneConsolePort(int original)
    {
        bool Free(int port) { try { var socket = new TcpListener(IPAddress.Loopback,port); socket.Start(); socket.Stop(); return true; } catch (SocketException) { return false; } }
        foreach (int port in Enumerable.Range(0,64).Select(i => 5554 + ((original - 5554 + 4 + i*2) % 128))) if (port != original && Free(port) && Free(port+1)) return port;
        throw new IOException("No free private Android port pair is available.");
    }
    private async Task OpenPaneAsync(WorkspacePane id)
    {
        if (id == WorkspacePane.Other) { await runtime.NavigateAsync(true,lifetime.Token); }
        else
        {
            if (WorkspacePanes.IsClone(id)) { await EnsureCloneRuntimeAsync(); await runtime.CopyInstalledAppToAsync(cloneRuntime!,WorkspacePanes.Package(id)!,lifetime.Token); }
            var pane = paneSurfaces[id];
            pane.Session = await ControllerFor(id).OpenAppDisplayAsync(WorkspacePanes.Package(id)!,lifetime.Token);
            pane.Frame.Visibility = Visibility.Visible; pane.View.Visibility = Visibility.Visible; UpdateLayout(); pane.View.Attach(pane.Session.Window); pane.View.SetInputEnabled(false);
        }
        zoomRestore = null; workspaceSplit = false; selectedPanes.Clear(); selectedPanes.Add(id); ReflowWorkspace();
    }
    private Task ShowWorkspaceSplitAsync()
    {
        if (!paneSurfaces.Keys.Any(id => id != WorkspacePane.Other && PaneRunning(id))) throw new InvalidOperationException("Start at least one astronomy app first.");
        zoomRestore = null; workspaceSplit = true; selectedPanes.Clear();
        foreach (var id in paneSurfaces.Keys.Where(id => id != WorkspacePane.Other && PaneRunning(id))) selectedPanes.Add(id);
        ReflowWorkspace(); return Task.CompletedTask;
    }
    private Task TogglePaneAsync(WorkspacePane id, bool wanted)
    {
        if (!workspaceSplit || !PaneRunning(id)) return Task.CompletedTask;
        if (wanted) selectedPanes.Add(id);
        else if (selectedPanes.Count > 1) selectedPanes.Remove(id);
        ReflowWorkspace(); return Task.CompletedTask;
    }
    private Task ZoomPaneAsync(WorkspacePane id)
    {
        if (!PaneRunning(id)) return Task.CompletedTask;
        if (workspaceSplit) zoomRestore = new(selectedPanes);
        selectedPanes.Clear(); selectedPanes.Add(id); workspaceSplit = false; ReflowWorkspace(); return Task.CompletedTask;
    }
    private void RestoreZoomedWorkspace()
    {
        if (zoomRestore is null) return;
        selectedPanes.Clear(); foreach (var id in zoomRestore.Where(PaneRunning)) selectedPanes.Add(id);
        zoomRestore = null; workspaceSplit = true; if (selectedPanes.Count == 0) selectedPanes.Add(WorkspacePane.Other); ReflowWorkspace();
    }
    private async Task StopPaneAsync(WorkspacePane id)
    {
        var pane = paneSurfaces[id]; pane.View.Detach(); pane.Session = null;
        await ControllerFor(id).CloseAppDisplayAsync(WorkspacePanes.Package(id)!,lifetime.Token);
        selectedPanes.Remove(id); zoomRestore?.Remove(id);
        if (selectedPanes.Count == 0) selectedPanes.Add(WorkspacePane.Other);
        ReflowWorkspace();
    }
    private async Task StopMultiWorkspaceAsync()
    {
        foreach (var pane in paneSurfaces.Values.Where(p => p.Id != WorkspacePane.Other)) { pane.View.Detach(); pane.Session = null; }
        if (cloneRuntime is not null) { await cloneRuntime.DisposeAsync(); cloneRuntime = null; }
        zoomRestore = null; workspaceSplit = false; selectedPanes.Clear(); selectedPanes.Add(WorkspacePane.Other); ReflowWorkspace();
    }
    private async Task BroadcastLocationAsync(double latitude, double longitude, CancellationToken token)
    {
        await runtime.SetLocationAsync(latitude,longitude,token);
        if (cloneRuntime is { IsRunning: true }) await cloneRuntime.SetLocationAsync(latitude,longitude,token);
    }
    private void SetWorkspaceLoading(bool loading)
    {
        foreach (var pane in paneSurfaces.Values) pane.View.Visibility = loading ? Visibility.Hidden : Visibility.Visible;
    }
}
