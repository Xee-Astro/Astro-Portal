using System.IO;

namespace AsiairContainer;

public sealed partial class RuntimeController
{
    public async Task VerifyWorkspaceAsync(CancellationToken token)
    {
        Stage("Checking astronomy split screen and the full-screen Others drawer…");
        string Pid(string package) => package == AsiairPackage ? "asiair" : "stellavita";
        var processes = new Dictionary<string, string>();
        foreach (string package in new[] { AsiairPackage, StellavitaPackage }) processes[package] = await ShellAsync(token, "pidof", package);
        await ShowSplitScreenAsync(token);
        await Task.Delay(3000, token);
        await AssertResponsiveUiAsync(token);
        await CaptureAsync(Path.Combine(root, "Split-screen.png"), token);
        await ShowOthersAsync(token);
        await Task.Delay(1500, token);
        var others = await ReadTasksAsync(token);
        if (!(await ListOtherAppsAsync(token)).Any(t => t.Package == "com.android.settings")) throw new IOException("The Others drawer is missing installed apps.");
        if (others.Any(t => t.Package is AsiairPackage or StellavitaPackage && t.Visible)) throw new IOException("Others did not minimize both astronomy apps.");

        await LaunchAppAsync("com.android.settings", token);
        await Task.Delay(1500, token);
        var settings = await ReadTasksAsync(token);
        if (!settings.Any(t => t.Package == "com.android.settings" && t.Visible && t.Mode == "fullscreen")) throw new IOException("Other apps did not open full screen.");
        await ShowSplitScreenAsync(token);
        await ShowAstroAppAsync(AsiairPackage, token);
        var single = await ReadTasksAsync(token);
        if (!single.Any(t => t.Package == AsiairPackage && t.Visible && (t.Mode == "freeform" || t.Mode == "fullscreen")) || single.Any(t => t.Package == StellavitaPackage && t.Visible)) { File.WriteAllText(Path.Combine(state,"single-layout-failure.txt"), await ShellAsync(token,"am","stack","list")); throw new IOException("The ASIAIR start button did not restore full screen."); }
        await ShowAstroAppAsync(StellavitaPackage, token);
        single = await ReadTasksAsync(token);
        if (!single.Any(t => t.Package == StellavitaPackage && t.Visible && t.Mode == "fullscreen")) throw new IOException("The Stellavita start button did not restore full screen.");
        await ShowSplitScreenAsync(token);
        foreach (string package in processes.Keys)
            if (processes[package] != await ShellAsync(token, "pidof", package)) throw new IOException(Pid(package) + " restarted while switching layouts.");
        await AssertResponsiveUiAsync(token);
        Stage("Split screen, both start buttons, full-screen Others and preserved app processes verified.");
    }
}
