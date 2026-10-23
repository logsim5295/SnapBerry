using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 저장 창을 테스트 대역으로 대체하고 창을 표시하지 않은 상태로 실제 저장 명령을 검증한다.
public class ImageExportWorkflowTests
{
    private sealed class Picker(string? path) : IExportPathPicker
    {
        public string? Pick(Window owner, string directory, string suggestedFileName) => path;
    }
    private static void Pump(Task task)
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }

    [Theory]
    [InlineData("png", "PNG")]
    [InlineData("jpeg", "JPEG")]
    [InlineData("webp", "WebP")]
    [InlineData("bmp", "BMP")]
    [InlineData("tiff", "TIFF")]
    [InlineData("gif", "GIF")]
    public void SaveCommandExportsTheCompositeInTheSelectedFormat(string extension, string displayName) => Sta.Run(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "SnapBerry-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "result." + extension);
        var settings = new SettingsService(folder);
        var window = new EditorWindow(new(Path.Combine(folder, "Library")), settings, new Picker(path));
        try
        {
            Assert.Equal("이미지 파일 저장", ((RibbonButton)window.FindName("SaveButton")).Label);
            window.Model.Open(new() { Width = 40, Height = 30, OriginalPng = PngFileWriter.Encode(RenderingTests.Image(40, 30)) });
            window.Model.History!.Crop(new PixelRect(0, 0, 20, 10));
            Pump(window.SaveAsync());
            var bytes = File.ReadAllBytes(path);
            if (extension == "webp")
            {
                using var image = SkiaSharp.SKBitmap.Decode(bytes);
                Assert.Equal(20, image.Width); Assert.Equal(10, image.Height);
            }
            else
            {
                using var stream = new MemoryStream(bytes);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var actual = decoder switch
                {
                    PngBitmapDecoder => ExportFormat.Png, JpegBitmapDecoder => ExportFormat.Jpeg,
                    BmpBitmapDecoder => ExportFormat.Bmp, TiffBitmapDecoder => ExportFormat.Tiff,
                    GifBitmapDecoder => ExportFormat.Gif, _ => throw new InvalidDataException()
                };
                Assert.Equal(ImageFileWriter.GetFormat(path), actual);
                var image = decoder.Frames[0];
                Assert.Equal(20, image.PixelWidth); Assert.Equal(10, image.PixelHeight);
            }
            Assert.StartsWith(displayName + " 저장됨", ((TextBlock)window.FindName("StatusText")).Text);
            Assert.False(window.Model.History.IsDirty);
            Assert.Equal(folder, settings.Load().LastSaveDirectory);
            Assert.False(window.IsVisible); Assert.False(window.IsBusy);
            Pump(window.FlushAsync());
        }
        finally { window.CloseForExit(); TestFiles.DeleteDirectory(folder); }
    });

    [Fact]
    public void CancellingSaveLeavesTheDocumentAndExportStateUntouched() => Sta.Run(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "SnapBerry-cancel-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var window = new EditorWindow(new(Path.Combine(folder, "Library")), new SettingsService(folder), new Picker(null));
        try
        {
            window.Model.Open(new() { Width = 40, Height = 30, OriginalPng = PngFileWriter.Encode(RenderingTests.Image(40, 30)) });
            var document = window.Model.Document; var token = window.Model.History!.CurrentStateToken;
            var dirty = window.Model.History.IsDirty;
            Pump(window.SaveAsync());
            Assert.Same(document, window.Model.Document); Assert.Equal(token, window.Model.History.CurrentStateToken);
            Assert.Equal(dirty, window.Model.History.IsDirty);
            Assert.False(window.IsBusy); Assert.False(window.IsVisible);
            Assert.Empty(Directory.GetFiles(folder, "result.*"));
        }
        finally { window.CloseForExit(); TestFiles.DeleteDirectory(folder); }
    });
}
