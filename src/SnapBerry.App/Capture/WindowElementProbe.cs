using System.Runtime.InteropServices;
using System.Windows.Automation;
using SnapBerry.App.Interop;
using SnapBerry.Core;

namespace SnapBerry.App.Capture;

internal sealed record WindowElementQuery(IReadOnlyList<PixelRect> Path, bool Retry = false);

internal interface IWindowElementNode
{
    PixelRect? Bounds { get; }
    bool IsHidden { get; }
    bool IsAtomic { get; }
    IEnumerable<IWindowElementNode> Children { get; }
}

// 모든 캡처 세션이 하나의 탐색기를 공유한다. 응답 없는 외부 앱에 작업이 계속 쌓이지 않는다.
internal sealed class WindowElementProbe(
    Func<CaptureWindow, PixelPoint, IReadOnlyList<PixelRect>> probe,
    TimeSpan? timeout = null)
{
    public static WindowElementProbe Shared { get; } = new(FindElementPath);
    private readonly object gate = new();
    private Task<IReadOnlyList<PixelRect>>? pending;
    private CaptureWindow? pendingWindow;
    private PixelPoint pendingPoint;
    private object? pendingSession;
    public bool IsBusy { get { lock (gate) return pending is { IsCompleted: false }; } }

    public async Task<WindowElementQuery> QueryAsync(CaptureWindow window, PixelPoint point, IReadOnlyList<PixelRect> fallback, object? session = null)
    {
        Task<IReadOnlyList<PixelRect>> work;
        lock (gate)
        {
            if (pending is not null && ReferenceEquals(pendingSession, session) && pendingWindow == window && pendingPoint == point)
                work = pending;
            else
            {
                if (pending is { IsCompleted: false }) return new(fallback, Retry: true);
                // 시간 초과 후에도 작업과 결과를 보존한다. 다음 tick은 같은 느린 조회를
                // 처음부터 반복하지 않고 완료된 결과를 받아 선택 영역에 반영한다.
                pendingWindow = window;
                pendingPoint = point;
                pendingSession = session;
                work = pending = Task.Run(() =>
                {
                    try { return probe(window, point); }
                    catch (Exception error) when (IsProviderFailure(error)) { return fallback; }
                });
            }
        }
        try
        {
            var result = await work.WaitAsync(timeout ?? TimeSpan.FromMilliseconds(250));
            lock (gate) { if (ReferenceEquals(pending, work)) { pending = null; pendingSession = null; } }
            if (result.Count == 0) return new(fallback);
            // UIA가 큰 컨테이너만 노출하는 앱에서는 더 구체적인 네이티브 컨트롤을 유지한다.
            if (fallback.Count > 0 && Area(fallback[^1]) < Area(result[^1])) return new(fallback);
            return new(result);
        }
        catch (TimeoutException) { return new(fallback, Retry: true); }
    }

    private static long Area(PixelRect rect) => (long)rect.Width * rect.Height;
    private static bool IsProviderFailure(Exception error) => error is
        InvalidOperationException or ElementNotAvailableException or ElementNotEnabledException or
        COMException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException or NotSupportedException;

    private static IReadOnlyList<PixelRect> FindElementPath(CaptureWindow window, PixelPoint point)
    {
        try
        {
            var hitPath = AccessibleWindowProbe.Find(window, point);
            if (hitPath.Count > 0) return hitPath;
        }
        catch (Exception error) when (IsProviderFailure(error)) { }
        return FindAutomationPath(window, point);
    }

    private static IReadOnlyList<PixelRect> FindAutomationPath(CaptureWindow window, PixelPoint point)
    {
        // FromPoint는 캡처 오버레이를 찾으므로, 저장한 원본 창의 하위 트리만 탐색한다.
        var root = AutomationElement.FromHandle(window.Handle);
        return root is null ? [] : FindPath(new AutomationNode(root), window.Bounds, point);
    }

    internal static IReadOnlyList<PixelRect> FindPath(IWindowElementNode root, PixelRect bounds, PixelPoint point)
    {
        var path = new List<PixelRect>();
        var remaining = 512;
        var node = root;
        // 시간 제한은 비동기 호출부에서 UI 대기에만 적용한다. 외부 앱의 첫 응답이
        // 느리다는 이유로 탐색 결과를 빈 경로로 확정하지 않는다.
        for (var depth = 0; depth < 32 && remaining > 0; depth++)
        {
            IWindowElementNode? match = null;
            try
            {
                foreach (var child in node.Children)
                {
                    if (--remaining < 0) break;
                    try
                    {
                        if (child.IsHidden || child.Bounds is not { } rectangle ||
                            WindowCaptureSelection.Clip(rectangle, bounds) is not { } clipped ||
                            !WindowCaptureSelection.Contains(clipped, point)) continue;
                        match = child;
                        if (clipped != bounds) path.Add(clipped);
                        bounds = clipped;
                        break;
                    }
                    catch (Exception error) when (IsProviderFailure(error)) { }
                }
                if (match is null || match.IsAtomic) break;
                node = match;
            }
            catch (Exception error) when (IsProviderFailure(error)) { break; }
        }
        return path;
    }

    internal static IReadOnlyList<PixelRect> FindNativePath(CaptureWindow window, PixelPoint point)
    {
        var result = new List<PixelRect>();
        var parent = window.Handle;
        var bounds = window.Bounds;
        var remaining = 512;
        for (var depth = 0; depth < 32 && remaining > 0; depth++)
        {
            nint match = 0;
            // GW_CHILD / GW_HWNDNEXT는 Z 순서이며, 대상 앱에 메시지를 보내지 않는다.
            for (var child = NativeMethods.GetWindow(parent, 5); child != 0 && --remaining >= 0; child = NativeMethods.GetWindow(child, 2))
            {
                if (!NativeMethods.IsWindowVisible(child) || !NativeMethods.GetWindowRect(child, out var rect)) continue;
                var rectangle = new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                if (WindowCaptureSelection.Clip(rectangle, bounds) is not { } clipped ||
                    !WindowCaptureSelection.Contains(clipped, point)) continue;
                match = child;
                if (clipped != bounds) result.Add(clipped);
                bounds = clipped;
                break;
            }
            if (match == 0) break;
            parent = match;
        }
        return result;
    }

    private sealed class AutomationNode(AutomationElement element) : IWindowElementNode
    {
        private AutomationElement.AutomationElementInformation? information;
        private AutomationElement.AutomationElementInformation Info => information ??= element.Current;
        public bool IsHidden => Info.IsOffscreen;
        public bool IsAtomic => Info.ControlType is var type &&
            (type == ControlType.Button || type == ControlType.CheckBox || type == ControlType.RadioButton ||
             type == ControlType.Edit || type == ControlType.ComboBox || type == ControlType.Slider ||
             type == ControlType.Spinner || type == ControlType.ScrollBar || type == ControlType.Hyperlink);
        public PixelRect? Bounds
        {
            get
            {
                var rect = Info.BoundingRectangle;
                var left = Math.Floor(rect.Left);
                var top = Math.Floor(rect.Top);
                var right = Math.Ceiling(rect.Right);
                var bottom = Math.Ceiling(rect.Bottom);
                if (rect.IsEmpty || !double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom) ||
                    left < int.MinValue || top < int.MinValue || right > int.MaxValue || bottom > int.MaxValue ||
                    right <= left || bottom <= top || right - left > int.MaxValue || bottom - top > int.MaxValue) return null;
                return new((int)left, (int)top, (int)(right - left), (int)(bottom - top));
            }
        }
        public IEnumerable<IWindowElementNode> Children
        {
            get
            {
                var walker = TreeWalker.ControlViewWalker;
                for (var child = walker.GetFirstChild(element); child is not null; child = walker.GetNextSibling(child))
                    yield return new AutomationNode(child);
            }
        }
    }
}
