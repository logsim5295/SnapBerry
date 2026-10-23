using SnapBerry.Core;
using SnapBerry.Desktop.Editing;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class GestureTests
{
    [Fact] public void NegativeArrowAndCancelledMoveKeepDocumentAndUndoConsistent()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.Tool=DesktopTool.Arrow;var gesture=new EditorGesture(editor);
        gesture.Begin(new(60,50));gesture.Move(new(10,5));Assert.Empty(editor.Document!.Annotations);gesture.End(new(10,5));
        var arrow=Assert.Single(editor.Document.Annotations);Assert.Equal(-50,arrow.Width);Assert.Equal(-45,arrow.Height);
        editor.Tool=DesktopTool.Select;gesture.Begin(new(35,27.5));gesture.Move(new(40,32.5));gesture.Cancel();Assert.Equal(arrow,Assert.Single(editor.Document.Annotations));
        editor.History!.Undo();Assert.Empty(editor.Document.Annotations);
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
