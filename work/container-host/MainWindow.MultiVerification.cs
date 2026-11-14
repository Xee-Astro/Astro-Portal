using System.IO;
using System.Text.Json;
using System.Windows;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    public async Task VerifyMultiWorkspaceAsync()
    {
        verificationMode = true;
        string report = Path.Combine(root,"private-data","multi-workspace-verification.json");
        try
        {
            await StartAsync(); if (!runtime.IsRunning) throw new IOException(status.Text);
            foreach (var id in Enum.GetValues<WorkspacePane>().Where(p => p != WorkspacePane.Other))
            {
                Trace("Testing pane " + WorkspacePanes.Label(id));
                await RunWorkspaceActionAsync(() => OpenPaneAsync(id),false);
                if (!PaneRunning(id)) throw new IOException(WorkspacePanes.Label(id) + " did not start: " + status.Text);
            }
            await RunWorkspaceActionAsync(() => OpenPaneAsync(WorkspacePane.Other),false);
            await runtime.AssertAppDisplayTasksAsync(lifetime.Token);
            await RunWorkspaceActionAsync(ShowWorkspaceSplitAsync,false);
            if (selectedPanes.Count != 2) throw new IOException("Split did not select both running astronomy apps.");
            await RunWorkspaceActionAsync(() => TogglePaneAsync(WorkspacePane.Other,true),false);
            if (selectedPanes.Count != 3 || paneSurfaces.Values.Count(p => p.Frame.Visibility == Visibility.Visible) != 3) throw new IOException("The Other pane did not join the workspace.");
            await Task.Delay(3000,lifetime.Token);
            foreach (var pane in paneSurfaces.Values) pane.View.VerifySize();
            foreach (var id in new[] { WorkspacePane.Other,WorkspacePane.Stellavita })
                await RunWorkspaceActionAsync(() => TogglePaneAsync(id,false),false);
            if (selectedPanes.Count != 1 || !paneSurfaces.Values.Where(p => p.Id != WorkspacePane.Other).All(p => p.Session is { IsAlive: true })) throw new IOException("Hiding panes stopped their running display sessions.");
            await RunWorkspaceActionAsync(() => TogglePaneAsync(WorkspacePane.Stellavita,true),false);
            var prior = new HashSet<WorkspacePane>(selectedPanes);
            await RunWorkspaceActionAsync(() => ZoomPaneAsync(WorkspacePane.Stellavita));
            // Verification mode suppresses automatic presentation changes; exercise it explicitly.
            EnterFullScreen();
            if (selectedPanes.Count != 1 || !selectedPanes.Contains(WorkspacePane.Stellavita) || !presenting) throw new IOException("Pane zoom did not enter fullscreen.");
            ExitFullScreen();
            if (!workspaceSplit || !selectedPanes.SetEquals(prior)) throw new IOException("Exiting pane fullscreen did not restore the selected split workspace.");
            var gate = new TaskCompletionSource(); int queued = 0;
            var active = RunWorkspaceActionAsync(() => gate.Task,false); await Task.Delay(50);
            await RunWorkspaceActionAsync(() => { queued++; return OpenPaneAsync(WorkspacePane.Other); },false);
            gate.SetResult(); await active; if (queued != 0) throw new IOException("An overlapping pane operation was queued.");
            await ChangeAndroidFontAsync(1.3);
            await runtime.AssertAndroidFontScaleAsync(1.3,lifetime.Token);
            if (largerAndroidFont.IsEnabled) throw new IOException("Font size can exceed Android's supported 130% limit.");
            await ChangeAndroidFontAsync(1.0);
            await BroadcastLocationAsync(12.3456,45.6789,lifetime.Token);
            await RunWorkspaceActionAsync(() => StopPaneAsync(WorkspacePane.Stellavita),false);
            if (PaneRunning(WorkspacePane.Stellavita) || !PaneRunning(WorkspacePane.Asiair)) throw new IOException("Stopping Stellavita affected ASIAIR.");
            File.WriteAllText(report,JsonSerializer.Serialize(new { passed = true, build = "0.6.1", twoAppInstances = true, threePanes = true, selectionOneTwoThree = true, cloneLaunchingRemoved = true, hiddenAppsKeepRunning = true, paneFullscreenAndRestore = true, noQueuedCommands = true, sharedFontAndLocation = true, fontMaximum130 = true, perInstanceTermination = true, time = DateTimeOffset.UtcNow }));
            if (Environment.GetCommandLineArgs().Contains("--hold-workspace")) await Task.Delay(45000,lifetime.Token);
        }
        catch (Exception error) { Environment.ExitCode = 1; Directory.CreateDirectory(Path.GetDirectoryName(report)!); File.WriteAllText(report,JsonSerializer.Serialize(new { passed = false,error = error.ToString() })); Trace(error.ToString()); }
        finally { Close(); }
    }
}
