using SnapBerry.App.Capture;
using SnapBerry.App.Shell;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 창과 화면 캡처 대신 경계 작업만 대체해 앱의 캡처 완료 분기를 검증한다.
public class CaptureCancellationTests
{
    [Fact]
    public async Task CancelledSelectionDoesNotShowEditorOrOpenCaptureAndReleasesBusyState()
    {
        var visible=false;var busy=false;var opened=false;
        var selection=new TaskCompletionSource<CaptureFrame?>();
        var capture=AppHost.RunCaptureAsync(()=>selection.Task,_=>{opened=true;return Task.CompletedTask;},()=>visible=true,value=>busy=value);
        Assert.True(busy);
        selection.SetResult(null);
        await capture;
        Assert.False(visible);
        Assert.False(opened);
        Assert.False(busy);
    }

    [Fact]
    public async Task CompletedSelectionShowsEditorBeforeOpeningCaptureAndReleasesBusyState()
    {
        var visible=false;var busy=false;CaptureFrame? opened=null;
        var selected=new CaptureFrame(null!,new(0,0,10,10));
        await AppHost.RunCaptureAsync(()=>Task.FromResult<CaptureFrame?>(selected),frame=>
        {
            Assert.True(visible);Assert.True(busy);opened=frame;return Task.CompletedTask;
        },()=>visible=true,value=>busy=value);
        Assert.Same(selected,opened);
        Assert.True(visible);
        Assert.False(busy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaptureOrOpenFailureRestoresEditorAndReleasesBusyState(bool failWhileOpening)
    {
        var visible=false;var busy=false;
        var error=new InvalidOperationException("capture failed");
        var selected=new CaptureFrame(null!,new(0,0,10,10));
        var actual=await Assert.ThrowsAsync<InvalidOperationException>(()=>AppHost.RunCaptureAsync(
            ()=>failWhileOpening?Task.FromResult<CaptureFrame?>(selected):Task.FromException<CaptureFrame?>(error),
            _=>Task.FromException(error),()=>visible=true,value=>busy=value));
        Assert.Same(error,actual);
        Assert.True(visible);
        Assert.False(busy);
    }
}
