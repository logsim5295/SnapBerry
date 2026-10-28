using System.ComponentModel;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class KeyboardHookThreadTests
{
    [Fact]
    public void HookLifecycleAndInputWorkRunAwayFromBlockedCaller()
    {
        var caller=Environment.CurrentManagedThreadId;var installed=0;var processed=0;var removed=0;
        var thread=new KeyboardHookThread(()=>installed=Environment.CurrentManagedThreadId,()=>removed=Environment.CurrentManagedThreadId);
        try
        {
            // 호출자는 동기 대기 중이어도 후크 스레드의 메시지 큐는 계속 처리되어야 한다.
            thread.Invoke(()=>processed=Environment.CurrentManagedThreadId);
            Assert.NotEqual(caller,installed);Assert.Equal(installed,processed);
        }
        finally{thread.Dispose();}
        Assert.Equal(installed,removed);thread.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>thread.Invoke(()=>{}));
    }

    [Fact]
    public void HookInstallationFailureReturnsToCallerWithoutLeavingWorkerRunning()
    {
        var failure=new Win32Exception(5);var uninstalled=false;
        var actual=Assert.Throws<Win32Exception>(()=>new KeyboardHookThread(()=>throw failure,()=>uninstalled=true));
        Assert.Same(failure,actual);Assert.False(uninstalled);
    }
}
