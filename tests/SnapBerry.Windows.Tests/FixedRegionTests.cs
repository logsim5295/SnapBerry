using SnapBerry.App.Capture;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class FixedRegionTests
{
    [Theory]
    [InlineData(1,6,34)]
    [InlineData(1.5,9,51)]
    [InlineData(2,12,68)]
    public void FrameUsesPhysicalPixelsAndKeepsChromeOutsideImage(double scale,int edge,int header)
    {
        var region=new PixelRect(-1200,100,640,480);
        var layout=FixedRegionGeometry.Layout(region,scale);
        Assert.Equal(new PixelRect(edge,header+edge,640,480),layout.Hole);
        Assert.Equal(-1200-edge,layout.Window.X);
        Assert.Equal(100-header-edge,layout.Window.Y);
        Assert.Equal(480+header+2*edge,layout.Window.Height);
    }

    [Fact]
    public void MissingMonitorRestoresWholeRegionInsideAvailableWorkArea()
    {
        var restored=FixedRegionGeometry.Restore(new(-1800,100,640,480),[new(0,0,1920,1040)]);
        Assert.Equal(new PixelRect(6,40,640,480),restored);
        Assert.Equal(new PixelRect(-1000,100,640,480),FixedRegionGeometry.Restore(new(-1000,100,640,480),[new(-1920,0,1920,1040)]));
    }

    [Fact]
    public void RegionAcrossAdjacentMonitorsIsPreservedButGapIsRejected()
    {
        var area=new PixelRect(-100,100,200,200);
        PixelRect[] screens=[new(-1920,0,1920,1080),new(0,0,1920,1080)];
        Assert.True(FixedRegionGeometry.IsCovered(area,screens));
        Assert.Equal(area,FixedRegionGeometry.Restore(area,screens));
        Assert.False(FixedRegionGeometry.IsCovered(area,[screens[0],new(100,0,1920,1080)]));
        Assert.False(FixedRegionGeometry.IsCovered(new(-2000,0,100,100),screens));
    }

    [Fact]
    public void DraggingLeftEdgePastRightKeepsRightEdgeAndOnePixelMinimum()
    {
        Assert.Equal(new PixelRect(199,200,1,80),FixedRegionGeometry.Drag(new(100,200,100,80),FixedRegionEdges.Left,500,0));
        Assert.Equal(new PixelRect(-100,220,100,80),FixedRegionGeometry.Drag(new(100,200,100,80),FixedRegionEdges.None,-200,20));
        Assert.Equal(new PixelRect(90,180,110,100),FixedRegionGeometry.Drag(new(100,200,100,80),FixedRegionEdges.Left|FixedRegionEdges.Top,-10,-20));
    }

    [Fact]
    public void RepeatedCaptureUsesNewFrameAtExactlyTheSameCoordinates()=>Sta.Run(()=>
    {
        var area=new PixelRect(-5,2,4,3);
        foreach(var color in new[]{System.Drawing.Color.Red,System.Drawing.Color.Blue})
        {
            using var bitmap=new System.Drawing.Bitmap(20,10);
            using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.Clear(color);
            var frame=new CaptureFrame(CaptureService.ToBitmapSource(bitmap),new(-10,0,20,10));
            var selected=CaptureService.CropFixedRegion(frame,area);
            Assert.Equal(area,selected.DesktopBounds);
            Assert.Equal((4,3),(selected.Image.PixelWidth,selected.Image.PixelHeight));
            var pixels=new byte[4*3*4];selected.Image.CopyPixels(pixels,16,0);
            Assert.Equal(new byte[]{color.B,color.G,color.R,255},pixels[..4]);
        }
    });

    [Fact]
    public void FixedCaptureNeverSilentlyShrinksAtDesktopEdge()=>Sta.Run(()=>
    {
        var frame=new CaptureFrame(RenderingTests.Image(20,10),new(0,0,20,10));
        Assert.Throws<InvalidOperationException>(()=>CaptureService.CropFixedRegion(frame,new(18,0,4,3)));
    });
}
