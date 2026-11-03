namespace SnapBerry.Core;

public static class ImagePlacement
{
    public static bool IsInsertedImage(Annotation? item) =>
        item is { Kind: AnnotationKind.Sticker, StickerId: "pasted-image" };

    public static Annotation Center(AnnotationDocument document, Annotation image, double outputWidth, double outputHeight)
    {
        var width=outputWidth*document.Width/document.OutputSize.Width;
        var height=outputHeight*document.Height/document.OutputSize.Height;
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0||width>1000000||height>1000000)
            throw new InvalidDataException("붙여넣을 이미지의 표시 크기가 올바르지 않습니다.");

        // 홀수 픽셀만큼 넘칠 때는 반 픽셀씩 양쪽에 보간하지 않는다.
        static double Offset(double gap)=>gap<0?Math.Ceiling(gap/2):gap/2;
        return image with{X=document.CropBounds.X+Offset(document.Width-width),
            Y=document.CropBounds.Y+Offset(document.Height-height),Width=width,Height=height};
    }

    public static PixelRect CanvasFor(PixelRect canvas, Annotation image)
    {
        if(!IsInsertedImage(image))return canvas;
        var left=Math.Min(canvas.X,Math.Floor(image.X));
        var top=Math.Min(canvas.Y,Math.Floor(image.Y));
        var right=Math.Max(canvas.X+canvas.Width,Math.Ceiling(image.X+image.Width));
        var bottom=Math.Max(canvas.Y+canvas.Height,Math.Ceiling(image.Y+image.Height));
        if(!(Math.Abs(image.X)<=1000000&&Math.Abs(image.Y)<=1000000&&image.Width is >0 and <=1000000&&image.Height is >0 and <=1000000)
            ||right-left>int.MaxValue||bottom-top>int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(image),"이미지를 배치할 수 있는 범위를 벗어났습니다.");
        var bounds=new PixelRect((int)left,(int)top,(int)(right-left),(int)(bottom-top));
        if(!CropPlacement.IsValidCanvas(bounds))
            throw new ArgumentOutOfRangeException(nameof(image),"확장할 캔버스가 너무 큽니다.");
        return bounds;
    }
}
