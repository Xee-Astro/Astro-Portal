using System.Windows;
using System.Windows.Controls;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    private readonly Button fullScreenButton = ToolButton("Full screen");
    private readonly Button fullSplitButton = ToolButton("Split screen");
    private readonly List<Button> presentationNavigation = [];
    private Border presentationBar = null!;
    private bool presenting;
    private WindowStyle previousStyle;
    private WindowState previousState;
    private ResizeMode previousResize;
    private Rect previousBounds;
    private double previousMinWidth, previousMinHeight;
    private readonly Dictionary<UIElement, Visibility> previousVisibility = [];
    private UIElement BuildPresentationShell(Grid content)
    {
        var shell = new Grid(); shell.RowDefinitions.Add(new() { Height = GridLength.Auto }); shell.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        var bar = new DockPanel { LastChildFill = false };
        var exit = ToolButton("Exit full screen"); exit.Tag = "Accent"; DockPanel.SetDock(exit, Dock.Right); exit.Click += async (_, _) => await ChangePresentationAsync(false); presentationExit = exit; bar.Children.Add(exit);
        void Add(WorkspacePane id)
        {
            var button = ToolButton(WorkspacePanes.Label(id)); button.DataContext = id;
            button.Tag = id is WorkspacePane.Asiair or WorkspacePane.Asiair2 ? "ASIAIR" : id is WorkspacePane.Stellavita or WorkspacePane.Stellavita2 ? "Stellavita" : null;
            button.Click += async (_, _) => await RunWorkspaceActionAsync(() => OpenPaneAsync(id)); bar.Children.Add(button); presentationNavigation.Add(button);
        }
        foreach (var id in Enum.GetValues<WorkspacePane>()) Add(id);
        fullSplitButton.Click += async (_, _) => await RunWorkspaceActionAsync(ShowWorkspaceSplitAsync, false); bar.Children.Add(fullSplitButton);
        presentationBar = new Border { Child = bar, Background = Brush("#10151E"), Padding = new(10, 7, 10, 7), Visibility = Visibility.Collapsed };
        shell.Children.Add(presentationBar); Grid.SetRow(content, 1); shell.Children.Add(content); return shell;
    }
    private void UpdatePresentationButtons()
    {
        bool active = runtime.IsRunning && workspaceSplit;
        foreach (var button in new[] { splitScreen, fullSplitButton }) { button.Tag = active ? "Active" : null; button.Content = active ? "Split screen: ON" : "Split screen"; }
        fullScreenButton.IsEnabled = !busy && runtime.IsRunning;
        splitScreen.IsEnabled = fullSplitButton.IsEnabled = !busy && runtime.IsRunning && paneSurfaces.Keys.Any(id => id != WorkspacePane.Other && PaneRunning(id));
        foreach (var button in presentationNavigation)
        {
            var id = (WorkspacePane)button.DataContext;
            button.Visibility = id == WorkspacePane.Asiair2 && !settings.CloneAsiair || id == WorkspacePane.Stellavita2 && !settings.CloneStellavita ? Visibility.Collapsed : Visibility.Visible;
            button.IsEnabled = !busy && (Equals(button.Tag,"ASIAIR") ? asiairInstalled : Equals(button.Tag,"Stellavita") ? stellaInstalled : true);
        }
        if (presentationExit is not null) presentationExit.IsEnabled = !busy;
    }
    private void EnterFullScreen()
    {
        if (presenting || !runtime.IsRunning) return;
        previousMinWidth = MinWidth; previousMinHeight = MinHeight;
        previousStyle = WindowStyle; previousState = WindowState; previousResize = ResizeMode;
        previousBounds = WindowState == WindowState.Normal ? new(Left, Top, Width, Height) : RestoreBounds;
        previousVisibility.Clear();
        foreach (UIElement element in mainContent.Children)
            if (element != screenBorder) { previousVisibility[element] = element.Visibility; element.Visibility = Visibility.Collapsed; }
        mainContent.Margin = new(0); presentationBar.Visibility = Visibility.Visible;
        MinWidth = 0; MinHeight = 0;
        WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized;
        presenting = true; UpdateLayout(); view.Fit();
        if (appDrawer.Visibility != Visibility.Visible) view.FocusAndroid();
    }
    private void ExitFullScreen()
    {
        if (!presenting) return;
        WindowState = WindowState.Normal; MinWidth = previousMinWidth; MinHeight = previousMinHeight; WindowStyle = previousStyle; ResizeMode = previousResize;
        Left = previousBounds.Left; Top = previousBounds.Top; Width = previousBounds.Width; Height = previousBounds.Height; WindowState = previousState;
        foreach (var entry in previousVisibility) entry.Key.Visibility = entry.Value;
        previousVisibility.Clear(); mainContent.Margin = new(20); presentationBar.Visibility = Visibility.Collapsed;
        presenting = false; RestoreZoomedWorkspace(); UpdateLayout(); view.Fit(); if (appDrawer.Visibility != Visibility.Visible) view.FocusAndroid();
    }
}
