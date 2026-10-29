using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Capture;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 합성 이미지와 메모리 렌더링만 사용한다. 창 표시와 실제 화면 캡처는 하지 않는다.
public class CaptureMagnifierTests
{
    [Fact]
    public void SampleUsesPhysicalPixelsOnNegativeOriginDesktop()
    {
        var sample = CaptureMagnifier.Sample(new(-100, 50), new(-1920, -200, 3840, 1280));
        Assert.Equal(new Int32Rect(1812, 242, 17, 17), sample.Source);
        Assert.Equal(new Rect(0, 0, 136, 136), sample.Destination);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 9, 9, 64, 64)]
    [InlineData(99, 79, 91, 71, 9, 9, 0, 0)]
    public void EdgeClippingKeepsPointedPixelAtCenter(int x, int y, int sx, int sy, int w, int h, int dx, int dy)
    {
        var sample = CaptureMagnifier.Sample(new(x, y), new(0, 0, 100, 80));
        Assert.Equal(new Int32Rect(sx, sy, w, h), sample.Source);
        Assert.Equal(new Rect(dx, dy, 72, 72), sample.Destination);
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(790, 20)]
    [InlineData(20, 590)]
    [InlineData(790, 590)]
    public void PanelFitsViewportWithoutCoveringPointer(double x, double y)
    {
        var pointer = new Point(x, y);
        var panel = CaptureMagnifier.Place(pointer, new Size(800, 600));
        Assert.True(new Rect(0, 0, 800, 600).Contains(panel));
        Assert.False(panel.IntersectsWith(new Rect(x - 18, y - 18, 36, 36)));
    }

    [Fact]
    public void ScaledOverlayShowsOriginalPixelAndClearsWhenPointerLeavesMonitor() => Sta.Run(() =>
    {
        // 물리 800×600 화면을 150% DPI에 해당하는 WPF 좌표로 그린다.
        var pixels = new byte[800 * 600 * 4];
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i + 2] = 255; pixels[i + 3] = 255; }
        var center = (150 * 800 + 300) * 4;
        pixels[center + 1] = 255; pixels[center + 2] = 0;
        pixels[center - 4] = 255; pixels[center - 2] = 0;
        var source = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Bgra32, null, pixels, 800 * 4);
        var bounds = new PixelRect(-800, 0, 800, 600);
        var visual = new CapturePointerVisual(bounds, new CaptureFrame(source, bounds));
        visual.Measure(new Size(800 / 1.5, 400));
        visual.Arrange(new Rect(0, 0, 800 / 1.5, 400));
        visual.SetPointer(new(-500, 150));
        var rendered = Render(visual);
        // 커서 (200,100), 우하단 패널 (228,128), 이미지 시작 (232,132).
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(rendered, 235, 135));
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, Pixel(rendered, 300, 200));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(rendered, 292, 200));
        Assert.Equal(Pixel(rendered, 300, 200), Pixel(rendered, 301, 201)); // 보간 없이 한 원본 픽셀을 확대
        Assert.True(Pixel(rendered, 215, 100)[3] > 0); // 확대된 +의 오른쪽 팔
        Assert.False(visual.IsHitTestVisible);
        visual.SetPointer(new(0, 150));
        Assert.All(Render(visual), value => Assert.Equal((byte)0, value));
    });

    [Fact]
    public void MagnifierIncludesNeighborMonitorPixelsAcrossSeam() => Sta.Run(() =>
    {
        var pixels = new byte[800 * 600 * 4];
        for (var y = 0; y < 600; y++)
        for (var x = 0; x < 800; x++)
        {
            var offset = (y * 800 + x) * 4;
            pixels[offset + (x < 400 ? 2 : 1)] = 255;
            pixels[offset + 3] = 255;
        }
        var source = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Bgra32, null, pixels, 800 * 4);
        var visual = new CapturePointerVisual(new(-400, 0, 400, 600), new CaptureFrame(source, new(-400, 0, 800, 600)));
        visual.Measure(new Size(400, 600));
        visual.Arrange(new Rect(0, 0, 400, 600));
        visual.SetPointer(new(-1, 100));
        var rendered = Render(visual);
        // 오른쪽 경계에서 확대경은 왼쪽에 놓이지만, 샘플은 이웃 모니터까지 포함한다.
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(rendered, 299, 200));
        Assert.Equal(new byte[] { 0, 255, 0, 255 }, Pixel(rendered, 307, 200));
    });

    private static byte[] Render(FrameworkElement visual)
    {
        visual.UpdateLayout();
        var target = new RenderTargetBitmap(534, 400, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var pixels = new byte[534 * 400 * 4];
        target.CopyPixels(pixels, 534 * 4, 0);
        return pixels;
    }

    private static byte[] Pixel(byte[] pixels, int x, int y) => pixels.Skip((y * 534 + x) * 4).Take(4).ToArray();
}
