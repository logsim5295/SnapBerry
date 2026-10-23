using SnapBerry.Core;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 단축키 등록·교체·충돌 시 이전 값 유지와 캡처 모드 해석을 검증한다.
public class HotkeyTests
{
    [Fact] public void PrintScreenConflictExplainsHowToReleaseOtherCaptureProgram()
    {var backend=new Backend{Fail=true};using var service=new HotkeyService(backend);var result=service.CheckAvailability("PrintScreen");Assert.False(result.Success);Assert.Contains("다른 캡처 프로그램",result.Error);Assert.Empty(backend.Registered);}
    [Fact] public void AvailabilityChecksReleaseTheirTemporaryRegistration()
    {var backend=new Backend();using var service=new HotkeyService(backend);Assert.True(service.CheckAvailability("Ctrl+Alt+4").Success);Assert.Empty(backend.Registered);Assert.Empty(service.Bindings);backend.Fail=true;Assert.False(service.CheckAvailability("Ctrl+Alt+4").Success);Assert.Empty(backend.Registered);}
    [Fact] public void SuspendingHotkeysAllowsSwappingAndRestoringBindings()
    {
        var backend=new Backend();using var service=new HotkeyService(backend);service.TryReplace(CaptureMode.Region,"Ctrl+Shift+1");service.TryReplace(CaptureMode.Window,"Ctrl+Shift+2");var ids=backend.Registered.ToArray();service.UnregisterAll();Assert.Empty(backend.Registered);Assert.All(ids,id=>Assert.Null(service.Resolve(id)));
        Assert.True(service.TryReplace(CaptureMode.Region,"Ctrl+Shift+2").Success);Assert.True(service.TryReplace(CaptureMode.Window,"Ctrl+Shift+1").Success);service.UnregisterAll();Assert.True(service.TryReplace(CaptureMode.Region,"Ctrl+Shift+1").Success);Assert.True(service.TryReplace(CaptureMode.Window,"Ctrl+Shift+2").Success);
    }
    private sealed class Backend : IHotkeyBackend
    {public bool Fail;public HashSet<int> Registered=[];public bool Register(int id,uint modifiers,uint key){if(Fail)return false;Registered.Add(id);return true;}public void Unregister(int id)=>Registered.Remove(id);}
    [Fact] public void ConflictingReplacementKeepsOldBinding()
    {var backend=new Backend();using var service=new HotkeyService(backend);Assert.True(service.TryReplace(CaptureMode.Region,"Ctrl+Shift+1").Success);backend.Fail=true;Assert.False(service.TryReplace(CaptureMode.Region,"Ctrl+Shift+9").Success);Assert.Equal("Ctrl+Shift+1",service.Bindings[CaptureMode.Region]);Assert.Single(backend.Registered);}
    [Fact] public void DuplicateBindingsAreRejected()
    {var backend=new Backend();using var service=new HotkeyService(backend);Assert.True(service.TryReplace(CaptureMode.Region,"Ctrl+Shift+1").Success);Assert.False(service.TryReplace(CaptureMode.Window,"Ctrl+Shift+1").Success);Assert.Single(backend.Registered);}
    [Fact] public void DisposeReleasesRegistrations()
    {var backend=new Backend();var service=new HotkeyService(backend);service.TryReplace(CaptureMode.Region,"Ctrl+Shift+1");service.Dispose();Assert.Empty(backend.Registered);}
}
