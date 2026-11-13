using System.ComponentModel;
using System.Runtime.InteropServices;
using SnapBerry.App.Capture;

namespace SnapBerry.App.Interop;

internal static class FixedRegionNativeShape
{
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] private static extern int CombineRgn(nint target,nint first,nint second,int mode);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint handle);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window,nint region,bool redraw);

    // 반환된 HRGN은 호출자가 소유한다. SetWindowRgn 성공 시에만 Windows로 소유권을 넘긴다.
    internal static nint Create(FixedRegionLayout layout)
    {
        var hole=layout.Hole;
        var ring=CreateRectRgn(0,layout.Header,hole.Width+2*layout.Edge,layout.Window.Height);
        var inner=CreateRectRgn(hole.X,hole.Y,hole.X+hole.Width,hole.Y+hole.Height);
        var toolbar=CreateRectRgn(0,0,layout.ToolbarWidth,layout.Header);
        try
        {
            if(ring==0||inner==0||toolbar==0||CombineRgn(ring,ring,inner,4)==0||CombineRgn(ring,ring,toolbar,2)==0)
                throw new Win32Exception("고정 영역 테두리를 만들 수 없습니다.");
            var result=ring;ring=0;return result;
        }
        finally
        {
            if(ring!=0)DeleteObject(ring);
            if(inner!=0)DeleteObject(inner);
            if(toolbar!=0)DeleteObject(toolbar);
        }
    }

    internal static void Apply(nint window,FixedRegionLayout layout)
    {
        var shape=Create(layout);
        if(SetWindowRgn(window,shape,true)!=0)return;
        DeleteObject(shape);
        throw new Win32Exception("고정 영역 테두리를 표시할 수 없습니다.");
    }
}
