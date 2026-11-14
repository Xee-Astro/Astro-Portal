using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AsiairContainer;

/// <summary>The supplied artwork remains unchanged while stopped or booting.</summary>
public sealed class PortalSplash : FrameworkElement
{
    private static readonly BitmapSource Artwork = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Splash.png"));
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16,21,30)),null,new Rect(RenderSize));
        double scale = Math.Min(ActualWidth/Artwork.PixelWidth,ActualHeight/Artwork.PixelHeight);
        if (!double.IsFinite(scale) || scale <= 0) return;
        double width = Artwork.PixelWidth*scale, height = Artwork.PixelHeight*scale;
        dc.DrawImage(Artwork,new Rect((ActualWidth-width)/2,(ActualHeight-height)/2,width,height));
    }
}
