using System.IO;
using SnapBerry.App.Capture;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class StartMenuDismissalTests
{
    [Fact]
    public async Task WaitsForShortcutReleaseAndForMenuToLeaveScreen()
    {
        var tick = 0;
        var sentAt = new List<int>();
        await StartMenuDismissal.WaitForCloseAsync(() => tick < 7, () => tick >= 3,
            () => sentAt.Add(tick), () => { tick++; return Task.CompletedTask; });
        Assert.Equal(new[] { 3 }, sentAt);
        Assert.Equal(7, tick);
    }

    [Fact]
    public async Task FocusChangeDoesNotSendEscapeToTheNewWindow()
    {
        var visible = true;
        await StartMenuDismissal.WaitForCloseAsync(() => visible, () => false,
            () => Assert.Fail("다른 창에 Esc가 전달되었습니다."), () => { visible = false; return Task.CompletedTask; });
    }

    [Fact]
    public async Task PersistentMenuStopsSelectionInsteadOfOpeningBehindIt()
    {
        var escapes = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartMenuDismissal.WaitForCloseAsync(
            () => true, () => true, () => escapes++, () => Task.CompletedTask));
        Assert.Equal(2, escapes);
    }

    [Fact]
    public void OnlyWindowsSystemStartAndSearchHostsQualify()
    {
        var apps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"SystemApps");
        Assert.True(StartMenuDismissal.IsShellLauncher(Path.Combine(apps,@"Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe")));
        Assert.True(StartMenuDismissal.IsShellLauncher(Path.Combine(apps,@"MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe")));
        Assert.False(StartMenuDismissal.IsShellLauncher(Path.Combine(Path.GetTempPath(),"StartMenuExperienceHost.exe")));
        Assert.False(StartMenuDismissal.IsShellLauncher(Path.Combine(apps,@"MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe")));
    }
}
