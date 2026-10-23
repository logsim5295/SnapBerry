using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SnapBerry.App.Editor;

// 포인터와 스크롤 오프셋은 같은 DIP 좌표를 사용한다. 문서와 확대율은 변경하지 않는다.
internal sealed class ScrollPanController
{
    private readonly ScrollViewer viewport;
    private readonly Func<bool> canPan,capture;
    private readonly Action cancelGesture,release;
    private Point lastPoint;private Vector offset;
    private Cursor? previousCursor;private bool previousForceCursor,hadCursor,hadForceCursor;
    internal bool IsActive {get;private set;}
    internal ScrollPanController(ScrollViewer viewport,Func<bool> canPan,Action cancelGesture,Func<bool>? capture=null,Action? release=null)
    {
        this.viewport=viewport;this.canPan=canPan;this.cancelGesture=cancelGesture;
        this.capture=capture??viewport.CaptureMouse;this.release=release??(()=>{if(viewport.IsMouseCaptured)viewport.ReleaseMouseCapture();});
        viewport.PreviewMouseDown+=MouseDown;viewport.PreviewMouseMove+=MouseMove;viewport.PreviewMouseUp+=MouseUp;
        viewport.LostMouseCapture+=(_,e)=>{if(e.OriginalSource==viewport)Cancel(false);};
        viewport.PreviewKeyDown+=(_,e)=>{if(IsActive&&e.Key==Key.Escape){Cancel();e.Handled=true;}};
        viewport.Unloaded+=(_,_)=>Cancel();
        viewport.ScrollChanged+=(_,e)=>
        {if(e.OriginalSource==viewport&&(e.ViewportWidthChange!=0||e.ViewportHeightChange!=0||e.ExtentWidthChange!=0||e.ExtentHeightChange!=0))Cancel();};
    }
    internal bool Begin(Point point)
    {
        if(IsActive||!canPan())return false;
        cancelGesture();
        // 직전 팬·스크롤의 대기 요청을 반영한 위치에서 새 드래그를 시작한다.
        viewport.UpdateLayout();if(!canPan()||!capture())return false;
        lastPoint=point;offset=new(viewport.HorizontalOffset,viewport.VerticalOffset);
        previousCursor=viewport.Cursor;previousForceCursor=viewport.ForceCursor;
        hadCursor=viewport.ReadLocalValue(FrameworkElement.CursorProperty)!=DependencyProperty.UnsetValue;
        hadForceCursor=viewport.ReadLocalValue(FrameworkElement.ForceCursorProperty)!=DependencyProperty.UnsetValue;
        IsActive=true;viewport.SetCurrentValue(FrameworkElement.CursorProperty,Cursors.Hand);viewport.SetCurrentValue(FrameworkElement.ForceCursorProperty,true);return true;
    }
    internal void Move(Point point)
    {
        if(!IsActive)return;if(!canPan()){Cancel();return;}
        var delta=point-lastPoint;lastPoint=point;
        // ScrollTo의 비동기 반영을 기다리지 않고 요청 오프셋을 누적해 빠른 드래그도 유실하지 않는다.
        offset=new(Math.Clamp(offset.X-delta.X,0,viewport.ScrollableWidth),Math.Clamp(offset.Y-delta.Y,0,viewport.ScrollableHeight));
        viewport.ScrollToHorizontalOffset(offset.X);viewport.ScrollToVerticalOffset(offset.Y);
    }
    internal void Cancel()=>Cancel(true);
    private void Cancel(bool releaseCapture)
    {
        if(!IsActive)return;IsActive=false;
        if(hadCursor)viewport.SetCurrentValue(FrameworkElement.CursorProperty,previousCursor);else viewport.ClearValue(FrameworkElement.CursorProperty);
        if(hadForceCursor)viewport.SetCurrentValue(FrameworkElement.ForceCursorProperty,previousForceCursor);else viewport.ClearValue(FrameworkElement.ForceCursorProperty);
        if(releaseCapture)release();
    }
    private bool IsScrollBarSource(DependencyObject? source)
    {
        for(var node=source;node is not null&&node!=viewport;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
            if(node is ScrollBar)return true;
        return false;
    }
    private void MouseDown(object sender,MouseButtonEventArgs e)
    {
        if(IsActive){e.Handled=true;return;}
        if(e.ChangedButton==MouseButton.Middle&&!IsScrollBarSource(e.OriginalSource as DependencyObject)&&Begin(e.GetPosition(viewport)))e.Handled=true;
    }
    private void MouseMove(object sender,MouseEventArgs e)
    {
        if(!IsActive)return;e.Handled=true;
        if(e.MiddleButton!=MouseButtonState.Pressed)Cancel();else Move(e.GetPosition(viewport));
    }
    private void MouseUp(object sender,MouseButtonEventArgs e)
    {if(!IsActive)return;e.Handled=true;if(e.ChangedButton==MouseButton.Middle)Cancel();}
}
