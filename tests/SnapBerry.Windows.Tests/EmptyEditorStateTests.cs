using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 문서가 없는 상태의 안내와 도구 비활성화 및 이미지 열기 후 복구를 검증한다.
public class EmptyEditorStateTests
{
    [Fact] public void DrawingToolsRequireAnOpenImageAndRecoverAfterBusyState()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Show();
            var tools=(ItemsControl)w.FindName("ToolsPanel");var properties=(UIElement)w.FindName("PropertiesPanel");
            var buttons=tools.Items.OfType<System.Windows.Controls.Primitives.ToggleButton>().ToArray();Assert.Equal(8,buttons.Length);
            void Check(bool enabled){Assert.Equal(enabled,tools.IsEnabled);Assert.Equal(enabled,properties.IsEnabled);Assert.All(buttons,b=>Assert.Equal(enabled,b.IsEnabled));}
            Check(false);w.SetBusy(true);w.SetBusy(false);Check(false);
            var sticker=buttons.Single(b=>(string)b.Tag=="Sticker");
            sticker.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(EditorTool.Select,w.Model.Tool);Assert.Empty(w.OwnedWindows.Cast<Window>());
            foreach(var name in new[]{"RegionCaptureButton","WindowCaptureButton","DesktopCaptureButton","RibbonSettings","RibbonAbout"})Assert.True(((UIElement)w.FindName(name)).IsEnabled);
            w.Model.Open(new(){Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))});Check(true);
            w.SetBusy(true);Check(false);w.SetBusy(false);Check(true);
            sticker.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Single(w.OwnedWindows.Cast<Window>());
            w.Model.Clear();Check(false);Assert.Empty(w.OwnedWindows.Cast<Window>());
            w.Model.Open(new(){Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))});Check(true);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void EmptyRegionButtonStartsCaptureAndIsDisabledWhileBusy()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            var button=Assert.IsType<Button>(w.FindName("EmptyRegionCaptureButton"));var requests=new List<SnapBerry.Core.CaptureMode>();w.CaptureRequested=mode=>{requests.Add(mode);return Task.CompletedTask;};
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Equal(new[]{SnapBerry.Core.CaptureMode.Region},requests);
            w.SetBusy(true);Assert.False(button.IsEnabled);button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Single(requests);w.SetBusy(false);Assert.True(button.IsEnabled);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void EmptyEditorGuidesCaptureAndHistoryAndHidesWhenImageOpens()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Show();w.UpdateLayout();var state=Assert.IsType<Grid>(w.FindName("EmptyEditorState"));Assert.Equal(Visibility.Visible,state.Visibility);
            Assert.Contains("상단",((TextBlock)w.FindName("EmptyCaptureHint")).Text);Assert.Contains("왼쪽 캡처 이력",((TextBlock)w.FindName("EmptyHistoryHint")).Text);Save(w,"editor-empty-desktop.png");
            w.Width=760;w.Height=560;w.UpdateLayout();var hint=(TextBlock)w.FindName("EmptyHistoryHint");Assert.Equal(TextWrapping.Wrap,hint.TextWrapping);Assert.True(hint.ActualWidth<=state.ActualWidth);Save(w,"editor-empty-compact.png");
            w.Model.Open(new(){Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))});Assert.Equal(Visibility.Collapsed,state.Visibility);w.Model.Clear();Assert.Equal(Visibility.Visible,state.Visibility);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    private static void Save(Window w,string name)
    {var path=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");if(string.IsNullOrEmpty(path))return;Directory.CreateDirectory(path);var bitmap=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(w);File.WriteAllBytes(Path.Combine(path,name),PngFileWriter.Encode(bitmap));}
}
