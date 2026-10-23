using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 설정 창의 입력란·체크박스가 공통 보라색 테마를 사용하는지 검증한다.
public class SettingsAppearanceTests
{
    [Fact] public void SettingsInputsAndCheckBoxUsePurpleFocusAndSelection()=>Sta.Run(()=>
    {
        var w=new SettingsWindow(new());
        try
        {
            w.Show();w.Activate();w.UpdateLayout();var panel=(StackPanel)((Border)w.Content).Child;
            foreach(var input in new[]{"RegionHotkey","WindowHotkey","DesktopHotkey"}.Select(name=>(TextBox)w.FindName(name)))
            {
                Keyboard.Focus(input);w.UpdateLayout();var chrome=Assert.IsType<Border>(input.Template.FindName("TextInputChrome",input));Assert.Equal("#FF923C83",chrome.BorderBrush.ToString());Assert.Equal("#FFF5EAF3",chrome.Background.ToString());
            }
            var check=panel.Children.OfType<CheckBox>().Single();Keyboard.Focus(check);check.IsChecked=true;w.UpdateLayout();var box=Assert.IsType<Border>(check.Template.FindName("CheckChrome",check));Assert.Equal("#FF923C83",box.BorderBrush.ToString());Assert.Equal("#FF923C83",box.Background.ToString());Assert.Equal(Visibility.Visible,((TextBlock)check.Template.FindName("CheckMark",check)).Visibility);
            check.IsChecked=false;w.UpdateLayout();Assert.Equal(Visibility.Collapsed,((TextBlock)check.Template.FindName("CheckMark",check)).Visibility);
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");if(!string.IsNullOrEmpty(folder)){System.IO.Directory.CreateDirectory(folder);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(w);System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"settings-hotkey-input.png"),SnapBerry.App.Export.PngFileWriter.Encode(bitmap));}
        }
        finally{w.Close();}
    });
    [Fact] public void StyledSettingsStillSaveHotkeysAndStartupChoice()=>Sta.Run(()=>
    {
        var original=new AppSettings{HistoryPanelWidth=310};var w=new SettingsWindow(original);
        try
        {
            w.Loaded+=(_,_)=>{var panel=(StackPanel)((Border)w.Content).Child;((TextBox)w.FindName("RegionHotkey")).Text="Ctrl+Alt+4";panel.Children.OfType<CheckBox>().Single().IsChecked=true;((Button)w.FindName("SaveSettingsButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));};
            Assert.True(w.ShowDialog());Assert.Equal(original with{RegionKey="Ctrl+Alt+4",StartWithWindows=true},w.Result);
        }
        finally{w.Close();}
    });
}
