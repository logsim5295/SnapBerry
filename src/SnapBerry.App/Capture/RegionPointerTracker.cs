using System.Windows.Threading;
using SnapBerry.Core;

namespace SnapBerry.App.Capture;

internal sealed class RegionPointerTracker(Func<PixelPoint?> readCursor, Action<PixelPoint> moved) : IDisposable
{
    // 시작 메뉴 등 다른 창이 이동 메시지를 받더라도 선택 화면의 좌표는 계속 갱신한다.
    private readonly DispatcherTimer timer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
    private PixelPoint? last;

    public void Start()
    {
        timer.Tick += Tick;
        timer.Start();
        Update();
    }

    private void Tick(object? sender, EventArgs args) => Update();

    private void Update()
    {
        if (readCursor() is not { } point || point == last) return;
        last = point;
        moved(point);
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= Tick;
    }
}
