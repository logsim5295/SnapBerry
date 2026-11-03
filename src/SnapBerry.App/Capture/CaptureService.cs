using SnapBerry.Core;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using SnapBerry.App.Rendering;
namespace SnapBerry.App.Capture;
// 비트맵의 (0,0)과 실제 가상 데스크톱의 원점은 다를 수 있어 화면 좌표를 함께 보관한다.
public sealed record CaptureFrame(BitmapSource Image,PixelRect DesktopBounds);
public sealed class CaptureService
{
    public static PixelRect ValidateRegion(PixelRect rect,PixelRect desktop)
    {
        var x=Math.Max(rect.X,desktop.X);var y=Math.Max(rect.Y,desktop.Y);var right=Math.Min(rect.X+rect.Width,desktop.X+desktop.Width);var bottom=Math.Min(rect.Y+rect.Height,desktop.Y+desktop.Height);
        if(right<=x||bottom<=y)throw new ArgumentException("Select a non-empty area.");return new(x,y,right-x,bottom-y);
    }
    public CaptureFrame CaptureDesktop()
    {
        // 음수 위치 모니터를 포함한 전체 경계를 만들고 각 모니터를 비트맵 내부 좌표로 이동해 복사한다.
        var screens=System.Windows.Forms.Screen.AllScreens;var left=screens.Min(x=>x.Bounds.Left);var top=screens.Min(x=>x.Bounds.Top);var right=screens.Max(x=>x.Bounds.Right);var bottom=screens.Max(x=>x.Bounds.Bottom);
        CompositeRenderer.GuardSize(right-left,bottom-top);
        using var bitmap=new Bitmap(right-left,bottom-top,PixelFormat.Format32bppArgb);
        using(var graphics=Graphics.FromImage(bitmap))
        {graphics.Clear(Color.Black);foreach(var screen in screens)graphics.CopyFromScreen(screen.Bounds.Location,new Point(screen.Bounds.Left-left,screen.Bounds.Top-top),screen.Bounds.Size,CopyPixelOperation.SourceCopy);}
        return new(ToBitmapSource(bitmap),new(left,top,right-left,bottom-top));
    }
    // PNG 인코딩·디코딩 왕복 없이 GDI+ 픽셀을 그대로 복사한다. 4K 기준 1초 가까운 캡처 지연이 수십 ms로 줄어든다.
    internal static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try
        {
            var source=BitmapSource.Create(bitmap.Width,bitmap.Height,96,96,System.Windows.Media.PixelFormats.Bgra32,null,data.Scan0,data.Stride*bitmap.Height,data.Stride);
            source.Freeze();return source;
        }
        finally{bitmap.UnlockBits(data);}
    }
    public static CaptureFrame Crop(CaptureFrame frame,PixelRect selection)
    {
        // 선택 영역을 데스크톱 안으로 제한한 다음 비트맵 원점 기준으로 변환한다.
        var rect=ValidateRegion(selection,frame.DesktopBounds);var image=new CroppedBitmap(frame.Image,new System.Windows.Int32Rect(rect.X-frame.DesktopBounds.X,rect.Y-frame.DesktopBounds.Y,rect.Width,rect.Height));image.Freeze();return new(image,rect);
    }
}
