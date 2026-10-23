using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class RibbonTabThemeTests
{
    [Theory] [InlineData(1240,0)] [InlineData(760,0)] [InlineData(1240,1)] [InlineData(760,1)]
    public void RibbonBackgroundStaysDarkAcrossTabAndThemeSwitches(int width,int initialTab)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-view-theme-"+Guid.NewGuid().ToString("N"));var service=new SettingsService(path);service.Save(new(){Theme=AppTheme.Dark});
        var editor=new EditorWindow(new(Path.Combine(path,"Library")),service);
        try
        {
            var ribbon=(Ribbon)editor.FindName("MainRibbon");var root=(FrameworkElement)editor.Content;editor.Content=null;root.Resources=editor.Resources;
            root.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,editor.Foreground);
            void Layout(int index)
            {
                ribbon.SelectedIndex=index;root.Measure(new Size(width,820));root.Arrange(new Rect(0,0,width,820));root.UpdateLayout();
                var frame=new DispatcherFrame();editor.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);root.UpdateLayout();
                var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
                if(folder is not null)
                {
                    Directory.CreateDirectory(folder);var image=new RenderTargetBitmap(width,820,96,96,PixelFormats.Pbgra32);image.Render(root);
                    File.WriteAllBytes(Path.Combine(folder,$"ribbon-tab-{index}-{width}.png"),PngFileWriter.Encode(image));
                }
            }
            void CheckDarkTab(int index)
            {
                Layout(index);var tab=(RibbonTab)ribbon.Items[index];
                foreach(var name in new[]{"OverlayRect","InnerOverlayRect"})
                    Assert.Equal("#00FFFFFF",((System.Windows.Shapes.Rectangle)tab.Template.FindName(name,tab)).Fill.ToString());
                foreach(var group in tab.Items.OfType<RibbonGroup>())
                    if(group.Template?.FindName("PART_HotBackground",group) is Border hot)Assert.Equal("#00FFFFFF",hot.Background.ToString());
                Assert.Equal("#FF252832",((Border)ribbon.Template.FindName("groupsBorder",ribbon)).Background.ToString());
            }
            CheckDarkTab(initialTab);CheckDarkTab(0);CheckDarkTab(1);CheckDarkTab(0);CheckDarkTab(1);
            ThemeResources.Apply(editor,AppTheme.Light);Layout(1);
            var view=(RibbonTab)ribbon.Items[1];Assert.IsType<LinearGradientBrush>(((System.Windows.Shapes.Rectangle)view.Template.FindName("OverlayRect",view)).Fill);
            ThemeResources.Apply(editor,AppTheme.Dark);CheckDarkTab(1);CheckDarkTab(0);CheckDarkTab(1);Assert.False(editor.IsVisible);
        }
        finally{editor.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(AppTheme.Dark,true,"#FF51334E","#FFE9EDF3")]
    [InlineData(AppTheme.Dark,false,"#FF252832","#FFE9EDF3")]
    [InlineData(AppTheme.Light,true,"#FFEBD5E6","#FF263B55")]
    [InlineData(AppTheme.Light,false,"#FFFAFBFC","#FF263B55")]
    public void HomeTabUsesReadableColorsWhenSelectedAndAfterThemeSwitch(AppTheme theme,bool selected,string background,string foreground)=>Sta.Run(()=>
    {
        var window=new Window();window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});ThemeResources.Initialize(window,theme);
        try
        {
            var header=new RibbonTabHeader{Content="홈"};window.Content=header;
            // 리본이 전달하는 읽기 전용 선택 상태를 재현한다. 실제 키보드·마우스 입력은 사용하지 않는다.
            var selectedKey=(DependencyPropertyKey)typeof(RibbonTabHeader).GetField("IsRibbonTabSelectedPropertyKey",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            header.SetValue(selectedKey,selected);
            void Layout()
            {
                header.Measure(new Size(100,40));header.Arrange(new Rect(0,0,100,40));header.UpdateLayout();
                var frame=new DispatcherFrame();window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);header.UpdateLayout();
            }
            void Check()
            {
                Assert.Equal(background,((Border)header.Template.FindName("PART_OuterBorder",header)).Background.ToString());
                Assert.Equal(foreground,header.Foreground.ToString());
                Assert.Equal(theme==AppTheme.Dark?"#FF392C3D":"#FFF5EAF3",header.MouseOverBackground?.ToString());
                Assert.Equal(header.MouseOverBackground,header.FocusedBackground);
            }
            Layout();Check();ThemeResources.Apply(window,theme==AppTheme.Dark?AppTheme.Light:AppTheme.Dark);Layout();ThemeResources.Apply(window,theme);Layout();Check();
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(folder is not null)
            {
                Directory.CreateDirectory(folder);var bitmap=new RenderTargetBitmap(100,40,96,96,PixelFormats.Pbgra32);bitmap.Render(header);
                File.WriteAllBytes(Path.Combine(folder,$"home-tab-{theme}-{selected}.png"),PngFileWriter.Encode(bitmap));
            }
            Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });
}
