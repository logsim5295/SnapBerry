using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 이력 패널의 분할 조절, 접기·펼치기와 재시작 후 너비 복원을 검증한다.
public class HistorySplitterTests
{
    [Fact] public void HistoryDividerResizesAndRestoresPreviousWidthAfterHiding()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Show();w.UpdateLayout();var splitter=Assert.IsType<GridSplitter>(w.FindName("HistorySplitter"));var column=(ColumnDefinition)w.FindName("LibraryColumn");var before=column.ActualWidth;
            Assert.Equal(GridResizeDirection.Columns,splitter.ResizeDirection);Assert.Equal(GridResizeBehavior.PreviousAndNext,splitter.ResizeBehavior);
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(splitter),0,Key.Right){RoutedEvent=Keyboard.KeyDownEvent});w.UpdateLayout();Assert.True(column.ActualWidth>before);var resized=column.ActualWidth;
            var toggle=(RibbonButton)w.FindName("RibbonHistory");toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));w.UpdateLayout();Assert.Equal(0,column.ActualWidth);Assert.Equal(Visibility.Collapsed,splitter.Visibility);
            toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));w.UpdateLayout();Assert.Equal(resized,column.ActualWidth,3);Assert.Equal(Visibility.Visible,splitter.Visibility);
            w.CloseForExit();Assert.Equal(resized,new SettingsService(path).Load().HistoryPanelWidth,3);
            w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));Assert.Equal(resized,((ColumnDefinition)w.FindName("LibraryColumn")).Width.Value,3);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
