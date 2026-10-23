using System.Diagnostics;
using System.Xml.Linq;
using SnapBerry.Desktop.Shell;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class PlatformParityTests
{
    private sealed class StartupFixture : IDisposable
    {
        public readonly string Home=Path.Combine(Path.GetTempPath(),"SnapBerry-login-"+Guid.NewGuid());
        public readonly List<ProcessStartInfo> Calls=[];
        public string Executable=>Path.Combine(Home,"Applications & Tools","SnapBerry <test>.app","Contents","MacOS","SnapBerry");
        public string Plist=>Path.Combine(Home,"Library","LaunchAgents","com.ynsim.snapberry.plist");
        public MacLoginStartupService Service(Func<ProcessStartInfo,LoginStartupProcessResult>? runner=null)=>new(Home,Executable,501,runner??(start=>{Calls.Add(start);return new(0,"","disabled services = {\n}");}));
        public void Dispose(){if(Directory.Exists(Home))Directory.Delete(Home,true);}
    }
    [Fact] public void LoginStartupEscapesExecutablePathAndStartsAtNextLoginWithoutLaunchingNow()
    {
        using var fixture=new StartupFixture();var service=fixture.Service();service.SetEnabled(true);
        Assert.True(service.IsEnabled);
        var plist=XDocument.Load(fixture.Plist);var elements=plist.Root!.Element("dict")!.Elements().ToArray();
        var arguments=elements[Array.FindIndex(elements,e=>e.Name=="key"&&e.Value=="ProgramArguments")+1];
        Assert.Equal(fixture.Executable,Assert.Single(arguments.Elements()).Value);
        Assert.Contains(elements,e=>e.Name=="true");
        var enable=Assert.Single(fixture.Calls,c=>c.ArgumentList[0]=="enable");
        Assert.Equal("/bin/launchctl",enable.FileName);Assert.False(enable.UseShellExecute);
        Assert.Equal(new[]{"enable","gui/501/com.ynsim.snapberry"},enable.ArgumentList);
        Assert.DoesNotContain(fixture.Calls,c=>c.ArgumentList[0] is "bootstrap" or "kickstart" or "bootout");
    }
    [Fact] public void DisablingLoginStartupOnlyRemovesTheAppOwnedPlist()
    {
        using var fixture=new StartupFixture();var service=fixture.Service();service.SetEnabled(true);
        var other=Path.Combine(Path.GetDirectoryName(fixture.Plist)!,"other.plist");File.WriteAllText(other,"other app");
        service.SetEnabled(false);Assert.False(service.IsEnabled);Assert.False(File.Exists(fixture.Plist));Assert.Equal("other app",File.ReadAllText(other));
        Assert.Contains(fixture.Calls,c=>c.ArgumentList.SequenceEqual(new[]{"disable","gui/501/com.ynsim.snapberry"}));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void LoginStartupRefusesToOverwriteOrRemoveAnUnownedPlist(bool enable)
    {
        using var fixture=new StartupFixture();Directory.CreateDirectory(Path.GetDirectoryName(fixture.Plist)!);File.WriteAllText(fixture.Plist,"<plist><dict><key>Label</key><string>somebody.else</string></dict></plist>");
        var original=File.ReadAllBytes(fixture.Plist);var service=fixture.Service();Assert.False(service.IsEnabled);
        Assert.Throws<IOException>(()=>service.SetEnabled(enable));Assert.Equal(original,File.ReadAllBytes(fixture.Plist));Assert.Empty(fixture.Calls);
    }
    [Fact] public void FailedEnableRestoresThePreviousConfigurationAndLeavesNoTemporaryFiles()
    {
        using var fixture=new StartupFixture();fixture.Service().SetEnabled(true);var original=File.ReadAllBytes(fixture.Plist);
        var service=fixture.Service(start=>start.ArgumentList[0]=="enable"?new(1,"permission denied"):new(0,""));
        Assert.Throws<IOException>(()=>service.SetEnabled(true));Assert.Equal(original,File.ReadAllBytes(fixture.Plist));Assert.Single(Directory.GetFiles(Path.GetDirectoryName(fixture.Plist)!));
    }
    [Fact] public void FailedFirstEnableDoesNotLeaveAnEnabledPlist()
    {
        using var fixture=new StartupFixture();var service=fixture.Service(_=>new(1,"permission denied"));
        Assert.Throws<IOException>(()=>service.SetEnabled(true));Assert.False(File.Exists(fixture.Plist));Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(fixture.Plist)!));
    }
    [Fact] public void FailedDisablePreservesTheEnabledConfiguration()
    {
        using var fixture=new StartupFixture();fixture.Service().SetEnabled(true);var original=File.ReadAllBytes(fixture.Plist);
        var service=fixture.Service(_=>new(1,"permission denied"));Assert.Throws<IOException>(()=>service.SetEnabled(false));Assert.Equal(original,File.ReadAllBytes(fixture.Plist));
    }
    [Fact] public void SystemDisabledOverrideIsReflectedInTheStartupSetting()
    {
        using var fixture=new StartupFixture();fixture.Service().SetEnabled(true);
        var service=fixture.Service(_=>new(0,"", "disabled services = {\n \"com.ynsim.snapberry\" => true\n}"));Assert.False(service.IsEnabled);
    }
    [Fact] public void UnrecognizedSystemStartupStateDoesNotSilentlyClaimStartupIsEnabled()
    {
        using var fixture=new StartupFixture();fixture.Service().SetEnabled(true);
        var service=fixture.Service(_=>new(0,"","unexpected future launchctl output"));
        Assert.Throws<IOException>(()=>service.IsEnabled);
    }
    [Fact] public void SymlinkedLaunchAgentsDirectoryIsRejectedBeforeWritingOrRunningCommands()
    {
        using var fixture=new StartupFixture();var target=Path.Combine(fixture.Home,"other");Directory.CreateDirectory(target);Directory.CreateDirectory(Path.Combine(fixture.Home,"Library"));
        // Windows does not allow unprivileged symbolic links on every developer machine.
        if(OperatingSystem.IsWindows())return;
        Directory.CreateSymbolicLink(Path.Combine(fixture.Home,"Library","LaunchAgents"),target);
        Assert.Throws<IOException>(()=>fixture.Service().SetEnabled(true));Assert.Empty(Directory.GetFiles(target));Assert.Empty(fixture.Calls);
    }
    [Fact] public void ProductionLoginStartupIsGuardedOnOtherOperatingSystems()
    {
        if(OperatingSystem.IsMacOS())return;
        Assert.Throws<PlatformNotSupportedException>(()=>new MacLoginStartupService());
    }
}
