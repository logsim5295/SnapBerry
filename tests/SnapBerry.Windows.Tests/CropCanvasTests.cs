using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 자르기 도구의 캔버스 입력·좌표 변환·미리보기와 완료 후 상태를 검증한다.
public class CropCanvasTests
{
    [Theory] [InlineData(.5)] [InlineData(1)] [InlineData(2)]
    public void CornerDragPreviewsAndCommitsAtDifferentZoomLevels(double zoom)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Show();w.UpdateLayout();
            var crop=((ItemsControl)w.FindName("ToolsPanel")).Items.OfType<ToggleButton>().Single(x=>(string)x.Tag=="Crop");Assert.False(crop.IsEnabled);
            w.Model.Open(new(){Width=400,Height=300,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(400,300))});
            crop.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Assert.Equal(EditorTool.Crop,w.Model.Tool);Assert.False(((UIElement)w.FindName("PropertiesPanel")).IsEnabled);
            var canvas=(EditorCanvas)((Grid)w.FindName("CanvasHost")).Children[0];w.Model.Zoom=zoom;canvas.Refresh();w.UpdateLayout();
            var token=w.Model.History!.CurrentStateToken;
            Assert.True(canvas.BeginCrop(new(0,0)));canvas.MoveCrop(new(40,30));
            Assert.Equal((400,300),(w.Model.Document!.Width,w.Model.Document.Height));Assert.Equal(token,w.Model.History.CurrentStateToken);
            SavePreview(canvas,zoom);
            canvas.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(canvas),0,Key.Escape){RoutedEvent=Keyboard.KeyDownEvent});
            canvas.FinishCrop(new(40,30));Assert.Equal(token,w.Model.History.CurrentStateToken);
            Assert.True(canvas.BeginCrop(new(0,0)));canvas.MoveCrop(new(40,30));
            canvas.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.LostMouseCaptureEvent});
            canvas.FinishCrop(new(40,30));Assert.Equal(token,w.Model.History.CurrentStateToken);
            Assert.True(canvas.BeginCrop(new(0,0)));canvas.MoveCrop(new(40,30));canvas.FinishCrop(new(40,30));
            Assert.Equal(new PixelRect(40,30,360,270),w.Model.Document.CropBounds);Assert.Equal(360*zoom,canvas.Width);Assert.Equal(270*zoom,canvas.Height);
            Assert.Equal(new PixelPoint(60,40),canvas.ToDocumentPoint(new Point(20*zoom,10*zoom)));
            w.Model.History.Undo();Assert.Equal(400*zoom,canvas.Width);w.Model.History.Redo();Assert.Equal(360*zoom,canvas.Width);
            Assert.True(canvas.BeginCrop(new(360,270)));canvas.MoveCrop(new(300,220));
            ((ItemsControl)w.FindName("ToolsPanel")).Items.OfType<ToggleButton>().Single(x=>(string)x.Tag=="Arrow").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            canvas.FinishCrop(new(300,220));Assert.Equal(new PixelRect(40,30,360,270),w.Model.Document.CropBounds);Assert.True(((UIElement)w.FindName("PropertiesPanel")).IsEnabled);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    private static void SavePreview(EditorCanvas canvas,double zoom)
    {
        var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");if(artifacts is null)return;
        canvas.UpdateLayout();var bitmap=new RenderTargetBitmap((int)canvas.Width,(int)canvas.Height,96,96,PixelFormats.Pbgra32);bitmap.Render(canvas);
        var pixels=new byte[4];bitmap.CopyPixels(new Int32Rect((int)(15*zoom),(int)(15*zoom),1,1),pixels,4,0);Assert.True(pixels[2]<120);
        Directory.CreateDirectory(artifacts);File.WriteAllBytes(Path.Combine(artifacts,$"crop-preview-{zoom}.png"),PngFileWriter.Encode(bitmap));
    }
}
