using System.Windows;
using System.Windows.Interop;
using SnapBerry.App.Capture;
using SnapBerry.App.Interop;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class CaptureNativeCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransitionSuppressionUsesSupportedDwmCallsOnAnUnshownWindow(bool transparent)=>Sta.Run(()=>
    {
        // EnsureHandle은 표시·활성화하지 않고 HWND만 만든다. 픽셀을 읽는 CaptureAsync는 호출하지 않는다.
        var window=new Window{WindowStyle=WindowStyle.None,AllowsTransparency=transparent};
        try
        {
            var handle=new WindowInteropHelper(window).EnsureHandle();
            using(var session=new CaptureSession(new WindowsCaptureDesktop(window)))
            {
                Assert.False(window.IsVisible);
                Assert.False(NativeMethods.IsWindowVisible(handle));
            }
            Assert.False(window.IsVisible);
            Assert.False(NativeMethods.IsWindowVisible(handle));
        }
        finally{window.Close();}
    });
}
