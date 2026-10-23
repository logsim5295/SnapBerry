using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.Core;
using SnapBerry.App.Rendering;
namespace SnapBerry.App.Editor;
// 이미지 렌더링과 포인터 제스처를 담당한다. 드래그 미리보기는 문서를 바꾸지 않고 놓을 때 확정한다.
public sealed class EditorCanvas : FrameworkElement
{
    private System.Windows.Media.Imaging.BitmapSource? wandOverlay;private byte[]? wandBytes;
    private static readonly Brush checker=CreateChecker();
    private static Brush CreateChecker(){var image=SnapBerry.App.Export.PngFileWriter.Decode(SnapBerry.Imaging.MagicWandSelection.CheckerboardPng());var brush=new ImageBrush(image){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,16,16)};brush.Freeze();return brush;}
    public EditorViewModel Model {get;set;}=null!;
    private PixelPoint start;private Annotation? before,preview;private bool dragging,resizing;private AnnotationDocument? lastDocument;
    private readonly ShapePlacement shape=new();private readonly CropPlacement crop=new();private PixelRect? lastCropBounds;private PixelSize? lastOutputSize;
    public event Action<PixelPoint,Annotation?>? TextRequested;
    public event Action? SelectionChanged;
    public EditorCanvas(){Focusable=true;ClipToBounds=true;Cursor=Cursors.Cross;}
    // 자르기는 현재 이미지 기준, 일반 주석은 자르기 원점까지 더한 원본 기준 좌표를 사용한다.
    private double ScaleX=>Model.Document is { } d?(double)d.OutputSize.Width/d.Width:1;
    private double ScaleY=>Model.Document is { } d?(double)d.OutputSize.Height/d.Height:1;
    private double ViewScaleX=>Model.Zoom*ScaleX;
    private double ViewScaleY=>Model.Zoom*ScaleY;
    internal bool IsResizeHandle(Annotation annotation,PixelPoint point)=>Math.Abs(point.X-annotation.X-annotation.Width)*ViewScaleX<10&&Math.Abs(point.Y-annotation.Y-annotation.Height)*ViewScaleY<10;
    private PixelPoint ViewPoint(MouseEventArgs e){var p=e.GetPosition(this);return new(p.X/(Model.Zoom*ScaleX),p.Y/(Model.Zoom*ScaleY));}
    internal PixelPoint ToDocumentPoint(Point point){var bounds=Model.Document?.CropBounds;return new(point.X/(Model.Zoom*ScaleX)+(bounds?.X??0),point.Y/(Model.Zoom*ScaleY)+(bounds?.Y??0));}
    private PixelPoint Point(MouseEventArgs e)=>ToDocumentPoint(e.GetPosition(this));
    public void Refresh(){var bytes=Model.MagicWandPreview;if(!ReferenceEquals(bytes,wandBytes)){wandBytes=bytes;wandOverlay=bytes is null?null:SnapBerry.App.Export.PngFileWriter.Decode(bytes);}if(lastDocument!=Model.Document||lastCropBounds!=Model.Document?.CropBounds||lastOutputSize!=Model.Document?.OutputSize){CancelGesture();lastDocument=Model.Document;lastCropBounds=Model.Document?.CropBounds;lastOutputSize=Model.Document?.OutputSize;}Width=(Model.Document?.OutputSize.Width??900)*Model.Zoom;Height=(Model.Document?.OutputSize.Height??600)*Model.Zoom;InvalidateVisual();}
    public void CancelGesture(){shape.Cancel();crop.Cancel();dragging=false;before=null;preview=null;resizing=false;if(IsMouseCaptured)ReleaseMouseCapture();InvalidateVisual();}
    protected override void OnLostMouseCapture(MouseEventArgs e){base.OnLostMouseCapture(e);if(dragging||crop.Active)CancelGesture();}
    internal Annotation? ShapePreview=>shape.Preview is Annotation a&&a.Kind==AnnotationKind.Mosaic?a with{MosaicBlockSize=Model.MosaicSize}:shape.Preview;
    internal bool BeginShape(PixelPoint point)
    {
        if(Model.Document is null||Model.Tool is not (EditorTool.Arrow or EditorTool.Rectangle or EditorTool.Mosaic))return false;
        Model.SelectedId=null;SelectionChanged?.Invoke();
        var kind=Model.Tool switch{EditorTool.Arrow=>AnnotationKind.Arrow,EditorTool.Rectangle=>AnnotationKind.Rectangle,_=>AnnotationKind.Mosaic};
        var completed=shape.PointerDown(point,kind,Model.Style,3,ViewScaleX,ViewScaleY);preview=ShapePreview;CommitShape(completed);InvalidateVisual();return true;
    }
    internal void FinishShape(PixelPoint point)
    {
        var completed=shape.PointerUp(point);preview=ShapePreview;if(IsMouseCaptured)ReleaseMouseCapture();CommitShape(completed);InvalidateVisual();
    }
    internal bool BeginCrop(PixelPoint point)
    {
        if(Model.Tool!=EditorTool.Crop||Model.Document is null)return false;
        Model.SelectedId=null;SelectionChanged?.Invoke();var result=crop.PointerDown(Model.Document.Width,Model.Document.Height,point,12,ViewScaleX,ViewScaleY);InvalidateVisual();return result;
    }
    internal void MoveCrop(PixelPoint point){crop.Move(point);InvalidateVisual();}
    internal void FinishCrop(PixelPoint point)
    {
        var result=crop.PointerUp(point);if(IsMouseCaptured)ReleaseMouseCapture();
        if(result is PixelRect bounds)Model.CropTo(bounds);InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        // 선택 테두리와 조절점은 편집용 장식이므로 합성 이미지나 PNG에는 포함하지 않는다.
        base.OnRender(dc);dc.DrawRectangle(Model.Document?.BackgroundAssetHash is null?Brushes.White:checker,null,new Rect(RenderSize));if(Model.Document is null)return;
        dc.PushTransform(new ScaleTransform(Model.Zoom*ScaleX,Model.Zoom*ScaleY));
        var image=Model.Composite();
        if(preview is not null&&Model.Background is not null)
        {
            var list=Model.Document.Annotations.ToList();var index=list.FindIndex(a=>a.Id==preview.Id);
            if(index>=0)list[index]=preview;else list.Add(preview);
            image=new CompositeRenderer().Render(Model.Background,list,Model.Assets,Model.Document.CropBounds,Model.Document.ResizeSize);
        }
        if(image is not null)dc.DrawImage(image,new Rect(0,0,Model.Document.Width,Model.Document.Height));
        if(wandOverlay is not null)dc.DrawImage(wandOverlay,new Rect(-Model.Document.CropBounds.X,-Model.Document.CropBounds.Y,Model.Document.SourceWidth,Model.Document.SourceHeight));
        var selected=preview??Model.Selected;
        if(selected is not null&&Model.Tool!=EditorTool.Crop)
        {
            // 조절점은 출력 좌표에서 그려 비균등 배율에도 화면상 크기를 유지한다.
            dc.PushTransform(new ScaleTransform(1/ScaleX,1/ScaleY));
            var x=(Math.Min(selected.X,selected.X+selected.Width)-Model.Document.CropBounds.X)*ScaleX;var y=(Math.Min(selected.Y,selected.Y+selected.Height)-Model.Document.CropBounds.Y)*ScaleY;var w=Math.Abs(selected.Width)*ScaleX;var h=Math.Abs(selected.Height)*ScaleY;
            var selectionBrush=new SolidColorBrush(Color.FromRgb(146,60,131));
            var pen=new Pen(selectionBrush,1/Model.Zoom){DashStyle=DashStyles.Dash};dc.DrawRectangle(null,pen,new Rect(x-3/Model.Zoom,y-3/Model.Zoom,w+6/Model.Zoom,h+6/Model.Zoom));
            var size=8/Model.Zoom;dc.DrawRectangle(Brushes.White,new Pen(selectionBrush,1/Model.Zoom),new Rect((selected.X+selected.Width-Model.Document.CropBounds.X)*ScaleX-size/2,(selected.Y+selected.Height-Model.Document.CropBounds.Y)*ScaleY-size/2,size,size));
            dc.Pop();
        }
        if(Model.Tool==EditorTool.Crop)DrawCrop(dc,crop.Preview??new(0,0,Model.Document.Width,Model.Document.Height));
        dc.Pop();
    }
    private void DrawCrop(DrawingContext dc,PixelRect bounds)
    {
        // 잘려 나갈 부분을 어둡게 표시하고 확대율과 무관한 두께로 가이드와 꼭짓점을 그린다.
        dc.PushTransform(new ScaleTransform(1/ScaleX,1/ScaleY));
        var rect=new Rect(bounds.X*ScaleX,bounds.Y*ScaleY,bounds.Width*ScaleX,bounds.Height*ScaleY);
        var outside=new CombinedGeometry(GeometryCombineMode.Exclude,new RectangleGeometry(new Rect(0,0,Model.Document!.OutputSize.Width,Model.Document.OutputSize.Height)),new RectangleGeometry(rect));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(140,0,0,0)),null,outside);
        var berry=new SolidColorBrush(Color.FromRgb(146,60,131));var pen=new Pen(berry,1.5/Model.Zoom);
        dc.DrawRectangle(null,pen,rect);
        var guide=new Pen(new SolidColorBrush(Color.FromArgb(110,255,255,255)),1/Model.Zoom);
        for(var i=1;i<=2;i++)
        {
            dc.DrawLine(guide,new Point(rect.Left+rect.Width*i/3,rect.Top),new Point(rect.Left+rect.Width*i/3,rect.Bottom));
            dc.DrawLine(guide,new Point(rect.Left,rect.Top+rect.Height*i/3),new Point(rect.Right,rect.Top+rect.Height*i/3));
        }
        var w=Math.Min(10/Model.Zoom,rect.Width);var h=Math.Min(10/Model.Zoom,rect.Height);
        foreach(var point in new[]{rect.TopLeft,new Point(rect.Right-w,rect.Top),new Point(rect.Left,rect.Bottom-h),new Point(rect.Right-w,rect.Bottom-h)})
            dc.DrawRectangle(Brushes.White,pen,new Rect(point.X,point.Y,w,h));
        dc.Pop();
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // 같은 도구로 기존 객체를 누르면 신규 생성보다 선택·편집을 먼저 처리한다.
        base.OnMouseLeftButtonDown(e);if(Model.Document is null)return;Focus();start=Point(e);
        try
        {
            if(Model.Tool==EditorTool.MagicWand){Model.SelectMagicWand(start,Model.MagicWandTolerance,Model.MagicWandGlobal);e.Handled=true;return;}
            if(Model.Tool==EditorTool.Crop)
            {if(BeginCrop(ViewPoint(e))&&!CaptureMouse())CancelGesture();e.Handled=true;return;}
            var shapeKind=Model.Tool switch{EditorTool.Arrow=>AnnotationKind.Arrow,EditorTool.Rectangle=>AnnotationKind.Rectangle,EditorTool.Number=>AnnotationKind.StepMarker,EditorTool.Sticker=>AnnotationKind.Sticker,EditorTool.Text=>AnnotationKind.Text,_=>AnnotationKind.Mosaic};
            var shapeHit=(Model.Tool is EditorTool.Arrow or EditorTool.Rectangle or EditorTool.Mosaic or EditorTool.Number or EditorTool.Sticker or EditorTool.Text)&&!shape.Active?AnnotationHitTester.Hit(Model.Document.Annotations.Where(a=>a.Kind==shapeKind).ToArray(),start,8,ViewScaleX,ViewScaleY):null;
            if(shapeHit is not null)
            {
                before=shapeHit;Model.SelectedId=before.Id;SelectionChanged?.Invoke();
                if(e.ClickCount==2&&(before.Kind is AnnotationKind.Text or AnnotationKind.StepMarker||(before.Kind==AnnotationKind.Sticker&&before.StickerId.StartsWith("key-")))){TextRequested?.Invoke(start,before);return;}
                resizing=IsResizeHandle(before,start);
                preview=before;dragging=true;CaptureMouse();InvalidateVisual();return;
            }
            if(BeginShape(start)){if(shape.Active)CaptureMouse();return;}
            if(e.ClickCount==2){var a=AnnotationHitTester.Hit(Model.Document.Annotations,start,6,ViewScaleX,ViewScaleY);if(a is not null&&(a.Kind is AnnotationKind.Text or AnnotationKind.StepMarker||(a.Kind==AnnotationKind.Sticker&&a.StickerId.StartsWith("key-")))){Model.SelectedId=a.Id;TextRequested?.Invoke(start,a);return;}}
            switch(Model.Tool)
            {
                case EditorTool.Text:
                    Model.SelectedId=null;SelectionChanged?.Invoke();TextRequested?.Invoke(start,null);InvalidateVisual();return;
                case EditorTool.Number:Model.SelectedId=null;Model.History!.InsertStep(start,Model.Style);Model.SelectedId=Model.Document.Annotations.Last().Id;SelectionChanged?.Invoke();InvalidateVisual();return;
                case EditorTool.Sticker:if(Model.SelectedSticker is not null)Model.InsertSticker(Model.SelectedSticker,start);return;
                case EditorTool.Select:
                    before=AnnotationHitTester.Hit(Model.Document.Annotations,start,8,ViewScaleX,ViewScaleY);Model.SelectedId=before?.Id;
                    SelectionChanged?.Invoke();if(before is null){InvalidateVisual();return;}resizing=IsResizeHandle(before,start);preview=before;break;
            }
            dragging=true;CaptureMouse();InvalidateVisual();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"SnapBerry");}
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        // preview만 갱신한다. 스티커·번호는 종횡비를 유지하고 텍스트는 글자 크기도 조절한다.
        base.OnMouseMove(e);
        if(Model.Tool==EditorTool.Crop&&Model.Document is not null)
        {
            var cropPoint=ViewPoint(e);var corner=crop.Corner??CropPlacement.HitCorner(Model.Document.Width,Model.Document.Height,cropPoint,12,ViewScaleX,ViewScaleY);
            Cursor=corner is CropCorner.TopLeft or CropCorner.BottomRight?Cursors.SizeNWSE:corner is null?Cursors.Arrow:Cursors.SizeNESW;
            if(crop.Active)MoveCrop(cropPoint);return;
        }
        MoveAnnotation(Point(e));
    }
    internal void MoveAnnotation(PixelPoint p)
    {
        if(shape.Active){shape.Move(p);preview=ShapePreview;InvalidateVisual();return;}
        if(!dragging||preview is null)return;
        if(before is not null)
        {
            if(resizing)
            {
                var w=p.X-before.X;var h=p.Y-before.Y;
                if(before.Kind==AnnotationKind.Sticker)
                {
                    var ratio=before.Width>0&&before.Height>0?before.Height/before.Width:1;
                    if(!double.IsFinite(ratio)||ratio<=0)ratio=1;
                    // 작은 이미지도 실제 비율을 유지하고, 최소 크기는 화면상 1픽셀을 기준으로 한다.
                    w=Math.Max(w,Math.Max(1/ViewScaleX,1/ViewScaleY/ratio));h=w*ratio;
                }
                else if(before.Kind==AnnotationKind.StepMarker){w=Math.Max(8,w);h=w*before.Height/Math.Max(1,before.Width);}
                if(before.Kind==AnnotationKind.Text){var font=Math.Clamp(before.Style.FontSize*Math.Max(0,w)/Math.Max(1,before.Width),1,4096);preview=EditorViewModel.MeasureText(before with {Style=before.Style with {FontSize=font}});}
                else preview=before with{Width=before.Kind is AnnotationKind.Arrow or AnnotationKind.Sticker?w:Math.Max(8,w),Height=before.Kind is AnnotationKind.Arrow or AnnotationKind.Sticker?h:Math.Max(8,h)};
            }
            else preview=before with{X=before.X+p.X-start.X,Y=before.Y+p.Y-start.Y};
        }
        else if(preview.Kind==AnnotationKind.Arrow)preview=preview with {Width=p.X-start.X,Height=p.Y-start.Y};
        else {var r=PixelRect.FromPoints(start,p);preview=preview with{X=r.X,Y=r.Y,Width=r.Width,Height=r.Height};}
        InvalidateVisual();
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        // 결과를 명령 하나로 기록하여 마우스를 여러 번 움직여도 실행 취소는 한 번만 필요하다.
        base.OnMouseLeftButtonUp(e);if(crop.Active){FinishCrop(ViewPoint(e));e.Handled=true;return;}if(shape.Active){FinishShape(Point(e));return;}if(!dragging)return;var result=preview;dragging=false;preview=null;ReleaseMouseCapture();
        // 최소 생성 크기는 새 주석에만 적용한다. 작은 기존 글자도 이동·크기 변경을 확정한다.
        if(result is not null&&(before is not null||Math.Abs(result.Width)>2||Math.Abs(result.Height)>2)){Model.SelectedId=result.Id;if(before is null)Model.History!.Add(result);else if(result!=before)Model.History!.Replace(result);}before=null;InvalidateVisual();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);if(e.Key==Key.Escape){Model.ClearMagicWand();CancelGesture();e.Handled=true;}
        if(e.Key==Key.Delete&&Model.SelectedId is not null){DeleteSelected();e.Handled=true;}
    }
    public void DeleteSelected()
    {
        CancelGesture();if(Model.DeleteMagicWandSelection()){SelectionChanged?.Invoke();InvalidateVisual();return;}if(Model.SelectedId is not Guid id)return;
        Model.SelectedId=null;Model.History?.Remove(id);SelectionChanged?.Invoke();InvalidateVisual();
    }
    private void CommitShape(Annotation? value){if(value is null)return;if(value.Kind==AnnotationKind.Mosaic)value=value with{MosaicBlockSize=Model.MosaicSize};Model.SelectedId=value.Id;Model.History!.Add(value);}
}
