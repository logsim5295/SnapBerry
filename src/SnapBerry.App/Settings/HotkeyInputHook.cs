using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
namespace SnapBerry.App.Settings;
// 설정 입력란에 포커스가 있을 때 PrintScreen 등 대상 키가 다른 캡처 앱으로 전달되지 않게 한다.
internal sealed class HotkeyInputHook : IDisposable
{
    private delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    private readonly HookProc callback;private readonly Func<bool> active;private readonly Action<Key,ModifierKeys> captured;private IntPtr handle;private readonly HashSet<int> pressed=[];
    internal bool IsInstalled=>handle!=IntPtr.Zero;
    internal HotkeyInputHook(Func<bool> active,Action<Key,ModifierKeys> captured)
    {
        this.active=active;this.captured=captured;callback=Callback;handle=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
        if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal static bool ShouldIntercept(int code,int message,int virtualKey,bool active,uint modifiers=0)=>code>=0&&active&&(virtualKey==0x2C||PriorityHotkeyRouter.IsSupported(modifiers,(uint)virtualKey))&&message is 0x100 or 0x101 or 0x104 or 0x105;
    private IntPtr Callback(int code,IntPtr message,IntPtr data)
    {
        // 눌림은 한 번만 전달하고 대응하는 뗌도 소비하여 다른 앱에 불완전한 키 입력이 새지 않게 한다.
        try
        {
            if(code>=0&&active()&&message.ToInt32() is 0x100 or 0x101 or 0x104 or 0x105)
            {
                var key=Marshal.ReadInt32(data);var up=message.ToInt32() is 0x101 or 0x105;
                if(up&&pressed.Remove(key))return new IntPtr(1);
                var modifiers=ModifierKeys.None;if(Down(0x11))modifiers|=ModifierKeys.Control;if(Down(0x12))modifiers|=ModifierKeys.Alt;if(Down(0x10))modifiers|=ModifierKeys.Shift;if(Down(0x5B)||Down(0x5C))modifiers|=ModifierKeys.Windows;
                if(ShouldIntercept(code,message.ToInt32(),key,true,(uint)modifiers))
                {
                    if(!up&&pressed.Add(key))captured(KeyInterop.KeyFromVirtualKey(key),modifiers);
                    return new IntPtr(1);
                }
            }
        }
        catch{ /* 관리 예외가 네이티브 후크 콜백 경계를 넘어가지 않게 한다. */ }
        return CallNextHookEx(handle,code,message,data);
    }
    private static bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    public void Dispose(){if(handle==IntPtr.Zero)return;UnhookWindowsHookEx(handle);handle=IntPtr.Zero;GC.KeepAlive(callback);}
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int type,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr GetModuleHandle(string? name);
}
