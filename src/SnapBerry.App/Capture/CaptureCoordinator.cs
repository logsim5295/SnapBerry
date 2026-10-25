using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        var source=new TaskCompletionSource<CaptureFrame?>();var windows=new List<CaptureOverlay>();PixelPoint? start=null;PixelRect? current=null;var complete=false;
        var selection=mode==CaptureMode.Window?new WindowCaptureSelection(NativeMethods.VisibleWindows(),frame.DesktopBounds):null;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(75)};
        var probing=false;long queriedVersion=-1;
        void DrawSelection(){foreach(var overlay in windows)overlay.SetSelection(current);}
        void MoveWindowPointer(PixelPoint point)
        {
            if(selection is null||selection.Pointer==point)return;
            selection.Move(point);
            if(selection.Window is { } target)
                selection.Apply(selection.Version,WindowElementProbe.FindNativePath(target,point));
            current=selection.Current;
            DrawSelection();
        }
        async void ProbeElements(object? sender,EventArgs args)
        {
            if(complete||probing||WindowElementProbe.Shared.IsBusy||selection is null||selection.Window is not { } target||selection.Pointer is not { } point||queriedVersion==selection.Version)return;
            probing=true;var version=selection.Version;queriedVersion=version;
            try
            {
                var fallback=WindowElementProbe.FindNativePath(target,point);
                var elements=await WindowElementProbe.Shared.QueryAsync(target,point,fallback,selection);
                // UI 스레드로 돌아온 시점에 실제 커서를 다시 확인한다.
                if(complete)return;
                MoveWindowPointer(NativeMethods.Cursor());
                if(selection.Apply(version,elements.Path))
                {
                    current=selection.Current;DrawSelection();
                    if(elements.Retry)queriedVersion=-1;
                }
            }
            finally{probing=false;}
        }
        timer.Tick+=ProbeElements;
        void Finish(PixelRect? rect,CaptureOverlay? closed=null)
        {
            // 마우스·취소·디스플레이 변경이 겹쳐도 한 번만 완료하고 모든 오버레이와 이벤트를 정리한다.
            if(complete)return;complete=true;SystemEvents.DisplaySettingsChanged-=Changed;
            timer.Stop();timer.Tick-=ProbeElements;selection?.Dispose();
            foreach(var window in windows)if(window!=closed)window.Close();
            try{source.TrySetResult(rect is null?null:CaptureService.Crop(frame,rect.Value));}catch(Exception e){source.TrySetException(e);}
        }
        void Changed(object? sender,EventArgs e)=>Application.Current.Dispatcher.BeginInvoke(()=>Finish(null));
        SystemEvents.DisplaySettingsChanged+=Changed;
        foreach(var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var b=screen.Bounds;var bounds=new PixelRect(b.X,b.Y,b.Width,b.Height);var crop=CaptureService.Crop(frame,bounds).Image;
            CaptureOverlay window=null!;window=new CaptureOverlay(bounds,crop,()=>Finish(null,window),mode==CaptureMode.Window);windows.Add(window);
            window.MouseLeftButtonDown+=(_,e)=>{var p=NativeMethods.Cursor();if(mode==CaptureMode.Window){MoveWindowPointer(p);Finish(current);e.Handled=true;return;}start=p;window.CaptureMouse();e.Handled=true;};
            window.MouseMove+=(_,_)=>
            {
                var p=NativeMethods.Cursor();if(mode==CaptureMode.Window){MoveWindowPointer(p);return;}
                else if(start is not null)current=PixelRect.FromPoints(start.Value,p);
                DrawSelection();
            };
            window.MouseWheel+=(_,e)=>
            {
                if(selection is null)return;
                MoveWindowPointer(NativeMethods.Cursor());selection.Step(e.Delta);
                current=selection.Current;DrawSelection();e.Handled=true;
            };
            window.MouseLeftButtonUp+=(_,_)=>{if(mode==CaptureMode.Region&&start is not null){window.ReleaseMouseCapture();var selection=PixelRect.FromPoints(start.Value,NativeMethods.Cursor());if(selection.Width>0&&selection.Height>0)Finish(selection);else {start=null;current=null;foreach(var overlay in windows)overlay.SetSelection(null);}}};
            window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)Finish(null);};window.Show();
        }
        if(selection is not null){MoveWindowPointer(NativeMethods.Cursor());timer.Start();}
        windows.FirstOrDefault()?.Activate();return source.Task;
    }
}
// 고정된 화면 이미지 위에 선택 영역과 크기를 표시한다. 네이티브 위치 지정은 물리 픽셀을 사용한다.
internal sealed class CaptureOverlay : Window
{
    private readonly Canvas canvas=new();private readonly Border selection=new(){BorderBrush=Brushes.White,BorderThickness=new Thickness(2),Background=new SolidColorBrush(Color.FromArgb(20,255,255,255))};private readonly TextBlock size=new(){Foreground=Brushes.White,Background=Brushes.Black,Padding=new Thickness(5)};
    private readonly PixelRect bounds;
    public CaptureOverlay(PixelRect screen,BitmapSource background,Action? cancel=null,bool windowSelection=false)
    {
        bounds=screen;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;Cursor=Cursors.Cross;Left=screen.X;Top=screen.Y;Width=screen.Width;Height=screen.Height;
        var grid=new Grid();grid.Children.Add(new Image{Source=background,Stretch=Stretch.Fill});grid.Children.Add(new Border{Background=new SolidColorBrush(Color.FromArgb(65,0,0,0)),IsHitTestVisible=false});grid.Children.Add(canvas);canvas.Background=Brushes.Transparent;canvas.Children.Add(selection);canvas.Children.Add(size);Content=grid;SetSelection(null);
        if(windowSelection)grid.Children.Add(new TextBlock{Text="클릭: 캡처  ·  휠 위/아래: 상위/하위 영역  ·  Esc: 취소",Foreground=Brushes.White,Background=Brushes.Black,Padding=new Thickness(12,8,12,8),Margin=new Thickness(12),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,TextWrapping=TextWrapping.Wrap,IsHitTestVisible=false});
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
