using System.Runtime.ExceptionServices;
namespace SnapBerry.Windows.Tests;
// WPF가 요구하는 STA 스레드와 Dispatcher 문맥에서 테스트를 실행하고 발생 예외를 호출자에게 전달한다.
internal static class Sta
{
    public static void Run(Action action)
    {
        Exception? error=null;var thread=new Thread(()=>{SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));try{action();}catch(Exception e){error=e;}finally{System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();
    }
}
