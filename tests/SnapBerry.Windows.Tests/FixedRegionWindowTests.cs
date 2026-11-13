using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SnapBerry.App.Capture;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class FixedRegionWindowTests
{
    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void ToolbarButtonsUseEditorHoverAndPressedColorsWithoutShowingAWindow(AppTheme theme)=>Sta.Run(()=>
    {
        var window=new FixedRegionWindow(new(80,100,640,480),"Ctrl+Shift+4");
        try
        {
            ThemeResources.Apply(window,theme);
            var canvas=Assert.IsType<Canvas>(window.Content);
            canvas.Measure(new Size(window.Width,window.Height));
            canvas.Arrange(new Rect(0,0,window.Width,window.Height));canvas.UpdateLayout();
            var buttons=((Grid)canvas.Children[0]).Children.OfType<Button>().ToArray();Assert.Equal(2,buttons.Length);
            // WPF 상태만 설정한다. 실제 마우스 이동이나 창 표시를 하지 않는다.
            var hoverKey=(DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            var pressedKey=(DependencyPropertyKey)typeof(ButtonBase).GetField("IsPressedPropertyKey",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            foreach(var button in buttons)
            {
                var chrome=Assert.IsType<Border>(button.Template.FindName("Chrome",button));
                void Check(string background,string border)
                {
                    Assert.Equal(ThemeResources.Get(window,background).Color,Assert.IsType<SolidColorBrush>(chrome.Background).Color);
                    Assert.Equal(ThemeResources.Get(window,border).Color,Assert.IsType<SolidColorBrush>(chrome.BorderBrush).Color);
                }
                Check("Surface","InputBorder");
                button.SetValue(hoverKey,true);Check("Hover","Accent");
                button.SetValue(pressedKey,true);Check("Selected","Accent");
                button.SetValue(pressedKey,false);Check("Hover","Accent");
                ThemeResources.Apply(window,theme==AppTheme.Light?AppTheme.Dark:AppTheme.Light);Check("Hover","Accent");
                button.SetValue(hoverKey,false);Check("Surface","InputBorder");
                ThemeResources.Apply(window,theme);
            }
            Assert.Equal(buttons[0].ActualWidth,buttons[1].ActualWidth);Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });

    [Fact]
    public void TitleAndEdgesHitDragHandlesWithoutShowingAWindow()=>Sta.Run(()=>
    {
        var window=new FixedRegionWindow(new(80,100,640,480),"Ctrl+Shift+4");
        try
        {
            var canvas=Assert.IsType<Canvas>(window.Content);
            window.Content=null;canvas.Resources=window.Resources;
            canvas.Measure(new Size(window.Width,window.Height));
            canvas.Arrange(new Rect(0,0,window.Width,window.Height));canvas.UpdateLayout();
            foreach(var point in new[]{new Point(150,17),new Point(2,100),new Point(650,100),new Point(100,524)})
            {
                // Visual hit-testing works without a presentation source or a visible HWND.
                DependencyObject? hit=null;
                VisualTreeHelper.HitTest(canvas,node=>node is UIElement element&&!element.IsHitTestVisible
                    ?HitTestFilterBehavior.ContinueSkipSelfAndChildren:HitTestFilterBehavior.Continue,
                    result=>{hit=result.VisualHit;return HitTestResultBehavior.Stop;},new PointHitTestParameters(point));
                var original=hit?.GetType().Name;
                while(hit is not null&&hit is not Thumb)hit=VisualTreeHelper.GetParent(hit);
                var title=((Grid)canvas.Children[0]).Children.OfType<Thumb>().Single();
                Assert.True(hit is Thumb,$"No drag handle at {point}; hit={original}; title={title.RenderSize}; canvas={canvas.RenderSize}");
            }
            if(Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS") is {} artifacts)
            {
                System.IO.Directory.CreateDirectory(artifacts);
                // 175% 배율의 조작 막대만 메모리에서 렌더링한다. 데스크톱을 촬영하지 않는다.
                var preview=new System.Windows.Media.Imaging.RenderTargetBitmap(525,60,168,168,PixelFormats.Pbgra32);
                preview.Render(canvas);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifacts,"fixed-region-toolbar-175.png"),SnapBerry.App.Export.PngFileWriter.Encode(preview));
            }
            Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });
}
