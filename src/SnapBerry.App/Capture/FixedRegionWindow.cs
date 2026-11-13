using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SnapBerry.App.Editor;
using SnapBerry.App.Interop;
using SnapBerry.Core;

namespace SnapBerry.App.Capture;

// 창의 가운데를 네이티브 영역에서 제거하여 뒤쪽 프로그램으로 클릭·휠 입력을 통과시킨다.
internal sealed class FixedRegionWindow : Window
{
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(146,60,131));
    static FixedRegionWindow()=>Accent.Freeze();
    private readonly Canvas canvas=new();
    private readonly Grid toolbar=new();
    private readonly TextBlock dimensions=new(){Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,4,0),IsHitTestVisible=false};
    private readonly List<(Thumb Grip,FixedRegionEdges Edges)> grips=[];
    private HwndSource? source;
    private bool closed,applying,initialized;
    private PixelPoint dragStart;
    private PixelRect dragRegion;
    internal PixelRect Region {get;private set;}
    internal event Action<PixelRect>? RegionChanged;
    internal event Action<Exception>? Failed;
    internal Func<Task>? CaptureRequested {get;set;}

    internal FixedRegionWindow(PixelRect region,string shortcut)
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        Region=region;Title="고정 영역 캡처";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.Manual;
        Background=Accent;Content=canvas;UseLayoutRounding=true;
        // 조작 막대는 촬영 영역이 좁아도 버튼을 사용할 수 있도록 독립적인 너비를 갖는다.
        toolbar.Background=Accent;toolbar.ColumnDefinitions.Add(new());toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(32)});toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(32)});
        var move=Grip(FixedRegionEdges.None,Cursors.SizeAll);toolbar.Children.Add(move);toolbar.Children.Add(dimensions);
        var capture=new Button{Margin=new Thickness(2),Padding=new Thickness(0),ToolTip="캡쳐",
            HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
        var cameraIcon=new System.Windows.Shapes.Path{
            Data=Geometry.Parse("M 3,5 H 7 L 9,2 H 15 L 17,5 H 21 Q 23,5 23,7 V 18 Q 23,20 21,20 H 3 Q 1,20 1,18 V 7 Q 1,5 3,5 Z M 16,12 A 4,4 0 1 1 8,12 A 4,4 0 1 1 16,12"),
            Width=20,Height=18,Stretch=Stretch.Uniform,StrokeThickness=1.5,StrokeLineJoin=PenLineJoin.Round,IsHitTestVisible=false};
        cameraIcon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding(nameof(Control.Foreground)){Source=capture});
        capture.Content=cameraIcon;System.Windows.Automation.AutomationProperties.SetName(capture,"캡쳐");
        Grid.SetColumn(capture,1);toolbar.Children.Add(capture);
        capture.Click+=async(_,_)=>{if(CaptureRequested is {} action)await action();};
        // 문자 ×의 글꼴 기준선 대신 대칭인 벡터 아이콘을 버튼 중앙에 배치한다.
        var close=new Button{Margin=new Thickness(2),Padding=new Thickness(0),ToolTip="고정 영역 닫기 (Esc)",
            HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
        var closeIcon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 1,1 L 11,11 M 11,1 L 1,11"),Width=12,Height=12,
            Stretch=Stretch.Uniform,StrokeThickness=1.5,IsHitTestVisible=false};
        closeIcon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding(nameof(Control.Foreground)){Source=close});
        close.Content=closeIcon;System.Windows.Automation.AutomationProperties.SetName(close,"고정 영역 닫기");
        Grid.SetColumn(close,2);toolbar.Children.Add(close);close.Click+=(_,_)=>Close();
        canvas.Children.Add(toolbar);
        foreach(var (edges,cursor) in new[]{(FixedRegionEdges.Left,Cursors.SizeWE),(FixedRegionEdges.Right,Cursors.SizeWE),(FixedRegionEdges.Top,Cursors.SizeNS),(FixedRegionEdges.Bottom,Cursors.SizeNS),
            (FixedRegionEdges.Left|FixedRegionEdges.Top,Cursors.SizeNWSE),(FixedRegionEdges.Right|FixedRegionEdges.Top,Cursors.SizeNESW),
            (FixedRegionEdges.Left|FixedRegionEdges.Bottom,Cursors.SizeNESW),(FixedRegionEdges.Right|FixedRegionEdges.Bottom,Cursors.SizeNWSE)})
        {var grip=Grip(edges,cursor);grips.Add((grip,edges));canvas.Children.Add(grip);}
        var initial=FixedRegionGeometry.Layout(region,1);Width=initial.Window.Width;Height=initial.Window.Height;
        UpdateShortcut(shortcut);Draw(initial,1);
        SourceInitialized+=(_,_)=>
        {
            source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);source.AddHook(WndProc);
            source.CompositionTarget.RenderMode=RenderMode.SoftwareOnly;
            ApplyBounds();SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        };
        Loaded+=(_,_)=>RunSafely(()=>{if(initialized)return;initialized=true;Recover();ApplyBounds();});
        Closed+=(_,_)=>{closed=true;SystemEvents.DisplaySettingsChanged-=DisplayChanged;source?.RemoveHook(WndProc);};
        PreviewKeyDown+=(_,e)=>
        {
            if(e.Key==Key.Escape){Close();e.Handled=true;return;}
            var step=Keyboard.Modifiers.HasFlag(ModifierKeys.Control)?10:1;
            var dx=e.Key==Key.Left?-step:e.Key==Key.Right?step:0;var dy=e.Key==Key.Up?-step:e.Key==Key.Down?step:0;
            if(dx==0&&dy==0)return;
            var edges=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?FixedRegionEdges.Right|FixedRegionEdges.Bottom:FixedRegionEdges.None;
            RunSafely(()=>{Region=FixedRegionGeometry.Drag(Region,edges,dx,dy);ApplyBounds();RegionChanged?.Invoke(Region);});e.Handled=true;
        };
    }

    internal void UpdateShortcut(string shortcut)=>toolbar.ToolTip=$"고정 영역 캡처 ({shortcut})\n제목 드래그: 이동 · 가장자리 드래그: 크기 조절\n방향키: 1px 이동 · Ctrl: 10px · Shift: 크기 조절";

    private Thumb Grip(FixedRegionEdges edges,Cursor cursor)
    {
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,Accent);
        var grip=new Thumb{Cursor=cursor,Focusable=true,Template=new ControlTemplate(typeof(Thumb)){VisualTree=border}};
        System.Windows.Automation.AutomationProperties.SetName(grip,edges==FixedRegionEdges.None?"고정 영역 이동":"고정 영역 크기 조절");
        grip.DragStarted+=(_,_)=>{dragStart=NativeMethods.Cursor();dragRegion=Region;};
        grip.DragDelta+=(_,_)=>RunSafely(()=>
        {
            var point=NativeMethods.Cursor();Region=FixedRegionGeometry.Drag(dragRegion,edges,(int)(point.X-dragStart.X),(int)(point.Y-dragStart.Y));ApplyBounds();
        });
        grip.DragCompleted+=(_,e)=>RunSafely(()=>{if(e.Canceled){Region=dragRegion;ApplyBounds();}RegionChanged?.Invoke(Region);});
        return grip;
    }

    private void Draw(FixedRegionLayout layout,double scale)
    {
        var edge=layout.Edge/scale;var header=layout.Header/scale;var width=layout.Hole.Width/scale;var height=layout.Hole.Height/scale;
        toolbar.Width=layout.ToolbarWidth/scale;toolbar.Height=header;dimensions.Text=$"고정 영역  {Region.Width} × {Region.Height}";
        foreach(var (grip,edges) in grips)
        {
            var horizontal=edges.HasFlag(FixedRegionEdges.Left)||edges.HasFlag(FixedRegionEdges.Right);
            var vertical=edges.HasFlag(FixedRegionEdges.Top)||edges.HasFlag(FixedRegionEdges.Bottom);
            var x=edges.HasFlag(FixedRegionEdges.Right)?edge+width:0;
            var y=edges.HasFlag(FixedRegionEdges.Bottom)?header+edge+height:header;
            grip.Width=horizontal?edge:width;grip.Height=vertical?edge:height;
            if(!horizontal)x=edge;if(!vertical)y=header+edge;
            Canvas.SetLeft(grip,x);Canvas.SetTop(grip,y);
        }
    }

    private double Scale=>source is null?1:Math.Max(1,NativeMethods.GetDpiForWindow(source.Handle)/96d);
    private void ApplyBounds()
    {
        if(closed||applying||source is null)return;
        applying=true;
        try
        {
            var scale=Scale;var layout=FixedRegionGeometry.Layout(Region,scale);Draw(layout,scale);
            var bounds=layout.Window;
            if(!NativeMethods.SetWindowPos(source.Handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0010|0x0200))
                throw new Win32Exception("고정 영역 위치를 변경할 수 없습니다.");
            FixedRegionNativeShape.Apply(source.Handle,layout);
        }
        finally { applying=false; }
    }

    internal void Recover()
    {
        var work=System.Windows.Forms.Screen.AllScreens.OrderByDescending(screen=>screen.Primary).Select(screen=>
        {var bounds=screen.WorkingArea;return new PixelRect(bounds.X,bounds.Y,bounds.Width,bounds.Height);}).ToArray();
        var restored=FixedRegionGeometry.Restore(Region,work,Scale);
        if(restored==Region)return;
        Region=restored;ApplyBounds();RegionChanged?.Invoke(Region);
    }

    private void DisplayChanged(object? sender,EventArgs args)=>Dispatcher.BeginInvoke(()=>RunSafely(Recover));
    private IntPtr WndProc(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        // WPF의 DPI 처리가 끝난 후 물리 픽셀 크기를 다시 적용한다.
        if(msg==0x02E0)Dispatcher.BeginInvoke(()=>RunSafely(ApplyBounds));
        return IntPtr.Zero;
    }
    private void RunSafely(Action action)
    {
        if(closed)return;
        try{action();}catch(Exception error){Close();Failed?.Invoke(error);}
    }
}
