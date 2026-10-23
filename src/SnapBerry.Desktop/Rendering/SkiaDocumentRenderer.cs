using SkiaSharp;
using SnapBerry.Core;

namespace SnapBerry.Desktop.Rendering;

// 화면·PNG·썸네일이 같은 원본 좌표와 합성 순서를 사용한다.
public static class SkiaDocumentRenderer
{
    private static readonly SKTypeface TextTypeface=SKFontManager.Default.MatchCharacter('한')??SKTypeface.Default;
    public static SKBitmap Decode(byte[] png)
    {
        using var data=SKData.CreateCopy(png);using var codec=SKCodec.Create(data)??throw new InvalidDataException("이미지를 읽을 수 없습니다.");
        ImageDimensions.GuardSize(codec.Info.Width,codec.Info.Height);
        return SKBitmap.Decode(png)??throw new InvalidDataException("이미지가 손상되었습니다.");
    }
    public static byte[] Encode(SKBitmap image)
    {using var data=image.Encode(SKEncodedImageFormat.Png,100)??throw new InvalidDataException("PNG 변환에 실패했습니다.");return data.ToArray();}
    public static byte[] Render(byte[] original,AnnotationDocument document,IReadOnlyDictionary<string,byte[]> assets)
    {
        if(document.BackgroundAssetHash is {} hash)
            original=assets.TryGetValue(hash,out var background)?background:throw new InvalidDataException("누끼 이미지가 없습니다.");
        using var source=Decode(original);
        if(source.Width!=document.SourceWidth||source.Height!=document.SourceHeight)throw new InvalidDataException("이미지 크기가 원본과 다릅니다.");
        using var mosaic=Mosaic(source,document.Annotations);
        var size=document.OutputSize;ImageDimensions.GuardSize(size.Width,size.Height);
        using var colorSpace=SKColorSpace.CreateSrgb();
        using var output=new SKBitmap(new SKImageInfo(size.Width,size.Height,SKColorType.Bgra8888,SKAlphaType.Premul,colorSpace));using var canvas=new SKCanvas(output);canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)size.Width/document.Width,(float)size.Height/document.Height);canvas.Translate(-document.CropBounds.X,-document.CropBounds.Y);
        using var imagePaint=new SKPaint{IsAntialias=true};canvas.DrawBitmap(mosaic,0,0,imagePaint);
        foreach(var item in document.Annotations.Where(a=>a.Kind!=AnnotationKind.Mosaic))Draw(canvas,item,assets);
        return Encode(output);
    }
    public static void Draw(SKCanvas canvas,Annotation a,IReadOnlyDictionary<string,byte[]> assets)
    {
        var x=(float)a.X;var y=(float)a.Y;var w=(float)a.Width;var h=(float)a.Height;
        using var paint=new SKPaint{Color=Color(a.Style.Color),StrokeWidth=(float)a.Style.Thickness,IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeCap=SKStrokeCap.Round,StrokeJoin=SKStrokeJoin.Round};
        using var dash=a.Style.LineStyle switch
        {
            StrokePattern.Dash=>SKPathEffect.CreateDash([4*paint.StrokeWidth,2*paint.StrokeWidth],0),
            StrokePattern.Dot=>SKPathEffect.CreateDash([paint.StrokeWidth,2*paint.StrokeWidth],0),
            StrokePattern.DashDot=>SKPathEffect.CreateDash([4*paint.StrokeWidth,2*paint.StrokeWidth,paint.StrokeWidth,2*paint.StrokeWidth],0),
            StrokePattern.DashDotDot=>SKPathEffect.CreateDash([4*paint.StrokeWidth,2*paint.StrokeWidth,paint.StrokeWidth,2*paint.StrokeWidth,paint.StrokeWidth,2*paint.StrokeWidth],0),_=>null
        };paint.PathEffect=dash;
        switch(a.Kind)
        {
            case AnnotationKind.Rectangle:canvas.DrawRect(x,y,Math.Max(1,w),Math.Max(1,h),paint);break;
            case AnnotationKind.Arrow:
                canvas.DrawLine(x,y,x+w,y+h,paint);paint.PathEffect=null;paint.Style=SKPaintStyle.Fill;
                var angle=Math.Atan2(h,w);var length=Math.Max(12,paint.StrokeWidth*4);using(var path=new SKPath())
                {path.MoveTo(x+w,y+h);path.LineTo(x+w-(float)(length*Math.Cos(angle-.45)),y+h-(float)(length*Math.Sin(angle-.45)));path.LineTo(x+w-(float)(length*Math.Cos(angle+.45)),y+h-(float)(length*Math.Sin(angle+.45)));path.Close();canvas.DrawPath(path,paint);}break;
            case AnnotationKind.Text:DrawText(canvas,a.Text,x,y,(float)a.Style.FontSize,Color(a.Style.Color));break;
            case AnnotationKind.StepMarker:
                paint.Style=SKPaintStyle.Fill;paint.Color=Color(a.Style.Fill);canvas.DrawOval(new SKRect(x,y,x+w,y+h),paint);
                var fontSize=Math.Min(w,h)*.52f;using(var font=Font(fontSize))
                {font.MeasureText(a.Text,out var bounds);DrawText(canvas,a.Text,x+(w-bounds.Width)/2-bounds.Left,y+(h-bounds.Height)/2-bounds.Top-fontSize,fontSize,Color(a.Style.TextColor));}break;
            case AnnotationKind.Sticker:
                if(!assets.TryGetValue(a.AssetHash,out var png))throw new InvalidDataException("추가 이미지가 없습니다.");
                using(var image=Decode(png)){paint.Style=SKPaintStyle.Fill;paint.Color=SKColors.White;paint.PathEffect=null;canvas.DrawBitmap(image,new SKRect(x,y,x+w,y+h),paint);}break;
        }
    }
    public static SKFont Font(float size)=>new(TextTypeface,size);
    public static PixelSize MeasureText(string text,double size)
    {
        using var font=Font((float)size);var lines=text.Split('\n');
        return new(Math.Max(1,(int)Math.Ceiling(lines.Max(line=>font.MeasureText(line)))),Math.Max(1,(int)Math.Ceiling(lines.Length*size*1.35)));
    }
    private static void DrawText(SKCanvas canvas,string text,float x,float y,float size,SKColor color)
    {
        using var font=Font(size);using var paint=new SKPaint{Color=color,IsAntialias=true};
        foreach(var line in text.Split('\n')){canvas.DrawText(line,x,y+size,SKTextAlign.Left,font,paint);y+=size*1.35f;}
    }
    private static SKColor Color(string value)=>SKColor.TryParse(value,out var color)?color:throw new InvalidDataException("주석 색상이 올바르지 않습니다.");
    private static SKBitmap Mosaic(SKBitmap source,IReadOnlyList<Annotation> annotations)
    {
        var result=source.Copy();var areas=annotations.Where(a=>a.Kind==AnnotationKind.Mosaic).ToArray();if(areas.Length==0)return result;
        var width=source.Width;var height=source.Height;var sizes=new int[width*height];
        foreach(var a in areas)
        {
            var left=(int)Math.Clamp(a.X,0,width);var top=(int)Math.Clamp(a.Y,0,height);var right=(int)Math.Clamp(Math.Ceiling(a.X+a.Width),0,width);var bottom=(int)Math.Clamp(Math.Ceiling(a.Y+a.Height),0,height);
            for(var y=top;y<bottom;y++)for(var x=left;x<right;x++)sizes[y*width+x]=Math.Max(sizes[y*width+x],Math.Clamp(a.MosaicBlockSize,1,1024));
        }
        var blocks=new Dictionary<(int,int,int),SKColor>();
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            var size=sizes[y*width+x];if(size==0)continue;var bx=x/size*size;var by=y/size*size;
            if(!blocks.TryGetValue((size,bx,by),out var color))
            {
                long red=0,green=0,blue=0,alpha=0,count=0;
                for(var yy=by;yy<Math.Min(by+size,height);yy++)for(var xx=bx;xx<Math.Min(bx+size,width);xx++){var pixel=source.GetPixel(xx,yy);red+=pixel.Red;green+=pixel.Green;blue+=pixel.Blue;alpha+=pixel.Alpha;count++;}
                color=new((byte)(red/count),(byte)(green/count),(byte)(blue/count),(byte)(alpha/count));blocks[(size,bx,by)]=color;
            }
            result.SetPixel(x,y,color);
        }
        return result;
    }
}
