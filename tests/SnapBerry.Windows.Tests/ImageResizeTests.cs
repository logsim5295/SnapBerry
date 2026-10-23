using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 합성·저장·좌표 변환은 합성 이미지로만 검사하며 창을 표시하지 않는다.
public class ImageResizeTests
{
    [Fact] public void ResizeScalesImageAndAnnotationsWithoutChangingOriginalOrUndoCoordinates()=>Sta.Run(()=>
    {
        var pixels=Enumerable.Repeat((byte)255,20*20*4).ToArray();
        var source=BitmapSource.Create(20,20,96,96,PixelFormats.Bgra32,null,pixels,80);source.Freeze();
        var original=PngFileWriter.Encode(source);
        var annotation=new Annotation{Kind=AnnotationKind.Rectangle,X=4,Y=4,Width=8,Height=8,Style=new(){Color="#FF0000",Thickness=2}};
        var model=new EditorViewModel();model.Open(new(){Width=20,Height=20,OriginalPng=original,Annotations=[annotation]});
        model.History!.Resize(new(40,60));var image=model.Composite()!;
        Assert.Equal((40,60),(image.PixelWidth,image.PixelHeight));
        var sample=new byte[4];image.CopyPixels(new Int32Rect(8,24,1,1),sample,4,0);Assert.True(sample[2]>200&&sample[1]<50);
        Assert.Equal(original,model.Snapshot().OriginalPng);Assert.Equal(annotation,Assert.Single(model.Snapshot().Annotations));
        var encoded=PngFileWriter.Decode(PngFileWriter.Encode(image));Assert.Equal((40,60),(encoded.PixelWidth,encoded.PixelHeight));
        model.History.Undo();Assert.Equal(20,model.Composite()!.PixelWidth);model.History.Redo();Assert.Equal(40,model.Composite()!.PixelWidth);
    });

    [Fact] public void ResizeAndCropPersistAndReopenWithOriginalBytesAndOutputSize()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try
        {
            var original=PngFileWriter.Encode(RenderingTests.Image(40,30));var model=new EditorViewModel();model.Open(new(){Width=40,Height=30,OriginalPng=original});
            model.CropTo(new(5,5,20,10));model.History!.Resize(new(60,40));
            var snapshot=model.Snapshot();
            using var json=JsonDocument.Parse(JsonSerializer.Serialize(snapshot));Assert.Equal(60,json.RootElement.GetProperty("ResizeSize").GetProperty("Width").GetInt32());
            var store=new CaptureLibraryStore(path);store.SaveAsync(snapshot).GetAwaiter().GetResult();
            var reopened=new EditorViewModel();reopened.Open(store.LoadAsync(snapshot.Id).GetAwaiter().GetResult());
            Assert.Equal(new PixelSize(60,40),reopened.Document!.OutputSize);Assert.Equal(new PixelRect(5,5,20,10),reopened.Document.CropBounds);
            Assert.Equal(original,reopened.Snapshot().OriginalPng);Assert.Equal((60,40),(reopened.Composite()!.PixelWidth,reopened.Composite()!.PixelHeight));
            var item=Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult());Assert.Equal((60,40),(item.Width,item.Height));
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    });

    [Fact] public void ResizedCanvasMapsPointersToOriginalCoordinatesAfterCrop()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,80))});
        model.CropTo(new(10,20,80,40));model.History!.Resize(new(160,20));model.Zoom=1.5;
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();
        Assert.Equal(240,canvas.Width);Assert.Equal(30,canvas.Height);
        Assert.Equal(new PixelPoint(30,30),canvas.ToDocumentPoint(new Point(60,7.5)));
    });

    [Fact] public void InvalidResizeMetadataIsRejectedBeforeSavingOrReplacingCurrentDocument()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try
        {
            var original=new CaptureRecord{Width=20,Height=10,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(20,10))};
            var model=new EditorViewModel();model.Open(original);
            var invalid=original with{ResizeSize=new(0,10)};
            Assert.Throws<ArgumentOutOfRangeException>(()=>model.Open(invalid));Assert.Equal(new PixelSize(20,10),model.Document!.OutputSize);
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CaptureLibraryStore(path).SaveAsync(invalid).GetAwaiter().GetResult());
            Assert.False(Directory.Exists(path));
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void NonuniformResizeDoesNotTreatImageCenterAsACropCorner()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
        model.History!.Resize(new(1000,10));model.Tool=EditorTool.Crop;
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();
        Assert.False(canvas.BeginCrop(new(50,0)));Assert.True(canvas.BeginCrop(new(0,0)));canvas.CancelGesture();
        var annotation=new Annotation{X=10,Y=10,Width=10,Height=10};
        Assert.False(canvas.IsResizeHandle(annotation,new(50,20)));Assert.True(canvas.IsResizeHandle(annotation,new(20.5,20)));
    });

    [Fact] public void CropAfterResizeKeepsCorrectImageContentAndCanReturnToNativeOutput()=>Sta.Run(()=>
    {
        var pixels=new byte[20*20*4];
        for(var y=0;y<20;y++)for(var x=0;x<20;x++){var i=(y*20+x)*4;pixels[i+2]=(byte)(x<10?255:0);pixels[i]=(byte)(x<10?0:255);pixels[i+3]=255;}
        var bitmap=BitmapSource.Create(20,20,96,96,PixelFormats.Bgra32,null,pixels,80);bitmap.Freeze();
        var model=new EditorViewModel();model.Open(new(){Width=20,Height=20,OriginalPng=PngFileWriter.Encode(bitmap)});
        model.History!.Resize(new(40,60));model.CropTo(new(10,0,10,20));var image=model.Composite()!;
        Assert.Equal((20,60),(image.PixelWidth,image.PixelHeight));var sample=new byte[4];image.CopyPixels(new Int32Rect(10,30,1,1),sample,4,0);
        Assert.True(sample[0]>240&&sample[2]<10);
        model.History.Resize(new(10,20));Assert.Null(model.Snapshot().ResizeSize);
        var reopened=new EditorViewModel();reopened.Open(model.Snapshot());Assert.Equal((10,20),(reopened.Composite()!.PixelWidth,reopened.Composite()!.PixelHeight));
    });
}
