using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SnapBerry.Core;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Rendering;
using PixelPoint=SnapBerry.Core.PixelPoint;
using PixelRect=SnapBerry.Core.PixelRect;

namespace SnapBerry.Desktop.Views;

public sealed class EditorSurface : Control,IDisposable
{
    private readonly DesktopEditor editor;private readonly EditorGesture gesture;private Bitmap? bitmap;private Annotation? lastPreview;
    private Bitmap? wandOverlay;
    private static readonly ImageBrush checker=new(new Bitmap(new MemoryStream(SnapBerry.Imaging.MagicWandSelection.CheckerboardPng())))
    {TileMode=TileMode.Tile,DestinationRect=new RelativeRect(0,0,16,16,RelativeUnit.Absolute),Stretch=Stretch.None};
    private bool pointerDown,panning;private Point panPosition;private double zoom=1;
    public EditorGesture Gesture=>gesture;
    public event Action<Vector>? PanRequested;
    public event Action? SelectionChanged;
    public event Action? TextEditRequested;
    public event Action<Exception>? EditFailed;
    public double Zoom {get=>zoom;set{zoom=Math.Clamp(value,.1,4);gesture.Cancel();Refresh();}}
    public EditorSurface(DesktopEditor editor)
    {this.editor=editor;gesture=new(editor);Focusable=true;ClipToBounds=true;editor.Changed+=Changed;editor.MagicWandChanged+=Changed;Refresh();}
    private void Changed(object? sender,EventArgs args)=>Refresh();
    public void CancelGesture(){gesture.Cancel();pointerDown=false;panning=false;Refresh();}
    public void Refresh()
    {
        bitmap?.Dispose();bitmap=null;lastPreview=null;wandOverlay?.Dispose();wandOverlay=null;
        if(editor.Document is AnnotationDocument doc)
        {Width=doc.OutputSize.Width*Zoom;Height=doc.OutputSize.Height*Zoom;bitmap=new Bitmap(new MemoryStream(editor.RenderPng()));}
        else{Width=0;Height=0;}
        if(editor.MagicWandPreview is {} overlay)wandOverlay=new Bitmap(new MemoryStream(overlay));
        InvalidateMeasure();InvalidateVisual();
    }
    private PixelPoint PointFor(Point point)
    {var doc=editor.Document!;return new(point.X/Zoom*doc.Width/doc.OutputSize.Width+doc.CropBounds.X,point.Y/Zoom*doc.Height/doc.OutputSize.Height+doc.CropBounds.Y);}
    public override void Render(DrawingContext context)
    {
        base.Render(context);if(editor.Document is not AnnotationDocument doc||bitmap is null)return;
        if(gesture.Preview!=lastPreview)
        {
            var preview=new AnnotationDocument(doc.SourceWidth,doc.SourceHeight){CropBounds=doc.CropBounds,ResizeSize=doc.ResizeSize,BackgroundAssetHash=doc.BackgroundAssetHash};
            preview.Annotations.AddRange(doc.Annotations.Select(a=>a.Id==gesture.Preview?.Id?gesture.Preview:a));
            if(gesture.Preview is Annotation extra&&!doc.Annotations.Any(a=>a.Id==extra.Id))preview.Annotations.Add(extra);
            bitmap.Dispose();bitmap=new Bitmap(new MemoryStream(gesture.Preview is null?editor.RenderPng():SkiaDocumentRenderer.Render(editor.Snapshot().OriginalPng,preview,editor.Assets)));lastPreview=gesture.Preview;
        }
        if(doc.BackgroundAssetHash is not null)context.DrawRectangle(checker,null,new Rect(0,0,Width,Height));
        context.DrawImage(bitmap,new Rect(0,0,Width,Height));
        if(wandOverlay is not null)
        {
            var sx=Zoom*doc.OutputSize.Width/doc.Width;var sy=Zoom*doc.OutputSize.Height/doc.Height;
            context.DrawImage(wandOverlay,new Rect(-doc.CropBounds.X*sx,-doc.CropBounds.Y*sy,doc.SourceWidth*sx,doc.SourceHeight*sy));
        }
        var selected=gesture.Preview??editor.Selected;
        Rect ViewRect(double x,double y,double w,double h)
        {var sx=Zoom*doc.OutputSize.Width/doc.Width;var sy=Zoom*doc.OutputSize.Height/doc.Height;return new((x-doc.CropBounds.X)*sx,(y-doc.CropBounds.Y)*sy,w*sx,h*sy);}
        if(selected is not null)
        {
            var x=Math.Min(selected.X,selected.X+selected.Width);var y=Math.Min(selected.Y,selected.Y+selected.Height);
            var rect=ViewRect(x,y,Math.Abs(selected.Width),Math.Abs(selected.Height));context.DrawRectangle(null,new Pen(Brushes.MediumOrchid,1.5),rect);
            var end=ViewRect(selected.X+selected.Width,selected.Y+selected.Height,0,0).Position;context.DrawRectangle(Brushes.White,new Pen(Brushes.MediumOrchid,1.5),new Rect(end.X-4,end.Y-4,8,8));
        }
        if(gesture.CropPreview is PixelRect crop)context.DrawRectangle(null,new Pen(Brushes.MediumOrchid,2),ViewRect(crop.X,crop.Y,crop.Width,crop.Height));
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);if(editor.Document is null)return;var properties=e.GetCurrentPoint(this).Properties;
        if(properties.IsMiddleButtonPressed){gesture.Cancel();panning=true;panPosition=e.GetPosition(TopLevel.GetTopLevel(this));Cursor=new(StandardCursorType.Hand);e.Pointer.Capture(this);e.Handled=true;return;}
        if(!properties.IsLeftButtonPressed||panning)return;
        if(e.ClickCount==2&&editor.Tool!=DesktopTool.MagicWand)
        {
            var hit=AnnotationHitTester.Hit(editor.Document.Annotations,PointFor(e.GetPosition(this)),6/Zoom);
            if(hit is not null&&(hit.Kind is AnnotationKind.Text or AnnotationKind.StepMarker||hit.Kind==AnnotationKind.Sticker&&hit.StickerId.StartsWith("key-",StringComparison.Ordinal)))
            {CancelGesture();editor.SelectedId=hit.Id;SelectionChanged?.Invoke();TextEditRequested?.Invoke();e.Handled=true;return;}
        }
        Focus();TryBeginGesture(PointFor(e.GetPosition(this)),6/Zoom*editor.Document.Width/editor.Document.OutputSize.Width);pointerDown=gesture.Active;
        if(pointerDown)e.Pointer.Capture(this);SelectionChanged?.Invoke();InvalidateVisual();e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);if(editor.Document is null)return;
        if(panning){var point=e.GetPosition(TopLevel.GetTopLevel(this));PanRequested?.Invoke(point-panPosition);panPosition=point;e.Handled=true;return;}
        if(pointerDown||gesture.Active){gesture.Move(PointFor(e.GetPosition(this)));InvalidateVisual();e.Handled=true;}
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if(panning){panning=false;Cursor=Cursor.Default;e.Pointer.Capture(null);e.Handled=true;return;}
        if(!pointerDown||editor.Document is null)return;pointerDown=false;TryGesture(()=>gesture.End(PointFor(e.GetPosition(this))));e.Pointer.Capture(null);InvalidateVisual();e.Handled=true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {base.OnPointerCaptureLost(e);if(pointerDown||panning){CancelGesture();Cursor=Cursor.Default;}}
    public bool TryBeginGesture(PixelPoint point,double tolerance=6)=>TryGesture(()=>gesture.Begin(point,tolerance));
    private bool TryGesture(Action action)
    {
        try{action();return true;}
        catch(Exception error)when(error is ArgumentException or InvalidOperationException or IOException)
        {CancelGesture();EditFailed?.Invoke(error);return false;}
    }
    public void Dispose(){editor.Changed-=Changed;editor.MagicWandChanged-=Changed;bitmap?.Dispose();wandOverlay?.Dispose();}
}
