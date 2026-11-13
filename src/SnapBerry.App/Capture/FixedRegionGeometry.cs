using SnapBerry.Core;

namespace SnapBerry.App.Capture;

[Flags]
internal enum FixedRegionEdges { None=0, Left=1, Top=2, Right=4, Bottom=8 }

internal readonly record struct FixedRegionLayout(PixelRect Window,PixelRect Hole,int Edge,int Header,int ToolbarWidth);

// 장식을 제외한 실제 화면의 물리 픽셀 좌표를 저장한다. DPI가 바뀌어도 촬영 크기는 유지한다.
internal static class FixedRegionGeometry
{
    internal static bool IsValid(PixelRect rect)=>rect.Width>0&&rect.Height>0
        &&rect.Width<=16384&&rect.Height<=16384&&(long)rect.Width*rect.Height<=512L*1024*1024/20
        &&Math.Abs((long)rect.X)<=1000000&&Math.Abs((long)rect.Y)<=1000000;

    internal static FixedRegionLayout Layout(PixelRect region,double scale)
    {
        var edge=(int)Math.Ceiling(6*scale);var header=(int)Math.Ceiling(34*scale);var toolbar=(int)Math.Ceiling(300*scale);
        return new(new(region.X-edge,region.Y-header-edge,Math.Max(region.Width+2*edge,toolbar),region.Height+header+2*edge),
            new(edge,header+edge,region.Width,region.Height),edge,header,toolbar);
    }

    internal static PixelRect Drag(PixelRect start,FixedRegionEdges edges,int dx,int dy)
    {
        long left=start.X,top=start.Y,right=left+start.Width,bottom=top+start.Height;
        if(edges==FixedRegionEdges.None){left+=dx;right+=dx;top+=dy;bottom+=dy;}
        else
        {
            if(edges.HasFlag(FixedRegionEdges.Left))left=Math.Min(right-1,left+dx);
            if(edges.HasFlag(FixedRegionEdges.Top))top=Math.Min(bottom-1,top+dy);
            if(edges.HasFlag(FixedRegionEdges.Right))right=Math.Max(left+1,right+dx);
            if(edges.HasFlag(FixedRegionEdges.Bottom))bottom=Math.Max(top+1,bottom+dy);
        }
        if(left<int.MinValue||left>int.MaxValue||top<int.MinValue||top>int.MaxValue||right-left>int.MaxValue||bottom-top>int.MaxValue)return start;
        var result=new PixelRect((int)left,(int)top,(int)(right-left),(int)(bottom-top));
        return IsValid(result)?result:start;
    }

    // 모니터 사이의 빈 공간을 실제 화면으로 취급하지 않는다. 미러링된 모니터도 중복 합산하지 않는다.
    internal static bool IsCovered(PixelRect region,IReadOnlyList<PixelRect> screens)
    {
        if(!IsValid(region))return false;
        var remaining=new List<PixelRect>{region};
        foreach(var screen in screens)
        {
            var next=new List<PixelRect>();
            foreach(var rect in remaining)
            {
                var overlap=rect.Intersect(screen);
                if(overlap.Width==0){next.Add(rect);continue;}
                Add(rect.X,rect.Y,rect.Width,overlap.Y-rect.Y);
                Add(rect.X,overlap.Y+overlap.Height,rect.Width,rect.Y+rect.Height-overlap.Y-overlap.Height);
                Add(rect.X,overlap.Y,overlap.X-rect.X,overlap.Height);
                Add(overlap.X+overlap.Width,overlap.Y,rect.X+rect.Width-overlap.X-overlap.Width,overlap.Height);
            }
            remaining=next;
            if(remaining.Count==0)return true;
            void Add(int x,int y,int width,int height){if(width>0&&height>0)next.Add(new(x,y,width,height));}
        }
        return false;
    }

    internal static PixelRect Restore(PixelRect? saved,IReadOnlyList<PixelRect> workAreas,double scale=1)
    {
        if(workAreas.Count==0)throw new InvalidOperationException("사용할 수 있는 모니터가 없습니다.");
        var region=saved is {} value&&IsValid(value)?value:new PixelRect(workAreas[0].X+80,workAreas[0].Y+100,640,480);
        var layout=Layout(region,scale);
        var toolbar=new PixelRect(layout.Window.X,layout.Window.Y,layout.ToolbarWidth,layout.Header);
        if(IsCovered(region,workAreas)&&IsCovered(toolbar,workAreas))return region;
        var work=workAreas.OrderByDescending(area=>(long)region.Intersect(area).Width*region.Intersect(area).Height).First();
        var width=Math.Min(region.Width,Math.Max(1,work.Width-2*layout.Edge));
        var height=Math.Min(region.Height,Math.Max(1,work.Height-layout.Header-2*layout.Edge));
        var x=work.X+layout.Edge;var y=work.Y+layout.Header+layout.Edge;
        return new(x,y,width,height);
    }
}
