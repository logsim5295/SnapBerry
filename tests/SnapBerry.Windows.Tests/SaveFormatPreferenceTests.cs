using System.IO;
using System.Windows;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 창을 열지 않고 형식 선택의 설정 저장, 다음 저장과 재실행 시 복원을 검증한다.
public class SaveFormatPreferenceTests
{
    private sealed class Picker(params string?[] paths) : IExportPathPicker
    {
        private readonly Queue<string?> results = new(paths);
        public List<string> SuggestedNames { get; } = [];
        public string? Pick(Window owner, string directory, string suggestedFileName)
        {
            SuggestedNames.Add(suggestedFileName);
            return results.Dequeue();
        }
    }

    private static void Pump(Task task)
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var frame = new DispatcherFrame();
        task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame); task.GetAwaiter().GetResult();
    }

    private static void OpenImage(EditorWindow window) => window.Model.Open(new()
    {
        Name = "최종 결과.png", Width = 40, Height = 30,
        OriginalPng = PngFileWriter.Encode(RenderingTests.Image(40, 30))
    });

    [Theory]
    [InlineData("png", ExportFormat.Png, ".png", 1)]
    [InlineData("jpg", ExportFormat.Jpeg, ".jpg", 2)]
    [InlineData("JPEG", ExportFormat.Jpeg, ".jpg", 2)]
    [InlineData("webp", ExportFormat.WebP, ".webp", 3)]
    [InlineData("bmp", ExportFormat.Bmp, ".bmp", 4)]
    [InlineData("tif", ExportFormat.Tiff, ".tif", 5)]
    [InlineData("TIFF", ExportFormat.Tiff, ".tif", 5)]
    [InlineData("gif", ExportFormat.Gif, ".gif", 6)]
    public void SuccessfulSaveRemembersFormatForNextSaveAndRestart(string extension, ExportFormat expected,
        string defaultExtension, int filterIndex) => Sta.Run(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "SnapBerry-save-format-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var settings = new SettingsService(folder);
        // 처음에는 다른 형식을 기억하도록 하여 PNG로 다시 저장하는 경우도 확인한다.
        settings.Save(new() { LastSaveFormat = ExportFormat.Gif, RegionKey = "Ctrl+Alt+7" });
        var picker = new Picker(Path.Combine(folder, "result." + extension), null);
        var window = new EditorWindow(new(Path.Combine(folder, "Library")), settings, picker);
        try
        {
            OpenImage(window); Pump(window.SaveAsync());
            Assert.Equal(expected, settings.Load().LastSaveFormat);
            Assert.Equal("Ctrl+Alt+7", settings.Load().RegionKey);
            Pump(window.SaveAsync());
            Assert.Equal("최종 결과" + defaultExtension, picker.SuggestedNames[1]);
            Assert.False(window.IsVisible); Pump(window.FlushAsync());
        }
        finally { window.CloseForExit(); }

        var nextPicker = new Picker((string?)null);
        var reopened = new EditorWindow(new(Path.Combine(folder, "Library")), new SettingsService(folder), nextPicker);
        try
        {
            OpenImage(reopened); Pump(reopened.SaveAsync());
            var suggestedName = Assert.Single(nextPicker.SuggestedNames);
            Assert.Equal("최종 결과" + defaultExtension, suggestedName);
            var dialog = ExportPathPicker.CreateDialog(folder, suggestedName);
            Assert.Equal(filterIndex, dialog.FilterIndex);
            Assert.Equal(defaultExtension.TrimStart('.'), dialog.DefaultExt);
            Assert.Equal(suggestedName, dialog.FileName);
            Assert.True(dialog.OverwritePrompt); Assert.False(reopened.IsVisible);
        }
        finally { reopened.CloseForExit(); TestFiles.DeleteDirectory(folder); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledOrFailedSaveKeepsThePreviousFormat(bool fail) => Sta.Run(() =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "SnapBerry-save-format-cancel-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(folder);
        settings.Save(new() { LastSaveFormat = ExportFormat.Jpeg });
        var picker = new Picker(fail ? Path.Combine(folder, "missing", "result.webp") : null, null);
        var window = new EditorWindow(new(Path.Combine(folder, "Library")), settings, picker);
        try
        {
            OpenImage(window);
            if (fail) Assert.ThrowsAny<IOException>(() => Pump(window.SaveAsync()));
            else Pump(window.SaveAsync());
            Assert.Equal(ExportFormat.Jpeg, settings.Load().LastSaveFormat);
            Pump(window.SaveAsync());
            Assert.All(picker.SuggestedNames, name => Assert.Equal("최종 결과.jpg", name));
            Assert.False(window.IsBusy); Assert.False(window.IsVisible);
        }
        finally { window.CloseForExit(); TestFiles.DeleteDirectory(folder); }
    });

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"LastSaveFormat\":999,\"RegionKey\":\"Ctrl+Alt+7\"}")]
    [InlineData("{\"LastSaveFormat\":-1,\"Theme\":1}")]
    public void OldOrUnknownFormatSettingsUsePngWithoutResettingOtherSettings(string json)
    {
        var folder = Path.Combine(Path.GetTempPath(), "SnapBerry-save-format-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "settings.json"), json);
            var settings = new SettingsService(folder).Load();
            Assert.Equal(ExportFormat.Png, settings.LastSaveFormat);
            if (json.Contains("Ctrl+Alt+7")) Assert.Equal("Ctrl+Alt+7", settings.RegionKey);
            if (json.Contains("Theme")) Assert.Equal(AppTheme.Dark, settings.Theme);
            Assert.Empty(Directory.GetFiles(folder, "settings.*.bak"));
        }
        finally { TestFiles.DeleteDirectory(folder); }
    }

    [Theory]
    [InlineData("", "Capture_20261022_120000.jpg")]
    [InlineData("CON", "_CON.jpg")]
    [InlineData("결과.tiff", "결과.jpg")]
    public void RememberedFormatKeepsFilenameSanitization(string name, string expected)
        => Assert.Equal(expected, ExportPathPicker.SuggestFileName(name, new(2026, 10, 22, 12, 0, 0), ExportFormat.Jpeg));
}
