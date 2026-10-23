using SnapBerry.Core;

namespace SnapBerry.Desktop.Editing;

// 드래그 중에는 문서를 수정하지 않고 끝점에서 한 명령만 만든다.
public sealed class EditorGesture(DesktopEditor editor)
{
    private PixelPoint start;private Annotation? original;private bool resize;private readonly ShapePlacement shape=new();
    public Annotation? Preview {get;private set;}public PixelRect? CropPreview {get;private set;}
    public string Text {get;set;}="텍스트";public int MosaicSize {get;set;}=12;
    public bool Active {get;private set;}
    public void Begin(PixelPoint point,double tolerance=6)
    {
        if(editor.Document is null)return;
        if(editor.Tool==DesktopTool.MagicWand){editor.SelectMagicWand(point,editor.MagicWandTolerance,editor.MagicWandGlobal);Cancel();return;}
        start=point;Active=true;
        var hit=shape.Active?null:AnnotationHitTester.Hit(editor.Document.Annotations,point,tolerance);
        var editsExisting=hit is not null&&editor.Tool switch
        {
            DesktopTool.Arrow=>hit.Kind==AnnotationKind.Arrow,
            DesktopTool.Rectangle=>hit.Kind==AnnotationKind.Rectangle,
            DesktopTool.Mosaic=>hit.Kind==AnnotationKind.Mosaic,
            DesktopTool.Text=>hit.Kind==AnnotationKind.Text,
            DesktopTool.Number=>hit.Kind==AnnotationKind.StepMarker,
            _=>false
        };
        if(editor.Tool!=DesktopTool.Select&&!editsExisting)editor.SelectedId=null;
        if(editor.Tool==DesktopTool.Select||editsExisting)
        {
            original=editor.Selected;
            resize=original is not null&&Math.Abs(point.X-original.X-original.Width)<=tolerance&&Math.Abs(point.Y-original.Y-original.Height)<=tolerance;
            if(!resize){original=hit;editor.SelectedId=original?.Id;}
            Preview=original;
        }
        else if(editor.Tool is DesktopTool.Arrow or DesktopTool.Rectangle or DesktopTool.Mosaic)
        {
            var kind=editor.Tool switch{DesktopTool.Arrow=>AnnotationKind.Arrow,DesktopTool.Mosaic=>AnnotationKind.Mosaic,_=>AnnotationKind.Rectangle};
            var completed=shape.PointerDown(point,kind,editor.Style);Preview=shape.Preview;
            if(completed is not null){editor.Commit(completed with{MosaicBlockSize=MosaicSize});Cancel();}
        }
        else if(editor.Tool==DesktopTool.Text)
        {if(!string.IsNullOrWhiteSpace(Text))editor.Commit(new(){Kind=AnnotationKind.Text,X=point.X,Y=point.Y,Text=Text,Style=editor.Style});Cancel();}
        else if(editor.Tool==DesktopTool.Number){editor.InsertStep(point);Cancel();}
        else if(editor.Tool==DesktopTool.Crop)CropPreview=new((int)point.X,(int)point.Y,1,1);
    }
    public void Move(PixelPoint point)
    {
        if(!Active)return;
        if(original is not null)
        {
            var dx=point.X-start.X;var dy=point.Y-start.Y;
            Preview=resize?original with{Width=original.Kind==AnnotationKind.Arrow?original.Width+dx:Math.Max(1,original.Width+dx),Height=original.Kind==AnnotationKind.Arrow?original.Height+dy:Math.Max(1,original.Height+dy)}:original with{X=original.X+dx,Y=original.Y+dy};
            if(resize&&original.Kind==AnnotationKind.Text)
                Preview=Preview with{Style=original.Style with{FontSize=Math.Clamp(original.Style.FontSize*Preview.Height/Math.Max(1,original.Height),1,4096)}};
        }
        else if(shape.Active){shape.Move(point);Preview=shape.Preview is Annotation a?a with{MosaicBlockSize=MosaicSize}:null;}
        else if(editor.Tool==DesktopTool.Crop&&editor.Document is AnnotationDocument doc)
        {
            var crop=doc.CropBounds;var a=new PixelPoint(Math.Clamp(start.X,crop.X,crop.X+crop.Width),Math.Clamp(start.Y,crop.Y,crop.Y+crop.Height));
            var b=new PixelPoint(Math.Clamp(point.X,crop.X,crop.X+crop.Width),Math.Clamp(point.Y,crop.Y,crop.Y+crop.Height));CropPreview=PixelRect.FromPoints(a,b);
        }
    }
    public void End(PixelPoint point)
    {
        if(!Active)return;Move(point);
        if(original is not null&&Preview is Annotation changed&&changed!=original)editor.ReplaceSelected(changed);
        else if(shape.Active)
        {var completed=shape.PointerUp(point);if(completed is null){Preview=shape.Preview;return;}editor.Commit(completed with{MosaicBlockSize=MosaicSize});}
        else if(CropPreview is PixelRect crop&&crop.Width>0&&crop.Height>0&&editor.Document is AnnotationDocument doc)editor.History!.Crop(crop.Translate(-doc.CropBounds.X,-doc.CropBounds.Y));
        Cancel();
    }
    public void Cancel(){original=null;Preview=null;CropPreview=null;Active=false;resize=false;shape.Cancel();}
}
