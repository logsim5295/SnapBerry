using SnapBerry.Core;
using SnapBerry.App.Capture;
using SnapBerry.App.Rendering;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 데스크톱 경계에 대한 선택 영역 보정과 잘라낸 이미지의 크기를 검증한다.
public class CaptureTests
{
    [Fact] public void ZeroSizeSelectionIsRejected()=>Assert.Throws<ArgumentException>(()=>CaptureService.ValidateRegion(new(0,0,0,10),new(0,0,100,100)));
    [Fact] public void RegionOutsideDesktopIsClipped()=>Assert.Equal(new PixelRect(0,0,20,20),CaptureService.ValidateRegion(new(-10,-10,30,30),new(0,0,100,100)));
    [Fact] public void ExcessiveImageSizeIsRejected()=>Assert.Throws<InvalidOperationException>(()=>CompositeRenderer.GuardSize(100000,100000));
}
