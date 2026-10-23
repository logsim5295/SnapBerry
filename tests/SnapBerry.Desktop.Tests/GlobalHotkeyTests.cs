using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Shortcuts;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class GlobalHotkeyTests
{
    public sealed class Backend : IGlobalHotkeyBackend
    {
        public readonly Dictionary<uint,HotkeyChord> Active=[];public HotkeyChord? Conflict;public Action<uint>? Callback;public bool Disposed;public int Installs;
        public void Install(Action<uint> callback){Installs++;Callback=callback;}
        public void Register(uint id,HotkeyChord chord){if(Conflict==chord)throw new IOException("사용 중인 단축키");Active.Add(id,chord);}
        public void Unregister(uint id)=>Active.Remove(id);
        public void Dispose(){Disposed=true;Active.Clear();}
    }
    [Theory] [InlineData("Ctrl+Shift+1",18,4608)] [InlineData("Command+Option+P",35,2304)] [InlineData("Control+F12",111,4096)]
    public void ChordsUseMacVirtualKeysAndNativeModifierMasks(string value,uint key,uint modifiers)
    {var parsed=HotkeyChord.Parse(value);Assert.Equal(key,parsed.KeyCode);Assert.Equal(modifiers,parsed.Modifiers);}
    [Theory] [InlineData("A")] [InlineData("Shift+A")] [InlineData("Ctrl++A")] [InlineData("Ctrl+Shift")] [InlineData("Ctrl+F25")] [InlineData("Ctrl+Ctrl+1")]
    public void InvalidChordsAreRejectedBeforeNativeRegistration(string value)=>Assert.Throws<ArgumentException>(()=>HotkeyChord.Parse(value));
    [Fact] public void DuplicateChordsDoNotReplaceTheExistingRegistration()
    {
        var backend=new Backend();using var service=new GlobalHotkeyService(backend);service.Apply(new());var original=backend.Active.ToArray();
        Assert.Throws<ArgumentException>(()=>service.Apply(new(){Window="Control+Shift+1"}));Assert.Equal(original,backend.Active.ToArray());
    }
    [Fact] public void ConflictRestoresOldBindingsAndDoesNotLeakPartialRegistrations()
    {
        var backend=new Backend();using var service=new GlobalHotkeyService(backend);service.Apply(new());var original=backend.Active.ToArray();backend.Conflict=HotkeyChord.Parse("Ctrl+Alt+2");
        Assert.Throws<IOException>(()=>service.Apply(new(){Region="Ctrl+Alt+1",Window="Ctrl+Alt+2",Display="Ctrl+Alt+3"}));Assert.Equal(original.OrderBy(x=>x.Key),backend.Active.OrderBy(x=>x.Key));Assert.Equal(1,backend.Installs);
    }
    [Fact] public void DisableAndDisposeStopDispatchAndReleaseEveryRegistration()
    {
        var backend=new Backend();var service=new GlobalHotkeyService(backend);var modes=new List<DesktopCaptureMode>();service.Pressed+=modes.Add;service.Apply(new());
        backend.Callback!(1);backend.Callback(2);backend.Callback(3);Assert.Equal(new[]{DesktopCaptureMode.Region,DesktopCaptureMode.Window,DesktopCaptureMode.Display},modes);
        service.Apply(new(){Enabled=false});backend.Callback(1);Assert.Equal(3,modes.Count);Assert.Empty(backend.Active);
        service.Apply(new());service.Dispose();backend.Callback(2);Assert.Equal(3,modes.Count);Assert.True(backend.Disposed);Assert.Empty(backend.Active);
    }
    [Fact] public void EventIdAndTypeStructsMatchTheCarbonCAbi()
    {Assert.Equal(8,System.Runtime.InteropServices.Marshal.SizeOf<CarbonHotkeyBackend.EventTypeSpec>());Assert.Equal(8,System.Runtime.InteropServices.Marshal.SizeOf<CarbonHotkeyBackend.EventHotKeyId>());}
    [Fact] public void CarbonByteCountAndItemCountParametersUseNativeWidth()
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
        var parameters=typeof(CarbonHotkeyBackend).GetMethod("GetEventParameter",flags)!.GetParameters();
        Assert.Equal(typeof(nuint),parameters[4].ParameterType);
        Assert.Equal(typeof(nuint).MakeByRefType(),parameters[5].ParameterType);
        Assert.Equal(typeof(nuint),typeof(CarbonHotkeyBackend).GetMethod("InstallEventHandler",flags)!.GetParameters()[2].ParameterType);
    }
}
