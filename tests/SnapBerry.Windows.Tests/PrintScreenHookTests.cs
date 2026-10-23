using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 단축키 설정 중 PrintScreen 입력의 후크 설치·소비 조건·해제를 검증한다.
public class PrintScreenHookTests
{
    [Theory] [InlineData(0,0x100,0x2C,true,true)] [InlineData(0,0x101,0x2C,true,true)] [InlineData(0,0x104,0x2C,true,true)] [InlineData(0,0x105,0x2C,true,true)] [InlineData(-1,0x100,0x2C,true,false)] [InlineData(0,0x100,0x2C,false,false)] [InlineData(0,0x100,0x41,true,false)]
    public void OnlyPrintScreenInActiveInputIsSuppressed(int code,int message,int key,bool active,bool expected)=>Assert.Equal(expected,HotkeyInputHook.ShouldIntercept(code,message,key,active));
    [Fact] public void ConflictingModifierGestureCanBeRecordedWithoutStealingNavigationKeys()
    {Assert.True(HotkeyInputHook.ShouldIntercept(0,0x100,0x41,true,2));Assert.False(HotkeyInputHook.ShouldIntercept(0,0x100,0x41,false,2));Assert.False(HotkeyInputHook.ShouldIntercept(0,0x100,0x09,true));Assert.False(HotkeyInputHook.ShouldIntercept(0,0x100,0x1B,true));}
    [Fact] public void HookIsReleasedOnFocusChangeAndWindowClose()=>Sta.Run(()=>
    {
        var w=new SettingsWindow(new());try
        {
            w.Show();w.Activate();w.UpdateLayout();var first=(HotkeyInput)w.FindName("RegionHotkey");var second=(HotkeyInput)w.FindName("WindowHotkey");Keyboard.Focus(first);Assert.True(first.IsPrintScreenHookActive);Keyboard.Focus(second);Assert.False(first.IsPrintScreenHookActive);Assert.True(second.IsPrintScreenHookActive);Keyboard.Focus((Button)w.FindName("SaveSettingsButton"));Assert.False(second.IsPrintScreenHookActive);Keyboard.Focus(first);w.Close();Assert.False(first.IsPrintScreenHookActive);
        }
        finally{w.Close();}
    });
}
