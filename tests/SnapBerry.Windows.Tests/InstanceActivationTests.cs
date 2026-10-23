using System.IO;
using System.Windows;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using SnapBerry.App.Shell;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 트레이로 숨기거나 최소화한 기존 창을 재실행 요청으로 복구하는지 검증한다.
public class InstanceActivationTests
{
    [Fact] public void RelaunchRestoresHiddenWpfVisibilityRepeatedly()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),confirmTrayHide:()=>true);
        try{w.Show();for(var i=0;i<5;i++){w.Close();Assert.Equal(Visibility.Hidden,w.Visibility);Assert.True(InstanceActivation.Handle(w,InstanceActivation.Message));Assert.Equal(Visibility.Visible,w.Visibility);Assert.True(w.IsVisible);}w.WindowState=WindowState.Minimized;InstanceActivation.Handle(w,InstanceActivation.Message);Assert.Equal(WindowState.Normal,w.WindowState);}
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void UnrelatedMessagesDoNotRestoreWindow()=>Sta.Run(()=>
    {var w=new Window();try{Assert.False(InstanceActivation.Handle(w,123));Assert.False(w.IsVisible);}finally{w.Close();}});
}
