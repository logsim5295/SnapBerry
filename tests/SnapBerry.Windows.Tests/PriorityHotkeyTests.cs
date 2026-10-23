using System.Windows;
using System.Windows.Controls;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 외부 점유 단축키의 우선 처리, 시스템 예약 조합 차단과 반복 입력 억제를 검증한다.
public class PriorityHotkeyTests
{
    [Theory] [InlineData("Ctrl+Alt+Delete")] [InlineData("Win+L")] [InlineData("Ctrl+F12")]
    public void ReservedGesturesAreRejectedBeforeNativeRegistration(string binding)=>Assert.Throws<ArgumentException>(()=>HotkeyService.Parse(binding));
    [Fact] public void FailedOverridePreservesPreviousBinding()
    {var backend=new Backend();using var service=new HotkeyService(backend);Assert.True(service.TryReplace(CaptureMode.Region,"PrintScreen").Success);backend.Available=false;Assert.False(service.TryReplace(CaptureMode.Region,"Ctrl+Alt+A").Success);Assert.Equal("PrintScreen",service.Bindings[CaptureMode.Region]);Assert.Single(backend.Priority);}
    [Fact] public void NativePriorityHookCanBeInstalledAndReleased()=>Sta.Run(()=>
    {
        var w=new Window();try{w.Show();using var backend=new NativeHotkeyBackend(new System.Windows.Interop.WindowInteropHelper(w).Handle);Assert.True(backend.RegisterPriority(999,7,0x87));backend.Unregister(999);Assert.True(backend.RegisterPriority(1000,7,0x87));backend.Unregister(1000);}finally{w.Close();}
    });
    private sealed class Backend : IHotkeyBackend,IPriorityHotkeyBackend
    {
        public bool Available=true;public HashSet<int> Priority=[];
        public bool Register(int id,uint modifiers,uint key)=>false;
        public bool CanOverride(uint modifiers,uint key)=>Available&&PriorityHotkeyRouter.IsSupported(modifiers,key);
        public bool RegisterPriority(int id,uint modifiers,uint key){if(!CanOverride(modifiers,key))return false;Priority.Add(id);return true;}
        public void Unregister(int id)=>Priority.Remove(id);
    }
    [Fact] public void ConflictCanBeSavedWithWarningAndReleased()
    {
        var backend=new Backend();using var service=new HotkeyService(backend);var check=service.CheckAvailability("PrintScreen");Assert.True(check.Success);Assert.Contains("SnapBerry가 우선 사용",check.Warning);Assert.True(service.TryReplace(CaptureMode.Region,"PrintScreen").Success);Assert.Single(backend.Priority);Assert.Equal(CaptureMode.Region,service.Resolve(backend.Priority.Single()));service.UnregisterAll();Assert.Empty(backend.Priority);
    }
    [Fact] public void ReservedCombinationAndUnavailableHookStayBlocked()
    {var backend=new Backend();using var service=new HotkeyService(backend);Assert.False(service.CheckAvailability("Ctrl+Alt+Delete").Success);Assert.False(service.CheckAvailability("Win+L").Success);backend.Available=false;Assert.False(service.CheckAvailability("PrintScreen").Success);}
    [Fact] public void RouterSuppressesOnlyConfiguredKeysWithoutRepeats()
    {
        var router=new PriorityHotkeyRouter();router.Add(10,2,0x41);Assert.False(router.Process(0x100,0x42,2,out _));Assert.True(router.Process(0x100,0x41,2,out var id));Assert.Equal(10,id);Assert.True(router.Process(0x100,0x41,2,out id));Assert.Null(id);Assert.True(router.Process(0x101,0x41,0,out id));Assert.Null(id);Assert.False(router.Process(0x100,0x41,0,out _));router.Remove(10);Assert.False(router.Process(0x100,0x41,2,out _));
    }
    [Fact] public void OverrideNoticeDoesNotDisableSave()=>Sta.Run(()=>
    {
        var w=new SettingsWindow(new(),inspectBinding:_=>new(true,Warning:HotkeyService.PriorityWarning));try{Assert.True(((Button)w.FindName("SaveSettingsButton")).IsEnabled);Assert.Equal(HotkeyService.PriorityWarning,((TextBlock)w.FindName("RegionHotkeyError")).Text);}finally{w.Close();}
    });
}
