using System.Runtime.InteropServices;
using SnapBerry.App.Capture;
using SnapBerry.App.Interop;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class FixedRegionShapeTests
{
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(nint region,int x,int y);

    [Theory]
    [InlineData(1,1,1)]
    [InlineData(640,480,1)]
    [InlineData(640,480,1.5)]
    [InlineData(640,480,2)]
    public void NativeShapeContainsOnlyToolbarAndBorder(int width,int height,double scale)
    {
        // HRGN만 생성한다. HWND 생성·창 표시·데스크톱 캡처는 하지 않는다.
        var layout=FixedRegionGeometry.Layout(new(-100,100,width,height),scale);
        var shape=FixedRegionNativeShape.Create(layout);
        try
        {
            var hole=layout.Hole;
            Assert.False(PtInRegion(shape,hole.X,hole.Y));
            Assert.False(PtInRegion(shape,hole.X+width-1,hole.Y+height-1));
            Assert.True(PtInRegion(shape,hole.X-1,hole.Y));
            Assert.True(PtInRegion(shape,hole.X+width,hole.Y));
            Assert.True(PtInRegion(shape,hole.X,hole.Y-1));
            Assert.True(PtInRegion(shape,hole.X,hole.Y+height));
            Assert.True(PtInRegion(shape,layout.ToolbarWidth-1,0));
            if(width==1)Assert.False(PtInRegion(shape,layout.ToolbarWidth-1,hole.Y));
        }
        finally { FixedRegionNativeShape.DeleteObject(shape); }
    }
}
