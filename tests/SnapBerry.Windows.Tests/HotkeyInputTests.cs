using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 실제 키 입력의 문자열 정규화와 설정 창의 중복·기본값 복원 동작을 검증한다.
public class HotkeyInputTests
{
    [Theory] [InlineData(ModifierKeys.None,"PrintScreen")] [InlineData(ModifierKeys.Control,"Ctrl+PrintScreen")]
    public void PrintScreenCanBeRecordedWithOrWithoutModifiers(ModifierKeys modifiers,string expected)
    {Assert.Equal(expected,HotkeyInput.FormatGesture(Key.Snapshot,modifiers));Assert.Equal(0x2Cu,HotkeyService.Parse(expected).Item2);}
    [Fact] public void EscapeRestoresInputAndIncompleteKeysCannotBeSaved()=>Sta.Run(()=>
    {
        var w=new SettingsWindow(new());try
        {
            w.Show();w.Activate();var input=(HotkeyInput)w.FindName("RegionHotkey");Keyboard.Focus(input);input.SetBindingText("Ctrl+Alt+7");
            input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),0,Key.A){RoutedEvent=Keyboard.PreviewKeyDownEvent});Assert.NotNull(input.CaptureError);Assert.False(((Button)w.FindName("SaveSettingsButton")).IsEnabled);
            input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});Assert.Equal("Ctrl+Shift+1",input.Text);Assert.Null(input.CaptureError);Assert.True(w.IsVisible);Assert.True(((Button)w.FindName("SaveSettingsButton")).IsEnabled);
        }
        finally{w.Close();}
    });
    [Theory] [InlineData(Key.A,ModifierKeys.Control|ModifierKeys.Shift,"Ctrl+Shift+A")] [InlineData(Key.D4,ModifierKeys.Control|ModifierKeys.Alt,"Ctrl+Alt+4")] [InlineData(Key.F5,ModifierKeys.Windows,"Win+F5")]
    public void GestureFormatsWithoutTyping(Key key,ModifierKeys modifiers,string expected)=>Assert.Equal(expected,HotkeyInput.FormatGesture(key,modifiers));
    [Theory] [InlineData(Key.A,ModifierKeys.None)] [InlineData(Key.LeftCtrl,ModifierKeys.Control)]
    public void IncompleteGesturesAreRejected(Key key,ModifierKeys modifiers)=>Assert.Throws<ArgumentException>(()=>HotkeyInput.FormatGesture(key,modifiers));
    [Fact] public void SettingsDetectDuplicatesResetDefaultsAndAvailabilityErrors()=>Sta.Run(()=>
    {
        var w=new SettingsWindow(new(){RegionKey="Ctrl+Alt+4"},key=>key=="Ctrl+Alt+9"?"다른 앱에서 사용 중":null);
        try
        {
            var region=(TextBox)w.FindName("RegionHotkey");var window=(TextBox)w.FindName("WindowHotkey");var save=(Button)w.FindName("SaveSettingsButton");Assert.True(region.IsReadOnly);
            region.Text=window.Text;Assert.False(save.IsEnabled);Assert.Contains("중복",((TextBlock)w.FindName("RegionHotkeyError")).Text);
            ((Button)w.FindName("RegionHotkeyReset")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Equal("Ctrl+Shift+1",region.Text);Assert.True(save.IsEnabled);
            region.Text="Ctrl+Alt+9";Assert.False(save.IsEnabled);Assert.Equal("다른 앱에서 사용 중",((TextBlock)w.FindName("RegionHotkeyError")).Text);Assert.Null(w.Result);
        }
        finally{w.Close();}
    });
}
