using Xunit;
namespace SnapBerry.Core.Tests;
// 드래그와 두 번 클릭 방식의 도형 생성 및 제스처 취소를 검증한다.
public class ShapePlacementTests
{
    [Theory][InlineData(AnnotationKind.Arrow)][InlineData(AnnotationKind.Rectangle)][InlineData(AnnotationKind.Mosaic)]
    public void TwoClicksCreateOneShape(AnnotationKind kind)
    {var gesture=new ShapePlacement();Assert.Null(gesture.PointerDown(new(10,20),kind,new()));Assert.Null(gesture.PointerUp(new(10,20)));Assert.True(gesture.Active);gesture.Move(new(80,90));var a=gesture.PointerDown(new(80,90),kind,new());Assert.NotNull(a);Assert.Equal(10,a.X);Assert.Equal(20,a.Y);Assert.Equal(70,a.Width);Assert.Equal(70,a.Height);Assert.False(gesture.Active);Assert.Null(gesture.PointerUp(new(80,90)));}
    [Fact] public void DragStillCreatesShape()
    {var g=new ShapePlacement();g.PointerDown(new(80,90),AnnotationKind.Rectangle,new());var a=g.PointerUp(new(10,20));Assert.NotNull(a);Assert.Equal(10,a.X);Assert.Equal(20,a.Y);Assert.Equal(70,a.Width);}
    [Fact] public void CancelRemovesPendingShape()
    {var g=new ShapePlacement();g.PointerDown(new(10,10),AnnotationKind.Arrow,new());g.PointerUp(new(10,10));g.Cancel();Assert.False(g.Active);Assert.Null(g.Preview);}

    [Theory][InlineData(0,0)][InlineData(70,0)][InlineData(0,70)][InlineData(2,70)][InlineData(70,2)]
    public void MosaicWaitsForAnAreaBeforeCommitting(double width,double height)
    {
        var g=new ShapePlacement();g.PointerDown(new(10,20),AnnotationKind.Mosaic,new());g.PointerUp(new(10,20));
        Assert.Null(g.PointerDown(new(10+width,20+height),AnnotationKind.Mosaic,new()));Assert.True(g.Active);
        var a=g.PointerDown(new(80,90),AnnotationKind.Mosaic,new());Assert.NotNull(a);Assert.Equal((10d,20d,70d,70d),(a.X,a.Y,a.Width,a.Height));Assert.False(g.Active);
    }
}
