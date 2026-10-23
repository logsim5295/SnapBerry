using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Views;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class DesktopLayoutTests
{
    private sealed class NoCapture : ICaptureService
    {
        public Task<byte[]?> CaptureAsync(DesktopCaptureMode mode, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Native capture prohibited in layout tests");
    }

    [AvaloniaFact]
    public void CompactLabelsKeepTheMacAutomationCommandNames()
    {
        var path = Path.Combine(Path.GetTempPath(), "SnapBerry-command-names-" + Guid.NewGuid());
        using var editor = new DesktopEditor();
        using var window = new MainWindow(editor, new NoCapture(), path);
        try
        {
            foreach (var (id, name) in new[] { ("WindowButton", "창 캡처"), ("OpenButton", "이미지 열기"), ("CopyButton", "이미지 복사"), ("RegionButton", "영역 캡처"), ("DisplayButton", "전체 화면") })
                Assert.Equal(name, AutomationProperties.GetName(window.FindControl<Button>(id)!));
        }
        finally { window.Dispose(); window.Close(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [AvaloniaFact]
    public async Task LongFailureMessagesRemainReadableWhenTheStatusLineIsTrimmed()
    {
        var path = Path.Combine(Path.GetTempPath(), "SnapBerry-status-" + Guid.NewGuid());
        using var editor = new DesktopEditor();
        using var window = new MainWindow(editor, new NoCapture(), path) { Width = 820 };
        try
        {
            var message = "파일을 저장할 수 없습니다: " + new string('가', 180);
            await window.RunAsync(() => throw new IOException(message));
            Dispatcher.UIThread.RunJobs();
            var status = window.FindControl<TextBlock>("StatusText")!;
            Assert.Contains(message, status.Text);
            var tip = Assert.IsType<TextBlock>(ToolTip.GetTip(status));
            Assert.Equal(status.Text, tip.Text);
            Assert.Equal(Avalonia.Media.TextWrapping.Wrap, tip.TextWrapping);
            Assert.True(tip.MaxWidth <= 520);
        }
        finally { window.Dispose(); window.Close(); if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingsFooterAndKeyboardFieldsRemainVisible(bool dark)
    {
        var variant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        foreach (var dialog in new Window[] {
            new DesktopSettingsWindow(dark, false, true, (_, _) => Task.CompletedTask),
            new HotkeySettingsWindow(new(), _ => Task.CompletedTask) })
        {
            try
            {
                dialog.RequestedThemeVariant = variant; dialog.Show(); Dispatcher.UIThread.RunJobs();
                var save = dialog.FindControl<Button>(dialog is HotkeySettingsWindow ? "SaveHotkeys" : "SaveSettingsButton")!;
                var position = save.TranslatePoint(default, dialog)!.Value;
                Assert.True(position.Y + save.Bounds.Height <= dialog.ClientSize.Height);
                Assert.InRange(dialog.ClientSize.Height, 250, 640);
                SaveFrame(dialog, $"{(dialog is HotkeySettingsWindow ? "hotkeys" : "settings")}-{dark}");
            }
            finally { dialog.Close(); }
        }
    }

    [AvaloniaTheory]
    [InlineData(false, 1240, 820)]
    [InlineData(true, 1240, 820)]
    [InlineData(false, 820, 560)]
    [InlineData(true, 820, 560)]
    public async Task CompactEditorKeepsCanvasAndEveryCommandReachable(bool dark, int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), "SnapBerry-layout-" + Guid.NewGuid());
        using var editor = new DesktopEditor();
        using var window = new MainWindow(editor, new NoCapture(), path) { Width = width, Height = height };
        try
        {
            window.ApplyTheme(dark); window.Show(); Dispatcher.UIThread.RunJobs();
            var bar = window.FindControl<Control>("CommandBar");
            Assert.NotNull(bar);
            Assert.InRange(bar.Bounds.Height, 40, 76);
            var toolScroll = window.FindControl<ScrollViewer>("ToolScroll")!;
            Assert.NotNull(toolScroll);
            Assert.True(window.FindControl<ScrollViewer>("ImageScroll")!.Bounds.Height >= height - 190);
            Assert.Equal(ScrollBarVisibility.Auto, toolScroll.HorizontalScrollBarVisibility);

            window.SetBusy(true);
            foreach (var name in new[] { "OpenButton", "RegionButton", "EmptyCaptureButton", "HistoryToggleButton", "ViewOptionsButton" })
                Assert.False(window.FindControl<Button>(name)!.IsEffectivelyEnabled, name);
            window.SetBusy(false);
            Assert.True(window.FindControl<Button>("EmptyCaptureButton")!.IsEffectivelyEnabled);
            SaveFrame(window, $"editor-empty-{dark}-{width}");

            editor.Open(EditorTests.Record()); Dispatcher.UIThread.RunJobs();
            var tools = window.FindControl<WrapPanel>("ToolButtons")!.Children.OfType<Button>().Where(x => x.Tag is string).ToArray();
            foreach (var button in tools)
            {
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(int.Parse((string)button.Tag!), (int)editor.Tool);
                Assert.Contains("active", button.Classes);
            }
            // Text properties are the widest group; scrolling must preserve access in the narrow layout.
            window.FindControl<ComboBox>("ToolBox")!.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<TextBox>("TextEntry")!.IsEffectivelyVisible);
            var properties = window.FindControl<ScrollViewer>("PropertiesScroll")!;
            Assert.Equal(ScrollBarVisibility.Auto, properties.HorizontalScrollBarVisibility);
            var number = window.FindControl<NumericUpDown>("FontBox")!;
            var input = number.GetVisualDescendants().OfType<TextBox>().Single();
            Assert.True(input.Bounds.Width >= 40, $"Numeric input width: {input.Bounds.Width}");
            Assert.True(window.FindControl<ScrollViewer>("ImageScroll")!.Bounds.Height >= height - 250);
            SaveFrame(window, $"editor-text-{dark}-{width}");
        }
        finally
        {
            await window.RequestExitAsync(); window.Dispose(); window.Close();
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AboutDialogKeepsIdentityAndLegalDocumentsWithinScrollableBody(bool dark)
    {
        var dialog = new AboutWindow("/tmp/layout", _ => { }, (Action<string>)(_ => { }))
        { RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light };
        try
        {
            dialog.Show(); Dispatcher.UIThread.RunJobs();
            Assert.InRange(dialog.ClientSize.Height, 300, 560);
            Assert.NotNull(dialog.FindControl<Image>("AboutAppIcon"));
            SaveFrame(dialog, $"about-{dark}");
            dialog.FindControl<Expander>("AboutLegalInformation")!.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, dialog.FindControl<TabControl>("AboutLegalDocuments")!.Items.Count);
            Assert.All(dialog.FindControl<TabControl>("AboutLegalDocuments")!.Items.OfType<TabItem>(), tab => Assert.InRange(tab.FontSize, 12, 15));
            Assert.IsType<ScrollViewer>(dialog.Content);
            SaveFrame(dialog, $"about-legal-{dark}");
        }
        finally { dialog.Close(); }
    }

    private static void SaveFrame(Window window, string name)
    {
        var folder = Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
        if (folder is null) return;
        Directory.CreateDirectory(folder); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
