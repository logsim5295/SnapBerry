using System.Runtime.InteropServices;

namespace SnapBerry.Desktop.Capture;

/// <summary>Native display identity, global Core Graphics coordinates, and backing scale.</summary>
public sealed record MacDisplay(uint Id,double X,double Y,double Width,double Height,double Scale);

public static class MacDisplays
{
    private const string CoreGraphics="/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    [StructLayout(LayoutKind.Sequential)] private struct Point{public double X,Y;}
    [StructLayout(LayoutKind.Sequential)] private struct Size{public double Width,Height;}
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle{public Point Origin;public Size Size;}
    [DllImport(CoreGraphics)] private static extern int CGGetActiveDisplayList(uint maximum,[Out] uint[]? displays,out uint count);
    [DllImport(CoreGraphics)] private static extern Rectangle CGDisplayBounds(uint display);
    [DllImport(CoreGraphics)] private static extern uint CGDisplayMirrorsDisplay(uint display);
    [DllImport(CoreGraphics)] private static extern IntPtr CGDisplayCopyDisplayMode(uint display);
    [DllImport(CoreGraphics)] private static extern nuint CGDisplayModeGetPixelWidth(IntPtr mode);
    [DllImport(CoreGraphics)] private static extern nuint CGDisplayModeGetWidth(IntPtr mode);
    [DllImport(CoreGraphics)] private static extern void CGDisplayModeRelease(IntPtr mode);
    public static IReadOnlyList<MacDisplay> GetActive()
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("화면 캡처는 macOS에서 사용할 수 있습니다.");
        if(CGGetActiveDisplayList(0,null,out var count)!=0||count==0)throw new IOException("활성 화면을 찾을 수 없습니다.");
        var ids=new uint[count];
        if(CGGetActiveDisplayList(count,ids,out count)!=0)throw new IOException("화면 목록을 읽을 수 없습니다.");
        var displays=new List<MacDisplay>();
        for(var index=0;index<count;index++)
        {
            var id=ids[index];if(CGDisplayMirrorsDisplay(id)!=0)continue;
            var bounds=CGDisplayBounds(id);var mode=CGDisplayCopyDisplayMode(id);
            if(mode==IntPtr.Zero)throw new IOException("화면 해상도를 읽을 수 없습니다.");
            try
            {
                var width=CGDisplayModeGetWidth(mode);if(width==0)throw new IOException("화면 해상도가 올바르지 않습니다.");
                var scale=(double)CGDisplayModeGetPixelWidth(mode)/width;
                displays.Add(new(id,bounds.Origin.X,bounds.Origin.Y,bounds.Size.Width,bounds.Size.Height,scale));
            }
            finally{CGDisplayModeRelease(mode);}
        }
        return displays;
    }
}
