using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창과 팝업 없이 기본 리본 템플릿의 실제 출력과 접힘·테마 전환을 검사한다.
public class RibbonAccentChromeTests
{
    [Theory]
    [InlineData(AppTheme.Light,"ColorMenuButton")] [InlineData(AppTheme.Dark,"ColorMenuButton")]
    [InlineData(AppTheme.Light,"ThicknessMenuButton")] [InlineData(AppTheme.Dark,"ThicknessMenuButton")]
    public void PropertyMenuUsesPurpleWhilePressedAndRestoresNormalChrome(AppTheme theme,string name)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-property-press-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            window.Model.Open(new(){Width=80,Height=60,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(80,60))});window.Model.Tool=EditorTool.Arrow;
            typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
            ((Ribbon)window.FindName("MainRibbon")).SelectedIndex=0;
            var root=(FrameworkElement)window.Content;window.Content=null;root.Resources=window.Resources;
            var menu=(RibbonMenuButton)window.FindName(name);
            var pressedKey=(DependencyPropertyKey)typeof(ButtonBase).GetField("IsPressedPropertyKey",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            var mouseKey=(DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            void Check()
            {
                Layout(root,1240,820);var toggle=Assert.IsType<RibbonToggleButton>(menu.Template.FindName("PART_ToggleButton",menu));
                toggle.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                var chrome=Assert.IsType<Border>(toggle.Template.FindName("OuterBorder",toggle));
                foreach(var hover in new[]{false,true})
                {
                    toggle.SetValue(mouseKey,hover);toggle.SetValue(pressedKey,true);Layout(root,1240,820);
                    Assert.Equal(ThemeResources.Get(window,"Selected").Color,Assert.IsType<SolidColorBrush>(chrome.Background).Color);
                    Assert.Equal(ThemeResources.Get(window,"Accent").Color,Assert.IsType<SolidColorBrush>(chrome.BorderBrush).Color);
                    toggle.SetValue(pressedKey,false);Layout(root,1240,820);
                    Assert.Equal(ThemeResources.Get(window,hover?"Hover":"Surface").Color,Assert.IsType<SolidColorBrush>(chrome.Background).Color);
                    Assert.False(menu.IsDropDownOpen);
                }
                toggle.SetValue(mouseKey,false);
            }
            ThemeResources.Apply(window,theme);Check();ThemeResources.Apply(window,theme==AppTheme.Light?AppTheme.Dark:AppTheme.Light);Check();ThemeResources.Apply(window,theme);Check();
            Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void SelectedToolHasNoOrangeChromeOnInitialLoadOrThemeSwitch(AppTheme theme)=>Sta.Run(()=>
    {
        var window=new Window();window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(window,theme);var button=new RibbonToggleButton{Label="선택",IsChecked=true};window.Content=button;
        try
        {
            void Check()
            {
                Layout(button,60,80);
                // 실제 Loaded 통지만 재현한다. 창 표시나 입력 장치 활성화는 하지 않는다.
                button.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));Layout(button,60,80);
                var image=new RenderTargetBitmap(60,80,96,96,PixelFormats.Pbgra32);image.Render(button);var pixels=new byte[60*80*4];image.CopyPixels(pixels,60*4,0);
                Assert.DoesNotContain(Enumerable.Range(0,60*80),i=>pixels[i*4+2]>pixels[i*4+1]+15&&pixels[i*4+1]>pixels[i*4]+15);
                Assert.True(button.IsChecked);Assert.Equal(ThemeResources.Get(window,"Selected").Color,Assert.IsType<SolidColorBrush>(button.CheckedBackground).Color);
            }
            Check();ThemeResources.Apply(window,theme==AppTheme.Light?AppTheme.Dark:AppTheme.Light);Check();ThemeResources.Apply(window,theme);Check();
            Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });

    [Theory] [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void CollapsedGroupsKeepPurpleChromeAcrossResizeAndThemeChanges(AppTheme theme)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-ribbon-accent-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            window.Model.Open(new(){Width=80,Height=60,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(80,60))});window.Model.Tool=EditorTool.Arrow;
            typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
            var ribbon=(Ribbon)window.FindName("MainRibbon");ribbon.SelectedIndex=0;var root=(FrameworkElement)window.Content;window.Content=null;root.Resources=window.Resources;
            void Check()
            {
                Layout(root,1240,820);Layout(root,760,820);
                var groups=((RibbonTab)ribbon.Items[0]).Items.OfType<RibbonGroup>().Where(x=>x.IsCollapsed&&x.Visibility==Visibility.Visible).ToArray();Assert.NotEmpty(groups);
                foreach(var group in groups)
                {
                    var toggle=Assert.IsType<RibbonToggleButton>(group.Template.FindName("PART_ToggleButton",group));
                    Assert.Equal(ThemeResources.Get(window,"Hover").Color,Assert.IsType<SolidColorBrush>(toggle.MouseOverBackground).Color);
                    Assert.Equal(ThemeResources.Get(window,"Selected").Color,Assert.IsType<SolidColorBrush>(toggle.CheckedBackground).Color);
                    var frame=Assert.IsType<Border>(toggle.Template.FindName("ImageMiddleBorder",toggle));
                    Assert.Equal(ThemeResources.Get(window,"Accent").Color,Assert.IsType<SolidColorBrush>(frame.BorderBrush).Color);
                    Assert.Equal(ThemeResources.Get(window,"Surface").Color,Assert.IsType<SolidColorBrush>(frame.Background).Color);
                }
            }
            ThemeResources.Apply(window,theme);Check();ThemeResources.Apply(window,theme==AppTheme.Light?AppTheme.Dark:AppTheme.Light);Check();ThemeResources.Apply(window,theme);Check();
            Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    private static void Layout(FrameworkElement root,int width,int height)
    {
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var frame=new DispatcherFrame();root.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);root.UpdateLayout();
    }
}
