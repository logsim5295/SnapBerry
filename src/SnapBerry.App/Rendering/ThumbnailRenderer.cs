using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapBerry.App.Rendering;

internal static class ThumbnailRenderer
{
    public static BitmapSource Create(BitmapSource image)
    {
        var scale=Math.Min(1,120.0/Math.Max(image.PixelWidth,image.PixelHeight));
        var width=Math.Max(1,(int)Math.Round(image.PixelWidth*scale));var height=Math.Max(1,(int)Math.Round(image.PixelHeight*scale));
        var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
        using(var dc=visual.RenderOpen())dc.DrawImage(image,new Rect(0,0,width,height));
        // 작은 픽셀 버퍼만 보관하여 원본 크기의 합성 이미지를 목록에서 붙잡지 않는다.
        var thumbnail=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);thumbnail.Render(visual);thumbnail.Freeze();return thumbnail;
    }
}
