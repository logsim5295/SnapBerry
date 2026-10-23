using System.Windows;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창을 표시하지 않고 설정 소유권 정책을 검증하여 숨겨진 편집기가 함께 복원되는 것을 방지한다.
public class TraySettingsTests
{
    [Theory] [InlineData(WindowState.Normal)] [InlineData(WindowState.Minimized)]
    public void HiddenEditorIsNotUsedAsSettingsOwner(WindowState state)=>Sta.Run(()=>
    {
        var editor=new Window{WindowState=state};var dialog=new SettingsWindow(new());
        try
        {
            dialog.ConfigureOwner(editor);
            Assert.Null(dialog.Owner);Assert.Equal(WindowStartupLocation.CenterScreen,dialog.WindowStartupLocation);
            Assert.False(editor.IsVisible);Assert.Equal(state,editor.WindowState);Assert.False(dialog.IsVisible);
            Assert.True(dialog.ShowInTaskbar);
        }
        finally{dialog.Close();editor.Close();}
    });
}
