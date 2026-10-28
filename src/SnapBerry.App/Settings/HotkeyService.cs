using SnapBerry.Core;
using System.Windows.Input;
using SnapBerry.App.Interop;
using System.Runtime.InteropServices;
namespace SnapBerry.App.Settings;
// OS 등록을 추상화해 실제 전역 단축키를 점유하지 않고 충돌·롤백을 테스트할 수 있게 한다.
public interface IHotkeyBackend {bool Register(int id,uint modifiers,uint key);void Unregister(int id);}
public interface IPriorityHotkeyBackend
{bool CanOverride(uint modifiers,uint key);bool RegisterPriority(int id,uint modifiers,uint key);}
public sealed record HotkeyRegistrationResult(bool Success,string? Error=null,string? Warning=null);
// 캡처 모드별 단축키를 관리한다. 앱 내부 중복은 거부하고 외부 충돌은 지원 조합만 우선 처리한다.
public sealed class HotkeyService(IHotkeyBackend backend) : IDisposable
{
    public const string PriorityWarning="다른 프로그램에서 사용 중인 단축키입니다. 저장하면 SnapBerry가 우선 사용합니다.";
    private readonly Dictionary<CaptureMode,int> ids=[];private readonly Dictionary<CaptureMode,(uint,uint)> combinations=[];private int nextId=100;
    public Dictionary<CaptureMode,string> Bindings {get;}=[];
    public HotkeyRegistrationResult TryReplace(CaptureMode mode,string binding)
    {
        // 새 조합 등록이 성공한 뒤 이전 조합을 해제하므로 등록 실패 시 기존 단축키가 유지된다.
        try
        {
            var combo=Parse(binding);
            if(combinations.Any(x=>x.Key!=mode&&x.Value==combo))return new(false,"다른 캡처 모드에서 사용 중인 단축키입니다.");
            if(combinations.TryGetValue(mode,out var same)&&same==combo)return new(true);
            var id=nextId++;var result=Register(id,combo);if(!result.Success)return result;
            if(ids.TryGetValue(mode,out var old))backend.Unregister(old);ids[mode]=id;combinations[mode]=combo;Bindings[mode]=binding.Trim();return result;
        }
        catch(ArgumentException e){return new(false,e.Message);}
    }
    public CaptureMode? Resolve(int id)=>ids.Where(x=>x.Value==id).Select(x=>(CaptureMode?)x.Key).FirstOrDefault();
    public static (uint,uint) Parse(string binding)
    {
        // 문자열 표현을 Win32 수정키 비트와 가상 키로 바꾼다. OS 예약 조합은 허용하지 않는다.
        var tokens=binding.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);if(tokens.Length==0)throw new ArgumentException("단축키를 입력해 주세요.");
        uint modifiers=0;foreach(var token in tokens[..^1])modifiers|=token.ToLowerInvariant() switch{"ctrl"=>2u,"shift"=>4u,"alt"=>1u,"win"=>8u,_=>throw new ArgumentException("Ctrl, Shift, Alt, Win만 사용할 수 있습니다.")};
        var keyName=tokens[^1];Key key;if(keyName.Equals("PrintScreen",StringComparison.OrdinalIgnoreCase))key=Key.Snapshot;else if(keyName.Length==1&&char.IsDigit(keyName[0]))key=(Key)((int)Key.D0+keyName[0]-'0');else if(!Enum.TryParse(keyName,true,out key))throw new ArgumentException("키 이름을 확인해 주세요.");
        if(tokens.Length==1&&key!=Key.Snapshot)throw new ArgumentException("Ctrl+Shift+1 형식으로 입력해 주세요.");
        var vk=KeyInterop.VirtualKeyFromKey(key);if(vk==0)throw new ArgumentException("지원하지 않는 키입니다.");if(PriorityHotkeyRouter.IsReserved(modifiers,(uint)vk))throw new ArgumentException("Windows에서 예약한 조합은 캡처 단축키로 사용할 수 없습니다.");return (modifiers,(uint)vk);
    }
    public HotkeyRegistrationResult CheckAvailability(string binding)
    {
        // 실제 저장과 같은 경로로 후크까지 검사하고 임시 등록은 즉시 해제한다.
        try
        {
            var combo=Parse(binding);var id=nextId++;
            var result=Register(id,combo);if(result.Success)backend.Unregister(id);return result;
        }
        catch(ArgumentException ex){return new(false,ex.Message);}
    }
    private HotkeyRegistrationResult Register(int id,(uint Modifiers,uint Key) combo)
    {
        if(backend.Register(id,combo.Modifiers|0x4000,combo.Key))
        {
            // Print Screen은 OS 등록 성공 후에도 배경에서 Windows 캡처가 우선할 수 있다.
            // 지원 조합에는 항상 후크를 함께 유지하고, 후크 실패를 저장 성공으로 숨기지 않는다.
            if(combo.Key==0x2C&&PriorityHotkeyRouter.IsSupported(combo.Modifiers,combo.Key)&&backend is IPriorityHotkeyBackend priority&&!priority.RegisterPriority(id,combo.Modifiers,combo.Key))
            {backend.Unregister(id);return new(false,"Print Screen 입력 처리를 준비하지 못했습니다. 다른 캡처 프로그램의 단축키를 해제하거나 다른 조합을 선택해 주세요.");}
            return new(true);
        }
        if(backend is IPriorityHotkeyBackend alternate&&alternate.CanOverride(combo.Modifiers,combo.Key)&&alternate.RegisterPriority(id,combo.Modifiers,combo.Key))return new(true,Warning:PriorityWarning);
        return new(false,combo.Key==0x2C?"Print Screen 키를 다른 캡처 프로그램에서 사용 중이거나 입력 처리를 준비할 수 없습니다. 해당 프로그램의 단축키를 해제하거나 다른 조합을 선택해 주세요.":"다른 프로그램에서 사용 중이거나 우선 처리할 수 없는 단축키입니다.");
    }
    public void UnregisterAll(){foreach(var id in ids.Values)backend.Unregister(id);ids.Clear();Bindings.Clear();combinations.Clear();}
    public void Dispose(){UnregisterAll();if(backend is IDisposable disposable)disposable.Dispose();}
}
// OS 등록과 Print Screen 보강·충돌 대체 후크의 수명을 함께 관리한다.
public sealed class NativeHotkeyBackend(IntPtr hwnd,Action<string>? diagnostic=null) : IHotkeyBackend,IPriorityHotkeyBackend,IDisposable
{
    private readonly HashSet<int> nativeIds=[],priorityIds=[];private PriorityHotkeyHook? hook;private int registrationError;
    public bool Register(int id,uint modifiers,uint key)
    {var success=NativeMethods.RegisterHotKey(hwnd,id,modifiers,key);registrationError=success?0:Marshal.GetLastWin32Error();if(success)nativeIds.Add(id);diagnostic?.Invoke($"hotkey.native id={id} modifiers=0x{modifiers:X} key=0x{key:X} success={success} error={registrationError}");return success;}
    public bool CanOverride(uint modifiers,uint key)
    {
        // 오류 1409는 기존 단축키와의 충돌이다. 권한 등 다른 실패를 무조건 후크로 우회하지 않는다.
        return registrationError==1409&&PriorityHotkeyRouter.IsSupported(modifiers,key);
    }
    public bool RegisterPriority(int id,uint modifiers,uint key)
    {
        if(!PriorityHotkeyRouter.IsSupported(modifiers,key))return false;
        try{hook??=new PriorityHotkeyHook(hwnd);hook.Add(id,modifiers,key);priorityIds.Add(id);diagnostic?.Invoke($"hotkey.hook id={id} modifiers=0x{modifiers:X} key=0x{key:X} success=True");return true;}catch(System.ComponentModel.Win32Exception error){diagnostic?.Invoke($"hotkey.hook id={id} success=False error={error.NativeErrorCode}");return false;}
    }
    public void Unregister(int id){if(nativeIds.Remove(id))NativeMethods.UnregisterHotKey(hwnd,id);if(priorityIds.Remove(id))hook?.Remove(id);if(priorityIds.Count==0){hook?.Dispose();hook=null;}}
    public void Dispose(){foreach(var id in nativeIds.Concat(priorityIds).Distinct().ToArray())Unregister(id);hook?.Dispose();hook=null;}
}
