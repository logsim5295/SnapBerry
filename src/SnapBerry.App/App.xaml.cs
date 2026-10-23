using System.Windows;
using SnapBerry.App.Shell;
using SnapBerry.App.Interop;
using System.Windows.Media;
using System.Windows.Interop;
namespace SnapBerry.App;
// WPF 진입점: 진단 로그, 단일 인스턴스 보장, 렌더링 방식과 앱 수명을 초기화한다.
public partial class App : Application
{
    private Mutex? mutex;private AppHost? host;private StartupDiagnostics? diagnostics;
    protected override void OnStartup(StartupEventArgs e)
    {
        diagnostics=new(AppDataPaths.LogsDirectory);
        var executable=Environment.ProcessPath;
        diagnostics.Write($"startup.begin exe={executable} version={typeof(App).Assembly.GetName().Version} build={(executable is null?"unknown":File.GetLastWriteTimeUtc(executable).ToString("O"))} runtime={Environment.Version}");diagnostics.Monitor(Dispatcher);
        // 일부 그래픽 환경의 간헐적 검정 화면을 피하도록 프로세스 전체를 소프트웨어 렌더링한다.
        RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
        // 구버전과 중복 실행되지 않도록 단일 인스턴스 식별자는 이전 이름을 유지한다.
        base.OnStartup(e);mutex=new Mutex(true,"Local\\QuickCapture-"+Environment.UserName,out var created);
        diagnostics.Write($"instance.checked primary={created}");
        if(!created){var hwnd=NativeMethods.FindWindow(null,"SnapBerry");if(hwnd==IntPtr.Zero)hwnd=NativeMethods.FindWindow(null,"QuickCapture");diagnostics.Write($"instance.activate hwnd={hwnd}");if(hwnd!=IntPtr.Zero)NativeMethods.PostMessage(hwnd,InstanceActivation.Message,IntPtr.Zero,IntPtr.Zero);Shutdown();return;}
        DispatcherUnhandledException+=(_,args)=>{diagnostics.Write("dispatcher.exception",args.Exception);MessageBox.Show(args.Exception.Message,"SnapBerry");args.Handled=true;};
        try{host=new(e.Args.Contains("--tray"),diagnostics);diagnostics.Write("startup.complete");}catch(Exception ex){diagnostics.Write("startup.failed",ex);MessageBox.Show(ex.Message,"SnapBerry");Shutdown(1);}
    }
    protected override void OnExit(ExitEventArgs e){diagnostics?.Write($"exit code={e.ApplicationExitCode}");host?.Dispose();mutex?.Dispose();diagnostics?.Dispose();base.OnExit(e);}
}
