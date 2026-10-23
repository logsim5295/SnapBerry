using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SnapBerry.App.Stickers;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 스티커 선택 강조 색상과 기본 창 크기를 검증한다.
public class StickerPickerAppearanceTests
{
    [Fact] public void DefaultWidthFitsFourColumnsWithVerticalScrollbar()=>Sta.Run(()=>
    {
        var w=new StickerPicker();
        try
        {
            w.Show();w.UpdateLayout();
            var dock=(DockPanel)w.FindName("StickerContent");var scroll=(ScrollViewer)dock.Children[1];var wrap=(WrapPanel)scroll.Content;
            var buttons=wrap.Children.OfType<Button>().ToArray();
            Assert.Equal(Visibility.Visible,scroll.ComputedVerticalScrollBarVisibility);
            var first=buttons[0].TranslatePoint(new Point(),wrap);
            for(var i=1;i<4;i++)Assert.Equal(first.Y,buttons[i].TranslatePoint(new Point(),wrap).Y,1);
            Assert.True(buttons[4].TranslatePoint(new Point(),wrap).Y>first.Y);
        }
        finally{w.Close();}
    });

    [Fact] public void StickerAndCategorySelectionUsePurpleHighlights()=>Sta.Run(()=>
    {
        var w=new StickerPicker();
        try
        {
            w.Show();w.Activate();w.UpdateLayout();var dock=(DockPanel)w.FindName("StickerContent");var wrap=(WrapPanel)((ScrollViewer)dock.Children[1]).Content;var button=(Button)wrap.Children[0];Keyboard.Focus(button);w.UpdateLayout();var chrome=Assert.IsType<Border>(button.Template.FindName("Chrome",button));Assert.Equal("#FFEBD5E6",chrome.Background.ToString());Assert.Equal("#FF923C83",chrome.BorderBrush.ToString());
            var category=(ComboBox)dock.Children[0];category.IsDropDownOpen=true;w.UpdateLayout();var selected=(ComboBoxItem)category.ItemContainerGenerator.ContainerFromIndex(category.SelectedIndex);var selectedChrome=Assert.IsType<Border>(selected.Template.FindName("ComboItemChrome",selected));Assert.Equal("#FFEBD5E6",selectedChrome.Background.ToString());Assert.Equal("#FF923C83",selectedChrome.BorderBrush.ToString());category.IsDropDownOpen=false;
        }
        finally{w.Close();}
    });
    [Fact] public void SelectingDifferentStickersKeepsPaletteOpen()=>Sta.Run(()=>
    {
        var w=new StickerPicker();
        try
        {
            w.Show();var dock=(DockPanel)w.FindName("StickerContent");((ComboBox)dock.Children[0]).SelectedIndex=1;var wrap=(WrapPanel)((ScrollViewer)dock.Children[1]).Content;
            ((Button)wrap.Children[0]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));var first=w.Selected;Assert.NotNull(first);Assert.True(w.IsVisible);
            ((Button)wrap.Children[1]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Assert.NotNull(w.Selected);Assert.NotEqual(first.Id,w.Selected.Id);Assert.True(w.IsVisible);Assert.Equal("Keyboard",w.Selected.Category);
        }
        finally{w.Close();}
    });
}
