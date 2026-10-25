using System.Runtime.InteropServices;
using SnapBerry.Core;
using SnapBerry.App.Capture;
namespace SnapBerry.App.Interop;
// Win32 경계: 창 열거·실제 화면 위치·전역 단축키·인스턴스 메시지 전달을 담당한다.
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]public struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]public struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
    public delegate bool EnumWindowProc(IntPtr hwnd,IntPtr data);
    [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point p);
    [DllImport("user32.dll")]public static extern bool EnumWindows(EnumWindowProc callback,IntPtr data);
    [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")]public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")]public static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    [DllImport("user32.dll")]public static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("dwmapi.dll")]public static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out Rect rect,int size);
    [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]private static extern int GetDwmIntAttribute(IntPtr hwnd,int attr,out int value,int size);
    [DllImport("dwmapi.dll")]public static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
    [DllImport("dwmapi.dll")]public static extern int DwmFlush();
    [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
    [DllImport("user32.dll",SetLastError=true)]public static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string? className,string title);
    [DllImport("user32.dll")]public static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll",SetLastError=true)]public static extern bool PostMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]public static extern void NotifyWinEvent(uint eventId,IntPtr hwnd,int objectId,int childId);
    public static PixelPoint Cursor(){GetCursorPos(out var p);return new(p.X,p.Y);}
    internal static bool IsCaptureCandidate(uint processId,int ownProcessId,bool visible,bool minimized,int cloaked)
        => processId!=ownProcessId&&visible&&!minimized&&cloaked==0;
    public static IReadOnlyList<CaptureWindow> VisibleWindows()
    {
        // WS_VISIBLE이어도 ClickToDo/다른 가상 데스크톱의 창은 DWM에서 숨겨져 있을 수 있다.
        var result=new List<CaptureWindow>();EnumWindows((hwnd,_)=>{GetWindowThreadProcessId(hwnd,out var pid);
            if(GetDwmIntAttribute(hwnd,14,out var cloaked,sizeof(int))!=0)cloaked=0;
            if(!IsCaptureCandidate(pid,Environment.ProcessId,IsWindowVisible(hwnd),IsIconic(hwnd),cloaked))return true;
            if(DwmGetWindowAttribute(hwnd,9,out var rect,16)!=0&&!GetWindowRect(hwnd,out rect))return true;
            if(rect.Right>rect.Left&&rect.Bottom>rect.Top)result.Add(new(hwnd,new(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top)));return true;},IntPtr.Zero);return result;
    }
}
