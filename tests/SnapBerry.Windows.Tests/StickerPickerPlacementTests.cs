using System.Windows;
using SnapBerry.App.Stickers;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class StickerPickerPlacementTests
{
    [Theory]
    [InlineData(1700,800,1240,820,0,0,1920,1040,1)]
    [InlineData(-2200,-400,1240,820,-1920,40,1920,1040,1.5)]
    [InlineData(3500,900,1800,1200,1920,0,2560,1400,2)]
    [InlineData(-8,-8,1936,1056,0,0,1920,1040,1.25)]
    [InlineData(0,0,640,480,0,0,640,440,2)]
    public void EntirePickerFitsOwnerMonitorWorkArea(double x,double y,double width,double height,
        double workX,double workY,double workWidth,double workHeight,double scale)
    {
        var work=new Rect(workX,workY,workWidth,workHeight);
        var result=StickerPickerPlacement.Calculate(new(x,y,width,height),work,new(490,450),scale,scale);
        Assert.True(work.Contains(result),$"Picker {result} extends outside work area {work}.");
        Assert.True(result.Width>0&&result.Height>0);
    }

    [Fact]
    public void UnclippedWindowKeepsPreferredSizeAndOffset()
    {
        var result=StickerPickerPlacement.Calculate(new(100,100,1240,820),new(0,0,1920,1040),new(490,450),1,1);
        Assert.Equal(new Rect(834,280,490,450),result);
    }

    [Fact]
    public void ScaledSecondaryMonitorKeepsPhysicalOrigin()
    {
        var result=StickerPickerPlacement.Calculate(new(-2500,100,1800,1200),new(-2560,0,2560,1400),new(490,450),2,2);
        Assert.Equal(new Rect(-1712,336,980,900),result);
    }
}
