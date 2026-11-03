using SnapBerry.Core;
using SnapBerry.App.Capture;
using SnapBerry.App.Rendering;
using SnapBerry.App.Export;
using SnapBerry.App.Shell;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 데스크톱 경계에 대한 선택 영역 보정과 잘라낸 이미지의 크기를 검증한다.
public class CaptureTests
{
    [Fact] public void ZeroSizeSelectionIsRejected()=>Assert.Throws<ArgumentException>(()=>CaptureService.ValidateRegion(new(0,0,0,10),new(0,0,100,100)));
    [Fact] public void RegionOutsideDesktopIsClipped()=>Assert.Equal(new PixelRect(0,0,20,20),CaptureService.ValidateRegion(new(-10,-10,30,30),new(0,0,100,100)));
    [Fact] public void GdiBitmapPixelsAreCopiedWithoutPngRoundTrip()=>Sta.Run(()=>
    {
        // 캡처 비트맵의 BGRA 바이트와 알파가 그대로 유지되고 스레드 간 공유를 위해 동결된다.
        using var bitmap=new System.Drawing.Bitmap(5,3,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for(var y=0;y<3;y++)for(var x=0;x<5;x++)bitmap.SetPixel(x,y,System.Drawing.Color.FromArgb(x==0?0:255,10*x,20*y,200));
        var source=CaptureService.ToBitmapSource(bitmap);
        Assert.True(source.IsFrozen);Assert.Equal(System.Windows.Media.PixelFormats.Bgra32,source.Format);Assert.Equal((5,3),(source.PixelWidth,source.PixelHeight));
        var bytes=new byte[5*3*4];source.CopyPixels(bytes,20,0);
        Assert.Equal(new byte[]{200,0,0,0},bytes[..4]);Assert.Equal(new byte[]{200,40,30,255},bytes[(2*20+3*4)..(2*20+4*4)]);
    });
    [Fact] public void CaptureRecordDetachesTheSelectionFromTheDesktopFrame()=>Sta.Run(()=>
    {
        // 선택 영역은 전체 화면 프레임을 참조하지 않는 독립 비트맵이어야 하고, PNG와 비트맵의 픽셀이 일치해야 한다.
        var frame=new CaptureFrame(RenderingTests.Image(40,30),new(-10,-5,40,30));
        var selected=CaptureService.Crop(frame,new(0,0,12,8));
        var (record,image)=AppHost.CreateRecordAsync(selected,CaptureMode.Region).GetAwaiter().GetResult();
        Assert.IsNotType<System.Windows.Media.Imaging.CroppedBitmap>(image);Assert.True(image.IsFrozen);
        Assert.Equal((12,8,CaptureMode.Region),(record.Width,record.Height,record.Mode));
        var expected=new byte[12*8*4];selected.Image.CopyPixels(expected,48,0);
        var actual=new byte[12*8*4];image.CopyPixels(actual,48,0);Assert.Equal(expected,actual);
        var decoded=new byte[12*8*4];PngFileWriter.Decode(record.OriginalPng).CopyPixels(decoded,48,0);Assert.Equal(expected,decoded);
    });
    [Fact] public void ExcessiveImageSizeIsRejected()=>Assert.Throws<InvalidOperationException>(()=>CompositeRenderer.GuardSize(100000,100000));
}
