using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Globalization;
using SnapBerry.Core;
using SnapBerry.App.Stickers;
namespace SnapBerry.App.Rendering;
// 화면 미리보기·썸네일·클립보드·PNG가 같은 합성 경로를 사용하여 결과 차이를 방지한다.
public sealed class CompositeRenderer
{
    public BitmapSource Render(BitmapSource original,IReadOnlyList<Annotation> items,StickerAssetStore assets,PixelRect? cropBounds=null,PixelSize? resizeSize=null)
    {
        // 원본 전체 좌표에서 모자이크와 주석을 합성한 뒤 마지막에 잘라 모자이크 격자의 기준을 유지한다.
        GuardSize(original.PixelWidth,original.PixelHeight);
        if(resizeSize is PixelSize target)ImageResize.Validate(target);
        if(cropBounds is PixelRect bounds&&!CropPlacement.IsWithin(bounds,original.PixelWidth,original.PixelHeight))throw new ArgumentOutOfRangeException(nameof(cropBounds));
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0,0,original.PixelWidth,original.PixelHeight)));
            dc.DrawImage(Mosaic(original,items),new Rect(0,0,original.PixelWidth,original.PixelHeight));
            foreach(var a in items.Where(x=>x.Kind!=AnnotationKind.Mosaic))DrawAnnotation(dc,a,assets);
            dc.Pop();
        }
        var output=new RenderTargetBitmap(original.PixelWidth,original.PixelHeight,96,96,PixelFormats.Pbgra32);output.Render(visual);output.Freeze();
        BitmapSource result=output;
        if(cropBounds is PixelRect crop&&crop!=new PixelRect(0,0,original.PixelWidth,original.PixelHeight))
        {var cropped=new CroppedBitmap(output,new Int32Rect(crop.X,crop.Y,crop.Width,crop.Height));cropped.Freeze();result=cropped;}
        if(resizeSize is not PixelSize size||size==new PixelSize(result.PixelWidth,result.PixelHeight))return result;
        // 합성·자르기 후 한 번만 리샘플링하므로 원본과 주석 데이터는 그대로 유지된다.
        var resizedVisual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(resizedVisual,BitmapScalingMode.HighQuality);
        using(var drawing=resizedVisual.RenderOpen())drawing.DrawImage(result,new Rect(0,0,size.Width,size.Height));
        var resized=new RenderTargetBitmap(size.Width,size.Height,96,96,PixelFormats.Pbgra32);resized.Render(resizedVisual);resized.Freeze();return resized;
    }
    // 중간 버퍼까지 포함한 예상 비용으로 이미지 크기를 제한한다.
    public static void GuardSize(int width,int height)
    {if(width<=0||height<=0||checked((long)width*height*20)>512L*1024*1024)throw new InvalidOperationException("Image is too large. Capture a smaller region.");}
    private static BitmapSource Mosaic(BitmapSource input,IReadOnlyList<Annotation> items)
    {
        // 겹친 영역에는 더 큰 블록을 적용한다. 평균색은 원본에서 계산하며 블록별로 캐시한다.
        var areas=items.Where(x=>x.Kind==AnnotationKind.Mosaic).ToArray();if(areas.Length==0)return input;
        var w=input.PixelWidth;var h=input.PixelHeight;var converted=new FormatConvertedBitmap(input,PixelFormats.Bgra32,null,0);var source=new byte[w*h*4];converted.CopyPixels(source,w*4,0);var output=source.ToArray();var sizes=new int[w*h];
        foreach(var a in areas)
        {
            var x1=Math.Clamp((int)a.X,0,w);var y1=Math.Clamp((int)a.Y,0,h);var x2=Math.Clamp((int)Math.Ceiling(a.X+a.Width),0,w);var y2=Math.Clamp((int)Math.Ceiling(a.Y+a.Height),0,h);
            for(var y=y1;y<y2;y++)for(var x=x1;x<x2;x++)sizes[y*w+x]=Math.Max(sizes[y*w+x],Math.Clamp(a.MosaicBlockSize,1,1024));
        }
        var colors=new Dictionary<(int,int,int),byte[]>();
        for(var y=0;y<h;y++)for(var x=0;x<w;x++)
        {
            var size=sizes[y*w+x];if(size==0)continue;var bx=x/size*size;var by=y/size*size;
            if(!colors.TryGetValue((size,bx,by),out var color))
            {
                long b=0,g=0,r=0,alpha=0,count=0;
                for(var yy=by;yy<Math.Min(by+size,h);yy++)for(var xx=bx;xx<Math.Min(bx+size,w);xx++){var i=(yy*w+xx)*4;b+=source[i];g+=source[i+1];r+=source[i+2];alpha+=source[i+3];count++;}
                color=[(byte)(b/count),(byte)(g/count),(byte)(r/count),(byte)(alpha/count)];colors[(size,bx,by)]=color;
            }
            Array.Copy(color,0,output,(y*w+x)*4,4);
        }
        var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,output,w*4);bitmap.Freeze();return bitmap;
    }
    public static Brush Brush(string color)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    public static DashStyle GetDashStyle(StrokePattern pattern)=>pattern switch
    {
        StrokePattern.Solid=>DashStyles.Solid,StrokePattern.Dash=>DashStyles.Dash,StrokePattern.Dot=>DashStyles.Dot,
        StrokePattern.DashDot=>DashStyles.DashDot,StrokePattern.DashDotDot=>DashStyles.DashDotDot,
        _=>throw new ArgumentOutOfRangeException(nameof(pattern))
    };
    private static void DrawStepLabel(DrawingContext dc,Annotation annotation,Rect rect)
    {
        var size=Math.Min(rect.Width,rect.Height)*.52;
        var text=new FormattedText(annotation.Text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Emoji"),size,Brush(annotation.Style.TextColor),1);
        var bounds=text.BuildGeometry(new Point()).Bounds;
        if(bounds.IsEmpty||bounds.Width<=0||bounds.Height<=0)return;
        var padding=Math.Min(4,Math.Min(rect.Width,rect.Height)*.1);
        var scale=Math.Min(1,Math.Min((rect.Width-2*padding)/bounds.Width,(rect.Height-2*padding)/bounds.Height));
        if(scale<1){text.SetFontSize(size*scale);bounds=text.BuildGeometry(new Point()).Bounds;}
        // 글꼴의 행 높이나 기준선이 아닌 실제 글리프 경계를 기준으로 원 중앙에 배치한다.
        dc.DrawText(text,new Point(rect.X+(rect.Width-bounds.Width)/2-bounds.X,rect.Y+(rect.Height-bounds.Height)/2-bounds.Y));
    }
    public static void DrawAnnotation(DrawingContext dc,Annotation a,StickerAssetStore assets)
    {
        // 점선의 길이 0 구간도 보이도록 둥근 대시 끝을 사용한다. 화살촉은 선 모양과 무관하게 채운다.
        var pen=new Pen(Brush(a.Style.Color),a.Style.Thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,DashStyle=GetDashStyle(a.Style.LineStyle),DashCap=PenLineCap.Round};
        var minimum=a.Kind==AnnotationKind.Sticker?0:1;
        var rect=new Rect(a.X,a.Y,Math.Max(minimum,a.Width),Math.Max(minimum,a.Height));
        switch(a.Kind)
        {
            case AnnotationKind.Rectangle:dc.DrawRectangle(null,pen,rect);break;
            case AnnotationKind.Arrow:
                var end=new Point(a.X+a.Width,a.Y+a.Height);dc.DrawLine(pen,new Point(a.X,a.Y),end);var angle=Math.Atan2(a.Height,a.Width);var len=Math.Max(12,a.Style.Thickness*4);
                var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(end,true,true);c.LineTo(new Point(end.X-len*Math.Cos(angle-.5),end.Y-len*Math.Sin(angle-.5)),true,false);c.LineTo(new Point(end.X-len*Math.Cos(angle+.5),end.Y-len*Math.Sin(angle+.5)),true,false);}dc.DrawGeometry(pen.Brush,null,g);break;
            case AnnotationKind.Text:
                var text=new FormattedText(a.Text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),a.Style.FontSize,pen.Brush,1);dc.DrawText(text,new Point(a.X,a.Y));break;
            case AnnotationKind.StepMarker:
                dc.DrawEllipse(Brush(a.Style.Fill),null,new Point(rect.X+rect.Width/2,rect.Y+rect.Height/2),rect.Width/2,rect.Height/2);DrawStepLabel(dc,a,rect);break;
            case AnnotationKind.Sticker:dc.DrawImage(assets.GetBitmap(a.AssetHash),rect);break;
        }
    }
}
