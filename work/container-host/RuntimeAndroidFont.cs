using System.Globalization;
using System.IO;

namespace AsiairContainer;

public sealed partial class RuntimeController
{
    private async Task ApplyAndroidFontScaleCoreAsync(double scale, CancellationToken token)
    {
        ContainerOptions.ValidateFontScale(scale);
        try { await ShellAsync(token, "settings", "put", "system", "font_scale", scale.ToString("0.0", CultureInfo.InvariantCulture)); }
        catch (IOException error) { throw new IOException("Android could not apply that font size. Choose a value from 80% to 130%.", error); }
        await AssertAndroidFontScaleAsync(scale, token);
    }
    public async Task AssertAndroidFontScaleAsync(double scale, CancellationToken token)
    {
        string actual = await ShellAsync(token, "settings", "get", "system", "font_scale");
        if (!double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out double readback) || Math.Abs(readback - scale) > 0.001)
            throw new IOException("Android did not confirm the requested font size.");
    }
    public async Task SetAndroidFontScaleAsync(double scale, CancellationToken token)
    {
        if (!IsRunning) throw new InvalidOperationException("Start Android before applying font size.");
        await ApplyAndroidFontScaleCoreAsync(scale, token);
        options = options with { AndroidFontScale = scale };
    }
}
