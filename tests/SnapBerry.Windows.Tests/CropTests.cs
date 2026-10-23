using System.IO;
using System.IO.Compression;
using System.Text.Json;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 자르기 결과의 합성·저장·다시 열기 및 원본 보존을 검증한다.
public class CropTests
{
    [Fact] public void AnnotationsOutsideTheCropAreRetainedButExcludedFromOutput()=>Sta.Run(()=>
    {
        var original=RenderingTests.Image(40,30);var annotation=new Annotation{Kind=AnnotationKind.StepMarker,X=0,Y=0,Width=5,Height=5,Text="1"};
        var vm=new EditorViewModel();vm.Open(new(){Width=40,Height=30,OriginalPng=PngFileWriter.Encode(original),Annotations=[annotation]});
        vm.CropTo(new(10,10,20,15));var output=vm.Composite()!;
        var expected=new byte[20*15*4];original.CopyPixels(new System.Windows.Int32Rect(10,10,20,15),expected,20*4,0);
        var actual=new byte[expected.Length];output.CopyPixels(actual,20*4,0);
        Assert.Equal(expected,actual);Assert.Equal(annotation,Assert.Single(vm.Snapshot().Annotations));
    });

    [Fact] public void CropExportsExactPixelsAndCanBeUndone()=>Sta.Run(()=>
    {
        var source=RenderingTests.Image(16,12);var original=PngFileWriter.Encode(source);
        var vm=new EditorViewModel();vm.Open(new(){Width=16,Height=12,OriginalPng=original});
        vm.CropTo(new(3,2,8,6));
        var image=vm.Composite()!;Assert.Equal((8,6),(image.PixelWidth,image.PixelHeight));
        var expected=new byte[4];source.CopyPixels(new System.Windows.Int32Rect(3,2,1,1),expected,4,0);
        var actual=new byte[4];image.CopyPixels(new System.Windows.Int32Rect(0,0,1,1),actual,4,0);Assert.Equal(expected,actual);
        Assert.Equal(original,vm.Snapshot().OriginalPng);
        var png=PngFileWriter.Decode(PngFileWriter.Encode(image));Assert.Equal((8,6),(png.PixelWidth,png.PixelHeight));
        vm.History!.Undo();Assert.Equal((16,12),(vm.Composite()!.PixelWidth,vm.Composite()!.PixelHeight));
        vm.History.Redo();Assert.Equal(new PixelRect(3,2,8,6),vm.Snapshot().CropBounds);
    });

    [Fact] public void CroppedHistoryReopensWithOriginalAndEditableAnnotations()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try
        {
            var vm=new EditorViewModel();var original=PngFileWriter.Encode(RenderingTests.Image(160,120));
            var annotation=new Annotation{Kind=AnnotationKind.Text,Text="editable",X=50,Y=40};
            vm.Open(new(){Width=160,Height=120,OriginalPng=original,Annotations=[annotation]});vm.CropTo(new(30,20,80,60));
            var record=vm.Snapshot();var store=new CaptureLibraryStore(path);store.SaveAsync(record).GetAwaiter().GetResult();
            var item=Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult());Assert.Equal((80,60),(item.Width,item.Height));
            var reopened=store.LoadAsync(record.Id).GetAwaiter().GetResult();vm.Open(reopened);
            Assert.Equal(original,reopened.OriginalPng);Assert.Equal(annotation,Assert.Single(vm.Document!.Annotations));
            Assert.Equal((80,60),(vm.Composite()!.PixelWidth,vm.Composite()!.PixelHeight));
            vm.UpdateText(annotation.Id,"changed");Assert.Equal("changed",vm.Document.Annotations[0].Text);
            Assert.Throws<InvalidDataException>(()=>store.SaveAsync(record with{CropBounds=new(159,0,2,20)}).GetAwaiter().GetResult());
            Assert.Equal(record.CropBounds,store.LoadAsync(record.Id).GetAwaiter().GetResult().CropBounds);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void HistoryWithoutCropMetadataStillOpensAtFullSize()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        try
        {
            var id=Guid.NewGuid();var original=PngFileWriter.Encode(RenderingTests.Image(16,12));
            using(var zip=ZipFile.Open(Path.Combine(path,id.ToString("N")+".qcapture"),ZipArchiveMode.Create))
            {
                using(var entry=zip.CreateEntry("document.json").Open())JsonSerializer.Serialize(entry,new{SchemaVersion=1,Id=id,Width=16,Height=12});
                using(var entry=zip.CreateEntry("original.png").Open())entry.Write(original);
            }
            var record=new CaptureLibraryStore(path).LoadAsync(id).GetAwaiter().GetResult();Assert.Null(record.CropBounds);
            var vm=new EditorViewModel();vm.Open(record);Assert.Equal((16,12),(vm.Document!.Width,vm.Document.Height));
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
}
