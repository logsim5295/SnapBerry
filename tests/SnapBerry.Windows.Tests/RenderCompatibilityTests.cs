using System.IO;
using System.Windows.Interop;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 소프트웨어 렌더링 설정과 반복 자원 로드가 정상 동작하는지 검증한다.
public class RenderCompatibilityTests
{
    [Fact] public void EditorRenderTargetExplicitlyUsesSoftwareRendering()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try{w.Show();var source=HwndSource.FromHwnd(new WindowInteropHelper(w).Handle);Assert.Equal(RenderMode.SoftwareOnly,source.CompositionTarget.RenderMode);}
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
