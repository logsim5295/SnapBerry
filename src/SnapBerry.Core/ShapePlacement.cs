namespace SnapBerry.Core;
// 드래그 그리기와 시작점 클릭→끝점 클릭 그리기를 하나의 상태 머신으로 처리한다.
public sealed class ShapePlacement
{
    private PixelPoint start;private bool waitingForEnd;private double threshold,scaleX=1,scaleY=1;
    public bool Active=>Preview is not null;public Annotation? Preview {get;private set;}
    public Annotation? PointerDown(PixelPoint point,AnnotationKind kind,AnnotationStyle style,double threshold=3,double scaleX=1,double scaleY=1)
    {
        if(waitingForEnd){Move(point);return Commit();}
        start=point;this.threshold=threshold;this.scaleX=scaleX;this.scaleY=scaleY;Preview=new(){Kind=kind,X=point.X,Y=point.Y,Width=0,Height=0,Style=style};return null;
    }
    public void Move(PixelPoint point)
    {
        // 화살표는 방향을 보존하고 사각형·모자이크는 좌상단과 양수 크기로 정규화한다.
        if(Preview is null)return;
        if(Preview.Kind==AnnotationKind.Arrow)Preview=Preview with{Width=point.X-start.X,Height=point.Y-start.Y};
        else {var r=PixelRect.FromPoints(start,point);Preview=Preview with{X=r.X,Y=r.Y,Width=r.Width,Height=r.Height};}
    }
    public Annotation? PointerUp(PixelPoint point)
    {
        // 이동량이 작으면 첫 클릭으로 판단하고 두 번째 클릭에서 확정하도록 대기한다.
        if(Preview is null||waitingForEnd)return null;
        Move(point);if(Math.Sqrt(Math.Pow((point.X-start.X)*scaleX,2)+Math.Pow((point.Y-start.Y)*scaleY,2))<=threshold){waitingForEnd=true;return null;}return Commit();
    }
    private Annotation? Commit()
    {
        var value=Preview;if(value is null)return null;
        if(value.Kind is AnnotationKind.Rectangle or AnnotationKind.Mosaic&&(value.Width*scaleX<=2||value.Height*scaleY<=2))return null;
        if(value.Kind==AnnotationKind.Arrow&&Math.Abs(value.Width*scaleX)<=2&&Math.Abs(value.Height*scaleY)<=2)return null;
        Cancel();return value;
    }
    public void Cancel(){Preview=null;waitingForEnd=false;}
}
