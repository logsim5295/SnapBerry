using System.IO;
using System.Windows;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 배포 및 창 아이콘 자원이 올바르게 포함되고 로드되는지 검증한다.
public class AppIconTests
{
    [Fact] public void EditorAndTrayIconResourceAreAvailable()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try{Assert.NotNull(w.Icon);using var stream=typeof(EditorWindow).Assembly.GetManifestResourceStream("SnapBerry.App.ico")!;using var icon=new System.Drawing.Icon(stream);Assert.True(icon.Width>0);Assert.True(icon.Height>0);}
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
