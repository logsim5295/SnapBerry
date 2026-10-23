using System.IO;
using SnapBerry.App;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 이력·설정·로그 경로가 사용자 LocalAppData의 SnapBerry 폴더를 사용하는지 검증한다.
public class AppDataPathsTests
{
    [Fact]
    public void CapturesSettingsAndLogsUseSnapBerryDirectory()
    {
        var expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SnapBerry");
        Assert.Equal(expected,AppDataPaths.RootDirectory);
        Assert.Equal(Path.Combine(expected,"Library"),AppDataPaths.LibraryDirectory);
        Assert.Equal(Path.Combine(expected,"Logs"),AppDataPaths.LogsDirectory);
    }
}
