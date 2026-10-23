using SnapBerry.Core;
using SnapBerry.Desktop.Editing;

namespace SnapBerry.Desktop.Capture;

public sealed class CaptureCoordinator(ICaptureService service)
{
    public async Task<bool> CaptureAsync(DesktopCaptureMode mode,DesktopEditor editor,Func<Task> flush,Action hide,Action restore,CancellationToken token=default)
    {
        await flush();hide();
        try
        {
            var png=await service.CaptureAsync(mode,token);if(png is null)return false;
            editor.OpenImage(png,mode switch{DesktopCaptureMode.Region=>CaptureMode.Region,DesktopCaptureMode.Window=>CaptureMode.Window,_=>CaptureMode.Desktop});return true;
        }
        finally{restore();}
    }
}
