using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Views;
using Xunit;

[assembly:AvaloniaTestApplication(typeof(SnapBerry.Desktop.Tests.TestAppBuilder))]
[assembly:CollectionBehavior(DisableTestParallelization=true)]
namespace SnapBerry.Desktop.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UseSkia().UseHeadless(new(){UseHeadlessDrawing=false});
}

public class ViewTests
{
    [AvaloniaFact]
    public void SelectingAnOlderAnnotationDoesNotReplaceItsStyleOnUnchangedFocusLoss()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-desktop-style-"+Guid.NewGuid());using var editor=new DesktopEditor();var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            editor.Open(EditorTests.Record());editor.Commit(new(){Kind=SnapBerry.Core.AnnotationKind.Rectangle,Style=new(){Color="#00FF00",Thickness=20,FontSize=40}});var first=editor.Selected!;
            editor.Commit(new(){Kind=SnapBerry.Core.AnnotationKind.Rectangle,Style=new(){Color="#FF0000",Thickness=3}});editor.SelectedId=first.Id;
            var color=window.FindControl<TextBox>("ColorBox")!;
            color.RaiseEvent(new Avalonia.Input.FocusChangedEventArgs(Avalonia.Input.InputElement.LostFocusEvent));
            Assert.Equal(first.Style,editor.Selected!.Style);
            window.RefreshSelection();Assert.Equal("#00FF00",color.Text);Assert.Equal(20,window.FindControl<NumericUpDown>("ThicknessBox")!.Value);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact]
    public void TextInputHasTheSameLimitAsTheSavedDocument()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-desktop-text-"+Guid.NewGuid());using var editor=new DesktopEditor();var window=new MainWindow(editor,new NoCapture(),path);
        try{Assert.Equal(SnapBerry.Core.AnnotationLimits.MaxTextLength,window.FindControl<TextBox>("TextEntry")!.MaxLength);}
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact]
    public void InvalidPointerTextReportsFailureWithoutMutatingTheDocument()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.Tool=DesktopTool.Text;using var surface=new EditorSurface(editor);
        surface.Gesture.Text=new string('a',8193);Exception? reported=null;surface.EditFailed+=error=>reported=error;
        Assert.False(surface.TryBeginGesture(new(4,4)));Assert.IsType<ArgumentException>(reported);Assert.Empty(editor.Document!.Annotations);Assert.False(surface.Gesture.Active);
    }
    private sealed class NoCapture : ICaptureService {public Task<byte[]?> CaptureAsync(DesktopCaptureMode mode,CancellationToken cancellationToken=default)=>throw new Exception("실제 캡처 금지");}
    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public async Task EditorLayoutRendersOffscreenAndKeepsThemedHistoryWhenBusy(bool dark)
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-desktop-view-"+Guid.NewGuid());using var editor=new DesktopEditor();
        var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            window.ApplyTheme(dark);editor.Open(EditorTests.Record());
            // UseHeadless는 네이티브 창 대신 메모리 창을 사용한다. 실제 창·포커스는 생성하지 않는다.
            window.Show();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var root=(Control)window.Content!;
            Assert.Equal(dark?ThemeVariant.Dark:ThemeVariant.Light,window.ActualThemeVariant);
            Assert.True(window.FindControl<Button>("OpenButton")!.IsEnabled);Assert.True(window.FindControl<Button>("SaveButton")!.IsEnabled);
            window.SetBusy(true);root.UpdateLayout();Assert.False(window.FindControl<Button>("OpenButton")!.IsEffectivelyEnabled);
            Assert.Equal(dark?ThemeVariant.Dark:ThemeVariant.Light,window.FindControl<ListBox>("HistoryList")!.ActualThemeVariant);
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            using var rendered=window.CaptureRenderedFrame()!;
            if(folder is not null){Directory.CreateDirectory(folder);rendered.Save(Path.Combine(folder,$"desktop-{dark}-busy.png"),PngBitmapEncoderOptions.Default);}
            window.SetBusy(false);root.UpdateLayout();Assert.True(window.FindControl<Button>("OpenButton")!.IsEffectivelyEnabled);
            Assert.True(window.IsVisible); // 메모리 창의 레이아웃 등록 상태다.
        }
        finally{window.SetBusy(false);await window.RequestExitAsync();window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact]
    public async Task FailedCommandsRestoreEnabledStateAndShowTheError()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-desktop-command-"+Guid.NewGuid());using var editor=new DesktopEditor();var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            await window.RunAsync(()=>throw new IOException("원인 확인"));Assert.False(window.IsBusy);
            Assert.True(window.FindControl<Button>("OpenButton")!.IsEnabled);Assert.Contains("원인 확인",window.FindControl<TextBlock>("StatusText")!.Text);Assert.False(window.IsVisible);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
}
