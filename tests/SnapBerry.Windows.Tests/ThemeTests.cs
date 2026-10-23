using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Stickers;
using SnapBerry.App.Shell;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창 표시·OS 설정 변경 없이 테마 저장과 실제 편집 컨트롤의 색상 전환을 검증한다.
public class ThemeTests
{
    [Fact] public void DarkTrayMenuAndThinIconsHaveReadableContrast()=>Sta.Run(()=>
    {
        var renderer=new TrayMenuRenderer(AppTheme.Dark);
        Assert.Equal("#FF22252D","#"+renderer.ColorTable.ToolStripDropDownBackground.ToArgb().ToString("X8"));
        var icon=RibbonIcons.Create("Rectangle",AppTheme.Dark);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(icon,new Rect(0,0,32,32));
        var bitmap=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[32*32*4];bitmap.CopyPixels(pixels,32*4,0);
        Assert.Contains(Enumerable.Range(0,32*32),i=>pixels[i*4+3]==255&&pixels[i*4]>200&&pixels[i*4+1]>200&&pixels[i*4+2]>200);
    });

    [Theory] [InlineData("Save")] [InlineData("Copy")]
    public void DarkIconDetailsContrastWithTheirPaperBackground(string name)=>Sta.Run(()=>
    {
        var drawing=(DrawingGroup)((DrawingImage)RibbonIcons.Create(name,AppTheme.Dark)).Drawing;
        IEnumerable<Drawing> Flatten(DrawingGroup group)
        {foreach(var item in group.Children){yield return item;if(item is DrawingGroup child)foreach(var part in Flatten(child))yield return part;}}
        var parts=Flatten(drawing).ToArray();var shapes=parts.OfType<GeometryDrawing>().ToArray();
        var paper=(SolidColorBrush)(name=="Save"?shapes.First(x=>x.Brush is SolidColorBrush{Color.A:255}).Brush:shapes.Last(x=>x.Brush is SolidColorBrush{Color.A:255}).Brush);
        var ink=(SolidColorBrush)(name=="Save"?parts.OfType<GlyphRunDrawing>().First().ForegroundBrush:shapes.First(x=>x.Geometry is LineGeometry).Pen.Brush);
        double Luminance(Color color)
        {double Channel(byte b){var value=b/255d;return value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);}return .2126*Channel(color.R)+.7152*Channel(color.G)+.0722*Channel(color.B);}
        var a=Luminance(paper.Color);var b=Luminance(ink.Color);Assert.True((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)>=4.5);
    });

    [Theory] [InlineData("{}",AppTheme.Light)] [InlineData("{\"Theme\":1}",AppTheme.Dark)] [InlineData("{\"Theme\":99,\"RegionKey\":\"Ctrl+Alt+7\"}",AppTheme.Light)]
    public void OldSettingsAndUnknownThemesKeepCompatibleDefaults(string json,AppTheme expected)
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-theme-settings-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        try
        {
            File.WriteAllText(Path.Combine(path,"settings.json"),json);var service=new SettingsService(path);var settings=service.Load();
            Assert.Equal(expected,settings.Theme);service.Save(settings);Assert.Equal(settings,service.Load());
            if(json.Contains("Ctrl+Alt+7"))Assert.Equal("Ctrl+Alt+7",settings.RegionKey);
        }
        finally{TestFiles.DeleteDirectory(path);}
    }
    private static void Render(Window window,string name,Size size)
    {
        var root=(FrameworkElement)window.Content;window.Content=null;root.Resources=window.Resources;
        root.SetValue(TextElement.ForegroundProperty,window.Foreground);root.SetValue(TextElement.FontFamilyProperty,window.FontFamily);root.SetValue(TextElement.FontSizeProperty,window.FontSize);
        root.Measure(size);root.Arrange(new Rect(new Size(size.Width,double.IsInfinity(size.Height)?root.DesiredSize.Height:size.Height)));root.UpdateLayout();
        var frame=new DispatcherFrame();window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);root.UpdateLayout();
        var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
        if(folder is not null)
        {
            Directory.CreateDirectory(folder);var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render(root);File.WriteAllBytes(Path.Combine(folder,name+".png"),PngFileWriter.Encode(bitmap));
        }
        window.Content=root;Assert.False(window.IsVisible);
    }

    [Fact] public void SwitchingBackToWhiteRestoresTheOriginalRibbonChrome()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-theme-toggle-"+Guid.NewGuid().ToString("N"));var editor=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            var ribbon=(Ribbon)editor.FindName("MainRibbon");ribbon.SelectedIndex=0;Render(editor,"theme-toggle-before",new Size(1240,820));
            IEnumerable<DependencyObject> Descendants(DependencyObject root)
            {for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var node in Descendants(child))yield return node;}}
            var border=Descendants(ribbon).OfType<Border>().First(x=>x.TemplatedParent==ribbon&&x.BorderBrush is SolidColorBrush{Color:{A:96,R:255,G:255,B:255}});
            ThemeResources.Apply(editor,AppTheme.Dark);Render(editor,"theme-toggle-dark",new Size(1240,820));Assert.Equal("#00FFFFFF",border.BorderBrush.ToString());
            ThemeResources.Apply(editor,AppTheme.Light);Render(editor,"theme-toggle-after",new Size(1240,820));Assert.Equal("#60FFFFFF",border.BorderBrush.ToString());
        }
        finally{editor.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void ThemeRendersEditorAndDialogControlsWithoutShowingWindows(AppTheme theme)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-theme-render-"+Guid.NewGuid().ToString("N"));var service=new SettingsService(path);service.Save(new(){Theme=theme});
        var editor=new EditorWindow(new(Path.Combine(path,"Library")),service);
        try
        {
            ((Ribbon)editor.FindName("MainRibbon")).SelectedIndex=0;
            editor.Model.Open(new(){Width=320,Height=180,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(320,180))});
            Render(editor,"theme-editor-"+theme,new Size(1240,820));
            if(theme==AppTheme.Dark)
            {
                var ribbon=(Ribbon)editor.FindName("MainRibbon");
                Assert.Equal("#FF252832",((Border)ribbon.Template.FindName("groupsBorder",ribbon)).Background.ToString());
            }
            foreach(var dialog in new Window[]{new SettingsWindow(new(){Theme=theme}),new AboutWindow(),new StickerPicker(),new TextAnnotationEditor("텍스트 내용"),new ImageResizeDialog(new(320,180)),new DeleteCaptureDialog(1),new ColorPickerWindow(Colors.White),new TrayNoticeDialog()})
            {
                try
                {
                    ThemeResources.Apply(dialog,theme);Assert.Equal(theme==AppTheme.Dark?"#FF22252D":"#FFFFFFFF",((Border)dialog.Content).Background.ToString());
                    Render(dialog,"theme-"+dialog.GetType().Name+"-"+theme,new Size(dialog.Width,double.IsNaN(dialog.Height)?double.PositiveInfinity:dialog.Height));
                }
                finally{dialog.Close();}
            }
        }
        finally{editor.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void SettingsOfferWhiteAndDarkModesAndPreviewOnlyTheDialog()=>Sta.Run(()=>
    {
        var dialog=new SettingsWindow(new());
        try
        {
            var selector=Assert.IsType<ComboBox>(dialog.FindName("ThemeSelector"));
            Assert.Equal(new[]{"화이트 모드","다크 모드"},selector.Items.Cast<ComboBoxItem>().Select(x=>x.Content));
            Assert.Equal(0,selector.SelectedIndex);selector.SelectedIndex=1;
            Assert.Equal("#FF22252D",((Border)dialog.Content).Background.ToString());
            Assert.Equal("#FFE9EDF3",((TextBox)dialog.FindName("RegionHotkey")).Foreground.ToString());
            Assert.Null(dialog.Result);selector.SelectedIndex=0;
            Assert.Equal("#FFFFFFFF",((Border)dialog.Content).Background.ToString());Assert.False(dialog.IsVisible);
        }
        finally{dialog.Close();}
    });

    [Fact] public void SavedDarkModeLoadsAndSwitchesBackWithoutChangingImageOrUndoHistory()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-theme-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path,"settings.json"),"{\"SchemaVersion\":1,\"Theme\":1}");
        var settings=new SettingsService(path);var editor=new EditorWindow(new(Path.Combine(path,"Library")),settings);
        try
        {
            editor.Model.Open(new(){Width=10,Height=10,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(10,10))});
            var document=editor.Model.Document;var token=editor.Model.History!.CurrentStateToken;var image=PngFileWriter.Encode(editor.Model.Composite()!);
            Assert.Equal("#FF181A20",editor.Background.ToString());
            Assert.Equal("#FF22252D",((Border)editor.FindName("HistoryPanel")).Background.ToString());
            Assert.Equal("#FF15171C",((ScrollViewer)editor.FindName("ImageScroll")).Background.ToString());
            Assert.Equal("#FFE9EDF3",((TextBox)editor.FindName("HistorySearchBox")).Foreground.ToString());
            File.WriteAllText(Path.Combine(path,"settings.json"),"{\"SchemaVersion\":1,\"Theme\":0}");editor.ReloadSettings();
            Assert.Equal("#FFF3F5F7",editor.Background.ToString());Assert.Equal("#FFFFFFFF",((Border)editor.FindName("HistoryPanel")).Background.ToString());
            Assert.Same(document,editor.Model.Document);Assert.Equal(token,editor.Model.History.CurrentStateToken);Assert.Equal(image,PngFileWriter.Encode(editor.Model.Composite()!));Assert.False(editor.IsVisible);
        }
        finally{editor.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void ThemeChoiceIsIncludedWhenSettingsAreSaved()=>Sta.Run(()=>
    {
        var dialog=new SettingsWindow(new(){RegionKey="Ctrl+Alt+7",HistoryPanelWidth=310});
        try
        {
            var selector=Assert.IsType<ComboBox>(dialog.FindName("ThemeSelector"));selector.SelectedIndex=1;
            // DialogResult 설정만 표시하지 않은 창에서 거부된다. 그 전에 작성한 저장 결과를 확인한다.
            Assert.Throws<InvalidOperationException>(()=>((Button)dialog.FindName("SaveSettingsButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
            Assert.NotNull(dialog.Result);var json=System.Text.Json.JsonSerializer.Serialize(dialog.Result);
            Assert.Contains("\"Theme\":1",json);Assert.Equal("Ctrl+Alt+7",dialog.Result!.RegionKey);Assert.Equal(310,dialog.Result.HistoryPanelWidth);
        }
        finally{dialog.Close();}
    });
}
