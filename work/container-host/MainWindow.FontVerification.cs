using System.IO;
using System.Globalization;
using System.Text.Json;

namespace AsiairContainer;
public sealed partial class MainWindow
{
    public async Task VerifyAndroidFontAsync()
    {
        verificationMode = true;
        string result = Path.Combine(root, "private-data", "font-verification.json");
        double original = settings.AndroidFontScale;
        try
        {
            await StartAsync();
            if (!runtime.IsRunning) throw new IOException(status.Text);
            foreach (double scale in new[] { 1.1, 0.9, 1.0 })
            {
                await ChangeAndroidFontAsync(scale);
                await runtime.AssertAndroidFontScaleAsync(scale, lifetime.Token);
                if (ContainerOptions.Load(root).AndroidFontScale != scale || androidFontPercent.Text != scale.ToString("0%", CultureInfo.InvariantCulture))
                    throw new IOException("Font setting, Android readback and dashboard disagree.");
            }
            await ChangeAndroidFontAsync(original);
            File.WriteAllText(result, JsonSerializer.Serialize(new { passed = true, build = "0.5.1", liveIncrease = true, liveDecrease = true, reset = true, persistence = true, time = DateTimeOffset.UtcNow }));
        }
        catch (Exception error) { Environment.ExitCode = 1; Directory.CreateDirectory(Path.GetDirectoryName(result)!); File.WriteAllText(result, JsonSerializer.Serialize(new { passed = false, error = error.ToString() })); }
        finally { Close(); }
    }
}
