using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace SnapBerry.Desktop.Capture;

internal static class MacPickerNative
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public double X, Y; }

    [DllImport(CoreGraphics)] private static extern IntPtr CGEventCreate(IntPtr source);
    [DllImport(CoreGraphics)] private static extern NativePoint CGEventGetLocation(IntPtr e);
    [DllImport(CoreGraphics)] private static extern int CGWindowLevelForKey(int key);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr value);
    [DllImport(ObjC)] private static extern IntPtr sel_registerName(string selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void SendInteger(IntPtr target, IntPtr selector, nint value);

    public static Point GetPointer()
    {
        var e = CGEventCreate(IntPtr.Zero);
        if (e == IntPtr.Zero) throw new IOException("마우스 위치를 읽을 수 없습니다.");
        try
        {
            var point = CGEventGetLocation(e);
            return new(point.X, point.Y);
        }
        finally { CFRelease(e); }
    }

    public static void ConfigureWindow(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle is not { HandleDescriptor: "NSWindow" } || handle.Handle == IntPtr.Zero)
            throw new IOException("영역 선택 창을 열 수 없습니다.");
        const int overlayWindowLevelKey = 15;
        const int canJoinAllSpaces = 1, fullScreenAuxiliary = 1 << 8;
        SendInteger(handle.Handle, sel_registerName("setCollectionBehavior:"), canJoinAllSpaces | fullScreenAuxiliary);
        SendInteger(handle.Handle, sel_registerName("setLevel:"), CGWindowLevelForKey(overlayWindowLevelKey));
    }
}
