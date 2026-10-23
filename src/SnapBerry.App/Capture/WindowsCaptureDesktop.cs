using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SnapBerry.App.Interop;

namespace SnapBerry.App.Capture;

internal sealed class WindowsCaptureDesktop : ICaptureDesktop
{
    private readonly Window editor;
    private readonly Window[] windows;
    public WindowsCaptureDesktop(Window editor)
    {
        this.editor=editor;
        // 작업 중 상태가 스티커 선택창을 숨기기 전에 소유 창도 전환 효과 억제 대상으로 확보한다.
        windows=editor.OwnedWindows.Cast<Window>().Prepend(editor).ToArray();
    }
    public IDisposable SuppressTransitions()=>new WindowTransitionScope(
        windows.Select(window=>new WindowInteropHelper(window).Handle).Where(handle=>handle!=IntPtr.Zero),new DwmTransitions());
    public void HideEditor()
    {foreach(var window in windows.Reverse())window.Hide();}
    public Task WaitForRenderAsync()=>editor.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle).Task;
    public void FlushComposition()=>Marshal.ThrowExceptionForHR(NativeMethods.DwmFlush());
    public bool IsEditorVisible=>windows.Any(window=>window.IsVisible||NativeMethods.IsWindowVisible(new WindowInteropHelper(window).Handle));
    public CaptureFrame CaptureDesktop()=>new CaptureService().CaptureDesktop();

    private sealed class DwmTransitions : IWindowTransitions
    {
        private const int TransitionsForceDisabled=3;
        public void SetDisabled(nint handle,bool disabled)
        {
            var value=disabled?1:0;
            Marshal.ThrowExceptionForHR(NativeMethods.DwmSetWindowAttribute(handle,TransitionsForceDisabled,ref value,sizeof(int)));
        }
    }
}
