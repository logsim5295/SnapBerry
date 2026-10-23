using System.ComponentModel;
using System.Runtime.InteropServices;
using SnapBerry.App.Interop;
namespace SnapBerry.App.Settings;
// 지원되는 충돌 단축키를 먼저 소비하고 WM_HOTKEY 메시지로 기존 앱 처리 경로에 연결한다.
internal sealed class PriorityHotkeyHook : IDisposable
{
    private delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    private readonly HookProc callback;private readonly PriorityHotkeyRouter router;private readonly IntPtr window;private IntPtr handle;
    internal PriorityHotkeyHook(IntPtr window,PriorityHotkeyRouter router)
    {
        this.window=window;this.router=router;callback=Callback;handle=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
        if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private IntPtr Callback(int code,IntPtr message,IntPtr data)
    {
        try
        {
            if(code>=0&&router.Count>0&&message.ToInt32() is 0x100 or 0x101 or 0x104 or 0x105)
            {
                uint modifiers=0;if(Down(0x11))modifiers|=2;if(Down(0x12))modifiers|=1;if(Down(0x10))modifiers|=4;if(Down(0x5B)||Down(0x5C))modifiers|=8;
                if(router.Process(message.ToInt32(),unchecked((uint)Marshal.ReadInt32(data)),modifiers,out var id))
                {
                    if(id is int value)NativeMethods.PostMessage(window,0x312,new IntPtr(value),IntPtr.Zero);
                    return new IntPtr(1);
                }
            }
        }
        catch{ /* 네이티브 후크 밖으로 관리 예외를 전파하지 않는다. */ }
        return CallNextHookEx(handle,code,message,data);
    }
    private static bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    public void Dispose(){if(handle==IntPtr.Zero)return;UnhookWindowsHookEx(handle);handle=IntPtr.Zero;GC.KeepAlive(callback);}
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int type,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);
}
