using SnapBerry.Desktop.Capture;

namespace SnapBerry.Desktop.Shortcuts;

public interface IGlobalHotkeyBackend : IDisposable
{
    void Install(Action<uint> callback);
    void Register(uint id,HotkeyChord chord);
    void Unregister(uint id);
}

public sealed class GlobalHotkeyService(IGlobalHotkeyBackend backend) : IDisposable
{
    private readonly Dictionary<uint,HotkeyChord> active=[];private bool installed,disposed;
    public event Action<DesktopCaptureMode>? Pressed;
    public void Apply(HotkeySettings settings)
    {
        ObjectDisposedException.ThrowIf(disposed,this);var chords=settings.Chords();
        var previous=active.ToArray();if(chords.SequenceEqual(previous.OrderBy(x=>x.Key).Select(x=>x.Value)))return;
        if(chords.Length>0&&!installed){backend.Install(Dispatch);installed=true;}
        Clear();
        try{for(uint i=0;i<chords.Length;i++){backend.Register(i+1,chords[i]);active[i+1]=chords[i];}}
        catch(Exception error)
        {
            Clear();
            try{foreach(var item in previous){backend.Register(item.Key,item.Value);active[item.Key]=item.Value;}}
            catch(Exception rollback){Clear();throw new IOException("단축키 등록과 이전 설정 복구에 실패했습니다. 다른 키 조합으로 다시 설정해 주세요.",new AggregateException(error,rollback));}
            throw new IOException("단축키를 등록하지 못해 이전 설정으로 복구했습니다. 다른 앱에서 사용하는 조합인지 확인해 주세요.",error);
        }
    }
    private void Dispatch(uint id)
    {if(!disposed&&active.ContainsKey(id))Pressed?.Invoke(id switch{1=>DesktopCaptureMode.Region,2=>DesktopCaptureMode.Window,_=>DesktopCaptureMode.Display});}
    private void Clear(){foreach(var id in active.Keys.ToArray()){backend.Unregister(id);active.Remove(id);}}
    public void Dispose(){if(disposed)return;disposed=true;try{Clear();}finally{backend.Dispose();}}
}
