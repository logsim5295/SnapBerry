using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// OS 등록 성공만으로 배경 Print Screen 전달을 보장할 수 없는 환경을 재현한다.
public class PrintScreenRegistrationTests
{
    [Theory]
    [InlineData("PrintScreen")]
    [InlineData("Ctrl+PrintScreen")]
    [InlineData("Alt+PrintScreen")]
    [InlineData("Shift+PrintScreen")]
    public void SuccessfulNativePrintScreenRegistrationAlsoUsesPriorityInput(string binding)
    {
        var backend=new Backend();using var service=new HotkeyService(backend);
        Assert.True(service.TryReplace(CaptureMode.Region,binding).Success);
        Assert.Single(backend.Priority);
        Assert.Equal(CaptureMode.Region,service.Resolve(backend.Priority.Single()));
        service.UnregisterAll();Assert.Empty(backend.Native);Assert.Empty(backend.Priority);
    }

    [Fact]
    public void FailedPrintScreenHookRollsBackNewRegistrationAndKeepsPreviousBinding()
    {
        var backend=new Backend();using var service=new HotkeyService(backend);
        service.TryReplace(CaptureMode.Region,"Ctrl+Shift+1");backend.HookAvailable=false;
        var result=service.TryReplace(CaptureMode.Region,"PrintScreen");
        Assert.False(result.Success);Assert.Contains("Print Screen",result.Error);
        Assert.Equal("Ctrl+Shift+1",service.Bindings[CaptureMode.Region]);
        Assert.Single(backend.Native);Assert.Empty(backend.Priority);
    }

    [Fact]
    public void AvailabilityChecksActualHookAndReleasesBothTemporaryRegistrations()
    {
        var backend=new Backend{HookAvailable=false};using var service=new HotkeyService(backend);
        Assert.False(service.CheckAvailability("PrintScreen").Success);
        Assert.Empty(backend.Native);Assert.Empty(backend.Priority);
        backend.HookAvailable=true;
        Assert.True(service.CheckAvailability("PrintScreen").Success);
        Assert.Equal(2,backend.HookAttempts);Assert.Empty(backend.Native);Assert.Empty(backend.Priority);
    }

    [Theory]
    [InlineData("Ctrl+Shift+1")]
    [InlineData("Win+PrintScreen")]
    public void OrdinaryAndWindowsGesturesKeepNativeRegistration(string binding)
    {
        var backend=new Backend{HookAvailable=false};using var service=new HotkeyService(backend);
        Assert.True(service.TryReplace(CaptureMode.Region,binding).Success);
        Assert.Single(backend.Native);Assert.Equal(0,backend.HookAttempts);
    }

    private sealed class Backend : IHotkeyBackend,IPriorityHotkeyBackend
    {
        public bool HookAvailable=true;
        public int HookAttempts;
        public HashSet<int> Native=[];
        public HashSet<int> Priority=[];
        public bool Register(int id,uint modifiers,uint key){Native.Add(id);return true;}
        public bool CanOverride(uint modifiers,uint key)=>false;
        public bool RegisterPriority(int id,uint modifiers,uint key)
        {HookAttempts++;if(!HookAvailable)return false;Priority.Add(id);return true;}
        public void Unregister(int id){Native.Remove(id);Priority.Remove(id);}
    }
}
