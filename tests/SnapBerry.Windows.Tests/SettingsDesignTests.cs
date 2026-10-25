using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창을 표시하지 않고 설정 창의 브랜드 외형·버튼 배치와 기존 검증 동작을 검사한다.
public class SettingsDesignTests
{
    [Fact] public void SettingsUseRoundedPurpleChromeAndPrimarySaveButton()=>Sta.Run(()=>
    {
        var w=new SettingsWindow(new());
        try
        {
            Assert.Equal(WindowStyle.None,w.WindowStyle);Assert.True(w.AllowsTransparency);
            var chrome=Assert.IsType<Border>(w.Content);Assert.Equal(8,chrome.CornerRadius.TopLeft);
            Assert.Equal("#FFFFFFFF",chrome.Background.ToString());Assert.Equal("#FFEBD5E6",chrome.BorderBrush.ToString());
            var header=Assert.IsType<StackPanel>(w.FindName("SettingsHeader"));Assert.NotNull(header.Background);
            Assert.NotNull(header.Children.OfType<Image>().Single().Source);Assert.Equal("설정",header.Children.OfType<TextBlock>().Single().Text);
            Assert.Null(w.FindName("CancelSettingsButton"));
            var close=Assert.IsType<Button>(w.FindName("CloseSettingsButton"));var save=Assert.IsType<Button>(w.FindName("SaveSettingsButton"));
            Assert.True(close.IsCancel);Assert.Equal("\uE8BB",Assert.IsType<TextBlock>(close.Content).Text);Assert.Same(header.Parent,close.Parent);Assert.Equal(1,Grid.GetColumn(close));
            Assert.Equal("#FF923C83",save.Background.ToString());Assert.Equal("#FFFFFFFF",save.Foreground.ToString());
            var buttons=Assert.IsType<StackPanel>(save.Parent);Assert.Same(save,Assert.Single(buttons.Children.Cast<UIElement>()));
            // 메모리상의 레이아웃만 계산한다. Show·Activate·키보드 포커스는 사용하지 않는다.
            chrome.Measure(new Size(w.Width,double.PositiveInfinity));chrome.Arrange(new Rect(new Size(w.Width,chrome.DesiredSize.Height)));chrome.UpdateLayout();
            Assert.True(chrome.ActualHeight>0);Assert.False(w.IsVisible);Assert.Null(w.Result);
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null)
            {
                System.IO.Directory.CreateDirectory(artifacts);
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(chrome.ActualWidth),(int)Math.Ceiling(chrome.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(chrome);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifacts,"settings-design-offscreen.png"),SnapBerry.App.Export.PngFileWriter.Encode(image));
                Assert.False(w.IsVisible);
            }
            ((HotkeyInput)w.FindName("RegionHotkey")).Text="Ctrl+Alt+7";
            var closed=false;w.Closed+=(_,_)=>closed=true;
            close.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.True(closed);Assert.Null(w.Result);
        }
        finally{w.Close();}
    });

    [Fact] public void NewChromeKeepsValidationResetAndReadOnlyHotkeyInput()=>Sta.Run(()=>
    {
        var w=new SettingsWindow(new(){RegionKey="Ctrl+Alt+7"});
        try
        {
            var region=(HotkeyInput)w.FindName("RegionHotkey");var save=(Button)w.FindName("SaveSettingsButton");
            Assert.True(region.IsReadOnly);region.Text=((HotkeyInput)w.FindName("WindowHotkey")).Text;Assert.False(save.IsEnabled);
            ((Button)w.FindName("RegionHotkeyReset")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(new AppSettings().RegionKey,region.Text);Assert.True(save.IsEnabled);Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });
}
