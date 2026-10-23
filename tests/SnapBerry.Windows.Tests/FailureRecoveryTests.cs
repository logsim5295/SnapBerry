using System.Reflection;
using System.Windows;
using SnapBerry.App.Capture;
using SnapBerry.App.Shell;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 창 표시·단축키 등록·레지스트리 변경 없이 취소와 실패 복구를 확인한다.
public class FailureRecoveryTests
{
    private static MethodInfo Method(Type type,string name)
    {var method=type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);Assert.NotNull(method);return method;}
    [Fact] public void ClosingAnUnshownOverlayCompletesCancellationAndReleasesBusy()=>Sta.Run(()=>
    {
        var selected=new TaskCompletionSource<CaptureFrame?>();var busy=false;
        var capture=AppHost.RunCaptureAsync(()=>selected.Task,_=>throw new Exception("열면 안 됩니다."),()=>throw new Exception("표시하면 안 됩니다."),value=>busy=value);
        var ctor=typeof(CaptureCoordinator).Assembly.GetType("SnapBerry.App.Capture.CaptureOverlay")!.GetConstructor([typeof(PixelRect),typeof(System.Windows.Media.Imaging.BitmapSource),typeof(Action)]);
        Assert.NotNull(ctor);
        var overlay=(Window)ctor.Invoke([new PixelRect(0,0,40,30),RenderingTests.Image(40,30),(Action)(()=>selected.TrySetResult(null))]);
        try{Assert.True(busy);Assert.False(overlay.IsVisible);overlay.Close();Assert.True(capture.IsCompletedSuccessfully);Assert.False(busy);}
        finally{overlay.Close();}
    });
    [Theory] [InlineData(false)] [InlineData(true)]
    public void SettingsOperationAlwaysReleasesBusyEvenWhenErrorReportingFails(bool reportingFails)
    {
        var busy=false;var reported=false;var readError=new System.IO.IOException("locked settings");
        var run=Method(typeof(AppHost),"RunSettings");
        Action action=()=>throw readError;Action<bool> setBusy=value=>busy=value;
        Action<Exception> report=error=>{Assert.Same(readError,error);reported=true;if(reportingFails)throw new InvalidOperationException("reporting failed");};
        if(reportingFails)Assert.Throws<TargetInvocationException>(()=>run.Invoke(null,[action,setBusy,report]));
        else run.Invoke(null,[action,setBusy,report]);
        Assert.True(reported);Assert.False(busy);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void FailedSettingsSaveRestoresPreviousStartupState(bool previousValue)
    {
        var apply=Method(typeof(AppHost),"ApplySettings");var previous=new AppSettings{StartWithWindows=previousValue};var proposed=previous with{StartWithWindows=!previousValue};
        var startup=previousValue;var applied=new List<bool>();
        Action<bool> setStartup=value=>{startup=value;applied.Add(value);};Action<AppSettings> save=_=>throw new System.IO.IOException("save failed");
        var error=Assert.Throws<TargetInvocationException>(()=>apply.Invoke(null,[previous,proposed,save,setStartup]));
        Assert.IsType<System.IO.IOException>(error.InnerException);Assert.Equal(previousValue,startup);Assert.Equal(new[]{!previousValue,previousValue},applied);
    }
    [Fact] public void SuccessfulSettingsSaveKeepsTheStartupChange()
    {
        var previous=new AppSettings();var proposed=previous with{StartWithWindows=true};var startup=false;AppSettings? saved=null;
        Method(typeof(AppHost),"ApplySettings").Invoke(null,[previous,proposed,(Action<AppSettings>)(value=>saved=value),(Action<bool>)(value=>startup=value)]);
        Assert.True(startup);Assert.Same(proposed,saved);
    }
    [Fact] public void UnchangedStartupDoesNotWriteTheRegistryOnSaveFailure()
    {
        var previous=new AppSettings();var writes=0;
        var error=Assert.Throws<TargetInvocationException>(()=>Method(typeof(AppHost),"ApplySettings").Invoke(null,[previous,previous,(Action<AppSettings>)(_=>throw new System.IO.IOException()),(Action<bool>)(_=>writes++)]));
        Assert.IsType<System.IO.IOException>(error.InnerException);Assert.Equal(0,writes);
    }
    [Fact] public void RollbackFailureReportsBothErrorsAndStillReleasesBusy()
    {
        var busy=false;Exception? reported=null;var previous=new AppSettings();var writes=0;
        var saveError=new System.IO.IOException("save");var rollbackError=new UnauthorizedAccessException("rollback");
        AppHost.RunSettings(()=>AppHost.ApplySettings(previous,previous with{StartWithWindows=true},_=>throw saveError,_=>{if(++writes==2)throw rollbackError;}),value=>busy=value,error=>reported=error);
        var aggregate=Assert.IsType<AggregateException>(reported);Assert.Equal(new Exception[]{saveError,rollbackError},aggregate.InnerExceptions);Assert.False(busy);
    }
}
