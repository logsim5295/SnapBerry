using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Core.Tests;

public class ImageResizeTests
{
    [Fact] public void ResizeUndoRedoPreservesOriginalCoordinatesAndPersistenceTokens()
    {
        var document=new AnnotationDocument(100,80);var history=new CommandHistory(document);
        var annotation=new Annotation{X=20,Y=10,Width=30,Height=15};history.Add(annotation);
        var token=history.CurrentStateToken;history.MarkPersisted(token);
        history.Resize(new(50,20));
        Assert.Equal(new PixelSize(50,20),document.OutputSize);Assert.Equal(new PixelRect(0,0,100,80),document.CropBounds);
        Assert.Equal(annotation,Assert.Single(document.Annotations));Assert.True(history.IsPersistenceDirty);
        history.Undo();Assert.Equal(new PixelSize(100,80),document.OutputSize);Assert.Equal(token,history.CurrentStateToken);Assert.False(history.IsPersistenceDirty);
        history.Redo();Assert.Equal(new PixelSize(50,20),document.OutputSize);
    }

    [Fact] public void CropAfterResizeRetainsScaleAndCanBeUndone()
    {
        var document=new AnnotationDocument(100,80);var history=new CommandHistory(document);
        history.Crop(new(10,10,80,60));history.Resize(new(160,30));history.Crop(new(10,10,40,20));
        Assert.Equal(new PixelSize(80,10),document.OutputSize);Assert.Equal(new PixelRect(20,20,40,20),document.CropBounds);
        history.Undo();Assert.Equal(new PixelSize(160,30),document.OutputSize);Assert.Equal(new PixelRect(10,10,80,60),document.CropBounds);
        history.Undo();Assert.Equal(new PixelSize(80,60),document.OutputSize);
    }

    [Fact] public void InvalidOrUnchangedResizeDoesNotCreateAnUndoStep()
    {
        var document=new AnnotationDocument(100,80);var history=new CommandHistory(document);var token=history.CurrentStateToken;
        history.Resize(new(100,80));
        foreach(var size in new[]{new PixelSize(0,10),new(-1,10),new(10,0),new(32768,1),new(10000,10000)})
            Assert.Throws<ArgumentOutOfRangeException>(()=>history.Resize(size));
        Assert.Equal(token,history.CurrentStateToken);Assert.False(history.CanUndo);
    }

    [Theory]
    [InlineData(1920,1080,960,true,960,540)]
    [InlineData(1920,1080,270,false,480,270)]
    [InlineData(3,2,1,true,1,1)]
    public void AspectRatioUsesOriginalSizeAndRoundsToPositivePixels(int width,int height,int value,bool editWidth,int expectedWidth,int expectedHeight)
        =>Assert.Equal(new PixelSize(expectedWidth,expectedHeight),ImageResize.KeepAspectRatio(new(width,height),value,editWidth));

    [Fact] public void ReturningToCropNativeSizeClearsResizeAndUndoRestoresIt()
    {
        var document=new AnnotationDocument(100,80);var history=new CommandHistory(document);history.Crop(new(10,10,60,40));
        history.Resize(new(120,80));history.Resize(new(60,40));Assert.Null(document.ResizeSize);
        history.Undo();Assert.Equal(new PixelSize(120,80),document.ResizeSize);history.Redo();Assert.Null(document.ResizeSize);
    }

    [Fact] public void NonuniformHitTestingUsesScreenDistancesForRectanglesAndArrows()
    {
        var rectangle=new Annotation{Kind=AnnotationKind.Rectangle,X=10,Y=10,Width=10,Height=10};
        Assert.Null(AnnotationHitTester.Hit([rectangle],new(50,10),8,10,.1));
        Assert.Equal(rectangle,AnnotationHitTester.Hit([rectangle],new(20.5,15),8,10,.1));
        var arrow=new Annotation{Kind=AnnotationKind.Arrow,X=10,Y=30,Width=20,Height=0};
        Assert.Null(AnnotationHitTester.Hit([arrow],new(20,200),8,10,.1));
        Assert.Equal(arrow,AnnotationHitTester.Hit([arrow],new(20,35),8,10,.1));
    }

    [Fact] public void NonuniformShapeGestureUsesScreenDistanceAndVisibleMinimumSize()
    {
        var shape=new ShapePlacement();shape.PointerDown(new(0,0),AnnotationKind.Rectangle,new(),3,10,.1);
        Assert.NotNull(shape.PointerUp(new(1,50)));
        shape.PointerDown(new(0,0),AnnotationKind.Rectangle,new(),3,10,.1);
        Assert.Null(shape.PointerUp(new(1,5)));
    }
}
