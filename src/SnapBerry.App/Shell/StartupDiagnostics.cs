using System.Windows.Threading;
namespace SnapBerry.App.Shell;
// 시작 단계와 UI 응답 상태를 프로세스별 로그에 남긴다. 로그 오류로 앱 시작을 방해하지 않는다.
public sealed class StartupDiagnostics : IDisposable
{
    private readonly object gate=new();private Timer? watchdog;
    private long lastResponse=Environment.TickCount64;private int pending,reported;private bool disposed;
    public string FilePath {get;}
    public StartupDiagnostics(string directory)
    {FilePath=Path.Combine(directory,$"startup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");}
    public void Write(string stage,Exception? error=null)
    {
        lock(gate)
        {
            if(disposed)return;
            try{Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.AppendAllText(FilePath,$"{DateTimeOffset.Now:O} [thread {Environment.CurrentManagedThreadId}] {stage}{(error is null?"":Environment.NewLine+error)}{Environment.NewLine}");}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
        }
    }
    public void Monitor(Dispatcher dispatcher)
    {
        // 백그라운드 타이머가 UI 큐에 응답 작업을 넣는다. 미처리 작업은 하나만 유지해 큐 누적을 피한다.
        watchdog=new Timer(_=>
        {
            if(dispatcher.HasShutdownStarted)return;
            var elapsed=Environment.TickCount64-Interlocked.Read(ref lastResponse);
            if(elapsed>10000&&Interlocked.Exchange(ref reported,1)==0)Write($"ui.unresponsive durationMs={elapsed}");
            if(Interlocked.CompareExchange(ref pending,1,0)!=0)return;
            dispatcher.BeginInvoke(DispatcherPriority.Background,()=>
            {
                Interlocked.Exchange(ref lastResponse,Environment.TickCount64);Interlocked.Exchange(ref pending,0);
                if(Interlocked.Exchange(ref reported,0)!=0)Write("ui.responsive.again");
            });
        },null,1000,1000);
    }
    public void Dispose(){watchdog?.Dispose();lock(gate)disposed=true;}
}
