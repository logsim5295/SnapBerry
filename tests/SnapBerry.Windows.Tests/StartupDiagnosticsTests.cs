using System.IO;
using SnapBerry.App.Shell;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 시작 로그 파일 생성과 단계·예외 기록의 안전성을 검증한다.
public class StartupDiagnosticsTests
{
    [Fact] public void StartupStagesAndFullExceptionAreWrittenImmediately()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try{using var log=new StartupDiagnostics(path);log.Write("editor.show.begin");log.Write("startup.failed",new InvalidOperationException("diagnostic failure"));var text=File.ReadAllText(log.FilePath);Assert.Contains("editor.show.begin",text);Assert.Contains("System.InvalidOperationException: diagnostic failure",text);}
        finally{TestFiles.DeleteDirectory(path);}
    }
    [Fact] public void UnwritableDiagnosticsDoNotPreventStartup()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));File.WriteAllText(path,"not a directory");
        try{using var log=new StartupDiagnostics(path);log.Write("startup.begin");}
        finally{File.Delete(path);}
    }
}
