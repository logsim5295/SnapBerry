using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace SnapBerry.App.Settings;

// UI의 이미지 처리·파일 I/O가 저수준 후크의 응답 제한 시간을 소모하지 않게 한다.
internal sealed class KeyboardHookThread : IDisposable
{
    private readonly Thread worker;
    private readonly Dispatcher dispatcher;
    private ExceptionDispatchInfo? shutdownError;
    private int disposed;

    internal KeyboardHookThread(Action install,Action uninstall)
    {
        var ready=new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        worker=new Thread(()=>
        {
            try
            {
                var current=Dispatcher.CurrentDispatcher;
                install();ready.SetResult(current);
                try{Dispatcher.Run();}
                finally{uninstall();}
            }
            catch(Exception error)
            {if(!ready.TrySetException(error))shutdownError=ExceptionDispatchInfo.Capture(error);}
        }){IsBackground=true,Name="SnapBerry keyboard hook"};
        worker.Start();
        try{dispatcher=ready.Task.GetAwaiter().GetResult();}
        catch{worker.Join();throw;}
    }

    internal void Invoke(Action action)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        dispatcher.Invoke(action);
    }

    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        try{dispatcher.InvokeShutdown();}
        finally{worker.Join();}
        shutdownError?.Throw();
    }
}
