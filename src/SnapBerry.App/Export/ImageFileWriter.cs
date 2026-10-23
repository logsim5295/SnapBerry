using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace SnapBerry.App.Export;

// 설정 JSON에 숫자로 저장되므로 기존 형식의 값을 유지한다.
public enum ExportFormat { Png = 0, Jpeg = 1, WebP = 2, Bmp = 3, Tiff = 4, Gif = 5 }

// 내부 이력·클립보드의 PNG와 별개로, 외부 파일의 확장자에 맞는 인코더를 선택한다.
public static class ImageFileWriter
{
    public static bool TryGetFormat(string path, out ExportFormat format)
    {
        ExportFormat? value = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ExportFormat.Png,
            ".jpg" or ".jpeg" => ExportFormat.Jpeg,
            ".webp" => ExportFormat.WebP,
            ".bmp" => ExportFormat.Bmp,
            ".tif" or ".tiff" => ExportFormat.Tiff,
            ".gif" => ExportFormat.Gif,
            _ => null
        };
        format = value.GetValueOrDefault();
        return value.HasValue;
    }

    public static ExportFormat GetFormat(string path) => TryGetFormat(path, out var format) ? format
        : throw new NotSupportedException("PNG, JPEG, WebP, BMP, TIFF, GIF 확장자로 저장해 주세요.");

    public static string GetDisplayName(ExportFormat format) => format switch
    {
        ExportFormat.Png => "PNG", ExportFormat.Jpeg => "JPEG", ExportFormat.WebP => "WebP",
        ExportFormat.Bmp => "BMP", ExportFormat.Tiff => "TIFF", ExportFormat.Gif => "GIF",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static string GetDefaultExtension(ExportFormat format) => format switch
    {
        ExportFormat.Png => ".png", ExportFormat.Jpeg => ".jpg", ExportFormat.WebP => ".webp",
        ExportFormat.Bmp => ".bmp", ExportFormat.Tiff => ".tif", ExportFormat.Gif => ".gif",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static Task WriteAsync(string path, byte[] bytes, bool overwrite)
        => PngFileWriter.WriteAsync(path, bytes, overwrite);

    public static byte[] Encode(BitmapSource bitmap, ExportFormat format)
    {
        if (format == ExportFormat.WebP) return EncodeWebP(bitmap);
        BitmapEncoder encoder = format switch
        {
            ExportFormat.Png => new PngBitmapEncoder(),
            ExportFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = 92 },
            ExportFormat.Bmp => new BmpBitmapEncoder(),
            ExportFormat.Tiff => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip },
            ExportFormat.Gif => new GifBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        if (format is ExportFormat.Jpeg or ExportFormat.Bmp or ExportFormat.Gif)
        {
            bitmap = FlattenOnWhite(bitmap);
            bitmap = new FormatConvertedBitmap(bitmap,
                format == ExportFormat.Gif ? PixelFormats.Indexed8 : PixelFormats.Bgr24,
                format == ExportFormat.Gif ? BitmapPalettes.Halftone256 : null, 0);
        }
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream); return stream.ToArray();
    }

    private static byte[] CopyPixels(BitmapSource bitmap)
    {
        var pixels = new byte[checked(bitmap.PixelWidth * bitmap.PixelHeight * 4)];
        new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, checked(bitmap.PixelWidth * 4), 0);
        return pixels;
    }

    private static BitmapSource FlattenOnWhite(BitmapSource bitmap)
    {
        var pixels = CopyPixels(bitmap);
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            for (var channel = 0; channel < 3; channel++)
                pixels[i + channel] = (byte)((pixels[i + channel] * alpha + 255 * (255 - alpha) + 127) / 255);
            pixels[i + 3] = 255;
        }
        var result = BitmapSource.Create(bitmap.PixelWidth, bitmap.PixelHeight, bitmap.DpiX, bitmap.DpiY,
            PixelFormats.Bgra32, null, pixels, checked(bitmap.PixelWidth * 4));
        result.Freeze(); return result;
    }

    private static byte[] EncodeWebP(BitmapSource bitmap)
    {
        if (bitmap.PixelWidth > 16383 || bitmap.PixelHeight > 16383)
            throw new InvalidOperationException("WebP는 가로·세로 각각 16383px 이하로 저장할 수 있습니다. 이미지 크기를 줄이거나 다른 파일 형식을 선택해 주세요.");
        var pixels = CopyPixels(bitmap);
        using var image = new SKBitmap(new SKImageInfo(bitmap.PixelWidth, bitmap.PixelHeight,
            SKColorType.Bgra8888, SKAlphaType.Unpremul));
        Marshal.Copy(pixels, 0, image.GetPixels(), pixels.Length);
        using var pixmap = image.PeekPixels();
        using var data = pixmap.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100))
            ?? throw new InvalidDataException("WebP 변환에 실패했습니다.");
        return data.ToArray();
    }
}
