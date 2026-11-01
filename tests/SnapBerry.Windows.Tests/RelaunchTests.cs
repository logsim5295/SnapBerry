using SnapBerry.App.Shell;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class RelaunchTests
{
    private const string RequestedPath = @"D:\새 폴더\SnapBerry.exe";
    private const string RequestedVersion = "1.1.7+abc";

    [Fact]
    public void BuildMarkerIdentifiesOnlyItsProcessAndBuildWhileRunning()
    {
        var build = Guid.NewGuid();
        var pid = Environment.ProcessId;
        using (Relaunch.MarkEditor(pid, 10, build))
        {
            Assert.True(Relaunch.IsSameBuild(pid, 10, build));
            Assert.False(Relaunch.IsSameBuild(pid, 10, Guid.NewGuid()));
            Assert.False(Relaunch.IsSameBuild(pid + 1, 10, build));
            Assert.False(Relaunch.IsSameBuild(pid, 11, build));
        }
        Assert.False(Relaunch.IsSameBuild(pid, 10, build));
    }

    [Theory]
    [InlineData("1.1.6+old")]
    [InlineData(RequestedVersion)]
    public void DifferentBuildNoticeIncludesBothExecutablesAndTrayInstructions(string runningVersion)
    {
        const string runningPath = @"C:\이전 폴더\QuickCapture.exe";
        var message = Relaunch.Notice(new(1, false, runningPath, runningVersion), RequestedPath, RequestedVersion);
        Assert.Contains(runningPath, message);
        Assert.Contains(runningVersion, message);
        Assert.Contains(RequestedPath, message);
        Assert.Contains(RequestedVersion, message);
        Assert.Contains("트레이", message);
        Assert.Contains("종료", message);
        Assert.Contains("다시 실행", message);
    }

    [Fact]
    public void SameBuildAtAnotherPathUsesExistingWindow()
    {
        Assert.Null(Relaunch.Notice(new(1, true, @"C:\다른 폴더\SnapBerry.exe", RequestedVersion),
            RequestedPath, RequestedVersion));
    }

    [Fact]
    public void MissingEditorStillExplainsHowToProceed()
    {
        var message = Relaunch.Notice(null, RequestedPath, RequestedVersion);
        Assert.Contains("확인할 수", message);
        Assert.Contains(RequestedPath, message);
        Assert.Contains("트레이", message);
    }

    [Theory]
    [InlineData("QuickCapture", true)]
    [InlineData("SnapBerry", true)]
    [InlineData("Other Editor", false)]
    [InlineData(null, false)]
    public void LegacyWindowRequiresAppProductMetadata(string? product, bool expected)
    {
        Assert.Equal(expected, Relaunch.IsAppProduct(product));
    }
}
