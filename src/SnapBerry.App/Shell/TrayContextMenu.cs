using SnapBerry.App.Interop;
using Forms=System.Windows.Forms;

namespace SnapBerry.App.Shell;

// .NET 10의 누락된 메뉴 알림을 보완해 숨김 아이콘 패널이 메뉴 사용 중 닫히지 않게 한다.
// https://github.com/dotnet/winforms/issues/15176
internal class TrayContextMenu : Forms.ContextMenuStrip
{
    private bool menuOpen;
    private IntPtr notifiedHandle;

    public TrayContextMenu()
    {
        var menuFont=new System.Drawing.Font(Font.FontFamily,Math.Max(9f,Font.SizeInPoints),Font.Style);
        Font=menuFont;
        Disposed+=(_,_)=>menuFont.Dispose();
    }

    protected override void OnItemAdded(Forms.ToolStripItemEventArgs e)
    {
        if(e.Item is Forms.ToolStripMenuItem item)item.Padding=new Forms.Padding(6,5,6,5);
        base.OnItemAdded(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        menuOpen=true;
        StartShellTracking();
        base.OnOpened(e);
    }

    protected override void OnClosed(Forms.ToolStripDropDownClosedEventArgs e)
    {
        menuOpen=false;
        EndShellTracking();
        base.OnClosed(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // 모니터 배율에 따라 열린 메뉴의 HWND가 재생성되어도 새 HWND로 알림을 이어간다.
        if(menuOpen)StartShellTracking();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        EndShellTracking();
        base.OnHandleDestroyed(e);
    }

    private void StartShellTracking()
    {
        // Handle을 처음 만들면 OnHandleCreated에서 먼저 알릴 수 있으므로 그 뒤에 중복을 확인한다.
        var handle=Handle;
        if(notifiedHandle!=IntPtr.Zero)return;
        notifiedHandle=handle;
        NativeMethods.NotifyWinEvent((uint)Forms.AccessibleEvents.SystemMenuPopupStart,handle,-4,0);
    }

    private void EndShellTracking()
    {
        var handle=notifiedHandle;
        if(handle==IntPtr.Zero)return;
        notifiedHandle=IntPtr.Zero;
        NativeMethods.NotifyWinEvent((uint)Forms.AccessibleEvents.SystemMenuPopupEnd,handle,-4,0);
    }
}
