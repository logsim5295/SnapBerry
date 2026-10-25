using SnapBerry.App.Interop;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class WindowCandidateTests
{
    [Theory]
    [InlineData(1)] // ClickToDo: WS_VISIBLE이지만 DWM_CLOAKED_APP 상태인 전체 모니터 창.
    [InlineData(2)] // TextInputHost / 다른 가상 데스크톱의 숨겨진 창.
    [InlineData(4)] // 부모로부터 상속된 cloak 상태.
    [InlineData(3)]
    public void CloakedWindowCannotCoverAVisibleSaveDialog(int cloaked)
        => Assert.False(NativeMethods.IsCaptureCandidate(100, 200, true, false, cloaked));

    [Fact]
    public void VisibleDialogRemainsEligible()
        => Assert.True(NativeMethods.IsCaptureCandidate(100, 200, true, false, 0));

    [Fact]
    public void OwnHiddenAndMinimizedWindowsRemainExcluded()
    {
        Assert.False(NativeMethods.IsCaptureCandidate(100, 100, true, false, 0));
        Assert.False(NativeMethods.IsCaptureCandidate(100, 200, false, false, 0));
        Assert.False(NativeMethods.IsCaptureCandidate(100, 200, true, true, 0));
    }
}
