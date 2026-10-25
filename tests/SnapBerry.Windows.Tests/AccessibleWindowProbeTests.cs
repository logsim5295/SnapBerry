using SnapBerry.App.Capture;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class AccessibleWindowProbeTests
{
    private static readonly PixelRect Window = new(0, 0, 500, 400);

    [Fact]
    public void WindowRootHitTestFindsRealButtonWithoutWalkingCompositorSiblings()
    {
        var root = new Node(Window);
        var panel = new Node(new(20, 20, 400, 300)) { Parent = root };
        var button = new Node(new(50, 60, 100, 40)) { Parent = panel, IsAtomic = true };
        root.Hit = button;
        Assert.Equal(new PixelRect[] { Window, new(20, 20, 400, 300), new(50, 60, 100, 40) },
            AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
    }

    [Theory]
    [InlineData(0x10000, false)] // offscreen: 다른 창/캡처 오버레이에 가려져도 root hit-test 결과는 유효하다.
    [InlineData(0x110000, false)] // Thunderbird 버튼: offscreen + focusable.
    [InlineData(0x8000, true)] // invisible: 실제로 숨겨진 컨트롤은 제외한다.
    [InlineData(0x18000, true)]
    public void OcclusionDoesNotMakeAHitTestedControlInvisible(int state, bool invisible)
        => Assert.Equal(invisible, AccessibleWindowProbe.IsInvisibleState(state));

    [Fact]
    public void TextInsideButtonSelectsWholeButtonAndRetainsParentPanel()
    {
        var root = new Node(Window);
        var panel = new Node(new(20, 20, 400, 300)) { Parent = root };
        var button = new Node(new(50, 60, 100, 40)) { Parent = panel, IsAtomic = true };
        var text = new Node(new(60, 70, 70, 20)) { Parent = button };
        root.Hit = text;
        Assert.Equal(new PixelRect[] { Window, new(20, 20, 400, 300), new(50, 60, 100, 40) },
            AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
    }

    [Fact]
    public void GenericRootOrUnrelatedObjectDoesNotPreventUiaFallback()
    {
        var root = new Node(Window);
        Assert.Empty(AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
        root.Hit = new Node(new(50, 60, 100, 40));
        Assert.Empty(AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
    }

    [Fact]
    public void DifferentComProxiesForTheSameWindowStillTerminateAtOwningWindow()
    {
        var root = new Node(Window);
        var providerRoot = new Node(Window) { IsWindowRoot = true };
        root.Hit = new Node(new(50, 60, 100, 40)) { Parent = providerRoot, IsAtomic = true };
        Assert.Equal(new PixelRect[] { Window, new(50, 60, 100, 40) },
            AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
    }

    [Fact]
    public void HitTestCannotFollowParentsIntoAnotherWindow()
    {
        var root = new Node(Window);
        root.Hit = new Node(new(50, 60, 100, 40)) { Parent = new Node(Window) { IsWindowRoot = true, WindowHandle = 2 } };
        Assert.Empty(AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
    }

    [Fact]
    public void InvisibleHitAndCyclicProviderDoNotProduceCaptureCandidates()
    {
        var root = new Node(Window);
        var child = new Node(new(50, 60, 100, 40)) { Parent = root, IsInvisible = true };
        root.Hit = child;
        Assert.Empty(AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
        child.IsInvisible = false;
        child.Parent = child;
        Assert.Empty(AccessibleWindowProbe.FindPath(root, Window, new(70, 80)));
    }

    private sealed class Node(PixelRect bounds) : IWindowHitTestNode
    {
        public PixelRect? Bounds => bounds;
        public nint WindowHandle { get; init; } = 1;
        public bool IsWindowRoot { get; init; }
        public bool IsAtomic { get; init; }
        public bool IsInvisible { get; set; }
        public IWindowHitTestNode? Parent { get; set; }
        public IWindowHitTestNode? Hit { get; set; }
        public IWindowHitTestNode? HitTest(PixelPoint point) => Hit ?? this;
    }
}
