using SnapBerry.App.Capture;
using SnapBerry.App.Shell;
using Xunit;

namespace SnapBerry.Windows.Tests;

// OS 창 대신 합성 완료를 지연시키는 경계를 사용해 잔상이 있는 프레임을 읽지 않는지 검증한다.
public class CaptureSessionTests
{
    [Fact]
    public async Task CaptureWaitsForHiddenWindowAndCompositionBeforeReadingPixels()
    {
        var desktop=new Desktop();
        using(var session=new CaptureSession(desktop))
        {
            var capture=session.CaptureAsync();
            Assert.False(capture.IsCompleted);
            Assert.Equal(0,desktop.Reads);
            desktop.Rendered.SetResult();
            Assert.Same(desktop.Frame,await capture);
            Assert.Equal(1,desktop.Reads);
            Assert.True(desktop.Suppressed);
        }
        Assert.False(desktop.Suppressed);
    }

    [Theory]
    [InlineData("hide")]
    [InlineData("render")]
    [InlineData("composition")]
    [InlineData("visible")]
    [InlineData("capture")]
    public async Task PreparationFailureRestoresEditorAndTransitionsAndDoesNotSaveAnImage(string failure)
    {
        var desktop=new Desktop{Failure=failure};var shown=false;var busy=false;var opened=false;
        desktop.Rendered.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>
        {
            using var session=new CaptureSession(desktop);
            await AppHost.RunCaptureAsync(async()=>await session.CaptureAsync(),_=>{opened=true;return Task.CompletedTask;},()=>shown=true,value=>busy=value);
        });
        Assert.True(shown);
        Assert.False(busy);
        Assert.False(opened);
        Assert.False(desktop.Suppressed);
        Assert.Equal(failure=="capture"?1:0,desktop.Reads);
    }

    [Fact]
    public async Task CancelledSelectionKeepsEditorHiddenAndRestoresTransitions()
    {
        var desktop=new Desktop();var shown=false;var busy=false;desktop.Rendered.SetResult();
        using(var session=new CaptureSession(desktop))
            await AppHost.RunCaptureAsync(async()=>{await session.CaptureAsync();return null;},_=>throw new InvalidOperationException(),()=>shown=true,value=>busy=value);
        Assert.False(shown);Assert.False(busy);Assert.False(desktop.Suppressed);
    }

    private sealed class Desktop : ICaptureDesktop
    {
        public TaskCompletionSource Rendered {get;}=new();
        public CaptureFrame Frame {get;}=new(null!,new(0,0,10,10));
        public string? Failure {get;init;}
        public bool Suppressed {get;private set;}
        public int Reads {get;private set;}
        private bool hidden,composed;
        public bool IsEditorVisible=>!hidden||Failure=="visible";
        public IDisposable SuppressTransitions(){Suppressed=true;return new Restore(()=>Suppressed=false);}
        public void HideEditor(){if(!Suppressed)throw new InvalidOperationException("Transitions are active.");Fail("hide");hidden=true;}
        public async Task WaitForRenderAsync(){await Rendered.Task;Fail("render");}
        public void FlushComposition(){if(!Rendered.Task.IsCompletedSuccessfully)throw new InvalidOperationException("Render is pending.");Fail("composition");composed=true;}
        public CaptureFrame CaptureDesktop()
        {
            Reads++;
            if(!Suppressed||!hidden||!composed)throw new InvalidOperationException("Stale desktop pixels.");
            Fail("capture");return Frame;
        }
        private void Fail(string stage){if(Failure==stage)throw new InvalidOperationException(stage);}
    }
    private sealed class Restore(Action action) : IDisposable
    {public void Dispose()=>action();}
}
