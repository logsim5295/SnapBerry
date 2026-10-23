namespace SnapBerry.Core;

public enum CropCorner { TopLeft,TopRight,BottomLeft,BottomRight }

// UI와 분리된 자르기 제스처 상태다. 드래그 중에는 Preview만 바꾸고 놓을 때 확정한다.
public sealed class CropPlacement
{
    private PixelRect bounds;private PixelPoint pressed;
    public CropCorner? Corner {get;private set;}
    public PixelRect? Preview {get;private set;}
    public bool Active=>Corner is not null;
    public static bool IsWithin(PixelRect rect,int width,int height)=>rect.X>=0&&rect.Y>=0&&rect.Width>0&&rect.Height>0&&(long)rect.X+rect.Width<=width&&(long)rect.Y+rect.Height<=height;

    public static CropCorner? HitCorner(int width,int height,PixelPoint point,double tolerance,double scaleX=1,double scaleY=1)
    {
        // 허용 범위 안에서 가장 가까운 꼭짓점을 골라 작은 이미지의 중복 판정을 해소한다.
        if(width<=0||height<=0||!double.IsFinite(point.X)||!double.IsFinite(point.Y))return null;
        var corners=new[]{(CropCorner.TopLeft,new PixelPoint(0,0)),(CropCorner.TopRight,new PixelPoint(width,0)),(CropCorner.BottomLeft,new PixelPoint(0,height)),(CropCorner.BottomRight,new PixelPoint(width,height))};
        return corners.Where(c=>Math.Abs(c.Item2.X-point.X)*scaleX<=tolerance&&Math.Abs(c.Item2.Y-point.Y)*scaleY<=tolerance)
            .OrderBy(c=>Math.Pow((c.Item2.X-point.X)*scaleX,2)+Math.Pow((c.Item2.Y-point.Y)*scaleY,2)).Select(c=>(CropCorner?)c.Item1).FirstOrDefault();
    }

    public bool PointerDown(int width,int height,PixelPoint point,double tolerance,double scaleX=1,double scaleY=1)
    {
        Cancel();Corner=HitCorner(width,height,point,tolerance,scaleX,scaleY);if(Corner is null)return false;
        bounds=new(0,0,width,height);pressed=point;Preview=bounds;return true;
    }

    public void Move(PixelPoint point)
    {
        // 선택한 꼭짓점만 움직이고 반대편은 고정한다. 결과는 이미지 내부의 최소 1픽셀이다.
        if(Corner is not CropCorner corner||!double.IsFinite(point.X)||!double.IsFinite(point.Y))return;
        var fromLeft=corner is CropCorner.TopLeft or CropCorner.BottomLeft;
        var fromTop=corner is CropCorner.TopLeft or CropCorner.TopRight;
        var x=(int)Math.Round(Math.Clamp((fromLeft?0:bounds.Width)+point.X-pressed.X,0,bounds.Width));
        var y=(int)Math.Round(Math.Clamp((fromTop?0:bounds.Height)+point.Y-pressed.Y,0,bounds.Height));
        var left=fromLeft?Math.Min(x,bounds.Width-1):0;var top=fromTop?Math.Min(y,bounds.Height-1):0;
        var right=fromLeft?bounds.Width:Math.Max(1,x);var bottom=fromTop?bounds.Height:Math.Max(1,y);
        Preview=new(left,top,right-left,bottom-top);
    }

    public PixelRect? PointerUp(PixelPoint point)
    {
        // 실제 영역 변화가 없으면 명령을 만들지 않아 불필요한 실행 취소 항목을 방지한다.
        if(!Active)return null;
        if(!double.IsFinite(point.X)||!double.IsFinite(point.Y)){Cancel();return null;}
        Move(point);var result=Preview;Cancel();return result==bounds?null:result;
    }
    public void Cancel(){Corner=null;Preview=null;}
}
