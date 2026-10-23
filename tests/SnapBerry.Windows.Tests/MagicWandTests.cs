using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class MagicWandTests
{
    [Fact] public void PreviewDoesNotEditAndDeletePreservesAnnotationsCropAndUndo()=>Sta.Run(()=>
    {
        var data=new byte[8*6*4];Array.Fill<byte>(data,255);
        var source=BitmapSource.Create(8,6,96,96,PixelFormats.Bgra32,null,data,32);source.Freeze();
        var model=new EditorViewModel();model.Open(new(){Width=8,Height=6,OriginalPng=PngFileWriter.Encode(source)});
        model.History!.Add(new(){Kind=AnnotationKind.Text,Text="메모",X=1,Y=1});model.CropTo(new(2,1,4,3));model.History.Resize(new(8,6));
        var token=model.History.CurrentStateToken;model.SelectMagicWand(new(3,2),0,true);
        Assert.Equal(12,model.MagicWandCount);Assert.NotNull(model.MagicWandPreview);Assert.Equal(token,model.History.CurrentStateToken);
        Assert.True(model.DeleteMagicWandSelection());Assert.False(model.HasPixelSelection);Assert.Single(model.Document!.Annotations);
        Assert.Equal(new PixelSize(8,6),model.Document.OutputSize);
        var background=model.Background!;var pixel=new byte[4];background.CopyPixels(new Int32Rect(3,2,1,1),pixel,4,0);Assert.Equal(0,pixel[3]);
        background.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);Assert.Equal(255,pixel[3]);
        model.History.Undo();Assert.Equal(token,model.History.CurrentStateToken);Assert.Null(model.Document.BackgroundAssetHash);
        model.SelectMagicWand(new(3,2),0,true);model.History.Redo();Assert.False(model.HasPixelSelection);
    });
}
