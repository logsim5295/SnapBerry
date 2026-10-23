using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Clipboard;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Shell;
using SnapBerry.Desktop.Shortcuts;
using SnapBerry.Desktop.Views;
using SnapBerry.App.Stickers;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class IntegrationTests
{
    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void HotkeySettingsFitsAllFieldsAndButtonsInBothThemes(bool dark)
    {
        var settings=new HotkeySettingsWindow(new(),_=>Task.CompletedTask){RequestedThemeVariant=dark?Avalonia.Styling.ThemeVariant.Dark:Avalonia.Styling.ThemeVariant.Light};
        try
        {
            settings.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();var save=settings.FindControl<Button>("SaveHotkeys")!;
            var point=save.TranslatePoint(default,settings);Assert.NotNull(point);Assert.True(point.Value.Y+save.Bounds.Height<=settings.ClientSize.Height);
            Assert.All(new[]{"RegionHotkey","WindowHotkey","DisplayHotkey"},name=>Assert.True(settings.FindControl<TextBox>(name)!.Bounds.Height>20));
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");using var frame=settings.CaptureRenderedFrame()!;if(folder is not null){Directory.CreateDirectory(folder);frame.Save(Path.Combine(folder,$"hotkeys-{dark}.png"),PngBitmapEncoderOptions.Default);}
        }
        finally{settings.Close();}
    }
    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void StickerPickerFiltersKoreanCategoriesAndInsertsWithoutClosing(bool dark)
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());var picker=new StickerPickerWindow(editor.InsertSticker){RequestedThemeVariant=dark?Avalonia.Styling.ThemeVariant.Dark:Avalonia.Styling.ThemeVariant.Light};
        try
        {
            picker.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();picker.FindControl<ComboBox>("StickerCategories")!.SelectedItem="키보드";picker.FindControl<TextBox>("StickerSearch")!.Text="Command";
            var root=(Avalonia.Controls.Grid)picker.Content!;var scroll=(ScrollViewer)root.Children[2];var panel=(WrapPanel)scroll.Content!;
            Assert.NotEmpty(panel.Children);Assert.All(panel.Children.OfType<Button>(),button=>Assert.Equal("Keyboard",Assert.IsType<StickerDefinition>(button.Tag).Category));
            panel.Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert.Single(editor.Document!.Annotations);Assert.True(picker.IsVisible);
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");using var frame=picker.CaptureRenderedFrame()!;if(folder is not null){Directory.CreateDirectory(folder);frame.Save(Path.Combine(folder,$"stickers-{dark}.png"),PngBitmapEncoderOptions.Default);}
        }
        finally{picker.Dispose();picker.Close();}
    }
    [AvaloniaFact] public async Task WindowCloseSavesThenHidesWithoutUnregisteringAndExitReleasesBindings()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-mac-resident-"+Guid.NewGuid());var backend=new GlobalHotkeyTests.Backend();var service=new GlobalHotkeyService(backend);using var editor=new DesktopEditor();var window=new MainWindow(editor,new NoCapture(),path,null,service);
        try
        {
            editor.Open(EditorTests.Record());window.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();window.Close();
            var until=DateTime.UtcNow.AddSeconds(5);while(window.IsBusy){Assert.True(DateTime.UtcNow<until);await Task.Delay(1,TestContext.Current.CancellationToken);}
            Assert.False(window.IsVisible);Assert.False(window.CanExit);Assert.False(backend.Disposed);Assert.Equal(3,backend.Active.Count);Assert.Single(Directory.GetFiles(Path.Combine(path,"Library"),"*.qcapture"));
            var exited=false;window.ExitRequested+=()=>exited=true;Assert.True(await window.RequestExitAsync());Assert.True(exited);Assert.True(backend.Disposed);Assert.Empty(backend.Active);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    private sealed class NoCapture : ICaptureService {public Task<byte[]?> CaptureAsync(DesktopCaptureMode mode,CancellationToken cancellationToken=default)=>throw new Exception("OS 캡처 금지");}
    [AvaloniaFact] public async Task ClipboardTransferPublishesStandardBitmapAndOwnSizeWithoutUsingSystemClipboard()
    {
        var png=EditorTests.Png();using var data=AvaloniaEditorClipboard.CreateData(new(png,new(17.5,12.25)));
        Assert.Contains(DataFormat.Bitmap,data.Formats);Assert.Contains(AvaloniaEditorClipboard.SizeFormat,data.Formats);
        var content=await AvaloniaEditorClipboard.ReadDataAsync(data);Assert.Equal(png,content!.Png);Assert.Equal(new ImageObjectSize(17.5,12.25),content.ObjectSize);
        using var external=new DataTransfer();using var bitmap=new Bitmap(new MemoryStream(png));external.Add(DataTransferItem.Create(DataFormat.Bitmap,bitmap));
        var fromExternal=await AvaloniaEditorClipboard.ReadDataAsync(external);Assert.NotNull(fromExternal);Assert.Null(fromExternal.ObjectSize);
    }
    [AvaloniaFact] public async Task PasteButtonAndCommandVInsertIntoCurrentImageWhileTextInputKeepsCommandV()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-mac-clipboard-ui-"+Guid.NewGuid());using var editor=new DesktopEditor();var clipboard=new ClipboardTests.MemoryClipboard{Content=new(EditorTests.Png(20,10))};var window=new MainWindow(editor,new NoCapture(),path,clipboard);
        try
        {
            editor.Open(EditorTests.Record());window.Show();Avalonia.Threading.Dispatcher.UIThread.RunJobs();await window.RunAsync(window.PasteImageAsync);Assert.Single(editor.Document!.Annotations);var document=editor.Document;
            var text=window.FindControl<TextBox>("TextEntry")!;var key=new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.V,KeyModifiers=KeyModifiers.Meta};text.RaiseEvent(key);Assert.False(key.Handled);Assert.Single(document.Annotations);
            var surface=(EditorSurface)window.FindControl<Panel>("SurfaceHost")!.Children[0];key=new(){RoutedEvent=InputElement.KeyDownEvent,Key=Key.V,KeyModifiers=KeyModifiers.Meta};surface.RaiseEvent(key);
            var until=DateTime.UtcNow.AddSeconds(5);while(window.IsBusy){Assert.True(DateTime.UtcNow<until);await Task.Delay(1,TestContext.Current.CancellationToken);}Assert.True(key.Handled);Assert.Equal(2,document.Annotations.Count);Assert.Same(document,editor.Document);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact] public async Task ThemeChangePreservesHotkeysAndClosingDisposesRegistrations()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-mac-hotkey-ui-"+Guid.NewGuid());var backend=new GlobalHotkeyTests.Backend();var service=new GlobalHotkeyService(backend);using var editor=new DesktopEditor();var window=new MainWindow(editor,new NoCapture(),path,null,service);
        try
        {
            var proposed=new HotkeySettings{Region="Ctrl+Alt+1",Window="Ctrl+Alt+2",Display="Ctrl+Alt+3"};await window.ApplyHotkeySettingsAsync(proposed);
            window.FindControl<ComboBox>("ThemeBox")!.SelectedIndex=1;var stored=DesktopPreferences.Load(path);Assert.True(stored.Dark);Assert.Equal(proposed,stored.Hotkeys);
            var menu=DesktopMenu.Create(window);var capture=Assert.IsType<NativeMenuItem>(menu.Items[2]);Assert.True(capture.Command!.CanExecute(null));window.SetBusy(true);Assert.False(capture.Command.CanExecute(null));window.SetBusy(false);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
        Assert.True(backend.Disposed);Assert.Empty(backend.Active);
    }
    [AvaloniaFact] public async Task SettingsWriteFailureRollsBackHotkeysAndExitFailureKeepsEditorOpen()
    {
        var path=Path.GetTempFileName();var backend=new GlobalHotkeyTests.Backend();var service=new GlobalHotkeyService(backend);service.Apply(new());var original=backend.Active.OrderBy(x=>x.Key).ToArray();
        using var editor=new DesktopEditor();var window=new MainWindow(editor,new NoCapture(),path,null,service);
        try
        {
            await Assert.ThrowsAnyAsync<IOException>(()=>window.ApplyHotkeySettingsAsync(new(){Region="Ctrl+Alt+1",Window="Ctrl+Alt+2",Display="Ctrl+Alt+3"}));Assert.Equal(original,backend.Active.OrderBy(x=>x.Key));
            editor.Open(EditorTests.Record());Assert.False(await window.RequestExitAsync());Assert.False(window.CanExit);Assert.False(window.IsBusy);Assert.NotNull(editor.Document);Assert.Contains("실패",window.FindControl<TextBlock>("StatusText")!.Text);
        }
        finally{window.Dispose();window.Close();File.Delete(path);}
    }
    [AvaloniaFact] public async Task SettingsValidationAndSaveFailuresStayVisibleWithoutChangingRegistration()
    {
        var calls=0;var settings=new HotkeySettingsWindow(new(),_=>{calls++;throw new IOException("설정 쓰기 실패");});
        try
        {
            Assert.False(await settings.TrySaveAsync());Assert.Equal(1,calls);Assert.Contains("설정 쓰기 실패",settings.FindControl<TextBlock>("HotkeyError")!.Text);
            settings.FindControl<TextBox>("WindowHotkey")!.Text="Ctrl+Shift+1";Assert.False(await settings.TrySaveAsync());Assert.Equal(1,calls);Assert.True(settings.FindControl<Button>("SaveHotkeys")!.IsEnabled);
        }
        finally{settings.Close();}
    }
}
