using System.Runtime.InteropServices;
using Accessibility;
using SnapBerry.Core;

namespace SnapBerry.App.Capture;

internal interface IWindowHitTestNode
{
    PixelRect? Bounds { get; }
    nint WindowHandle { get; }
    bool IsWindowRoot { get; }
    bool IsInvisible { get; }
    bool IsAtomic { get; }
    IWindowHitTestNode? Parent { get; }
    IWindowHitTestNode? HitTest(PixelPoint point);
}

// Gecko 등의 합성 창은 UIA의 첫 자식이 전체 화면 크기인 경우가 있다.
// 원래 HWND의 접근성 객체에 직접 hit-test하여 실제 표시된 컨트롤과 부모를 얻는다.
internal static class AccessibleWindowProbe
{
    internal static IReadOnlyList<PixelRect> Find(CaptureWindow window, PixelPoint point)
    {
        var iid = typeof(IAccessible).GUID;
        if (AccessibleObjectFromWindow(window.Handle, unchecked((uint)-4), ref iid, out var accessible) < 0 || accessible is null)
            return [];
        return FindPath(new AccessibleNode(accessible), window.Bounds, point);
    }

    internal static IReadOnlyList<PixelRect> FindPath(IWindowHitTestNode root, PixelRect bounds, PixelPoint point)
    {
        var hit = root;
        var owner = root.WindowHandle;
        if (owner == 0) return [];
        var stable = false;
        for (var depth = 0; depth < 32; depth++)
        {
            var next = hit.HitTest(point);
            if (next is null) return [];
            if (next.Equals(hit)) { stable = true; break; }
            hit = next;
        }
        if (!stable || hit.Equals(root)) return [];

        var path = new List<PixelRect>();
        for (var depth = 0; depth < 64; depth++)
        {
            if (hit.WindowHandle != owner || hit.IsInvisible) return [];
            if (hit.Bounds is { } rectangle && WindowCaptureSelection.Clip(rectangle, bounds) is { } clipped &&
                WindowCaptureSelection.Contains(clipped, point))
            {
                // 버튼 안의 라벨 등은 부모 컨트롤 전체를 선택한다.
                if (hit.IsAtomic) path.Clear();
                if (path.Count == 0 || path[^1] != clipped) path.Add(clipped);
            }
            // Gecko가 같은 객체를 서로 다른 COM 프록시로 돌려주므로 RCW 동일성만으로
            // 루트 도착을 판정하지 않는다. 원래 HWND의 window 역할에서 멈춘다.
            if (hit.Equals(root) || hit.IsWindowRoot)
            {
                if (path.Count < 2) return []; // 일반 클라이언트 영역만 찾았다면 UIA로 세부 요소를 탐색한다.
                path.Reverse();
                return path;
            }
            if (hit.Parent is not { } parent) return [];
            hit = parent;
        }
        return []; // 잘못된 순환이나 대상 창 밖으로 연결되는 부모는 선택하지 않는다.
    }

    internal static bool IsInvisibleState(int state) => (state & 0x8000) != 0;

    private sealed record AccessibleNode(IAccessible Accessible, int ChildId = 0) : IWindowHitTestNode
    {
        public nint WindowHandle => WindowFromAccessibleObject(Accessible, out var handle) >= 0 ? handle : 0;
        public bool IsWindowRoot => ChildId == 0 && Accessible.get_accRole(0) is int role && role == 9;
        public PixelRect? Bounds
        {
            get
            {
                Accessible.accLocation(out var x, out var y, out var width, out var height, ChildId);
                return width > 0 && height > 0 ? new(x, y, width, height) : null;
            }
        }
        // STATE_SYSTEM_OFFSCREEN는 가림에도 설정된다. root hit-test에 성공한 요소는
        // 명시적인 STATE_SYSTEM_INVISIBLE일 때만 제외한다. 숨겨진 탭/메뉴는 hit-test가 걸러낸다.
        public bool IsInvisible => Accessible.get_accState(ChildId) is int state && IsInvisibleState(state);
        public bool IsAtomic => Accessible.get_accRole(ChildId) is int role &&
            role is 0x1E or 0x2A or 0x2B or 0x2C or 0x2D or 0x2E or 0x2F or 0x33 or 0x34;
        public IWindowHitTestNode? Parent => ChildId != 0 ? new AccessibleNode(Accessible) :
            Accessible.accParent is IAccessible parent ? new AccessibleNode(parent) : null;
        public IWindowHitTestNode? HitTest(PixelPoint point)
        {
            if (ChildId != 0) return this;
            return Accessible.accHitTest((int)point.X, (int)point.Y) switch
            {
                IAccessible target => new AccessibleNode(target),
                int id when id == 0 => this,
                int id when id > 0 => Accessible.get_accChild(id) is IAccessible child
                    ? new AccessibleNode(child) : new AccessibleNode(Accessible, id),
                _ => null
            };
        }
    }

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(nint handle, uint objectId, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IAccessible? accessible);

    [DllImport("oleacc.dll")]
    private static extern int WindowFromAccessibleObject(IAccessible accessible, out nint handle);
}
