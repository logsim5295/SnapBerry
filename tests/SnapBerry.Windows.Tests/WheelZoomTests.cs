using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 휠 내림 확대·올림 축소와 작업 중 확대 차단 및 제스처 취소를 검증한다.
public class WheelZoomTests
{
    [Fact] public void WheelDownZoomsInAndWheelUpZoomsOutWithoutScrolling()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
            var scroll=(ScrollViewer)w.FindName("ImageScroll");var down=Wheel(scroll,-120);Assert.Equal(1.25,w.Model.Zoom,8);Assert.True(down.Handled);
            Wheel(scroll,120);Assert.Equal(1,w.Model.Zoom,8);
            Wheel(scroll,-12000);Assert.Equal(4,w.Model.Zoom);Wheel(scroll,12000);Assert.Equal(.1,w.Model.Zoom);
            w.SetBusy(true);Wheel(scroll,-120);Assert.Equal(.1,w.Model.Zoom);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    private static MouseWheelEventArgs Wheel(UIElement target,int delta)
    {var e=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,delta){RoutedEvent=UIElement.PreviewMouseWheelEvent};target.RaiseEvent(e);return e;}
}
