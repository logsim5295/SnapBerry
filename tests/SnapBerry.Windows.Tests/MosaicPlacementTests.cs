using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창이나 마우스 캡처 없이 실제 캔버스의 모자이크 입력과 실행 취소를 검증한다.
public class MosaicPlacementTests
{
    private static EditorCanvas Canvas()
    {
        var model=new EditorViewModel{Tool=EditorTool.Mosaic,MosaicSize=24};
        model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();return canvas;
    }

    [Fact] public void TwoClicksPreviewAndCommitOneMosaicWithTheConfiguredBlockSize()=>Sta.Run(()=>
    {
        var canvas=Canvas();var model=canvas.Model;
        Assert.True(canvas.BeginShape(new(80,90)));canvas.FinishShape(new(80,90));
        Assert.Empty(model.Document!.Annotations);Assert.False(model.History!.CanUndo);
        canvas.MoveAnnotation(new(10,20));
        Assert.Equal((10d,20d,70d,70d),(canvas.ShapePreview!.X,canvas.ShapePreview.Y,canvas.ShapePreview.Width,canvas.ShapePreview.Height));
        Assert.Equal(24,canvas.ShapePreview.MosaicBlockSize);Assert.Empty(model.Document.Annotations);
        Assert.True(canvas.BeginShape(new(10,20)));canvas.FinishShape(new(10,20));
        var mosaic=Assert.Single(model.Document.Annotations);Assert.Equal(AnnotationKind.Mosaic,mosaic.Kind);
        Assert.Equal((10d,20d,70d,70d),(mosaic.X,mosaic.Y,mosaic.Width,mosaic.Height));Assert.Equal(24,mosaic.MosaicBlockSize);
        Assert.Equal(mosaic.Id,model.SelectedId);Assert.Null(canvas.ShapePreview);
        model.History.Undo();Assert.Empty(model.Document.Annotations);Assert.False(model.History.CanUndo);
        model.History.Redo();Assert.Equal(mosaic,Assert.Single(model.Document.Annotations));
    });

    [Fact] public void DragStillCommitsAMosaic()=>Sta.Run(()=>
    {
        var canvas=Canvas();Assert.True(canvas.BeginShape(new(10,20)));canvas.MoveAnnotation(new(80,90));canvas.FinishShape(new(80,90));
        var mosaic=Assert.Single(canvas.Model.Document!.Annotations);Assert.Equal(AnnotationKind.Mosaic,mosaic.Kind);
        Assert.Equal((10d,20d,70d,70d),(mosaic.X,mosaic.Y,mosaic.Width,mosaic.Height));Assert.Equal(24,mosaic.MosaicBlockSize);
    });

    [Fact] public void CancelDiscardsThePendingMosaic()=>Sta.Run(()=>
    {
        var canvas=Canvas();Assert.True(canvas.BeginShape(new(10,20)));canvas.FinishShape(new(10,20));canvas.MoveAnnotation(new(80,90));Assert.NotNull(canvas.ShapePreview);canvas.CancelGesture();
        canvas.FinishShape(new(80,90));Assert.Null(canvas.ShapePreview);Assert.Empty(canvas.Model.Document!.Annotations);Assert.False(canvas.Model.History!.CanUndo);
    });
}
