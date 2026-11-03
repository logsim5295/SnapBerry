using SnapBerry.Core;
using SnapBerry.Desktop.Editing;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class GestureTests
{
    [Fact] public void MovingAnInsertedImageOutsideAddsOnlyTheNeededTransparentMargin()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());
        editor.InsertImage(EditorTests.Png(90,20,SkiaSharp.SKColors.Red));
        var centered=new PixelRect(-5,0,90,60);
        Assert.Equal(centered,editor.Document!.CropBounds);
        var gesture=new EditorGesture(editor);
        gesture.Begin(new(40,30));gesture.Move(new(50,0));
        Assert.Equal(centered,editor.Document.CropBounds);
        gesture.End(new(50,0));
        Assert.Equal(new PixelRect(-5,-10,100,70),editor.Document.CropBounds);
        using var reopened=new DesktopEditor();reopened.Open(editor.Snapshot());
        using var result=SkiaSharp.SKBitmap.Decode(reopened.RenderPng());
        Assert.Equal(0,result.GetPixel(0,0).Alpha);
        Assert.Equal(SkiaSharp.SKColors.Red,result.GetPixel(99,0));
        Assert.Equal(SkiaSharp.SKColors.Blue,result.GetPixel(5,69));
        editor.History!.Undo();Assert.Equal(centered,editor.Document.CropBounds);
        editor.History.Redo();Assert.Equal(reopened.Document!.CropBounds,editor.Document.CropBounds);
    }

    [Fact] public void OutwardCornerDragPadsExportAndWandIgnoresTransparentMargin()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.Tool=DesktopTool.Crop;
        var gesture=new EditorGesture(editor);
        gesture.Begin(new(1,1));gesture.Move(new(-4,-6));
        Assert.Equal(new PixelRect(-5,-7,85,67),gesture.CropPreview);
        gesture.End(new(-4,-6));
        using var image=SkiaSharp.SKBitmap.Decode(editor.RenderPng());
        Assert.Equal((85,67),(image.Width,image.Height));Assert.Equal(0,image.GetPixel(4,7).Alpha);
        Assert.Equal(SkiaSharp.SKColors.Blue,image.GetPixel(5,7));
        editor.SelectMagicWand(new(-1,-1),0,true);Assert.Null(editor.MagicWandPreview);
        editor.SelectMagicWand(new(0,0),0,true);Assert.NotNull(editor.MagicWandPreview);
    }

    [Fact] public void NegativeArrowAndCancelledMoveKeepDocumentAndUndoConsistent()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.Tool=DesktopTool.Arrow;var gesture=new EditorGesture(editor);
        gesture.Begin(new(60,50));gesture.Move(new(10,5));Assert.Empty(editor.Document!.Annotations);gesture.End(new(10,5));
        var arrow=Assert.Single(editor.Document.Annotations);Assert.Equal(-50,arrow.Width);Assert.Equal(-45,arrow.Height);
        editor.Tool=DesktopTool.Select;gesture.Begin(new(35,27.5));gesture.Move(new(40,32.5));gesture.Cancel();Assert.Equal(arrow,Assert.Single(editor.Document.Annotations));
        editor.History!.Undo();Assert.Empty(editor.Document.Annotations);
    }
    [Fact] public void ExpandingAfterCropKeepsCutImageAndStickerPixelsTransparent()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());
        editor.InsertImage(EditorTests.Png(20,10,SkiaSharp.SKColors.Red));
        editor.History!.Crop(new(40,25,20,10));
        editor.History.Crop(new(-10,0,30,10));
        using var expanded=SkiaSharp.SKBitmap.Decode(editor.RenderPng());
        Assert.Equal(0,expanded.GetPixel(9,5).Alpha);
        Assert.Equal(SkiaSharp.SKColors.Red,expanded.GetPixel(10,5));
        editor.SelectMagicWand(new(39,30),0,true);Assert.Null(editor.MagicWandPreview);
        editor.History.Undo();editor.History.Undo();
        using var original=SkiaSharp.SKBitmap.Decode(editor.RenderPng());
        Assert.Equal(SkiaSharp.SKColors.Red,original.GetPixel(39,30));
        editor.History.Redo();editor.History.Redo();
        editor.Commit(new(){Kind=AnnotationKind.StepMarker,X=30,Y=25,Width=8,Height=8});
        using var edited=SkiaSharp.SKBitmap.Decode(editor.RenderPng());
        Assert.Equal(255,edited.GetPixel(4,4).Alpha);
    }
    [Fact] public void CroppedDocumentGesturesUseOriginalCoordinatesAndUndoOneStep()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.History!.Crop(new(10,10,50,40));editor.Tool=DesktopTool.Crop;var gesture=new EditorGesture(editor);
        gesture.Begin(new(20,20));gesture.End(new(40,35));Assert.Equal(new PixelRect(20,20,20,15),editor.Document!.CropBounds);
        editor.History.Undo();Assert.Equal(new PixelRect(10,10,50,40),editor.Document.CropBounds);
    }
    [Theory][InlineData(DesktopTool.Rectangle)][InlineData(DesktopTool.Mosaic)]
    public void TwoClickPlacementCommitsOnceAndCancelDoesNotAddAnAnnotation(DesktopTool tool)
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.Tool=tool;var gesture=new EditorGesture(editor){MosaicSize=24};
        gesture.Begin(new(5,5));gesture.End(new(5,5));Assert.Empty(editor.Document!.Annotations);gesture.Move(new(30,20));gesture.Begin(new(30,20));
        var annotation=Assert.Single(editor.Document.Annotations);Assert.Equal(tool==DesktopTool.Mosaic?AnnotationKind.Mosaic:AnnotationKind.Rectangle,annotation.Kind);Assert.Equal(24,annotation.MosaicBlockSize);
        gesture.End(new(30,20));Assert.Single(editor.Document.Annotations);editor.History!.Undo();Assert.Empty(editor.Document.Annotations);Assert.False(editor.History.CanUndo);
        gesture.Begin(new(10,10));gesture.Move(new(40,30));gesture.Cancel();Assert.Empty(editor.Document.Annotations);
    }
    [Fact] public void ResizingAnImageCreatesOneUndoCommand()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.InsertImage(EditorTests.Png(20,10));var original=editor.Selected!;editor.Tool=DesktopTool.Select;
        var gesture=new EditorGesture(editor);gesture.Begin(new(original.X+original.Width,original.Y+original.Height));gesture.Move(new(original.X+30,original.Y+20));gesture.End(new(original.X+40,original.Y+30));
        Assert.Equal(40,editor.Selected!.Width);Assert.Equal(30,editor.Selected.Height);editor.History!.Undo();Assert.Equal(original,Assert.Single(editor.Document!.Annotations));
    }
    [Fact] public async Task AtomicExportFailurePreservesThePreviousFile()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-export-"+Guid.NewGuid());Directory.CreateDirectory(path);var target=Path.Combine(path,"image.png");await File.WriteAllBytesAsync(target,[1,2,3],TestContext.Current.CancellationToken);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(()=>SnapBerry.Desktop.Rendering.AtomicPngWriter.WriteAsync(target,[0],TestContext.Current.CancellationToken));
            Assert.Equal(new byte[]{1,2,3},await File.ReadAllBytesAsync(target,TestContext.Current.CancellationToken));
            await SnapBerry.Desktop.Rendering.AtomicPngWriter.WriteAsync(target,EditorTests.Png(),TestContext.Current.CancellationToken);
            Assert.Equal(EditorTests.Png(),await File.ReadAllBytesAsync(target,TestContext.Current.CancellationToken));Assert.Single(Directory.EnumerateFiles(path));
        }
        finally{Directory.Delete(path,true);}
    }
}
