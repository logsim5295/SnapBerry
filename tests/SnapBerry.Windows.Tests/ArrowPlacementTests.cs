using System.Windows;
using System.Windows.Input;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class ArrowPlacementTests
{
    private static EditorCanvas OpenCanvas()
    {
        var model = new EditorViewModel { Tool = EditorTool.Arrow };
        model.Open(new() { Width = 100, Height = 100, OriginalPng = PngFileWriter.Encode(RenderingTests.Image(100, 100)) });
        var canvas = new EditorCanvas { Model = model };
        canvas.Refresh();
        return canvas;
    }

    [Fact]
    public void FirstClickWithinDragToleranceWaitsForEndpoint() => Sta.Run(() =>
    {
        var canvas = OpenCanvas();
        canvas.BeginShape(new(10, 20));
        canvas.FinishShape(new(10 + SystemParameters.MinimumHorizontalDragDistance, 20 + SystemParameters.MinimumVerticalDragDistance));
        Assert.Empty(canvas.Model.Document!.Annotations);

        // 첫 클릭 뒤 정상적으로 캡처를 놓아도 두 번째 클릭은 끝점을 지정한다.
        canvas.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        canvas.BeginShape(new(40, 70));
        canvas.FinishShape(new(40, 70));
        var arrow = Assert.Single(canvas.Model.Document.Annotations);
        Assert.Equal((10d, 20d, 30d, 50d), (arrow.X, arrow.Y, arrow.Width, arrow.Height));
    });

    [Fact]
    public void LostCaptureDuringPressDiscardsArrowBeforeLateRelease() => Sta.Run(() =>
    {
        var canvas = OpenCanvas();
        canvas.BeginShape(new(10, 20));
        canvas.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        canvas.FinishShape(new(16, 20));
        Assert.Empty(canvas.Model.Document!.Annotations);
        Assert.Null(canvas.ShapePreview);

        canvas.BeginShape(new(70, 60));
        canvas.FinishShape(new(30, 10));
        var arrow = Assert.Single(canvas.Model.Document.Annotations);
        Assert.Equal((70d, 60d, -40d, -50d), (arrow.X, arrow.Y, arrow.Width, arrow.Height));
    });
}
