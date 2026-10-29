using System.Windows;
using System.Windows.Controls;
using SnapBerry.App.Capture;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창을 표시하지 않고 실제 오버레이 콘텐츠의 테두리 중심선을 검증한다.
public class CaptureOverlayAlignmentTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(1.25, false)]
    [InlineData(1.5, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(1.5, true)]
    [InlineData(2, true)]
    public void SelectionStrokeCornersMatchPointerCoordinates(double scale, bool reverse) => Sta.Run(() =>
    {
        var bounds = new PixelRect(-800, 0, 800, 600);
        var window = new CaptureOverlay(bounds, RenderingTests.Image(800, 600));
        try
        {
            var root = Assert.IsType<Grid>(window.Content);
            root.Measure(new Size(800 / scale, 600 / scale));
            root.Arrange(new Rect(0, 0, 800 / scale, 600 / scale));
            root.UpdateLayout();
            var pointer = Assert.Single(root.Children.OfType<CapturePointerVisual>());
            var canvas = Assert.Single(root.Children.OfType<Canvas>());
            var selection = Assert.Single(canvas.Children.OfType<Border>());
            var start = new PixelPoint(-700, 100);
            var end = new PixelPoint(-200, 400);
            window.SetPointer(reverse ? start : end);
            window.SetSelection(reverse ? PixelRect.FromPoints(end, start) : PixelRect.FromPoints(start, end));
            root.UpdateLayout();
            Assert.Equal(800 / scale, pointer.ActualWidth, 6);
            Assert.Equal(600 / scale, pointer.ActualHeight, 6);
            var topLeft = selection.TranslatePoint(new Point(selection.BorderThickness.Left / 2, selection.BorderThickness.Top / 2), pointer);
            var bottomRight = selection.TranslatePoint(new Point(selection.ActualWidth - selection.BorderThickness.Right / 2,
                selection.ActualHeight - selection.BorderThickness.Bottom / 2), pointer);
            Assert.Equal(100 / scale, topLeft.X, 6);
            Assert.Equal(100 / scale, topLeft.Y, 6);
            Assert.Equal(600 / scale, bottomRight.X, 6);
            Assert.Equal(400 / scale, bottomRight.Y, 6);
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    });
}
