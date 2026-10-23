using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow
{
    private async Task ShowAboutAsync()
    {
        var dialog=new AboutWindow(dataDirectory,target=>
        {
            if(target==dataDirectory)Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
        },async text=>
        {
            var clipboard=Clipboard??throw new InvalidOperationException("클립보드를 사용할 수 없습니다.");
            await clipboard.SetTextAsync(text);
        }){RequestedThemeVariant=ActualThemeVariant};
        await dialog.ShowDialog(this);
    }
    private async Task ShowSettingsAsync()
    {
        var enabled=preferences.StartAtLogin;string? warning=null;
        if(loginStartup is not null)
        {try{enabled=loginStartup.IsEnabled;}catch(Exception error){warning="자동 실행 상태 확인 실패: "+error.Message;}}
        var dialog=new DesktopSettingsWindow(preferences.Dark,enabled,loginStartup is not null,ApplyAppearanceSettingsAsync,warning){RequestedThemeVariant=ActualThemeVariant};
        await dialog.ShowDialog<bool>(this);
    }
    public Task ApplyAppearanceSettingsAsync(bool dark,bool startAtLogin)
    {
        var previous=preferences;var wasEnabled=loginStartup?.IsEnabled??previous.StartAtLogin;
        // 앱이 이동했으면 현재 경로의 IsEnabled가 false여도 이전 경로의 등록이 남아 있을 수 있다.
        var startupChanged=loginStartup is not null;
        if(startupChanged)loginStartup!.SetEnabled(startAtLogin);
        try
        {
            var next=previous with{Dark=dark,StartAtLogin=startAtLogin};next.Save(dataDirectory);preferences=next;
        }
        catch(Exception error)
        {
            if(startupChanged)
            {try{loginStartup!.SetEnabled(wasEnabled);}catch(Exception rollback){throw new IOException("설정 저장과 이전 자동 실행 설정 복구에 실패했습니다.",new AggregateException(error,rollback));}}
            throw;
        }
        ApplyTheme(dark);Control<ComboBox>("ThemeBox").SelectedIndex=dark?1:0;Status("설정 저장됨");return Task.CompletedTask;
    }
}
