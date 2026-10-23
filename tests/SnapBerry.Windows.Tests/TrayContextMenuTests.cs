using System.Diagnostics;
using System.Runtime.InteropServices;
using SnapBerry.App.Interop;
using SnapBerry.App.Shell;
using Xunit;
using Forms=System.Windows.Forms;

namespace SnapBerry.Windows.Tests;

// 메뉴를 표시하거나 활성화하지 않고, Windows에 전달되는 실제 메뉴 수명 알림을 관찰한다.
public class TrayContextMenuTests
{
    [Fact]
    public void EachOpenAndCloseNotifiesShellWithTheSameMenuHandle()=>Sta.Run(()=>
    {
        using var menu=new TestMenu();var handle=menu.Handle;
        using var events=new MenuEvents(handle);
        menu.Opened+=(_,_)=>Assert.False(menu.Visible);
        foreach(var reason in new[]{Forms.ToolStripDropDownCloseReason.Keyboard,Forms.ToolStripDropDownCloseReason.AppClicked,Forms.ToolStripDropDownCloseReason.ItemClicked})
        {
            menu.OpenWithoutShowing();menu.CloseWithoutShowing(reason);
        }
        events.WaitFor(6);
        Assert.Equal(new uint[]{6,7,6,7,6,7},events.Received.Select(e=>e.Event));
        Assert.All(events.Received,e=>{Assert.Equal(-4,e.Object);Assert.Equal(0,e.Child);});
        Assert.False(NativeMethods.IsWindowVisible(handle));
    });

    [Fact]
    public void DisposingAnOpenMenuEndsShellTrackingExactlyOnce()=>Sta.Run(()=>
    {
        using var menu=new TestMenu();var handle=menu.Handle;
        using var events=new MenuEvents(handle);
        menu.OpenWithoutShowing();menu.Dispose();menu.Dispose();
        events.WaitFor(2);
        Assert.Equal(new uint[]{6,7},events.Received.Select(e=>e.Event));
    });

    [Fact]
    public void RecreatingAnOpenMenuTransfersShellTrackingToTheNewHandle()=>Sta.Run(()=>
    {
        using var menu=new TestMenu();var original=menu.Handle;
        using var events=new MenuEvents(IntPtr.Zero);
        menu.OpenWithoutShowing();menu.RecreateWithoutShowing();var replacement=menu.Handle;
        menu.CloseWithoutShowing(Forms.ToolStripDropDownCloseReason.Keyboard);
        events.WaitFor(4);
        Assert.NotEqual(original,replacement);
        Assert.Equal(new uint[]{6,7,6,7},events.Received.Select(e=>e.Event));
        Assert.Equal(new[]{original,original,replacement,replacement},events.Received.Select(e=>e.Handle));
        Assert.False(NativeMethods.IsWindowVisible(replacement));
    });

    [Fact]
    public void UnopenedMenuDoesNotNotifyOrCreateAWindowWhenClosedOrDisposed()=>Sta.Run(()=>
    {
        using var menu=new TestMenu();
        menu.CloseWithoutShowing(Forms.ToolStripDropDownCloseReason.CloseCalled);menu.Dispose();
        Assert.False(menu.IsHandleCreated);
    });

    private sealed class TestMenu : TrayContextMenu
    {
        internal void OpenWithoutShowing()=>OnOpened(EventArgs.Empty);
        internal void CloseWithoutShowing(Forms.ToolStripDropDownCloseReason reason)=>OnClosed(new(reason));
        internal void RecreateWithoutShowing()=>RecreateHandle();
    }

    private sealed class MenuEvents : IDisposable
    {
        private readonly WinEventProc callback;
        private readonly IntPtr hook;
        internal List<(uint Event,int Object,int Child,IntPtr Handle)> Received {get;}=[];
        internal MenuEvents(IntPtr menu)
        {
            callback=(_,kind,hwnd,obj,child,_,_)=>{if(menu==IntPtr.Zero||hwnd==menu)Received.Add((kind,obj,child,hwnd));};
            hook=SetWinEventHook(6,7,IntPtr.Zero,callback,(uint)Environment.ProcessId,GetCurrentThreadId(),0);
            Assert.NotEqual(IntPtr.Zero,hook);
        }
        internal void WaitFor(int count)
        {
            var timer=Stopwatch.StartNew();
            do{Forms.Application.DoEvents();if(Received.Count>=count)break;Thread.Sleep(1);}while(timer.ElapsedMilliseconds<2000);
            Forms.Application.DoEvents();
        }
        public void Dispose(){UnhookWinEvent(hook);GC.KeepAlive(callback);}
        private delegate void WinEventProc(IntPtr hook,uint kind,IntPtr hwnd,int objectId,int childId,uint thread,uint time);
        [DllImport("user32.dll")]private static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]private static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    }
}
