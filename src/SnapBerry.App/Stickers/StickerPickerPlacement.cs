using System.Windows;
using System.Windows.Interop;
using SnapBerry.App.Interop;
using SnapBerry.App.Settings;
using Screen=System.Windows.Forms.Screen;

namespace SnapBerry.App.Stickers;

public static class StickerPickerPlacement
{
    internal static string SelectMonitor(StickerWindowState? saved,IReadOnlyList<string> monitors,string fallback)
        =>saved is {IsValid:true}?monitors.FirstOrDefault(name=>string.Equals(name,saved.MonitorName,StringComparison.OrdinalIgnoreCase))??fallback:fallback;

    internal static Rect Restore(StickerWindowState saved,Rect work,double scaleX,double scaleY)
    {
        var width=Math.Min(Math.Max(270,saved.Width),work.Width/scaleX)*scaleX;
        var height=Math.Min(Math.Max(300,saved.Height),work.Height/scaleY)*scaleY;
        // 먼저 논리 단위에서 제한해 손상된 큰 값도 곱셈 오버플로 없이 처리한다.
        var left=work.Left+Math.Clamp(saved.OffsetX,0,Math.Max(0,(work.Width-width)/scaleX))*scaleX;
        var top=work.Top+Math.Clamp(saved.OffsetY,0,Math.Max(0,(work.Height-height)/scaleY))*scaleY;
        return new(left,top,width,height);
    }

    internal static StickerWindowState Remember(string monitor,Rect bounds,Rect work,double scaleX,double scaleY)
        =>new(monitor,(bounds.Left-work.Left)/scaleX,(bounds.Top-work.Top)/scaleY,bounds.Width/scaleX,bounds.Height/scaleY);

    // owner/work는 화면의 물리 픽셀, preferredSize는 WPF 논리 단위다.
    public static Rect Calculate(Rect owner,Rect work,Size preferredSize,double scaleX,double scaleY)
    {
        var width=Math.Min(preferredSize.Width*scaleX,work.Width);
        var height=Math.Min(preferredSize.Height*scaleY,work.Height);
        var left=owner.Left+Math.Max(0,owner.Width-width-16*scaleX);
        var top=owner.Top+Math.Max(0,Math.Min(180*scaleY,owner.Height-height-32*scaleY));
        return new(Math.Clamp(left,work.Left,work.Right-width),Math.Clamp(top,work.Top,work.Bottom-height),width,height);
    }

    internal static void Apply(Window picker,Window owner,StickerWindowState? saved,Size initialSize)
    {
        var ownerHandle=new WindowInteropHelper(owner).Handle;
        var pickerHandle=new WindowInteropHelper(picker).Handle;
        if(pickerHandle==IntPtr.Zero||!NativeMethods.GetWindowRect(ownerHandle,out var ownerRect))return;
        var screens=Screen.AllScreens;var fallback=Screen.FromHandle(ownerHandle);
        var monitorName=SelectMonitor(saved,screens.Select(screen=>screen.DeviceName).ToArray(),fallback.DeviceName);
        var target=screens.FirstOrDefault(screen=>screen.DeviceName==monitorName)??fallback;
        var area=target.WorkingArea;var work=new Rect(area.Left,area.Top,area.Width,area.Height);
        // 먼저 대상 모니터로 옮겨 네이티브 HWND의 실제 DPI를 얻는다. 편집기와 배율이 달라도 된다.
        if(Screen.FromHandle(pickerHandle).DeviceName!=target.DeviceName)
            NativeMethods.SetWindowPos(pickerHandle,IntPtr.Zero,area.Left,area.Top,
                Math.Max(1,Math.Min(area.Width,(int)initialSize.Width)),Math.Max(1,Math.Min(area.Height,(int)initialSize.Height)),0x14);
        var scale=NativeMethods.GetDpiForWindow(pickerHandle)/96d;if(scale<=0)scale=1;
        var bounds=saved is {IsValid:true}?Restore(saved,work,scale,scale)
            :Calculate(new(ownerRect.Left,ownerRect.Top,ownerRect.Right-ownerRect.Left,ownerRect.Bottom-ownerRect.Top),work,initialSize,scale,scale);
        picker.MinWidth=Math.Min(picker.MinWidth,bounds.Width/scale);
        picker.MinHeight=Math.Min(picker.MinHeight,bounds.Height/scale);
        picker.Width=bounds.Width/scale;picker.Height=bounds.Height/scale;
        // 최대화 전 Left/Top과 현재 크기를 섞지 않는다. 음수 모니터 원점도 물리 좌표 그대로 사용한다.
        NativeMethods.SetWindowPos(pickerHandle,IntPtr.Zero,(int)Math.Round(bounds.Left),(int)Math.Round(bounds.Top),
            (int)Math.Floor(bounds.Width),(int)Math.Floor(bounds.Height),0x14); // SWP_NOZORDER | SWP_NOACTIVATE
    }

    internal static StickerWindowState? Capture(Window picker)
    {
        var handle=new WindowInteropHelper(picker).Handle;
        if(handle==IntPtr.Zero||picker.WindowState!=WindowState.Normal||NativeMethods.IsIconic(handle)
            ||!NativeMethods.GetWindowRect(handle,out var bounds))return null;
        var screen=Screen.FromHandle(handle);var work=screen.WorkingArea;
        var scale=NativeMethods.GetDpiForWindow(handle)/96d;if(scale<=0)return null;
        var saved=Remember(screen.DeviceName,new(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top),
            new(work.Left,work.Top,work.Width,work.Height),scale,scale);
        return saved.IsValid?saved:null;
    }
}
