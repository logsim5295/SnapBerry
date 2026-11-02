using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using SkiaSharp;
using SnapBerry.Desktop.Capture;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class RegionGuideTests
{
    [AvaloniaFact]
    public async Task GuidesSpanBothDisplaysOnEntryAndFollowTheDragEndpoint()
    {
        var windows = new List<Window>();
        using var cancellation = new CancellationTokenSource();
        var selection = new MacRegionPicker(new(-20, 28)).SelectAsync(
            [new(1, -100, 0, 100, 80, 1), new(2, 0, 0, 120, 80, 2)], windows.Add, cancellation.Token);
        try
        {
            // Both windows and all input are provided by Avalonia.Headless.
            var left = windows[0];
            var right = windows[1];
            AssertGuides(left, vertical: 80, horizontal: 28);
            AssertGuides(right, vertical: null, horizontal: 28);
            left.MouseMove(new(44, 36));
            AssertGuides(left, vertical: 44, horizontal: 36);
            left.MouseDown(new(44, 36), MouseButton.Left);
            left.MouseMove(new(125, 10), RawInputModifiers.LeftMouseButton);
            AssertGuides(right, vertical: 25, horizontal: 10);
            left.MouseUp(new(125, 10), MouseButton.Left);
            Assert.Equal(new Rect(-56, 10, 81, 26),
                await selection.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
            Assert.All(windows, window => Assert.False(window.IsVisible));
        }
        finally { cancellation.Cancel(); }
    }

    private static void AssertGuides(Window window, int? vertical, int horizontal)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        using var bitmap = SKBitmap.Decode(stream.ToArray());
        static bool IsGuide(SKColor color) => color.Red > color.Green + 25 && color.Green == color.Blue;
        foreach (var x in new[] { 2, bitmap.Width - 3 })
            Assert.True(IsGuide(bitmap.GetPixel(x, horizontal)) || IsGuide(bitmap.GetPixel(x, horizontal - 1)));
        if (vertical is int column)
            foreach (var y in new[] { 2, bitmap.Height - 3 })
                Assert.True(IsGuide(bitmap.GetPixel(column, y)) || IsGuide(bitmap.GetPixel(column - 1, y)));
    }

    [AvaloniaFact]
    public async Task EscapeClosesAllGuideWindowsWithoutASelection()
    {
        var windows = new List<Window>();
        var selection = new MacRegionPicker(new(20, 30)).SelectAsync(
            [new(1, 0, 0, 100, 80, 1)], windows.Add, TestContext.Current.CancellationToken);
        windows[0].KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Null(await selection.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        Assert.False(windows[0].IsVisible);
    }

    [Fact]
    public async Task DefaultRegionCaptureUsesTheGuidedSelectionInGlobalPoints()
    {
        var png = EditorTests.Png();
        var service = new MacCaptureService(async (start, token) =>
        {
            Assert.DoesNotContain("-i", start.ArgumentList);
            var index = start.ArgumentList.IndexOf("-R");
            Assert.True(index >= 0);
            Assert.Equal("-120,64,200,80", start.ArgumentList[index + 1]);
            await File.WriteAllBytesAsync(start.ArgumentList.Last(), png, token);
            return new(0, "");
        }, regionPicker: _ => Task.FromResult<Rect?>(new(-120, 64, 200, 80)));

        Assert.Equal(png, await service.CaptureAsync(DesktopCaptureMode.Region, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellingTheGuidedSelectionDoesNotRunScreenCapture()
    {
        var service = new MacCaptureService((_, _) => throw new InvalidOperationException(),
            regionPicker: _ => Task.FromResult<Rect?>(null));
        Assert.Null(await service.CaptureAsync(DesktopCaptureMode.Region, TestContext.Current.CancellationToken));
    }
}
