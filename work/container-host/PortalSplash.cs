using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AsiairContainer;

/// <summary>Resource-backed splash; boot time approaches the portal but only readiness consumes the last letters.</summary>
public sealed class PortalSplash : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(PortalSplash), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    private static BitmapSource Asset(string name) => BitmapFrame.Create(new Uri("pack://application:,,,/Assets/" + name));
    private readonly BitmapSource original = Asset("Splash.jpg"), plate = Asset("SplashConsumed.png");
    private readonly BitmapSource[] strips;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch elapsed = new();
    private bool booting, completing;
    private readonly Stopwatch rotationClock = Stopwatch.StartNew();
    private const int StripCount = 48;
    private const double Top = .17, Bottom = .66, PortalY = .61;
    public PortalSplash()
    {
        ClipToBounds = true;
        var foreground = Asset("SplashAstro.png"); strips = new BitmapSource[StripCount];
        for (int i = 0; i < StripCount; i++)
        {
            int y = (int)(foreground.PixelHeight * (Top + (Bottom - Top) * i / StripCount));
            int next = (int)(foreground.PixelHeight * (Top + (Bottom - Top) * (i + 1) / StripCount));
            strips[i] = new CroppedBitmap(foreground, new Int32Rect(0, y, foreground.PixelWidth, next - y)); strips[i].Freeze();
        }
        timer.Tick += (_, _) => { if (booting && !completing) Progress = .9 * (1 - Math.Exp(-elapsed.Elapsed.TotalSeconds / 23)); InvalidateVisual(); };
        Loaded += (_, _) => UpdateMotion(); IsVisibleChanged += (_, _) => UpdateMotion();
        Unloaded += (_, _) => timer.Stop();
    }
    public void BeginBoot()
    {
        BeginAnimation(ProgressProperty, null); Progress = 0; booting = true; completing = false; elapsed.Restart();
        if (SystemParameters.ClientAreaAnimation) timer.Start();
        InvalidateVisual();
    }
    public async Task CompleteBootAsync()
    {
        completing = true;
        if (SystemParameters.ClientAreaAnimation)
        {
            var finished = new TaskCompletionSource();
            var animation = new DoubleAnimation(Progress, 1, TimeSpan.FromMilliseconds(700)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            animation.Completed += (_, _) => finished.TrySetResult(); BeginAnimation(ProgressProperty, animation); await finished.Task;
        }
        BeginAnimation(ProgressProperty, null); Progress = 1; InvalidateVisual();
    }
    public void Reset()
    {
        timer.Stop(); elapsed.Reset(); BeginAnimation(ProgressProperty, null); Progress = 0; booting = false; completing = false; UpdateMotion(); InvalidateVisual();
    }
    private void UpdateMotion()
    {
        if (IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation) timer.Start(); else timer.Stop();
    }
    private void DrawPortals(DrawingContext dc, BitmapSource image, Rect area)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        double seconds = rotationClock.Elapsed.TotalSeconds;
        // Unproject the ellipse to a circle, rotate in that plane, then reproject.
        // The silhouette and tilt never rotate in screen space.
        void Portal(double cx, double cy, double major, double minor, double tilt, double angle)
        {
            var center = new Point(area.X + area.Width * cx, area.Y + area.Height * cy);
            var clip = new EllipseGeometry(center, major, minor) { Transform = new RotateTransform(tilt,center.X,center.Y) };
            var matrix = Matrix.Identity;
            matrix.Translate(-center.X,-center.Y); matrix.Rotate(-tilt);
            matrix.Scale(1,major/minor); matrix.Rotate(angle); matrix.Scale(1,minor/major);
            matrix.Rotate(tilt); matrix.Translate(center.X,center.Y);
            dc.PushClip(clip); dc.PushTransform(new MatrixTransform(matrix)); dc.DrawImage(image,area); dc.Pop(); dc.Pop();
        }
        Portal(.5,.610,area.Width*.39,area.Height*.070,0,seconds*19);
        Portal(.342,.886,area.Height*.088,area.Width*.059,-68,-seconds*27);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 21, 30)), null, new Rect(RenderSize));
        double scale = Math.Min(ActualWidth / original.PixelWidth, ActualHeight / original.PixelHeight);
        if (!double.IsFinite(scale) || scale <= 0) return;
        double width = original.PixelWidth * scale, height = original.PixelHeight * scale;
        var area = new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
        if (!booting) { dc.DrawImage(original, area); DrawPortals(dc,plate,area); return; }
        double p = Math.Clamp(Progress, 0, 1);
        dc.DrawImage(plate, area); DrawPortals(dc,plate,area);
        if (p < 1)
        {
            // Horizontal slices taper and curve inward, drawing the lettering into the ring.
            dc.PushOpacity(Math.Min(1, (1 - p) * 5));
            for (int i = 0; i < StripCount; i++)
            {
                double y0 = Top + (Bottom - Top) * i / StripCount, y1 = Top + (Bottom - Top) * (i + 1) / StripCount;
                double taper = Math.Max(.012, 1 - p * (.75 + .23 * i / StripCount));
                double center = .5 + Math.Sin(i * .065 + p * 5) * p * (1 - p) * .12;
                double first = y0 + (PortalY - y0) * p, last = y1 + (PortalY - y1) * p;
                dc.DrawImage(strips[i], new Rect(area.X + width * (center - taper / 2), area.Y + first * height, width * taper, Math.Max(.1, (last - first) * height + .5)));
            }
            dc.Pop();
        }
        // Crossfade the untouched supplied artwork into the matching animation layers at boot start.
        if (p < .08) { dc.PushOpacity(1 - p / .08); dc.DrawImage(original, area); DrawPortals(dc,plate,area); dc.Pop(); }
        if (SystemParameters.ClientAreaAnimation)
        {
            double pulse = .5 + .5 * Math.Sin(elapsed.Elapsed.TotalSeconds * 3);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(35 + pulse * 45), 121, 184, 255)), Math.Max(1, width * .004)), new Point(area.X + width * .5, area.Y + height * PortalY), width * .30, height * .043);
        }
    }
}
