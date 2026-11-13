using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AsiairContainer;
public sealed partial class MainWindow
{
    public async Task VerifyToolbarAsync()
    {
        verificationMode = true;
        string result = Path.Combine(root, "private-data", "toolbar-verification.json");
        try
        {
            await StartAsync();
            await RunWorkspaceActionAsync(() => runtime.ShowAstroAppAsync(RuntimeController.AsiairPackage, lifetime.Token), false);
            view.SetInputEnabled(true);
            await runtime.EnterToolbarScreenForVerificationAsync(view.ClickGuestWithWindowsMouse, lifetime.Token);
            await RunWorkspaceActionAsync(() => runtime.RepairAsiairCaptionAsync(lifetime.Token), false);
            view.SetInputEnabled(true);
            await Task.Delay(800, lifetime.Token);
            await runtime.VerifyToolbarInputAsync(view.ClickGuestWithWindowsMouse, lifetime.Token);
            File.WriteAllText(result, JsonSerializer.Serialize(new { passed = true, captionFreeControlScreen = true, nativeMouseToolbar = true, time = DateTimeOffset.UtcNow }));
        }
        catch (Exception error) { Environment.ExitCode = 1; File.WriteAllText(result, JsonSerializer.Serialize(new { passed = false, error = error.ToString() })); }
        finally { Close(); }
    }
}
public sealed partial class RuntimeController
{
    public async Task EnterToolbarScreenForVerificationAsync(Action<double,double,double,double> click, CancellationToken token)
    {
        for (int i = 0; i < 20; i++)
        {
            if ((await ReadTasksAsync(token)).Any(t => t.Visible && IsAsiairControlScreen(t))) return;
            var ui = await ReadUiAsync(token);
            var button = ui.Descendants("node").FirstOrDefault(n => (string?)n.Attribute("package") == AsiairPackage && ((string?)n.Attribute("text") is "Connect" or "Enter Device" or "ENTER"));
            if (button is not null)
            {
                int[] b = Regex.Matches((string)button.Attribute("bounds")!, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
                click((b[0]+b[2])/2d,(b[1]+b[3])/2d,2560,DisplayHeight);
            }
            await Task.Delay(1500,token);
        }
        throw new IOException("ASIAIR did not reach its control screen; toolbar verification requires a reachable device.");
    }
    public async Task VerifyToolbarInputAsync(Action<double,double,double,double> click, CancellationToken token)
    {
        var task = (await ReadTasksAsync(token)).FirstOrDefault(t => t.Visible && IsAsiairControlScreen(t));
        if (task?.Mode != "fullscreen") throw new IOException("Open the ASIAIR main control screen before running the toolbar check.");
        var ui = await ReadUiAsync(token);
        var button = ui.Descendants("node").FirstOrDefault(n => ((string?)n.Attribute("resource-id"))?.EndsWith("/top_about_iv") == true)
            ?? throw new IOException("ASIAIR information toolbar button missing.");
        int[] bounds = Regex.Matches((string)button.Attribute("bounds")!, @"\d+").Select(m => int.Parse(m.Value)).ToArray();
        click((bounds[0]+bounds[2])/2d,(bounds[1]+bounds[3])/2d,2560,DisplayHeight);
        await Task.Delay(1200,token);
        var after = await ReadUiAsync(token);
        if (!after.Descendants("node").Any(n => ((string?)n.Attribute("resource-id"))?.EndsWith("/common_setting_title") == true && (string?)n.Attribute("text") == "About")) throw new IOException("The toolbar did not respond to the native Windows mouse click.");
        await CaptureAsync(Path.Combine(root,"ASIAIR-toolbar-response.png"),token);
        await NavigateAsync(false,token);
        Stage("ASIAIR top toolbar responded to native Windows mouse input without a caption overlay.");
    }
}
