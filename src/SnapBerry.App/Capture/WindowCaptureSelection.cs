using SnapBerry.Core;

namespace SnapBerry.App.Capture;

internal sealed record CaptureWindow(nint Handle, PixelRect Bounds);

// UI 스레드에서만 사용하는 선택 상태. OS 조회와 분리하여 좌표·지연 응답을 검증한다.
internal sealed class WindowCaptureSelection(IReadOnlyList<CaptureWindow> windows, PixelRect desktop) : IDisposable
{
    private readonly List<PixelRect> path = [];
    private int index;
    private PixelRect? preferred;
    private bool disposed;
    public long Version { get; private set; }
    public PixelPoint? Pointer { get; private set; }
    public CaptureWindow? Window { get; private set; }
    public PixelRect? Current => path.Count == 0 ? null : path[index];

    public void Move(PixelPoint point)
    {
        if (disposed || Pointer == point) return;
        Pointer = point;
        Version++;
        Window = windows.FirstOrDefault(window => Contains(window.Bounds, point));
        preferred = null;
        path.Clear();
        index = 0;
        if (Window is not null && Clip(Window.Bounds, desktop) is { } bounds && Contains(bounds, point))
            path.Add(bounds);
    }

    public bool Apply(long version, IReadOnlyList<PixelRect> elements)
    {
        if (disposed || version != Version || path.Count == 0 || Pointer is not { } point) return false;
        var parent = path[0];
        path.Clear();
        path.Add(parent);
        foreach (var element in elements)
        {
            if (Clip(element, parent) is not { } bounds || !Contains(bounds, point) || bounds == parent) continue;
            path.Add(bounds);
            parent = bounds;
        }
        if (preferred is { } chosen)
        {
            // UIA 응답은 계속 받아 하위 영역을 채우되, 이미 휠로 고른 경계는 유지한다.
            // 네이티브 컨트롤이 UIA 트리에 없으면 양쪽에서 중첩되는 경계만 병합한다.
            path.RemoveAll(rect => Clip(rect, chosen) != rect && Clip(chosen, rect) != chosen);
            if (!path.Contains(chosen)) path.Add(chosen);
            path.Sort((a, b) => ((long)b.Width * b.Height).CompareTo((long)a.Width * a.Height));
            index = path.IndexOf(chosen);
        }
        else index = path.Count - 1;
        return true;
    }

    public void Step(int direction)
    {
        if (disposed || path.Count == 0 || direction == 0) return;
        if (direction < 0 && index == path.Count - 1) { preferred = null; return; }
        index = Math.Clamp(index - Math.Sign(direction), 0, path.Count - 1);
        preferred = path[index];
    }

    public void Dispose()
    {
        disposed = true;
        Version++;
        path.Clear();
        Window = null;
    }

    internal static bool Contains(PixelRect rect, PixelPoint point) => rect.Width > 0 && rect.Height > 0 &&
        point.X >= rect.X && point.Y >= rect.Y && point.X < (long)rect.X + rect.Width && point.Y < (long)rect.Y + rect.Height;

    internal static PixelRect? Clip(PixelRect rect, PixelRect parent)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || parent.Width <= 0 || parent.Height <= 0) return null;
        var x = Math.Max(rect.X, parent.X);
        var y = Math.Max(rect.Y, parent.Y);
        var right = Math.Min((long)rect.X + rect.Width, (long)parent.X + parent.Width);
        var bottom = Math.Min((long)rect.Y + rect.Height, (long)parent.Y + parent.Height);
        return right > x && bottom > y ? new(x, y, (int)(right - x), (int)(bottom - y)) : null;
    }
}
