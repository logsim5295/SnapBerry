using Xunit;

namespace SnapBerry.Core.Tests;

// 자르기 꼭짓점 판정, 경계 제한 및 문서 자르기 상태의 실행 취소를 검증한다.
public class CropTests
{
    [Theory]
    [InlineData(0,0,20,10,20,10,80,70)]
    [InlineData(100,0,80,10,0,10,80,70)]
    [InlineData(0,80,20,60,20,0,80,60)]
    [InlineData(100,80,80,60,0,0,80,60)]
    [InlineData(0,0,-12,-8,-12,-8,112,88)]
    [InlineData(100,0,112,-8,0,-8,112,88)]
    [InlineData(0,80,-12,88,-12,0,112,88)]
    [InlineData(100,80,112,88,0,0,112,88)]
    [InlineData(0,0,-12,8,-12,8,112,72)]
    public void FourCornersKeepTheOppositeCornerFixed(int sx,int sy,int x,int y,int left,int top,int width,int height)
    {
        var gesture=new CropPlacement();
        Assert.True(gesture.PointerDown(100,80,new(sx,sy),10));
        gesture.Move(new(x,y));
        Assert.Equal(new PixelRect(left,top,width,height),gesture.Preview);
        Assert.Equal(new PixelRect(left,top,width,height),gesture.PointerUp(new(x,y)));
        Assert.False(gesture.Active);
    }

    [Fact] public void CancelAndUnchangedDragDoNotCommit()
    {
        var gesture=new CropPlacement();
        Assert.False(gesture.PointerDown(100,80,new(50,40),10));
        Assert.True(gesture.PointerDown(100,80,new(0,0),10));
        gesture.Move(new(20,10));gesture.Cancel();
        Assert.Null(gesture.PointerUp(new(20,10)));
        Assert.True(gesture.PointerDown(100,80,new(0,0),10));
        Assert.Null(gesture.PointerUp(new(0,0)));
        Assert.True(gesture.PointerDown(100,80,new(4,4),10));
        Assert.Null(gesture.PointerUp(new(4,4)));
    }

    [Fact] public void DragPastOppositeCornerKeepsAtLeastOnePixel()
    {
        var gesture=new CropPlacement();gesture.PointerDown(100,80,new(0,0),10);
        Assert.Equal(new PixelRect(99,79,1,1),gesture.PointerUp(new(999,999)));
        gesture.PointerDown(100,80,new(100,80),10);
        Assert.Equal(new PixelRect(0,0,1,1),gesture.PointerUp(new(-999,-999)));
    }

    [Fact] public void CropUndoRedoPreservesAnnotationsAndStateTokens()
    {
        var doc=new AnnotationDocument(100,80);var history=new CommandHistory(doc);
        var annotation=new Annotation{Kind=AnnotationKind.Arrow,X=80,Y=60,Width=-50,Height=-30};history.Add(annotation);
        var initial=history.CurrentStateToken;history.MarkPersisted(initial);
        history.Crop(new(20,10,60,50));
        Assert.Equal((60,50),(doc.Width,doc.Height));Assert.Equal(new PixelRect(20,10,60,50),doc.CropBounds);
        Assert.Equal(annotation,doc.Annotations.Single());Assert.True(history.IsPersistenceDirty);
        history.Undo();Assert.Equal((100,80),(doc.Width,doc.Height));Assert.Equal(initial,history.CurrentStateToken);Assert.False(history.IsPersistenceDirty);
        history.Redo();Assert.Equal(new PixelRect(20,10,60,50),doc.CropBounds);
        history.Crop(new(10,5,30,20));Assert.Equal(new PixelRect(30,15,30,20),doc.CropBounds);
        history.Undo();Assert.Equal(new PixelRect(20,10,60,50),doc.CropBounds);Assert.Equal(annotation,doc.Annotations.Single());
    }

    [Fact] public void EmptyAndOversizedCropsDoNotChangeHistory()
    {
        var doc=new AnnotationDocument(100,80);var history=new CommandHistory(doc);var token=history.CurrentStateToken;
        history.Crop(new(0,0,100,80));Assert.False(history.CanUndo);
        foreach(var invalid in new[]{new PixelRect(0,0,0,10),new(0,0,10000,10000),new(1,0,int.MaxValue,10)})
            Assert.Throws<ArgumentOutOfRangeException>(()=>history.Crop(invalid));
        Assert.Equal(token,history.CurrentStateToken);Assert.False(history.CanUndo);
    }

    [Fact] public void OversizedExpansionAfterResizeLeavesDocumentAndHistoryIntact()
    {
        var doc=new AnnotationDocument(100,80);var history=new CommandHistory(doc);
        history.Resize(new(1000,800));var token=history.CurrentStateToken;
        Assert.Throws<ArgumentOutOfRangeException>(()=>history.Crop(new(0,0,5000,80)));
        Assert.Equal(new PixelRect(0,0,100,80),doc.CropBounds);
        Assert.Equal(new PixelSize(1000,800),doc.OutputSize);
        Assert.Equal(token,history.CurrentStateToken);
    }
}
