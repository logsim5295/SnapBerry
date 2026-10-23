using SkiaSharp;
using SnapBerry.Core;
using SnapBerry.Imaging;
using SnapBerry.Desktop.Editing;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class MagicWandTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact] public void KeyboardDeleteReportsOversizedResultAndKeepsSelectionAndHistory()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-wand-budget-"+Guid.NewGuid());using var editor=new DesktopEditor();
        using var bitmap=new SKBitmap(new SKImageInfo(2500,2500,SKColorType.Rgba8888,SKAlphaType.Unpremul));
        var pixels=new byte[2500*2500*4];new Random(42).NextBytes(pixels);for(var i=3;i<pixels.Length;i+=4)pixels[i]=255;
        System.Runtime.InteropServices.Marshal.Copy(pixels,0,bitmap.GetPixels(),pixels.Length);
        using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);Assert.True(data.Size>16*1024*1024);
        var window=new SnapBerry.Desktop.Views.MainWindow(editor,new NoCapture(),path);
        try
        {
            editor.OpenImage(data.ToArray());var token=editor.History!.CurrentStateToken;editor.Tool=DesktopTool.MagicWand;
            editor.SelectMagicWand(new(1,1),0,false);
            var error=Record.Exception(()=>window.RaiseEvent(new Avalonia.Input.KeyEventArgs{RoutedEvent=Avalonia.Input.InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Delete}));
            Assert.Null(error);Assert.True(editor.HasPixelSelection);Assert.Equal(token,editor.History.CurrentStateToken);
            var status=Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.TextBlock>(window,"StatusText")!.Text;
            Assert.Contains("실패",status);Assert.Null(editor.Document!.BackgroundAssetHash);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }

    [Avalonia.Headless.XUnit.AvaloniaFact] public void ToolbarPropertiesDeleteAndEscapeUseTheSamePixelSelection()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-wand-ui-"+Guid.NewGuid());using var editor=new DesktopEditor();
        var window=new SnapBerry.Desktop.Views.MainWindow(editor,new NoCapture(),path);
        T Control<T>(string name) where T:Avalonia.Controls.Control=>Avalonia.Controls.ControlExtensions.FindControl<T>(window,name)!;
        try
        {
            editor.OpenImage(Stripes());var token=editor.History!.CurrentStateToken;
            Control<Avalonia.Controls.Button>("MagicWandButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(DesktopTool.MagicWand,editor.Tool);Assert.True(Control<Avalonia.Controls.Border>("PropertiesBorder").IsVisible);
            Assert.True(Control<Avalonia.Controls.WrapPanel>("MagicWandControls").IsVisible);
            new EditorGesture(editor).Begin(new(1,1));Assert.True(editor.HasPixelSelection);Assert.Equal(token,editor.History.CurrentStateToken);
            Control<Avalonia.Controls.CheckBox>("MagicWandGlobal").IsChecked=true;Control<Avalonia.Controls.Slider>("MagicWandTolerance").Value=5;
            Assert.Equal(12,editor.MagicWandCount);
            Control<Avalonia.Controls.Button>("DeleteMagicWandButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.False(editor.HasPixelSelection);using(var image=SKBitmap.Decode(editor.RenderPng()))Assert.Equal(0,image.GetPixel(4,1).Alpha);
            editor.History.Undo();new EditorGesture(editor).Begin(new(1,1));
            window.RaiseEvent(new Avalonia.Input.KeyEventArgs{RoutedEvent=Avalonia.Input.InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Escape});
            Assert.False(editor.HasPixelSelection);Assert.Equal(token,editor.History.CurrentStateToken);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    private sealed class NoCapture : SnapBerry.Desktop.Capture.ICaptureService
    {public Task<byte[]?> CaptureAsync(SnapBerry.Desktop.Capture.DesktopCaptureMode mode,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("실제 캡처 금지");}

    private static byte[] Stripes()
    {
        using var bitmap=new SKBitmap(5,3);bitmap.Erase(SKColors.White);
        for(var y=0;y<3;y++)bitmap.SetPixel(2,y,SKColors.Black);
        bitmap.SetPixel(0,0,new(245,245,245));
        using var data=bitmap.Encode(SKEncodedImageFormat.Png,100);return data.ToArray();
    }

    [Fact] public void ContiguousAndGlobalSelectionsHaveDifferentReachAndToleranceUsesSeedColor()
    {
        using var selection=new MagicWandSelection(Stripes(),new(0,0,5,3));
        selection.Select(1,1,0,false);Assert.Equal(5,selection.Count);
        selection.Select(1,1,5,false);Assert.Equal(6,selection.Count);
        selection.Select(1,1,0,true);Assert.Equal(11,selection.Count);
        selection.Select(1,1,5,true);Assert.Equal(12,selection.Count);
    }

    [Fact] public void CropBoundsRestrictGlobalSelectionAndEraseKeepsUnselectedPixels()
    {
        var png=Stripes();using var selection=new MagicWandSelection(png,new(0,0,3,3));
        selection.Select(1,1,5,true);Assert.Equal(6,selection.Count);
        using var result=SKBitmap.Decode(selection.Erase());
        Assert.Equal(0,result.GetPixel(1,1).Alpha);Assert.Equal(SKColors.Black,result.GetPixel(2,1));Assert.Equal(SKColors.White,result.GetPixel(4,1));
        using var original=SKBitmap.Decode(png);Assert.Equal(SKColors.White,original.GetPixel(1,1));
    }

    [Fact] public void SelectionDoesNotEditDocumentAndEraseIsUndoableThenCanRepeat()
    {
        using var editor=new DesktopEditor();editor.OpenImage(Stripes());var token=editor.History!.CurrentStateToken;
        editor.SelectMagicWand(new(1,1),0,false);Assert.True(editor.HasPixelSelection);Assert.Equal(token,editor.History.CurrentStateToken);
        editor.UpdateMagicWand(5,true);Assert.Equal(12,editor.MagicWandCount);Assert.Equal(token,editor.History.CurrentStateToken);
        Assert.True(editor.DeleteMagicWandSelection());Assert.False(editor.HasPixelSelection);
        using(var result=SKBitmap.Decode(editor.RenderPng()))Assert.Equal(0,result.GetPixel(4,1).Alpha);
        editor.History.Undo();using(var restored=SKBitmap.Decode(editor.RenderPng()))Assert.Equal(SKColors.White,restored.GetPixel(4,1));
        editor.SelectMagicWand(new(1,1),0,false);editor.History.Redo();Assert.False(editor.HasPixelSelection);
    }

    [Fact] public void TransparentPixelsAndOutsideClicksDoNotSelectOrChangeImage()
    {
        using var selection=new MagicWandSelection(EditorTests.Png(5,3,SKColors.Transparent),new(0,0,5,3));
        selection.Select(1,1,100,true);Assert.Equal(0,selection.Count);
        selection.Select(-1,1,20,false);Assert.Equal(0,selection.Count);
    }
}
