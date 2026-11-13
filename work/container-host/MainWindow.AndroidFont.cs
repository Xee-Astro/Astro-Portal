using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace AsiairContainer;

public sealed partial class MainWindow
{
    private readonly Button smallerAndroidFont = ToolButton("A−");
    private readonly Button largerAndroidFont = ToolButton("A+");
    private readonly Button resetAndroidFont = ToolButton("Reset");
    private readonly TextBlock androidFontPercent = new() { Width = 48, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private void BuildAndroidFontControls(Panel panel)
    {
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(8, 4, 0, 4) };
        controls.Children.Add(new TextBlock { Text = "Android font", VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) });
        foreach (var button in new[] { smallerAndroidFont, largerAndroidFont, resetAndroidFont }) button.MinHeight = 36;
        smallerAndroidFont.ToolTip = "Decrease Android system font size by 10%";
        largerAndroidFont.ToolTip = "Increase Android system font size by 10%";
        resetAndroidFont.ToolTip = "Restore Android system font size to 100%";
        androidFontPercent.ToolTip = "Applies to Android system text and apps that follow its font setting. Some app text uses a fixed size.";
        controls.Children.Add(smallerAndroidFont); controls.Children.Add(androidFontPercent);
        controls.Children.Add(largerAndroidFont); controls.Children.Add(resetAndroidFont);
        panel.Children.Add(controls);
        smallerAndroidFont.Click += async (_, _) => await ChangeAndroidFontAsync(Math.Round(settings.AndroidFontScale - 0.1, 1));
        largerAndroidFont.Click += async (_, _) => await ChangeAndroidFontAsync(Math.Round(settings.AndroidFontScale + 0.1, 1));
        resetAndroidFont.Click += async (_, _) => await ChangeAndroidFontAsync(1.0);
        UpdateAndroidFontButtons();
    }
    private void UpdateAndroidFontButtons()
    {
        androidFontPercent.Text = settings.AndroidFontScale.ToString("0%", CultureInfo.InvariantCulture);
        smallerAndroidFont.IsEnabled = !busy && !closing && settings.AndroidFontScale > 0.8;
        largerAndroidFont.IsEnabled = !busy && !closing && settings.AndroidFontScale < 1.3;
        resetAndroidFont.IsEnabled = !busy && !closing && Math.Abs(settings.AndroidFontScale - 1.0) > 0.001;
    }
    private async Task ChangeAndroidFontAsync(double scale)
    {
        if (busy || commandActive || closing) return;
        scale = Math.Clamp(scale, 0.8, 1.3);
        async Task Apply()
        {
            var changed = CurrentOptions() with { AndroidFontScale = scale };
            if (runtime.IsRunning) await runtime.SetAndroidFontScaleAsync(scale, lifetime.Token);
            if (cloneRuntime is { IsRunning: true }) await cloneRuntime.SetAndroidFontScaleAsync(scale,lifetime.Token);
            changed.Save(root); settings = changed;
        }
        if (runtime.IsRunning) await RunWorkspaceActionAsync(Apply, false);
        else
        {
            try { await Apply(); status.Text = "Android font size saved for the next start."; }
            catch (Exception error) { status.Text = error.Message; }
        }
        UpdateAndroidFontButtons();
    }
}
