using System.Runtime.InteropServices;

namespace SnapBerry.Desktop.Shortcuts;

public sealed class CarbonHotkeyBackend : IGlobalHotkeyBackend
{
    private const string Carbon="/System/Library/Frameworks/Carbon.framework/Carbon";
    private const uint Signature=0x53425259;private const int NotHandled=-9874;
    [StructLayout(LayoutKind.Sequential)] public struct EventTypeSpec {public uint EventClass,EventKind;}
    [StructLayout(LayoutKind.Sequential)] public struct EventHotKeyId {public uint Signature,Id;}
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EventHandler(IntPtr call,IntPtr evt,IntPtr userData);
    private readonly Dictionary<uint,IntPtr> registrations=[];private readonly EventHandler callback;
    private IntPtr handler;private Action<uint>? pressed;private bool disposed;
    public CarbonHotkeyBackend()=>callback=OnEvent;
    public void Install(Action<uint> onPressed)
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("전역 단축키는 macOS에서 사용할 수 있습니다.");
        ObjectDisposedException.ThrowIf(disposed,this);if(handler!=IntPtr.Zero)throw new InvalidOperationException("단축키 처리기가 이미 설치되었습니다.");
        var type=new EventTypeSpec{EventClass=0x6B657962,EventKind=6};
        var status=InstallEventHandler(GetApplicationEventTarget(),callback,1,ref type,IntPtr.Zero,out handler);
        if(status!=0){handler=IntPtr.Zero;throw new IOException($"macOS 단축키 처리기 설치 실패 ({status})");}pressed=onPressed;
    }
    public void Register(uint id,HotkeyChord chord)
    {
        ObjectDisposedException.ThrowIf(disposed,this);if(handler==IntPtr.Zero||registrations.ContainsKey(id))throw new InvalidOperationException("단축키 처리기 상태가 올바르지 않습니다.");
        var status=RegisterEventHotKey(chord.KeyCode,chord.Modifiers,new(){Signature=Signature,Id=id},GetApplicationEventTarget(),0,out var reference);
        if(status!=0)throw new IOException($"다른 앱에서 사용 중이거나 등록할 수 없는 단축키입니다 ({status}).");registrations.Add(id,reference);
    }
    public void Unregister(uint id)
    {if(registrations.Remove(id,out var reference))UnregisterEventHotKey(reference);}
    private int OnEvent(IntPtr call,IntPtr evt,IntPtr userData)
    {
        if(disposed)return NotHandled;
        try
        {
            var result=GetEventParameter(evt,0x2D2D2D2D,0x686B6964,IntPtr.Zero,8,out var actualSize,out var data);
            if(result!=0||actualSize!=8||data.Signature!=Signature)return NotHandled;pressed?.Invoke(data.Id);return 0;
        }
        catch{return NotHandled;} // 관리 예외를 네이티브 이벤트 루프로 전달하지 않는다.
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;pressed=null;foreach(var id in registrations.Keys.ToArray())Unregister(id);
        if(handler!=IntPtr.Zero){RemoveEventHandler(handler);handler=IntPtr.Zero;}GC.KeepAlive(callback);
    }
    [DllImport(Carbon,CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr GetApplicationEventTarget();
    [DllImport(Carbon,CallingConvention=CallingConvention.Cdecl)] private static extern int InstallEventHandler(IntPtr target,EventHandler callback,nuint count,ref EventTypeSpec type,IntPtr userData,out IntPtr handler);
    [DllImport(Carbon,CallingConvention=CallingConvention.Cdecl)] private static extern int RemoveEventHandler(IntPtr handler);
    [DllImport(Carbon,CallingConvention=CallingConvention.Cdecl)] private static extern int RegisterEventHotKey(uint key,uint modifiers,EventHotKeyId id,IntPtr target,uint options,out IntPtr reference);
    [DllImport(Carbon,CallingConvention=CallingConvention.Cdecl)] private static extern int UnregisterEventHotKey(IntPtr reference);
    [DllImport(Carbon,CallingConvention=CallingConvention.Cdecl)] private static extern int GetEventParameter(IntPtr evt,uint name,uint type,IntPtr actualType,nuint size,out nuint actualSize,out EventHotKeyId value);
}
