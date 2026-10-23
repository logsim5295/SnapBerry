using System.Diagnostics;
using SkiaSharp;
using SnapBerry.Core;
using SnapBerry.Desktop.Rendering;

namespace SnapBerry.Desktop.Capture;

public enum DesktopCaptureMode { Region,Window,Display }
public interface ICaptureService {Task<byte[]?> CaptureAsync(DesktopCaptureMode mode,CancellationToken cancellationToken=default);}
public sealed record CaptureProcessResult(int ExitCode,string Error);

public sealed class MacCaptureService(Func<ProcessStartInfo,CancellationToken,Task<CaptureProcessResult>>? runner=null,IScreenRecordingPermission? permission=null,Func<IReadOnlyList<MacDisplay>>? displayProvider=null) : ICaptureService
{
    private readonly IScreenRecordingPermission? screenPermission=permission??(runner is null?new MacScreenRecordingPermission():null);
    public async Task<byte[]?> CaptureAsync(DesktopCaptureMode mode,CancellationToken cancellationToken=default)
    {
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        cancellationToken.ThrowIfCancellationRequested();
        if(screenPermission is not null&&!screenPermission.HasAccess())
        {
            screenPermission.RequestAccess();
            if(!screenPermission.HasAccess())throw new IOException("화면 기록 권한이 필요합니다. 시스템 설정 → 개인정보 보호 및 보안 → 화면 및 시스템 오디오 기록에서 SnapBerry를 허용한 뒤 앱을 다시 열어 주세요.");
        }
        if(mode==DesktopCaptureMode.Display&&(displayProvider is not null||runner is null))
        {
            var displays=(displayProvider??MacDisplays.GetActive)().DistinctBy(d=>(d.X,d.Y,d.Width,d.Height)).ToArray();
            if(displays.Length==0)throw new IOException("캡처할 화면을 찾을 수 없습니다.");
            ValidateDisplays(displays);
            if(displays.Length>1)return await CaptureDesktopAsync(displays,cancellationToken);
        }
        return await CaptureOneAsync(mode,null,cancellationToken);
    }
    private async Task<byte[]?> CaptureOneAsync(DesktopCaptureMode mode,MacDisplay? display,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-capture-"+Guid.NewGuid().ToString("N")+".png");
        var start=new ProcessStartInfo("/usr/sbin/screencapture"){UseShellExecute=false,RedirectStandardError=true,CreateNoWindow=true};
        start.ArgumentList.Add("-x");start.ArgumentList.Add("-t");start.ArgumentList.Add("png");
        if(mode==DesktopCaptureMode.Display)
        {
            if(display is not null)
            {
                start.ArgumentList.Add("-R");
                start.ArgumentList.Add(FormattableString.Invariant($"{display.X:R},{display.Y:R},{display.Width:R},{display.Height:R}"));
            }
            else start.ArgumentList.Add("-m");
        }
        else{start.ArgumentList.Add("-i");start.ArgumentList.Add(mode==DesktopCaptureMode.Region?"-s":"-w");}
        start.ArgumentList.Add(path);
        try
        {
            var result=await (runner??RunProcessAsync)(start,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if(result.ExitCode==0&&File.Exists(path))return await File.ReadAllBytesAsync(path,cancellationToken);
            if(mode!=DesktopCaptureMode.Display&&result.ExitCode==1&&string.IsNullOrWhiteSpace(result.Error))return null;
            throw new IOException("화면을 캡처하지 못했습니다. 시스템 설정 → 개인정보 보호 및 보안 → 화면 기록에서 SnapBerry 권한을 확인해 주세요. "+result.Error.Trim());
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
    private async Task<byte[]> CaptureDesktopAsync(MacDisplay[] displays,CancellationToken token)
    {
        var images=new List<SKBitmap>();
        try
        {
            var scale=displays.Max(d=>d.Scale);
            foreach(var display in displays)
            {
                var bytes=await CaptureOneAsync(DesktopCaptureMode.Display,display,token)??throw new IOException("화면을 캡처하지 못했습니다.");
                var image=SkiaDocumentRenderer.Decode(bytes);images.Add(image);
                // Captured pixel dimensions are authoritative, including rotated/scaled Retina modes.
                scale=Math.Max(scale,Math.Max(image.Width/display.Width,image.Height/display.Height));
            }
            var (left,top,width,height)=CanvasBounds(displays,scale);
            using var colorSpace=SKColorSpace.CreateSrgb();
            using var output=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Premul,colorSpace));
            using var canvas=new SKCanvas(output);canvas.Clear(SKColors.Transparent);
            for(var i=0;i<displays.Length;i++)
            {
                token.ThrowIfCancellationRequested();var display=displays[i];
                var rectangle=new SKRect((float)((display.X-left)*scale),(float)((display.Y-top)*scale),(float)((display.X+display.Width-left)*scale),(float)((display.Y+display.Height-top)*scale));
                canvas.DrawBitmap(images[i],rectangle);
            }
            token.ThrowIfCancellationRequested();return SkiaDocumentRenderer.Encode(output);
        }
        finally{foreach(var image in images)image.Dispose();}
    }
    private static void ValidateDisplays(MacDisplay[] displays)
    {
        if(displays.Any(d=>d.Id==0||!double.IsFinite(d.X)||!double.IsFinite(d.Y)||!double.IsFinite(d.Width)||!double.IsFinite(d.Height)||!double.IsFinite(d.Scale)||d.Width<=0||d.Height<=0||d.Scale<=0))throw new InvalidDataException("화면 배치 정보가 올바르지 않습니다.");
        _=CanvasBounds(displays,displays.Max(d=>d.Scale));
    }
    private static (double Left,double Top,int Width,int Height) CanvasBounds(MacDisplay[] displays,double scale)
    {
        var left=displays.Min(d=>d.X);var top=displays.Min(d=>d.Y);
        var width=Math.Ceiling((displays.Max(d=>d.X+d.Width)-left)*scale);var height=Math.Ceiling((displays.Max(d=>d.Y+d.Height)-top)*scale);
        if(!double.IsFinite(width)||!double.IsFinite(height)||width>int.MaxValue||height>int.MaxValue)throw new InvalidDataException("전체 화면 이미지가 너무 큽니다.");
        try{ImageDimensions.GuardSize((int)width,(int)height);}catch(Exception error) when(error is InvalidOperationException or OverflowException){throw new InvalidDataException("전체 화면 이미지가 너무 큽니다. 더 작은 영역을 캡처해 주세요.",error);}
        return(left,top,(int)width,(int)height);
    }
    private static async Task<CaptureProcessResult> RunProcessAsync(ProcessStartInfo start,CancellationToken token)
    {
        if(!OperatingSystem.IsMacOS())throw new PlatformNotSupportedException("화면 캡처는 macOS에서 사용할 수 있습니다.");
        using var process=Process.Start(start)??throw new IOException("macOS 캡처 도구를 시작하지 못했습니다.");
        using var registration=token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}});
        var error=process.StandardError.ReadToEndAsync(token);await process.WaitForExitAsync(token);return new(process.ExitCode,await error);
    }
}
