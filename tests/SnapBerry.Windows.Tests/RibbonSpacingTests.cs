using System.IO;
using System.Windows;
using System.Windows.Controls.Ribbon;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 리본 제목 영역을 제거한 후 상단 여백과 버튼 배치를 검증한다.
public class RibbonSpacingTests
{
    [Fact] public void RibbonTabsStartAtTopWithoutUnusedCaptionRow()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try{w.Show();w.UpdateLayout();var ribbon=(Ribbon)w.FindName("MainRibbon");var tabs=(FrameworkElement)ribbon.Template.FindName("TabHeaderItemsControl",ribbon);Assert.NotNull(tabs);var top=tabs.TransformToAncestor(ribbon).Transform(new Point());Assert.InRange(top.Y,0,3);}
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
