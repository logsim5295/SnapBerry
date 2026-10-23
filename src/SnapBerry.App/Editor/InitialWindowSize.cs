using System.Windows;
namespace SnapBerry.App.Editor;
// 현재 모니터 작업 영역의 85% 안에 초기 창을 배치하도록 물리 픽셀을 WPF 논리 크기로 환산한다.
public static class InitialWindowSize
{
    public static Size Calculate(double preferredWidth,double preferredHeight,double workWidthPixels,double workHeightPixels,double scaleX,double scaleY)
    {
        if(!double.IsFinite(scaleX)||!double.IsFinite(scaleY)||scaleX<=0||scaleY<=0||workWidthPixels<=0||workHeightPixels<=0)throw new ArgumentOutOfRangeException(nameof(scaleX));
        return new(Math.Min(preferredWidth,workWidthPixels*.85/scaleX),Math.Min(preferredHeight,workHeightPixels*.85/scaleY));
    }
}
