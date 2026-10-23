namespace SnapBerry.Core;
// 포인터 위치에서 편집할 주석을 찾는다. 나중에 그린 객체가 화면의 위쪽에 있으므로 역순 검색한다.
public static class AnnotationHitTester
{
    public static Annotation? Hit(IReadOnlyList<Annotation> items,PixelPoint p,double tolerance,double scaleX=1,double scaleY=1)
    {
        for(var i=items.Count-1;i>=0;i--)
        {
            var a=items[i];
            if(a.Kind==AnnotationKind.Arrow)
            {
                // 선분 위의 최근접점을 구해 거리로 판정한다. 사각 바운드만 쓰면 대각선 빈 곳도 선택된다.
                var dx=a.Width*scaleX;var dy=a.Height*scaleY;var px=(p.X-a.X)*scaleX;var py=(p.Y-a.Y)*scaleY;
                var l=dx*dx+dy*dy;var t=l==0?0:Math.Clamp((px*dx+py*dy)/l,0,1);
                var penScale=l==0?Math.Max(scaleX,scaleY):Math.Sqrt((dy*dy*scaleX*scaleX+dx*dx*scaleY*scaleY)/l);
                if(Math.Sqrt(Math.Pow(px-t*dx,2)+Math.Pow(py-t*dy,2))<=tolerance+a.Style.Thickness*penScale)return a;
            }
            else if(p.X>=a.X-tolerance/scaleX&&p.X<=a.X+a.Width+tolerance/scaleX&&p.Y>=a.Y-tolerance/scaleY&&p.Y<=a.Y+a.Height+tolerance/scaleY)return a;
        }
        return null;
    }
}
