using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SnapBerry.Core;
using SnapBerry.App.Interop;
namespace SnapBerry.App.Capture;
// 모니터마다 캡처 오버레이를 띄우고 영역 또는 창 선택 결과를 하나의 비동기 작업으로 반환한다.
public sealed class CaptureCoordinator
{
    public Task<CaptureFrame?> SelectAsync(CaptureFrame frame,CaptureMode mode)
    {
        if(mode==CaptureMode.Desktop)return Task.FromResult<CaptureFrame?>(frame);
        var source=new TaskCompletionSource<CaptureFrame?>();var windows=new List<CaptureOverlay>();var candidates=NativeMethods.VisibleWindows();PixelPoint? start=null;PixelRect? current=null;var complete=false;
        void Finish(PixelRect? rect,CaptureOverlay? closed=null)
        {
            // 마우스·취소·디스플레이 변경이 겹쳐도 한 번만 완료하고 모든 오버레이와 이벤트를 정리한다.
            if(complete)return;complete=true;SystemEvents.DisplaySettingsChanged-=Changed;
            foreach(var window in windows)if(window!=closed)window.Close();
            try{source.TrySetResult(rect is null?null:CaptureService.Crop(frame,rect.Value));}catch(Exception e){source.TrySetException(e);}
        }
        void Changed(object? sender,EventArgs e)=>Application.Current.Dispatcher.BeginInvoke(()=>Finish(null));
        SystemEvents.DisplaySettingsChanged+=Changed;
        foreach(var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var b=screen.Bounds;var bounds=new PixelRect(b.X,b.Y,b.Width,b.Height);var crop=CaptureService.Crop(frame,bounds).Image;
            CaptureOverlay window=null!;window=new CaptureOverlay(bounds,crop,()=>Finish(null,window));windows.Add(window);
            window.MouseLeftButtonDown+=(_,e)=>{var p=NativeMethods.Cursor();if(mode==CaptureMode.Window){Finish(current);return;}start=p;window.CaptureMouse();e.Handled=true;};
            window.MouseMove+=(_,_)=>
            {
                var p=NativeMethods.Cursor();if(mode==CaptureMode.Window)current=candidates.Where(r=>p.X>=r.X&&p.X<r.X+r.Width&&p.Y>=r.Y&&p.Y<r.Y+r.Height).Select(r=>(PixelRect?)r).FirstOrDefault();
                else if(start is not null)current=PixelRect.FromPoints(start.Value,p);
                foreach(var overlay in windows)overlay.SetSelection(current);
            };
            window.MouseLeftButtonUp+=(_,_)=>{if(mode==CaptureMode.Region&&start is not null){window.ReleaseMouseCapture();var selection=PixelRect.FromPoints(start.Value,NativeMethods.Cursor());if(selection.Width>0&&selection.Height>0)Finish(selection);else {start=null;current=null;foreach(var overlay in windows)overlay.SetSelection(null);}}};
            window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)Finish(null);};window.Show();
        }
        windows.FirstOrDefault()?.Activate();return source.Task;
    }
}
// 고정된 화면 이미지 위에 선택 영역과 크기를 표시한다. 네이티브 위치 지정은 물리 픽셀을 사용한다.
internal sealed class CaptureOverlay : Window
{
    private readonly Canvas canvas=new();private readonly Border selection=new(){BorderBrush=Brushes.White,BorderThickness=new Thickness(2),Background=new SolidColorBrush(Color.FromArgb(20,255,255,255))};private readonly TextBlock size=new(){Foreground=Brushes.White,Background=Brushes.Black,Padding=new Thickness(5)};
    private readonly PixelRect bounds;
    public CaptureOverlay(PixelRect screen,BitmapSource background,Action? cancel=null)
    {
        bounds=screen;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;Cursor=Cursors.Cross;Left=screen.X;Top=screen.Y;Width=screen.Width;Height=screen.Height;
        var grid=new Grid();grid.Children.Add(new Image{Source=background,Stretch=Stretch.Fill});grid.Children.Add(new Border{Background=new SolidColorBrush(Color.FromArgb(65,0,0,0)),IsHitTestVisible=false});grid.Children.Add(canvas);canvas.Background=Brushes.Transparent;canvas.Children.Add(selection);canvas.Children.Add(size);Content=grid;SetSelection(null);
        SourceInitialized+=(_,_)=>NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0040);
        // Alt+F4 등 일반 창 닫기도 Esc와 같은 취소 경로로 완료한다.
        Closed+=(_,_)=>cancel?.Invoke();
    }
    public void SetSelection(PixelRect? value)
    {
        // 화면 전체 기준 선택 영역을 이 모니터 창의 WPF 좌표로 바꿔 그린다.
        if(value is null){selection.Visibility=size.Visibility=Visibility.Hidden;return;}var r=value.Value;selection.Visibility=size.Visibility=Visibility.Visible;
        var scaleX=ActualWidth/bounds.Width;var scaleY=ActualHeight/bounds.Height;var x=(r.X-bounds.X)*scaleX;var y=(r.Y-bounds.Y)*scaleY;
        Canvas.SetLeft(selection,x);Canvas.SetTop(selection,y);selection.Width=Math.Max(0,r.Width*scaleX);selection.Height=Math.Max(0,r.Height*scaleY);
        size.Text=$"{r.Width} × {r.Height}";Canvas.SetLeft(size,Math.Clamp(x,0,Math.Max(0,ActualWidth-180)));Canvas.SetTop(size,Math.Clamp(y-32,0,Math.Max(0,ActualHeight-35)));
    }
}
