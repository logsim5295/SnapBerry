using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Editing;
using Xunit;
using SkiaSharp;

namespace SnapBerry.Desktop.Tests;

public class CaptureTests
{
    [Fact] public async Task FullCaptureIncludesNegativeOriginsAndMixedRetinaDisplaysWithoutClipping()
    {
        var paths=new List<string>();
        var service=new MacCaptureService(async(start,token)=>
        {
            var index=start.ArgumentList.IndexOf("-R");Assert.True(index>=0);var rectangle=start.ArgumentList[index+1];
            Assert.Contains(rectangle,new[]{"0,0,4,3","-3,-2,3,2"});
            var path=start.ArgumentList.Last();paths.Add(path);
            await File.WriteAllBytesAsync(path,rectangle=="0,0,4,3"?EditorTests.Png(8,6,SKColors.Blue):EditorTests.Png(3,2,SKColors.Red),token);
            return new CaptureProcessResult(0,"");
        },new Permission(true),()=>[new(1,0,0,4,3,2),new(2,-3,-2,3,2,1)]);
        using var image=SKBitmap.Decode(await service.CaptureAsync(DesktopCaptureMode.Display,TestContext.Current.CancellationToken));
        Assert.Equal(14,image.Width);Assert.Equal(10,image.Height);
        Assert.Equal(SKColors.Red,image.GetPixel(0,0));Assert.Equal(SKColors.Red,image.GetPixel(5,3));
        Assert.Equal(SKColors.Blue,image.GetPixel(6,4));Assert.Equal(SKColors.Blue,image.GetPixel(13,9));
        Assert.Equal((byte)0,image.GetPixel(13,0).Alpha);Assert.All(paths,path=>Assert.False(File.Exists(path)));
    }
    [Fact] public async Task FullCaptureFailureCleansEveryTemporaryImage()
    {
        var paths=new List<string>();
        var service=new MacCaptureService(async(start,token)=>
        {
            var path=start.ArgumentList.Last();paths.Add(path);await File.WriteAllBytesAsync(path,EditorTests.Png(),token);
            return new CaptureProcessResult(paths.Count==1?0:1,"permission denied");
        },new Permission(true),()=>[new(1,0,0,80,60,1),new(2,80,0,80,60,1)]);
        await Assert.ThrowsAsync<IOException>(()=>service.CaptureAsync(DesktopCaptureMode.Display,TestContext.Current.CancellationToken));
        Assert.Equal(2,paths.Count);Assert.All(paths,path=>Assert.False(File.Exists(path)));
    }
    [Fact] public async Task FullCaptureCancellationCleansPartialFilesAndDoesNotCaptureRemainingScreens()
    {
        using var cancellation=new CancellationTokenSource();var paths=new List<string>();
        var service=new MacCaptureService(async(start,token)=>
        {
            var path=start.ArgumentList.Last();paths.Add(path);await File.WriteAllBytesAsync(path,EditorTests.Png(),token);
            cancellation.Cancel();return new CaptureProcessResult(0,"");
        },new Permission(true),()=>[new(1,0,0,80,60,1),new(2,80,0,80,60,1)]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.CaptureAsync(DesktopCaptureMode.Display,cancellation.Token));
        Assert.Single(paths);Assert.All(paths,path=>Assert.False(File.Exists(path)));
    }
    [Theory] [InlineData(100000)] [InlineData(1000000000)]
    public async Task OversizedVirtualDesktopIsRejectedBeforeCapturing(int size)
    {
        var invoked=false;var service=new MacCaptureService((_,_)=>{invoked=true;return Task.FromResult(new CaptureProcessResult(0,""));},new Permission(true),()=>[new(1,0,0,size,size,2)]);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.CaptureAsync(DesktopCaptureMode.Display,TestContext.Current.CancellationToken));Assert.False(invoked);
    }
    [Fact] public async Task MirroredDisplaysAreNotCapturedTwice()
    {
        var calls=0;var service=new MacCaptureService(async(start,token)=>{calls++;Assert.Contains("-m",start.ArgumentList);await File.WriteAllBytesAsync(start.ArgumentList.Last(),EditorTests.Png(),token);return new CaptureProcessResult(0,"");},new Permission(true),()=>[new(1,0,0,80,60,1),new(2,0,0,80,60,1)]);
        Assert.NotNull(await service.CaptureAsync(DesktopCaptureMode.Display,TestContext.Current.CancellationToken));Assert.Equal(1,calls);
    }
    private sealed class Permission(bool granted,bool grantOnRequest=false) : IScreenRecordingPermission
    {
        public int Requests;public bool HasAccess()=>granted;
        public void RequestAccess(){Requests++;granted|=grantOnRequest;}
    }
    [Fact] public async Task MissingScreenPermissionRequestsConsentWithoutCapturingBlackPixels()
    {
        var permission=new Permission(false);var invoked=false;
        var service=new MacCaptureService((_,_)=>{invoked=true;return Task.FromResult(new CaptureProcessResult(0,""));},permission);
        await Assert.ThrowsAsync<IOException>(()=>service.CaptureAsync(DesktopCaptureMode.Display,TestContext.Current.CancellationToken));
        Assert.Equal(1,permission.Requests);Assert.False(invoked);
    }
    [Theory] [InlineData(true,0)] [InlineData(false,1)]
    public async Task CaptureRunsOnlyAfterScreenPermissionIsGranted(bool initiallyGranted,int requests)
    {
        var permission=new Permission(initiallyGranted,true);
        var service=new MacCaptureService(async(start,token)=>{await File.WriteAllBytesAsync(start.ArgumentList.Last(),EditorTests.Png(),token);return new CaptureProcessResult(0,"");},permission);
        Assert.NotNull(await service.CaptureAsync(DesktopCaptureMode.Display,TestContext.Current.CancellationToken));
        Assert.Equal(requests,permission.Requests);
    }
    [Theory] [InlineData(DesktopCaptureMode.Region,"-s")] [InlineData(DesktopCaptureMode.Window,"-w")] [InlineData(DesktopCaptureMode.Display,"-m")]
    public async Task CaptureUsesArgumentListAndRemovesTheTemporaryFile(DesktopCaptureMode mode,string argument)
    {
        string? output=null;
        var service=new MacCaptureService(async (start,token)=>
        {
            Assert.Equal("/usr/sbin/screencapture",start.FileName);Assert.False(start.UseShellExecute);Assert.Contains(argument,start.ArgumentList);
            output=start.ArgumentList.Last();await File.WriteAllBytesAsync(output,EditorTests.Png(),token);return new CaptureProcessResult(0,"");
        });
        Assert.NotNull(await service.CaptureAsync(mode,TestContext.Current.CancellationToken));Assert.NotNull(output);Assert.False(File.Exists(output));
    }
    [Fact] public async Task CancelPreservesTheCurrentDocumentAndRestoresTheWindow()
    {
        var service=new MacCaptureService((_,_)=>Task.FromResult(new CaptureProcessResult(1,"")));
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());var document=editor.Document;var calls=new List<string>();
        var coordinator=new CaptureCoordinator(service);
        var captured=await coordinator.CaptureAsync(DesktopCaptureMode.Region,editor,()=>{calls.Add("flush");return Task.CompletedTask;},()=>calls.Add("hide"),()=>calls.Add("restore"),TestContext.Current.CancellationToken);
        Assert.False(captured);Assert.Same(document,editor.Document);Assert.Equal(new[]{"flush","hide","restore"},calls);
    }
    [Fact] public async Task PermissionFailureRestoresTheWindowAndKeepsTheDocument()
    {
        var service=new MacCaptureService((_,_)=>Task.FromResult(new CaptureProcessResult(1,"could not create image from display")));
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());var document=editor.Document;var restored=false;
        await Assert.ThrowsAsync<IOException>(()=>new CaptureCoordinator(service).CaptureAsync(DesktopCaptureMode.Display,editor,()=>Task.CompletedTask,()=>{},()=>restored=true,TestContext.Current.CancellationToken));
        Assert.True(restored);Assert.Same(document,editor.Document);
    }
    [Fact] public async Task FailedFlushNeverHidesTheWindowOrStartsCapture()
    {
        var invoked=false;var hidden=false;var service=new MacCaptureService((_,_)=>{invoked=true;return Task.FromResult(new CaptureProcessResult(0,""));});
        using var editor=new DesktopEditor();
        await Assert.ThrowsAsync<IOException>(()=>new CaptureCoordinator(service).CaptureAsync(DesktopCaptureMode.Region,editor,()=>throw new IOException(),()=>hidden=true,()=>{},TestContext.Current.CancellationToken));
        Assert.False(invoked);Assert.False(hidden);
    }
}
