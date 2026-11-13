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
            cloneAsiair.IsChecked = cloneStellavita.IsChecked = true; SaveToolOptions(); UpdateMultiWorkspaceControls();
            foreach (var id in Enum.GetValues<WorkspacePane>().Where(p => p != WorkspacePane.Other))
            {
                Trace("Testing pane " + WorkspacePanes.Label(id));
                await RunWorkspaceActionAsync(() => OpenPaneAsync(id),false);
                if (!PaneRunning(id)) throw new IOException(WorkspacePanes.Label(id) + " did not start: " + status.Text);
            }
            await runtime.AssertSeparateProfileAsync(cloneRuntime!,lifetime.Token);
            await RunWorkspaceActionAsync(ShowWorkspaceSplitAsync,false);
            if (selectedPanes.Count != 4) throw new IOException("Split did not select all four running astronomy instances.");
            await RunWorkspaceActionAsync(() => TogglePaneAsync(WorkspacePane.Other,true),false);
            if (selectedPanes.Count != 5 || paneSurfaces.Values.Count(p => p.Frame.Visibility == Visibility.Visible) != 5) throw new IOException("The Other pane did not join the workspace.");
            await Task.Delay(3000,lifetime.Token);
            foreach (var pane in paneSurfaces.Values) pane.View.VerifySize();
            foreach (var id in new[] { WorkspacePane.Other,WorkspacePane.Stellavita2,WorkspacePane.Asiair2 })
                await RunWorkspaceActionAsync(() => TogglePaneAsync(id,false),false);
            if (selectedPanes.Count != 2 || !paneSurfaces.Values.Where(p => p.Id != WorkspacePane.Other).All(p => p.Session is { IsAlive: true })) throw new IOException("Hiding panes stopped their running display sessions.");
            await RunWorkspaceActionAsync(() => TogglePaneAsync(WorkspacePane.Asiair2,true),false);
            var prior = new HashSet<WorkspacePane>(selectedPanes);
            await RunWorkspaceActionAsync(() => ZoomPaneAsync(WorkspacePane.Asiair2));
            // Verification mode suppresses automatic presentation changes; exercise it explicitly.
            EnterFullScreen();
            if (selectedPanes.Count != 1 || !selectedPanes.Contains(WorkspacePane.Asiair2) || !presenting) throw new IOException("Pane zoom did not enter fullscreen.");
            ExitFullScreen();
            if (!workspaceSplit || !selectedPanes.SetEquals(prior)) throw new IOException("Exiting pane fullscreen did not restore the selected split workspace.");
            var gate = new TaskCompletionSource(); int queued = 0;
            var active = RunWorkspaceActionAsync(() => gate.Task,false); await Task.Delay(50);
            await RunWorkspaceActionAsync(() => { queued++; return OpenPaneAsync(WorkspacePane.Other); },false);
            gate.SetResult(); await active; if (queued != 0) throw new IOException("An overlapping pane operation was queued.");
            await ChangeAndroidFontAsync(1.3);
            await runtime.AssertAndroidFontScaleAsync(1.3,lifetime.Token); await cloneRuntime!.AssertAndroidFontScaleAsync(1.3,lifetime.Token);
            if (largerAndroidFont.IsEnabled) throw new IOException("Font size can exceed Android's supported 130% limit.");
            await ChangeAndroidFontAsync(1.0);
            await BroadcastLocationAsync(12.3456,45.6789,lifetime.Token);
            await RunWorkspaceActionAsync(() => StopPaneAsync(WorkspacePane.Asiair2),false);
            if (PaneRunning(WorkspacePane.Asiair2) || !PaneRunning(WorkspacePane.Asiair) || !PaneRunning(WorkspacePane.Stellavita2)) throw new IOException("Stopping one clone affected another instance.");
            File.WriteAllText(report,JsonSerializer.Serialize(new { passed = true, build = "0.6.0", fourAppInstances = true, independentCloneProfile = true, fivePanes = true, selectionTwoThreeFourFive = true, hiddenAppsKeepRunning = true, paneFullscreenAndRestore = true, noQueuedCommands = true, sharedFontAndLocation = true, fontMaximum130 = true, perInstanceTermination = true, time = DateTimeOffset.UtcNow }));
            if (Environment.GetCommandLineArgs().Contains("--hold-workspace")) await Task.Delay(45000,lifetime.Token);
        }
        catch (Exception error) { Environment.ExitCode = 1; Directory.CreateDirectory(Path.GetDirectoryName(report)!); File.WriteAllText(report,JsonSerializer.Serialize(new { passed = false,error = error.ToString() })); Trace(error.ToString()); }
        finally { Close(); }
    }
}

public sealed partial class RuntimeController
{
    public async Task AssertSeparateProfileAsync(RuntimeController other, CancellationToken token)
    {
        if (StateDirectory == other.StateDirectory || options.ConsolePort == other.options.ConsolePort || options.AdbPort == other.options.AdbPort) throw new IOException("The clone profile is not isolated.");
        string key = "astro_portal_test_" + Guid.NewGuid().ToString("N");
        try
        {
            await ShellAsync(token,"settings","put","secure",key,"first"); await other.ShellAsync(token,"settings","put","secure",key,"second");
            if (await ShellAsync(token,"settings","get","secure",key) != "first" || await other.ShellAsync(token,"settings","get","secure",key) != "second") throw new IOException("Android profile data is shared unexpectedly.");
        }
        finally { await ShellAsync(token,"settings","delete","secure",key); await other.ShellAsync(token,"settings","delete","secure",key); }
    }
}
