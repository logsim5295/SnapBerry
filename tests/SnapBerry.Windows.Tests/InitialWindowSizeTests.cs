using SnapBerry.App.Editor;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 다양한 모니터 크기와 DPI 배율에서 초기 창이 작업 영역 안에 맞는지 검증한다.
public class InitialWindowSizeTests
{
    [Theory] [InlineData(1920,1040,1)] [InlineData(1920,1040,1.25)] [InlineData(1920,1040,1.5)] [InlineData(1920,1040,1.75)] [InlineData(2560,1520,2)] [InlineData(3840,2080,2.5)] [InlineData(1280,680,2)]
    public void StartupSizeFitsScaledMonitorWorkArea(double width,double height,double scale)
    {var size=InitialWindowSize.Calculate(1240,820,width,height,scale,scale);Assert.True(size.Width*scale<=width*.85+.01);Assert.True(size.Height*scale<=height*.85+.01);Assert.InRange(size.Width,1,1240);Assert.InRange(size.Height,1,820);}
    [Fact] public void LargeMonitorKeepsPreferredLogicalSize()
    {var size=InitialWindowSize.Calculate(1240,820,3840,2080,1.5,1.5);Assert.Equal(1240,size.Width);Assert.Equal(820,size.Height);}
}
