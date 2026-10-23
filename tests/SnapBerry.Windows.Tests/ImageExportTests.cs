using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Export;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 대화상자나 실제 화면 캡처 없이 파일 헤더, 픽셀과 저장 형식을 확인한다.
public class ImageExportTests
{
    [Theory]
    [InlineData("png", ExportFormat.Png)]
    [InlineData("JPG", ExportFormat.Jpeg)]
    [InlineData("jpeg", ExportFormat.Jpeg)]
    [InlineData("webp", ExportFormat.WebP)]
    [InlineData("bmp", ExportFormat.Bmp)]
    [InlineData("tif", ExportFormat.Tiff)]
    [InlineData("TIFF", ExportFormat.Tiff)]
    [InlineData("gif", ExportFormat.Gif)]
    public void ExtensionSelectsTheActualEncoder(string extension, ExportFormat expected)
        => Assert.Equal(expected, ImageFileWriter.GetFormat("결과." + extension));

    [Theory]
    [InlineData("결과")]
    [InlineData("결과.txt")]
    public void UnsupportedExtensionsAreRejected(string path)
        => Assert.Throws<NotSupportedException>(() => ImageFileWriter.GetFormat(path));

    [Theory]
    [InlineData(ExportFormat.Png, "89504E470D0A1A0A")]
    [InlineData(ExportFormat.Jpeg, "FFD8FF")]
    [InlineData(ExportFormat.WebP, "52494646")]
    [InlineData(ExportFormat.Bmp, "424D")]
    [InlineData(ExportFormat.Tiff, "49492A00")]
    [InlineData(ExportFormat.Gif, "47494638")]
    public void EveryFormatHasItsOwnHeaderAndKeepsDimensions(ExportFormat format, string header) => Sta.Run(() =>
    {
        var bytes = ImageFileWriter.Encode(RenderingTests.Image(40, 30), format);
        Assert.Equal(Convert.FromHexString(header), bytes[..(header.Length / 2)]);
        if (format == ExportFormat.WebP)
        {
            Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
            using var image = SkiaSharp.SKBitmap.Decode(bytes);
            Assert.Equal(40, image.Width); Assert.Equal(30, image.Height);
            Assert.Equal(new SkiaSharp.SKColor(120, 80, 0, 255), image.GetPixel(0, 0));
        }
        else
        {
            var image = PngFileWriter.Decode(bytes);
            Assert.Equal(40, image.PixelWidth); Assert.Equal(30, image.PixelHeight);
        }
    });

    private static BitmapSource TransparentImage()
    {
        var image = BitmapSource.Create(3, 1, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { 0, 0, 0, 0, 0, 0, 240, 128, 40, 80, 120, 255 }, 12);
        image.Freeze(); return image;
    }

    [Theory]
    [InlineData(ExportFormat.Png)]
    [InlineData(ExportFormat.Tiff)]
    [InlineData(ExportFormat.WebP)]
    public void LosslessFormatsKeepColorAndTransparency(ExportFormat format) => Sta.Run(() =>
    {
        var bytes = ImageFileWriter.Encode(TransparentImage(), format);
        // Skia는 TIFF 디코더가 없으므로 WPF로 읽는다.
        if (format == ExportFormat.Tiff)
        {
            var image = new FormatConvertedBitmap(PngFileWriter.Decode(bytes), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[12]; image.CopyPixels(pixels, 12, 0);
            Assert.Equal(0, pixels[3]); Assert.Equal(128, pixels[7]);
            Assert.Equal(new byte[] { 40, 80, 120, 255 }, pixels[8..12]);
        }
        else
        {
            using var bitmap = SkiaSharp.SKBitmap.Decode(bytes);
            Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);
            Assert.Equal(128, bitmap.GetPixel(1, 0).Alpha);
            Assert.Equal(new SkiaSharp.SKColor(120, 80, 40, 255), bitmap.GetPixel(2, 0));
        }
    });

    [Theory]
    [InlineData(ExportFormat.Jpeg)]
    [InlineData(ExportFormat.Bmp)]
    [InlineData(ExportFormat.Gif)]
    public void OpaqueFormatsUseAWhiteBackground(ExportFormat format) => Sta.Run(() =>
    {
        var image = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, new byte[8 * 8 * 4], 32);
        image.Freeze();
        var decoded = new FormatConvertedBitmap(PngFileWriter.Decode(ImageFileWriter.Encode(image, format)), PixelFormats.Bgra32, null, 0);
        var pixels = new byte[8 * 8 * 4]; decoded.CopyPixels(pixels, 32, 0);
        Assert.All(pixels, value => Assert.Equal(255, value));
    });

    [Fact]
    public void SaveDialogOffersSixFormatsAndDefaultsToPng() => Sta.Run(() =>
    {
        var dialog = ExportPathPicker.CreateDialog("", "결과.png");
        Assert.Equal("이미지 파일 저장", dialog.Title);
        Assert.Equal(new[] { "*.png", "*.jpg;*.jpeg", "*.webp", "*.bmp", "*.tif;*.tiff", "*.gif" },
            dialog.Filter.Split('|').Where((_, index) => index % 2 == 1));
        Assert.Equal(1, dialog.FilterIndex); Assert.True(dialog.AddExtension); Assert.True(dialog.OverwritePrompt);
    });

    [Theory]
    [InlineData(16384, 1)]
    [InlineData(1, 16384)]
    public void OversizedWebPReportsItsDimensionLimitWithoutRestrictingPng(int width, int height) => Sta.Run(() =>
    {
        var image = RenderingTests.Image(width, height);
        var error = Assert.Throws<InvalidOperationException>(() => ImageFileWriter.Encode(image, ExportFormat.WebP));
        Assert.Contains("16383", error.Message);
        Assert.NotEmpty(ImageFileWriter.Encode(image, ExportFormat.Png));
    });

    [Theory]
    [InlineData("최종.jpeg")]
    [InlineData("최종.WEBP")]
    [InlineData("최종.tiff")]
    public void SuggestedNameDoesNotStackImageExtensions(string name)
        => Assert.Equal("최종.png", ExportPathPicker.SuggestFileName(name, DateTime.Now));

    [Fact]
    public async Task ExportFailurePreservesTheExistingTargetAndCleansTemporaryFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "SnapBerry-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "result.jpg");
        try
        {
            await File.WriteAllTextAsync(path, "original");
            await Assert.ThrowsAsync<IOException>(() => ImageFileWriter.WriteAsync(path, [1, 2, 3], false));
            Assert.Equal("original", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(folder));
        }
        finally { Directory.Delete(folder, true); }
    }
}
