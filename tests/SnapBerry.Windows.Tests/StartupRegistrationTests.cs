using Microsoft.Win32;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

public sealed class StartupRegistrationTests : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Executable = @"D:\Moved Folder\SnapBerry.exe";
    private const string ExpectedCommand = "\"" + Executable + "\" --tray";
    private readonly string testKey = @"Software\SnapBerry.Tests-" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey root;

    public StartupRegistrationTests()
    {
        root = Registry.CurrentUser.CreateSubKey(testKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("\"C:\\Old Folder\\SnapBerry.exe\" --tray")]
    [InlineData("\"D:\\Moved Folder\\SnapBerry.exe\"")]
    [InlineData("\"D:\\Moved Folder\\SnapBerry.exe\" --TRAY")]
    public void EnabledStartupRepairsMissingOrOutdatedCommand(string? previous)
    {
        if (previous != null)
        {
            using var key = root.CreateSubKey(RunKey);
            key.SetValue("QuickCapture", previous);
        }

        StartupService.RefreshIfEnabled(true, root, Executable);

        using var repaired = root.OpenSubKey(RunKey);
        Assert.Equal(ExpectedCommand, repaired?.GetValue("QuickCapture"));
    }

    [Fact]
    public void MatchingPathWithDifferentCasingNeedsOnlyReadAccess()
    {
        const string registered = "\"d:\\moved folder\\snapberry.exe\" --tray";
        using (var key = root.CreateSubKey(RunKey))
            key.SetValue("QuickCapture", registered);
        using var readOnlyRoot = Registry.CurrentUser.OpenSubKey(testKey)!;

        StartupService.RefreshIfEnabled(true, readOnlyRoot, Executable);

        using var result = root.OpenSubKey(RunKey);
        Assert.Equal(registered, result!.GetValue("QuickCapture"));
    }

    [Fact]
    public void DisabledStartupLeavesRegistryUntouched()
    {
        StartupService.RefreshIfEnabled(false, root, Executable);
        Assert.Null(root.OpenSubKey(RunKey));
    }

    public void Dispose()
    {
        root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(testKey);
    }
}
