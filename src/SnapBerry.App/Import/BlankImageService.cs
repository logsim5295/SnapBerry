using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.Core;

namespace SnapBerry.App.Import;

public sealed record BlankImageOptions(PixelSize Size,Color Background);

// 화면 캡처 없이 독립된 PNG 원본을 만들어 기존 편집·이력 경로로 전달한다.
public static class BlankImageService
{
    public static CaptureRecord Create(BlankImageOptions options)
    {
        ImageResize.Validate(options.Size);
        var (width,height)=options.Size;
        var stride=checked(width*4);var pixels=new byte[checked(stride*height)];
        var color=options.Background;
        if(color.A!=0)
            for(var i=0;i<pixels.Length;i+=4)
            {pixels[i]=color.B;pixels[i+1]=color.G;pixels[i+2]=color.R;pixels[i+3]=color.A;}
        var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,stride);image.Freeze();
        return new(){Width=width,Height=height,Mode=CaptureMode.Blank,OriginalPng=PngFileWriter.Encode(image)};
    }
}
