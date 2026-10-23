using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 정보 창의 버전·제작자 표시와 리본 버튼 연결을 검증한다.
public class AboutWindowTests
{
    [Fact] public void AboutButtonAppearsAfterSettingsAndUsesBrandIcon()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try{Assert.Equal("SnapBerry",w.Title);Assert.Equal("SnapBerry",typeof(EditorWindow).Assembly.GetName().Name);var group=(RibbonGroup)w.FindName("SettingsGroup");Assert.Equal("RibbonSettings",((RibbonButton)group.Items[0]).Name);var about=Assert.IsType<RibbonButton>(group.Items[1]);Assert.Equal("정보",about.Label);Assert.NotNull(about.LargeImageSource);Assert.Equal("RibbonAbout",about.Name);}
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void AboutDialogShowsBuildVersionAndAuthor()=>Sta.Run(()=>
    {
        var w=new AboutWindow();try{Assert.Equal("SnapBerry 정보",w.Title);Assert.False(string.IsNullOrWhiteSpace(w.DevelopmentVersion));Assert.Equal("logsim1234@gmail.com",w.AuthorEmail);Assert.Equal("SnapBerry",((TextBlock)w.FindName("AboutProductName")).Text);Assert.Contains(w.DevelopmentVersion,((TextBlock)w.FindName("AboutVersion")).Text);Assert.Equal(ResizeMode.NoResize,w.ResizeMode);Assert.True(((Button)w.FindName("CloseAboutButton")).IsCancel);}
        finally{w.Close();}
    });
}
