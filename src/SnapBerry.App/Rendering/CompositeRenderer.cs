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
    public BitmapSource Render(BitmapSource original,IReadOnlyList<Annotation> items,StickerAssetStore assets,PixelRect? cropBounds=null,PixelSize? resizeSize=null,PixelRect? sourceClip=null)
    {
        // 투명한 출력 캔버스에 원본과 주석을 같은 원본 좌표계로 그린다.
        GuardSize(original.PixelWidth,original.PixelHeight);
        if(resizeSize is PixelSize target)ImageResize.Validate(target);
        var crop=cropBounds??new PixelRect(0,0,original.PixelWidth,original.PixelHeight);
        if(!CropPlacement.IsValidCanvas(crop))throw new ArgumentOutOfRangeException(nameof(cropBounds));
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-crop.X,-crop.Y));
            DrawBackground(dc,original,items,sourceClip);
            foreach(var a in items.Where(x=>x.Kind!=AnnotationKind.Mosaic))DrawAnnotation(dc,a,assets);
            dc.Pop();
        }
        var result=new RenderTargetBitmap(crop.Width,crop.Height,96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();
        if(resizeSize is not PixelSize size||size==new PixelSize(result.PixelWidth,result.PixelHeight))return result;
        // 합성·자르기 후 한 번만 리샘플링하므로 원본과 주석 데이터는 그대로 유지된다.
        var resizedVisual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(resizedVisual,BitmapScalingMode.HighQuality);
        using(var drawing=resizedVisual.RenderOpen())drawing.DrawImage(result,new Rect(0,0,size.Width,size.Height));
        var resized=new RenderTargetBitmap(size.Width,size.Height,96,96,PixelFormats.Pbgra32);resized.Render(resizedVisual);resized.Freeze();return resized;
    }
    internal static void DrawBackground(DrawingContext dc,BitmapSource original,IReadOnlyList<Annotation> items,PixelRect? sourceClip)
    {
        if(sourceClip is PixelRect clip)dc.PushClip(new RectangleGeometry(new Rect(clip.X,clip.Y,clip.Width,clip.Height)));
        dc.DrawImage(Mosaic(original,items),new Rect(0,0,original.PixelWidth,original.PixelHeight));
        if(sourceClip.HasValue)dc.Pop();
    }
    // 중간 버퍼까지 포함한 예상 비용으로 이미지 크기를 제한한다.
    public static void GuardSize(int width,int height)
    {if(width<=0||height<=0||checked((long)width*height*20)>512L*1024*1024)throw new InvalidOperationException("Image is too large. Capture a smaller region.");}
    internal static BitmapSource Mosaic(BitmapSource input,IReadOnlyList<Annotation> items)
    {
        // 겹친 영역에는 더 큰 블록을 적용한다. 블록 격자는 이미지 원점에 고정되고 평균색은 주석 밖 픽셀을 포함한 블록 전체에서 계산한다.
        // 전체 픽셀을 순회하지 않고, 모자이크 영역의 합집합을 블록 경계까지 넓힌 부분만 읽고 계산한다.
        var w=input.PixelWidth;var h=input.PixelHeight;
        var areas=new List<(int X1,int Y1,int X2,int Y2,int Size)>();int left=w,top=h,right=0,bottom=0;
        foreach(var a in items)
        {
            if(a.Kind!=AnnotationKind.Mosaic)continue;var size=Math.Clamp(a.MosaicBlockSize,1,1024);
            var x1=Math.Clamp((int)a.X,0,w);var y1=Math.Clamp((int)a.Y,0,h);var x2=Math.Clamp((int)Math.Ceiling(a.X+a.Width),0,w);var y2=Math.Clamp((int)Math.Ceiling(a.Y+a.Height),0,h);
            if(x2<=x1||y2<=y1)continue;areas.Add((x1,y1,x2,y2,size));
            left=Math.Min(left,x1/size*size);top=Math.Min(top,y1/size*size);right=Math.Max(right,Math.Min(w,(x2+size-1)/size*size));bottom=Math.Max(bottom,Math.Min(h,(y2+size-1)/size*size));
        }
        if(areas.Count==0)return input;
        var pixels=input.Format==PixelFormats.Bgra32?input:new FormatConvertedBitmap(input,PixelFormats.Bgra32,null,0);
        var rw=right-left;var rh=bottom-top;var stride=rw*4;var source=new byte[checked(stride*rh)];pixels.CopyPixels(new Int32Rect(left,top,rw,rh),source,stride,0);
        var sizes=new ushort[rw*rh];
        foreach(var (x1,y1,x2,y2,size) in areas)for(var y=y1;y<y2;y++)for(var x=x1;x<x2;x++){var i=(y-top)*rw+x-left;if(sizes[i]<size)sizes[i]=(ushort)size;}
        var output=(byte[])source.Clone();var colors=new Dictionary<(int,int,int),uint>();
        for(var y=0;y<rh;y++)for(var x=0;x<rw;x++)
        {
            var size=sizes[y*rw+x];if(size==0)continue;var bx=(x+left)/size*size;var by=(y+top)/size*size;
            if(!colors.TryGetValue((size,bx,by),out var color))
            {
                long b=0,g=0,r=0,alpha=0,count=0;var xe=Math.Min(bx+size,w);var ye=Math.Min(by+size,h);
                for(var yy=by;yy<ye;yy++){var row=(yy-top)*stride-left*4;for(var xx=bx;xx<xe;xx++){var i=row+xx*4;b+=source[i];g+=source[i+1];r+=source[i+2];alpha+=source[i+3];count++;}}
                color=(uint)(b/count)|(uint)(g/count)<<8|(uint)(r/count)<<16|(uint)(alpha/count)<<24;colors[(size,bx,by)]=color;
            }
            var o=(y*rw+x)*4;output[o]=(byte)color;output[o+1]=(byte)(color>>8);output[o+2]=(byte)(color>>16);output[o+3]=(byte)(color>>24);
        }
        // 원본 전체는 한 번만 복사하고 계산한 부분만 덮어써 픽셀 단위로 기존과 같은 이미지를 만든다.
        var full=new byte[checked(w*4*h)];pixels.CopyPixels(full,w*4,0);
        for(var y=0;y<rh;y++)Buffer.BlockCopy(output,y*stride,full,((y+top)*w+left)*4,stride);
        var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,full,w*4);bitmap.Freeze();return bitmap;
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
        if(a.CropClip is {} clip)
        {
            var b=clip.Bounds(a);
            dc.PushClip(new RectangleGeometry(new Rect(b.X,b.Y,b.Width,b.Height)));
        }
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
        if(a.CropClip.HasValue)dc.Pop();
    }
}
