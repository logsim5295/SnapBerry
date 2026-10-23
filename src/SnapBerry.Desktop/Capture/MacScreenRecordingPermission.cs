using System.Runtime.InteropServices;

namespace SnapBerry.Desktop.Capture;

public interface IScreenRecordingPermission
{
    bool HasAccess();
    void RequestAccess();
}

public sealed class MacScreenRecordingPermission : IScreenRecordingPermission
{
    private const string CoreGraphics="/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    [DllImport(CoreGraphics)] [return:MarshalAs(UnmanagedType.I1)]
    private static extern bool CGPreflightScreenCaptureAccess();
    [DllImport(CoreGraphics)] [return:MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRequestScreenCaptureAccess();
    public bool HasAccess()=>OperatingSystem.IsMacOS()?CGPreflightScreenCaptureAccess():throw new PlatformNotSupportedException("화면 캡처는 macOS에서 사용할 수 있습니다.");
    public void RequestAccess()
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("화면 캡처는 macOS에서 사용할 수 있습니다.");
        _=CGRequestScreenCaptureAccess();
    }
}
