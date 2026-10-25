using SnapBerry.App.Capture;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class WindowElementProbeTests
{
    private static readonly PixelRect Window = new(0, 0, 500, 400);

    [Fact]
    public void TraversalFindsPanelAndButtonButDoesNotSelectButtonLabel()
    {
        var button = new Node(new(50, 60, 100, 40), atomic: true, children: [new(new(60, 65, 60, 20))]);
        var root = new Node(Window, children: [new(new(20, 20, 400, 300), children: [button])]);
        Assert.Equal(new PixelRect[] { new(20, 20, 400, 300), new(50, 60, 100, 40) },
            WindowElementProbe.FindPath(root, Window, new(70, 80)));
    }

    [Fact]
    public void HiddenAndNonContainingSiblingsDoNotHideTheFirstVisibleMatch()
    {
        var root = new Node(Window, children: [new(new(0, 0, 100, 100), hidden: true),
            new(new(200, 200, 100, 100)), new(new(50, 60, 100, 40)), new(new(60, 70, 20, 20))]);
        Assert.Equal(new PixelRect[] { new(50, 60, 100, 40) }, WindowElementProbe.FindPath(root, Window, new(70, 80)));
    }

    [Fact]
    public void CyclicProviderTreeHasBoundedTraversal()
    {
        var root = new Node(Window);
        root.Items = [root];
        Assert.Empty(WindowElementProbe.FindPath(root, Window, new(70, 80)));
        Assert.InRange(root.Reads, 1, 64);
    }

    [Fact]
    public void SlowFirstProviderResponseStillFindsButtonInsteadOfPermanentlyReturningWindow()
    {
        var root = new DelayedNode(new Node(Window, children: [new(new(50, 60, 100, 40), atomic: true)]));
        Assert.Equal(new PixelRect[] { new(50, 60, 100, 40) },
            WindowElementProbe.FindPath(root, Window, new(70, 80)));
    }

    [Fact]
    public async Task CompletedSlowLookupIsConsumedOnRetryWithoutStartingTheSameLookupAgain()
    {
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var probe = new WindowElementProbe((_, _) =>
        {
            Interlocked.Increment(ref calls);
            release.Wait();
            return [new(50, 60, 100, 40)];
        }, TimeSpan.FromMilliseconds(30));
        try
        {
            Assert.True((await probe.QueryAsync(new(1, Window), new(70, 80), [])).Retry);
            release.Set();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (probe.IsBusy) await Task.Delay(5, deadline.Token);
            var result = await probe.QueryAsync(new(1, Window), new(70, 80), []);
            Assert.False(result.Retry);
            Assert.Equal(new PixelRect(50, 60, 100, 40), Assert.Single(result.Path));
            Assert.Equal(1, calls);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task NewCaptureDoesNotConsumeTheCancelledSessionsCompletedLookup()
    {
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var probe = new WindowElementProbe((_, _) =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1) release.Wait();
            return call == 1 ? [new(50, 60, 100, 40)] : [new(60, 70, 80, 30)];
        }, TimeSpan.FromMilliseconds(30));
        try
        {
            Assert.True((await probe.QueryAsync(new(1, Window), new(70, 80), [], new object())).Retry);
            release.Set();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (probe.IsBusy) await Task.Delay(5, deadline.Token);
            var result = await probe.QueryAsync(new(1, Window), new(70, 80), [], new object());
            Assert.Equal(new PixelRect(60, 70, 80, 30), Assert.Single(result.Path));
            Assert.Equal(2, calls);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task ProviderFailureKeepsNativeFallback()
    {
        var probe = new WindowElementProbe((_, _) => throw new InvalidOperationException("closed window"));
        PixelRect[] fallback = [new(20, 20, 200, 200)];
        Assert.Equal(fallback, (await probe.QueryAsync(new(1, Window), new(70, 80), fallback)).Path);
    }

    [Fact]
    public async Task SlowProviderTimesOutWithoutQueuingAnotherWorker()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var probe = new WindowElementProbe((_, _) =>
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            try { release.Wait(); return [new(50, 60, 100, 40)]; }
            finally { exited.SetResult(); }
        }, TimeSpan.FromMilliseconds(50));
        PixelRect[] fallback = [new(20, 20, 200, 200)];
        try
        {
            var first = probe.QueryAsync(new(1, Window), new(70, 80), fallback);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var timedOut = await first.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(fallback, timedOut.Path);
            Assert.True(timedOut.Retry);
            var deferred = await probe.QueryAsync(new(1, Window), new(75, 80), fallback);
            Assert.Equal(fallback, deferred.Path);
            Assert.True(deferred.Retry);
            Assert.Equal(1, calls);
        }
        finally { release.Set(); await exited.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    [Fact]
    public async Task EmptyAutomationPathKeepsNativeChildSelection()
    {
        var probe = new WindowElementProbe((_, _) => []);
        PixelRect[] fallback = [new(20, 20, 200, 200)];
        Assert.Equal(fallback, (await probe.QueryAsync(new(1, Window), new(70, 80), fallback)).Path);
    }

    [Fact]
    public async Task DeferredLocationCanBeRetriedAfterTimedOutWorkerFinishes()
    {
        using var release = new ManualResetEventSlim();
        var probe = new WindowElementProbe((window, _) =>
        {
            if (window.Handle == 1) release.Wait();
            return [new(50, 60, 100, 40)];
        }, TimeSpan.FromMilliseconds(30));
        try
        {
            Assert.True((await probe.QueryAsync(new(1, Window), new(70, 80), [])).Retry);
            Assert.True((await probe.QueryAsync(new(2, Window), new(70, 80), [])).Retry);
            release.Set();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            WindowElementQuery result;
            do
            {
                await Task.Delay(5, deadline.Token);
                result = await probe.QueryAsync(new(2, Window), new(70, 80), []);
            } while (result.Retry);
            Assert.Equal(new PixelRect(50, 60, 100, 40), Assert.Single(result.Path));
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task AutomationRunsOnMtaWorkerAndKeepsMorePreciseNativeBounds()
    {
        var probe = new WindowElementProbe((_, _) =>
        {
            Assert.Equal(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());
            return [new(20, 20, 400, 300)];
        });
        PixelRect[] fallback = [new(50, 60, 100, 40)];
        var result = await probe.QueryAsync(new(1, Window), new(70, 80), fallback);
        Assert.False(result.Retry);
        Assert.Equal(fallback, result.Path);
    }

    private sealed class Node(PixelRect bounds, bool atomic = false, bool hidden = false, Node[]? children = null) : IWindowElementNode
    {
        public Node[] Items { get; set; } = children ?? [];
        public int Reads { get; private set; }
        public PixelRect? Bounds => bounds;
        public bool IsHidden => hidden;
        public bool IsAtomic => atomic;
        public IEnumerable<IWindowElementNode> Children { get { Reads++; return Items; } }
    }

    private sealed class DelayedNode(IWindowElementNode node) : IWindowElementNode
    {
        public PixelRect? Bounds => node.Bounds;
        public bool IsHidden => node.IsHidden;
        public bool IsAtomic => node.IsAtomic;
        public IEnumerable<IWindowElementNode> Children
        {
            get
            {
                Thread.Sleep(180); // 외부 앱의 첫 UIA 응답이 기존 150ms 제한을 넘는 경우.
                return node.Children;
            }
        }
    }
}
