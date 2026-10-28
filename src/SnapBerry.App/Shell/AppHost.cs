using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SnapBerry.App.Editor;
using SnapBerry.App.Capture;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.App.Interop;
using SnapBerry.App.Stickers;
using SnapBerry.App.Export;
using Forms=System.Windows.Forms;
namespace SnapBerry.App.Shell;
// 편집기, 트레이, 전역 단축키와 캡처 서비스를 연결하는 앱의 수명 관리자다.
public sealed class AppHost : IDisposable
{
    private readonly EditorWindow window;private readonly SettingsService settings;private readonly Forms.NotifyIcon tray;
    private readonly StartupDiagnostics? diagnostics;
    private HotkeyService? hotkeys;private HwndSource? source;private readonly System.Drawing.Icon trayIcon;
    public AppHost(bool startInTray,StartupDiagnostics? diagnostics=null)
    {
        this.diagnostics=diagnostics;
        diagnostics?.Write("editor.create.begin");
        settings=new(AppDataPaths.RootDirectory);window=new(new CaptureLibraryStore(AppDataPaths.LibraryDirectory),settings);
        diagnostics?.Write("editor.create.end");window.DiagnosticStage=stage=>diagnostics?.Write(stage);window.ContentRendered+=(_,_)=>diagnostics?.Write("editor.content.rendered");
        Application.Current.MainWindow=window;window.CaptureRequested=CaptureAsync;window.SettingsRequested=SettingsAsync;
        window.SourceInitialized+=(_,_)=>{diagnostics?.Write("editor.source.begin");source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);source.AddHook(WndProc);hotkeys=new(new NativeHotkeyBackend(source.Handle,stage=>diagnostics?.Write(stage)));RegisterSettings(settings.Load());diagnostics?.Write("editor.source.end");};
        using(var stream=typeof(AppHost).Assembly.GetManifestResourceStream("SnapBerry.App.ico")!)
        using(var icon=new System.Drawing.Icon(stream))trayIcon=(System.Drawing.Icon)icon.Clone();
        tray=new(){Text="SnapBerry",Icon=trayIcon,Visible=true};
        var menu=new TrayContextMenu{Renderer=new TrayMenuRenderer(ThemeResources.GetTheme(window))};menu.Items.Add("편집기 열기",null,(_,_)=>Show());
        menu.Items.Add("영역 캡처",null,async(_,_)=>await SafeCapture(CaptureMode.Region));menu.Items.Add("창 캡처",null,async(_,_)=>await SafeCapture(CaptureMode.Window));menu.Items.Add("전체 화면 캡처",null,async(_,_)=>await SafeCapture(CaptureMode.Desktop));menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("설정",null,async(_,_)=>await SettingsAsync());menu.Items.Add("종료",null,async(_,_)=>await ExitAsync());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>Show();
        TrayMenuIcons.Apply(menu);
        diagnostics?.Write($"tray.created context={SynchronizationContext.Current?.GetType().FullName}");diagnostics?.Write("editor.show.begin");window.Show();diagnostics?.Write("editor.show.end");if(startInTray)window.Hide();
    }
    private void Show()=>InstanceActivation.Handle(window,InstanceActivation.Message);
    private IntPtr WndProc(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
    {if(msg==InstanceActivation.Message){handled=true;if(!window.IsBusy)Show();return IntPtr.Zero;}if(msg==0x312&&hotkeys?.Resolve(wParam.ToInt32()) is CaptureMode mode){handled=true;diagnostics?.Write($"hotkey.received id={wParam.ToInt32()} mode={mode} busy={window.IsBusy}");_ = SafeCapture(mode);}return IntPtr.Zero;}
    private async Task SafeCapture(CaptureMode mode)
    {try{await CaptureAsync(mode);}catch(Exception e){diagnostics?.Write($"capture.failed mode={mode}",e);Show();MessageBox.Show(window,e.Message,"SnapBerry",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    private async Task CaptureAsync(CaptureMode mode)
    {
        if(window.IsBusy)return;
        using var capture=new CaptureSession(new WindowsCaptureDesktop(window));
        await RunCaptureAsync(async()=>
        {
            await window.FlushAsync();var frame=await capture.CaptureAsync();
            return await new CaptureCoordinator().SelectAsync(frame,mode);
        },async selected=>
        {
            var record=new CaptureRecord{Width=selected.Image.PixelWidth,Height=selected.Image.PixelHeight,Mode=mode,OriginalPng=PngFileWriter.Encode(selected.Image)};
            await window.OpenNewAsync(record);
        },Show,window.SetBusy);
    }
    // 취소 시에는 편집기를 숨긴 상태로 끝내고, 성공 또는 오류일 때만 편집기를 표시한다.
    internal static async Task RunCaptureAsync(Func<Task<CaptureFrame?>> select,Func<CaptureFrame,Task> open,Action showEditor,Action<bool> setBusy)
    {
        setBusy(true);try
        {
            var selected=await select();
            if(selected is null)return;
            showEditor();
            await open(selected);
        }
        catch{showEditor();throw;}
        finally{setBusy(false);}
    }
    private void RegisterSettings(AppSettings value)
    {
        var failures=new List<string>();foreach(var (mode,key) in new[]{(CaptureMode.Region,value.RegionKey),(CaptureMode.Window,value.WindowKey),(CaptureMode.Desktop,value.DesktopKey)})
        {var result=hotkeys!.TryReplace(mode,key);diagnostics?.Write($"hotkey.binding mode={mode} binding={key} success={result.Success} error={result.Error}");if(!result.Success)failures.Add($"{key}: {result.Error}");}
        if(failures.Count>0)window.Dispatcher.BeginInvoke(()=>MessageBox.Show(window,string.Join(Environment.NewLine,failures),"단축키 설정",MessageBoxButton.OK,MessageBoxImage.Information));
    }
    private Task SettingsAsync()
    {
        // 입력 도중 자신의 단축키가 실행되지 않게 등록을 풀고, 저장 실패나 취소 시 이전 설정을 복구한다.
        if(window.IsBusy||hotkeys is null)return Task.CompletedTask;
        RunSettings(()=>
        {
            var previous=settings.Load();var saved=false;
            try
            {
                hotkeys.UnregisterAll();
                var dialog=new SettingsWindow(previous,inspectBinding:hotkeys.CheckAvailability);dialog.ConfigureOwner(window);if(dialog.ShowDialog()!=true)return;var proposed=dialog.Result!;
                foreach(var (mode,key) in new[]{(CaptureMode.Region,proposed.RegionKey),(CaptureMode.Window,proposed.WindowKey),(CaptureMode.Desktop,proposed.DesktopKey)})
                {var result=hotkeys.TryReplace(mode,key);if(!result.Success)throw new InvalidOperationException(result.Error);}
                ApplySettings(previous,proposed,settings.Save,StartupService.SetEnabled);saved=true;window.ReloadSettings();
                tray.ContextMenuStrip!.Renderer=new TrayMenuRenderer(proposed.Theme);
            }
            finally{if(!saved){hotkeys.UnregisterAll();RegisterSettings(previous);}}
        },window.SetBusy,e=>MessageBox.Show(window,e.Message,"설정 저장 실패"));
        return Task.CompletedTask;
    }
    internal static void RunSettings(Action action,Action<bool> setBusy,Action<Exception> reportError)
    {
        // 읽기·복구 처리 자체가 실패해도 작업 중 상태를 해제한다.
        setBusy(true);try{action();}catch(Exception e){reportError(e);}finally{setBusy(false);}
    }
    internal static void ApplySettings(AppSettings previous,AppSettings proposed,Action<AppSettings> save,Action<bool> setStartup)
    {
        var changeStartup=previous.StartWithWindows!=proposed.StartWithWindows;
        try{if(changeStartup)setStartup(proposed.StartWithWindows);save(proposed);}
        catch(Exception error)
        {
            if(changeStartup)
            {
                try{setStartup(previous.StartWithWindows);}
                catch(Exception rollback){throw new AggregateException("설정 저장과 Windows 자동 실행 설정 복구에 실패했습니다.",error,rollback);}
            }
            throw;
        }
    }
    private async Task ExitAsync()
    {
        // 저장이 실패하면 종료를 취소하여 사용자의 편집 내용이 사라지지 않도록 한다.
        if(window.IsBusy)return;try{window.SetBusy(true);await window.FlushAsync();window.CloseForExit();Dispose();Application.Current.Shutdown();}
        catch(Exception e){window.SetBusy(false);Show();MessageBox.Show(window,"이력을 저장하지 못했습니다. 다시 시도해 주세요.\n"+e.Message,"종료 취소");}
    }
    public void Dispose(){hotkeys?.Dispose();source?.RemoveHook(WndProc);tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();trayIcon.Dispose();}
}
