using System.Windows.Threading;
using SnapBerry.App.Capture;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class RegionPointerTrackerTests
{
    [Fact]
    public void TracksLatestPositionWithoutWindowInputAndStopsAfterSelection() => Sta.Run(() =>
    {
        var origin = new PixelPoint(-120, 300);
        var latest = new PixelPoint(-30, 470);
        PixelPoint? cursor = origin;
        var points = new List<PixelPoint>();
        var reads = 0;
        using var tracker = new RegionPointerTracker(() => { reads++; return cursor; }, points.Add);
        tracker.Start();
        Assert.Equal(new[] { origin }, points);

        cursor = new PixelPoint(-80, 400);
        cursor = latest; // 두 이동 사이에는 WPF MouseMove를 전달하지 않는다.
        PumpUntil(() => points.Contains(latest));
        var atMove = reads;
        PumpUntil(() => reads >= atMove + 2);
        cursor = null; // 좌표를 읽지 못한 동안은 마지막 위치 유지
        PumpUntil(() => reads >= atMove + 4);
        Assert.Equal(new[] { origin, latest }, points);

        tracker.Dispose();
        var atStop = reads;
        cursor = origin;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        PumpUntil(() => watch.ElapsedMilliseconds >= 60);
        Assert.Equal(atStop, reads);
    });

    private static void PumpUntil(Func<bool> done)
    {
        var frame = new DispatcherFrame();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        timer.Tick += (_, _) => { if (done() || watch.Elapsed > TimeSpan.FromSeconds(2)) frame.Continue = false; };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.True(done(), "창 입력 없이도 영역 캡처 좌표를 갱신해야 합니다.");
    }
}
