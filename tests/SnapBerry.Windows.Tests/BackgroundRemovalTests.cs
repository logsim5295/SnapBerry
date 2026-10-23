using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 합성 비트맵과 표시하지 않은 편집 모델만 사용한다.
public class BackgroundRemovalTests
{
    [Fact] public void RibbonCommandTracksEmptyBusyAppliedAndUndoStatesWithoutShowingWindow()=>Sta.Run(()=>
    {
        var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(System.IO.Path.Combine(path,"Library")),new(path));
        try
        {
            var button=Assert.IsType<System.Windows.Controls.Ribbon.RibbonToggleButton>(window.FindName("MagicWandButton"));
            Assert.False(button.IsEnabled);
            window.Model.Open(new(){Width=80,Height=60,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(80,60))});Assert.True(button.IsEnabled);
            window.SetBusy(true);Assert.False(button.IsEnabled);window.SetBusy(false);Assert.True(button.IsEnabled);
            window.Model.ApplyBackgroundRemoval(window.Model.Snapshot().OriginalPng);Assert.True(button.IsEnabled);
            window.Model.History!.Undo();Assert.True(button.IsEnabled);
            Assert.NotNull(button.LargeImageSource);Assert.NotNull(window.FindName("MagicWandTolerance"));
        }
        finally{window.CloseForExit();}
    });

    [Fact] public void CutoutCompositeIsTransparentAndUndoKeepsOriginalAndCrop()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();var original=PngFileWriter.Encode(RenderingTests.Image(80,60));
        model.Open(new(){Width=80,Height=60,OriginalPng=original});model.CropTo(new(10,10,40,30));model.History!.Resize(new(80,60));
        var image=BitmapSource.Create(80,60,96,96,PixelFormats.Bgra32,null,new byte[80*60*4],80*4);image.Freeze();
        model.ApplyBackgroundRemoval(PngFileWriter.Encode(image));
        var result=model.Composite()!;var pixel=new byte[4];result.CopyPixels(new Int32Rect(1,1,1,1),pixel,4,0);Assert.Equal(0,pixel[3]);
        Assert.Equal(original,model.Snapshot().OriginalPng);Assert.Single(model.Snapshot().StickerAssets);
        model.History.Undo();Assert.Null(model.Document!.BackgroundAssetHash);Assert.Equal(new PixelRect(10,10,40,30),model.Document.CropBounds);
        model.History.Redo();Assert.NotNull(model.Document.BackgroundAssetHash);
        var reopened=new EditorViewModel();reopened.Open(model.Snapshot());Assert.Equal(PngFileWriter.Encode(result),PngFileWriter.Encode(reopened.Composite()!));
    });
}
