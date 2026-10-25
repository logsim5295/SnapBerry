using SnapBerry.App.Capture;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class WindowCaptureSelectionTests
{
    private static readonly CaptureWindow Window = new(1, new(0, 0, 500, 400));

    [Fact]
    public void InitialPointerSelectsFrontWindowWithoutWaitingForMovement()
    {
        var selection = new WindowCaptureSelection([Window, new(2, new(0, 0, 900, 900))], new(0, 0, 1000, 1000));
        selection.Move(new(70, 80));
        Assert.Equal(Window, selection.Window);
        Assert.Equal(Window.Bounds, selection.Current);
    }

    [Fact]
    public void HoverChoosesButtonAndWheelCanSelectPanelAndWholeWindow()
    {
        var selection = Create();
        selection.Apply(selection.Version, [new(20, 20, 400, 300), new(50, 60, 100, 40)]);
        Assert.Equal(new PixelRect(50, 60, 100, 40), selection.Current);
        selection.Step(1);
        Assert.Equal(new PixelRect(20, 20, 400, 300), selection.Current);
        selection.Step(1);
        selection.Step(1);
        Assert.Equal(Window.Bounds, selection.Current);
        selection.Move(new(70, 80)); // 클릭 시 같은 위치를 갱신해도 휠 선택을 유지한다.
        Assert.Equal(Window.Bounds, selection.Current);
        selection.Step(-1);
        Assert.Equal(new PixelRect(20, 20, 400, 300), selection.Current);
    }

    [Fact]
    public void LateResultsCannotReplaceNewPointerOrManualParentSelection()
    {
        var selection = Create();
        var old = selection.Version;
        selection.Move(new(200, 200));
        Assert.False(selection.Apply(old, [new(50, 60, 100, 40)]));
        var pending = selection.Version;
        selection.Step(1);
        Assert.True(selection.Apply(pending, [new(190, 190, 50, 50)]));
        Assert.Equal(Window.Bounds, selection.Current);
        selection.Step(-1);
        Assert.Equal(new PixelRect(190, 190, 50, 50), selection.Current);
    }

    [Fact]
    public void WheelBeforeDiscoveryKeepsChosenWindowButStillLoadsChildHierarchy()
    {
        var selection = Create();
        var pending = selection.Version;
        selection.Step(1);
        selection.Apply(pending, [new(20, 20, 400, 300), new(50, 60, 100, 40)]);
        Assert.Equal(Window.Bounds, selection.Current);
        selection.Step(-1);
        Assert.Equal(new PixelRect(20, 20, 400, 300), selection.Current);
        selection.Step(-1);
        Assert.Equal(new PixelRect(50, 60, 100, 40), selection.Current);
    }

    [Fact]
    public void WheelDownBeforeDiscoveryStillSelectsTheDiscoveredButton()
    {
        var selection = Create();
        var pending = selection.Version;
        selection.Step(-1);
        selection.Apply(pending, [new(50, 60, 100, 40)]);
        Assert.Equal(new PixelRect(50, 60, 100, 40), selection.Current);
    }

    [Fact]
    public void InvalidAndUnrelatedBoundsAreIgnoredAndChildrenAreClippedToParents()
    {
        var selection = Create();
        selection.Apply(selection.Version, [new(20, 20, 200, 200), new(0, 0, 100, 100), new(1, 1, 0, 0), new(300, 300, 10, 10)]);
        Assert.Equal(new PixelRect(20, 20, 80, 80), selection.Current);
    }

    [Fact]
    public void NegativeMonitorCoordinatesAreClippedToCapturedDesktop()
    {
        var selection = new WindowCaptureSelection([new(1, new(-600, -100, 500, 500))], new(-500, 0, 1000, 600));
        selection.Move(new(-450, 40));
        Assert.Equal(new PixelRect(-500, 0, 400, 400), selection.Current);
        selection.Apply(selection.Version, [new(-550, -50, 200, 150)]);
        Assert.Equal(new PixelRect(-500, 0, 150, 100), selection.Current);
    }

    [Fact]
    public void LeavingWindowsOrEndingSessionInvalidatesPendingResults()
    {
        var selection = Create();
        var pending = selection.Version;
        selection.Move(new(500, 400)); // 오른쪽/아래쪽 끝은 영역 밖이다.
        Assert.Null(selection.Current);
        Assert.False(selection.Apply(pending, [new(50, 60, 100, 40)]));
        selection.Move(new(70, 80));
        pending = selection.Version;
        selection.Dispose();
        Assert.False(selection.Apply(pending, [new(50, 60, 100, 40)]));
        Assert.Null(selection.Current);
    }

    private static WindowCaptureSelection Create()
    {
        var selection = new WindowCaptureSelection([Window], new(0, 0, 1000, 1000));
        selection.Move(new(70, 80));
        return selection;
    }
}
