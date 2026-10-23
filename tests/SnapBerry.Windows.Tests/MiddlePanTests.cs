using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 창·마우스 캡처 없이 같은 팬 컨트롤러의 스크롤·취소 동작을 검증한다.
public class MiddlePanTests
{
    private sealed class Source : PresentationSource
    {public override Visual RootVisual {get;set;}=new DrawingVisual();public override bool IsDisposed=>false;protected override CompositionTarget GetCompositionTargetCore()=>null!;}
    private sealed class Surface
    {
        internal readonly Border Content=new(){Width=1200,Height=900};internal readonly ScrollViewer Viewport;
        internal readonly ScrollPanController Pan;internal bool CanPan=true,CaptureSucceeds=true;internal int Captured,Released,Cancelled;
        internal Surface()
        {
            Viewport=new(){Content=Content,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            Layout();Viewport.ScrollToHorizontalOffset(200);Viewport.ScrollToVerticalOffset(150);Layout();
            Pan=new(Viewport,()=>CanPan,()=>Cancelled++,()=>{Captured++;return CaptureSucceeds;},()=>Released++);
        }
        internal void Layout(){Viewport.Measure(new Size(400,300));Viewport.Arrange(new Rect(0,0,400,300));Viewport.UpdateLayout();}
    }

    [Fact] public void MiddleDragMovesBothScrollOffsetsWithoutChangingTheDocument()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=300,Height=225,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(300,225))});model.Zoom=4;
        var annotation=new Annotation{Kind=AnnotationKind.Rectangle,X=10,Y=10,Width=30,Height=20};model.History!.Add(annotation);model.SelectedId=annotation.Id;
        var document=model.Document;var token=model.History.CurrentStateToken;var original=model.Snapshot().OriginalPng;
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();
        var viewport=new ScrollViewer{Content=canvas,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        void Layout(){viewport.Measure(new Size(400,300));viewport.Arrange(new Rect(0,0,400,300));viewport.UpdateLayout();}
        Layout();viewport.ScrollToHorizontalOffset(200);viewport.ScrollToVerticalOffset(150);Layout();
        var cancelled=0;var released=0;
        var pan=new ScrollPanController(viewport,()=>true,()=>{cancelled++;canvas.CancelGesture();},()=>true,()=>released++);
        Assert.True(pan.Begin(new Point(100,100)));Assert.Equal(1,cancelled);Assert.Equal(Cursors.Hand,viewport.Cursor);Assert.True(viewport.ForceCursor);
        pan.Move(new Point(130,120));pan.Move(new Point(150,135));Layout();
        Assert.Equal(150,viewport.HorizontalOffset);Assert.Equal(115,viewport.VerticalOffset);
        pan.Cancel();Assert.Equal(1,released);Assert.Null(viewport.Cursor);Assert.False(viewport.ForceCursor);
        Assert.Same(document,model.Document);Assert.Equal(token,model.History.CurrentStateToken);Assert.Equal(annotation,Assert.Single(document!.Annotations));
        Assert.Equal(annotation.Id,model.SelectedId);Assert.Equal(4,model.Zoom);Assert.Equal(original,model.Snapshot().OriginalPng);
    });

    [Fact] public void PanningClampsAtTheEdgesAndReversesWithoutADeadZone()=>Sta.Run(()=>
    {
        var surface=new Surface();var pan=surface.Pan;var viewport=surface.Viewport;Assert.True(pan.Begin(new(0,0)));
        pan.Move(new(10000,10000));surface.Layout();Assert.Equal(0,viewport.HorizontalOffset);Assert.Equal(0,viewport.VerticalOffset);
        pan.Move(new(9990,9980));surface.Layout();Assert.Equal(10,viewport.HorizontalOffset);Assert.Equal(20,viewport.VerticalOffset);
        pan.Move(new(-10000,-10000));surface.Layout();Assert.Equal(viewport.ScrollableWidth,viewport.HorizontalOffset);Assert.Equal(viewport.ScrollableHeight,viewport.VerticalOffset);
        pan.Move(new(-9990,-9980));surface.Layout();Assert.Equal(viewport.ScrollableWidth-10,viewport.HorizontalOffset);Assert.Equal(viewport.ScrollableHeight-20,viewport.VerticalOffset);pan.Cancel();
    });

    [Fact] public void RapidlyReleasedAndRestartedDragsKeepThePreviousPendingScroll()=>Sta.Run(()=>
    {
        var surface=new Surface();Assert.True(surface.Pan.Begin(new(0,0)));surface.Pan.Move(new(50,35));surface.Pan.Cancel();
        // 첫 팬의 ScrollTo 요청이 배치에 반영되기 전에 곧바로 두 번째 팬을 시작한다.
        Assert.True(surface.Pan.Begin(new(0,0)));surface.Pan.Move(new(10,5));surface.Pan.Cancel();surface.Layout();
        Assert.Equal(140,surface.Viewport.HorizontalOffset);Assert.Equal(110,surface.Viewport.VerticalOffset);
    });

    [Theory] [InlineData(false,true)] [InlineData(true,false)]
    public void UnavailableDocumentsBusyWorkAndFailedCaptureCannotStartPanning(bool allowed,bool capture)=>Sta.Run(()=>
    {
        var surface=new Surface{CanPan=allowed,CaptureSucceeds=capture};Assert.False(surface.Pan.Begin(new(0,0)));
        Assert.False(surface.Pan.IsActive);Assert.Equal(allowed?1:0,surface.Captured);Assert.Equal(allowed?1:0,surface.Cancelled);
        surface.Pan.Move(new(50,50));surface.Layout();Assert.Equal(200,surface.Viewport.HorizontalOffset);Assert.Equal(150,surface.Viewport.VerticalOffset);
        Assert.Null(surface.Viewport.Cursor);Assert.False(surface.Viewport.ForceCursor);Assert.Equal(0,surface.Released);
    });

    [Theory] [InlineData("release")] [InlineData("lost")] [InlineData("escape")] [InlineData("unload")] [InlineData("resize")] [InlineData("disabled")]
    public void EndingOrInterruptingPanningRestoresTheCursorAndStopsMoving(string reason)=>Sta.Run(()=>
    {
        var surface=new Surface();var viewport=surface.Viewport;viewport.Cursor=Cursors.Cross;viewport.ForceCursor=true;
        Assert.True(surface.Pan.Begin(new(100,100)));
        switch(reason)
        {
            case "release":
                var up=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Middle){RoutedEvent=UIElement.PreviewMouseUpEvent};viewport.RaiseEvent(up);Assert.True(up.Handled);break;
            case "lost":viewport.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=Mouse.LostMouseCaptureEvent});break;
            case "escape":
                var key=new KeyEventArgs(Keyboard.PrimaryDevice,new Source(),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};viewport.RaiseEvent(key);Assert.True(key.Handled);break;
            case "unload":viewport.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));break;
            case "resize":surface.Content.Width=1300;surface.Layout();break;
            case "disabled":surface.CanPan=false;surface.Pan.Move(new(120,120));break;
        }
        Assert.False(surface.Pan.IsActive);Assert.Equal(Cursors.Cross,viewport.Cursor);Assert.True(viewport.ForceCursor);
        Assert.Equal(reason=="lost"?0:1,surface.Released);surface.Pan.Cancel();surface.Pan.Move(new(500,500));surface.Layout();
        Assert.Equal(200,viewport.HorizontalOffset);Assert.Equal(150,viewport.VerticalOffset);Assert.Equal(reason=="lost"?0:1,surface.Released);
    });

    [Fact] public void OnlyMiddleButtonOverTheWorkspaceStartsPanningAndOtherButtonsCannotEditDuringIt()=>Sta.Run(()=>
    {
        var surface=new Surface();var viewport=surface.Viewport;
        MouseButtonEventArgs Down(UIElement target,MouseButton button)
        {var e=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,button){RoutedEvent=UIElement.PreviewMouseDownEvent};target.RaiseEvent(e);return e;}
        Assert.False(Down(viewport,MouseButton.Left).Handled);Assert.False(Down(viewport,MouseButton.Right).Handled);Assert.False(surface.Pan.IsActive);
        var bar=(ScrollBar)viewport.Template.FindName("PART_VerticalScrollBar",viewport);Down(bar,MouseButton.Middle);Assert.False(surface.Pan.IsActive);
        Assert.True(Down(viewport,MouseButton.Middle).Handled);Assert.True(surface.Pan.IsActive);Assert.Equal(1,surface.Captured);
        Assert.True(Down(viewport,MouseButton.Left).Handled);Assert.True(Down(viewport,MouseButton.Right).Handled);Assert.Equal(1,surface.Captured);surface.Pan.Cancel();
    });

    [Fact] public void ImagesThatFitTheViewportCannotBeScrolledOutsideTheirBounds()=>Sta.Run(()=>
    {
        var surface=new Surface();surface.Content.Width=100;surface.Content.Height=80;surface.Layout();
        Assert.True(surface.Pan.Begin(new(0,0)));surface.Pan.Move(new(-1000,1000));surface.Layout();
        Assert.Equal(0,surface.Viewport.HorizontalOffset);Assert.Equal(0,surface.Viewport.VerticalOffset);surface.Pan.Cancel();
    });
}
