using System.Globalization;
using System.IO;

namespace AsiairContainer;

public sealed partial class RuntimeController
{
    private async Task ApplyAndroidFontScaleCoreAsync(double scale, CancellationToken token)
    {
        ContainerOptions.ValidateFontScale(scale);
        await ShellAsync(token, "settings", "put", "system", "font_scale", scale.ToString("0.0", CultureInfo.InvariantCulture));
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
