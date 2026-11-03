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
    public static bool IsValidCanvas(PixelRect rect)=>ImageDimensions.IsSupported(rect.Width,rect.Height)
        &&rect.X>int.MinValue&&rect.Y>int.MinValue&&(long)rect.X+rect.Width<=int.MaxValue&&(long)rect.Y+rect.Height<=int.MaxValue;

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
        // 반대쪽 꼭짓점을 고정하고 원본 밖까지 늘릴 수 있다. 반대편을 지나도 1픽셀은 남긴다.
        if(Corner is not CropCorner corner||!double.IsFinite(point.X)||!double.IsFinite(point.Y))return;
        var fromLeft=corner is CropCorner.TopLeft or CropCorner.BottomLeft;
        var fromTop=corner is CropCorner.TopLeft or CropCorner.TopRight;
        var dx=point.X-pressed.X;var dy=point.Y-pressed.Y;
        var width=(int)Math.Clamp(Math.Round(bounds.Width+(fromLeft?-dx:dx)),1,int.MaxValue);
        var height=(int)Math.Clamp(Math.Round(bounds.Height+(fromTop?-dy:dy)),1,int.MaxValue);
        if(ImageDimensions.IsSupported(width,height))
            Preview=new(fromLeft?bounds.Width-width:0,fromTop?bounds.Height-height:0,width,height);
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
