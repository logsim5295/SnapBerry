using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 편집 창은 생성만 하고 표시하지 않는다. 실제 상태 갱신 이벤트를 검증한다.
public class ImageSizeStatusTests
{
    [Fact] public void StatusTracksOpenedCroppedResizedAndUndoneImageAndDisablesWhenBusyOrEmpty()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            var button=Assert.IsType<Button>(window.FindName("ImageSizeButton"));Assert.False(button.IsEnabled);
            window.Model.Open(new(){Width=80,Height=60,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(80,60))});
            Assert.Equal("80 × 60 px",button.Content);Assert.True(button.IsEnabled);
            window.Model.CropTo(new(10,10,40,30));Assert.Equal("40 × 30 px",button.Content);
            window.Model.History!.Resize(new(20,10));Assert.Equal("20 × 10 px",button.Content);
            window.Model.History.Undo();Assert.Equal("40 × 30 px",button.Content);
            window.SetBusy(true);Assert.False(button.IsEnabled);window.SetBusy(false);Assert.True(button.IsEnabled);
            Pump(window.FlushAsync());window.Model.Clear();Assert.False(button.IsEnabled);
        }
        finally{Pump(window.FlushAsync());window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    private static void Pump(Task task)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
    }
}
