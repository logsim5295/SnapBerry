using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Rendering;
using SnapBerry.Core;

namespace SnapBerry.App.Import;

// 외부 이미지는 첫 프레임의 독립 픽셀과 PNG로 정규화한다. 사용자 파일은 수정하지 않는다.
public static class ImageImportService
{
    public const long MaxFileBytes=128L*1024*1024;
    public static Task<CaptureRecord> FromFileAsync(string path)=>Task.Run(()=>
    {
        using var stream=File.OpenRead(path);
        if(stream.Length>MaxFileBytes)throw new InvalidDataException("이미지 파일은 128MB 이하만 열 수 있습니다.");
        return CreateRecord(Decode(stream),CaptureMode.ImageFile);
    });
    public static CaptureRecord FromBitmap(BitmapSource image,CaptureMode mode=CaptureMode.Clipboard)=>CreateRecord(CopyImage(image),mode);
    private static CaptureRecord CreateRecord(BitmapSource image,CaptureMode mode)=>new()
    {Width=image.PixelWidth,Height=image.PixelHeight,Mode=mode,OriginalPng=PngFileWriter.Encode(image)};
    public static BitmapSource Decode(Stream stream)
    {
        try
        {
            // 헤더 크기를 검사한 뒤 필요한 첫 프레임만 복사한다. 스트림이나 디코더를 문서에 남기지 않는다.
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.None);
            if(decoder is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder or GifBitmapDecoder or TiffBitmapDecoder)||decoder.Frames.Count==0)
                throw new InvalidDataException("PNG, JPEG, BMP, GIF, TIFF 이미지를 선택해 주세요.");
            return CopyImage(decoder.Frames[0]);
        }
        catch(Exception e)when(e is FileFormatException or NotSupportedException or ArgumentException)
        {throw new InvalidDataException("이미지를 읽을 수 없습니다. 올바른 PNG, JPEG, BMP, GIF, TIFF 파일을 선택해 주세요.",e);}
    }
    private static BitmapSource CopyImage(BitmapSource image)
    {
        try{CompositeRenderer.GuardSize(image.PixelWidth,image.PixelHeight);}
        catch(InvalidOperationException e){throw new InvalidDataException("이미지가 너무 큽니다. 더 작은 이미지를 선택해 주세요.",e);}
        var stride=checked(image.PixelWidth*4);var pixels=new byte[checked(stride*image.PixelHeight)];
        new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0).CopyPixels(pixels,stride,0);
        var copy=BitmapSource.Create(image.PixelWidth,image.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride);copy.Freeze();return copy;
    }
}
