using Avalonia.Threading;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Shortcuts;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow
{
    private bool configuringHotkeys;
    public event Action? ExitRequested;
    public bool CanExit=>allowClose;
    private static GlobalHotkeyService? CreateGlobalHotkeys()=>OperatingSystem.IsMacOS()?new(new CarbonHotkeyBackend()):null;
    private void InitializeHotkeys()
    {if(hotkeys is null)return;try{hotkeys.Apply(preferences.Hotkeys);}catch(Exception error){Status("전역 단축키 오류: "+error.Message);}}
    private void OnGlobalCapture(DesktopCaptureMode mode)=>Dispatcher.UIThread.Post(()=>{if(!closed&&!busy)_=RequestCaptureAsync(mode);});
    public Task RequestCaptureAsync(DesktopCaptureMode mode)=>RunAsync(()=>CaptureAsync(mode));
    public void ShowEditor(){if(closed||busy)return;Show();Activate();}
    private async Task ShowHotkeysAsync()
    {
        configuringHotkeys=true;
        try
        {
            hotkeys?.Apply(preferences.Hotkeys with{Enabled=false});
            var dialog=new HotkeySettingsWindow(preferences.Hotkeys,ApplyHotkeySettingsAsync){RequestedThemeVariant=ActualThemeVariant};await dialog.ShowDialog<bool>(this);
        }
        finally{configuringHotkeys=false;hotkeys?.Apply(preferences.Hotkeys);}
    }
    public Task RequestHotkeySettingsAsync()
    {
        ShowEditor();return RunAsync(ShowHotkeysAsync);
    }
    public Task ApplyHotkeySettingsAsync(HotkeySettings proposed)
    {
        proposed.Chords();var previous=preferences;hotkeys?.Apply(proposed);
        try{var next=previous with{Hotkeys=proposed};next.Save(dataDirectory);preferences=next;}
        catch(Exception error)
        {
            try{hotkeys?.Apply(configuringHotkeys?previous.Hotkeys with{Enabled=false}:previous.Hotkeys);}
            catch(Exception rollback){throw new IOException("설정 저장과 이전 단축키 복구에 실패했습니다.",new AggregateException(error,rollback));}
            throw;
        }
        if(configuringHotkeys)hotkeys?.Apply(proposed with{Enabled=false});
        Status(hotkeys is null?"단축키 설정 저장됨 · 전역 등록은 macOS에서 적용됩니다.":proposed.Enabled?"전역 캡처 단축키 적용됨":"전역 캡처 단축키 사용 안 함");return Task.CompletedTask;
    }
    public async Task<bool> RequestExitAsync()
    {
        if(busy||closed)return false;var success=false;
        await RunAsync(async ()=>{await FlushAsync();allowClose=true;stickerPicker?.Close();Close();ExitRequested?.Invoke();success=true;});return success;
    }
}
