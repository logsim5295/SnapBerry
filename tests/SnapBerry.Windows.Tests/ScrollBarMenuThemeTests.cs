using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 메뉴 생성 이벤트만 전달하며 실제 팝업·창·마우스·키보드를 사용하지 않는다.
public class ScrollBarMenuThemeTests
{
    [Theory]
    [InlineData(AppTheme.Light,Orientation.Vertical,FlowDirection.LeftToRight)]
    [InlineData(AppTheme.Dark,Orientation.Vertical,FlowDirection.LeftToRight)]
    [InlineData(AppTheme.Light,Orientation.Horizontal,FlowDirection.LeftToRight)]
    [InlineData(AppTheme.Dark,Orientation.Horizontal,FlowDirection.LeftToRight)]
    [InlineData(AppTheme.Light,Orientation.Horizontal,FlowDirection.RightToLeft)]
    [InlineData(AppTheme.Dark,Orientation.Horizontal,FlowDirection.RightToLeft)]
    public void NativeMenuKeepsScrollCommandsAndUsesPurpleHighlight(AppTheme theme,Orientation orientation,FlowDirection flow)=>Sta.Run(()=>
    {
        var window=new Window();
        window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(window,theme);
        var bar=new ScrollBar{Orientation=orientation,FlowDirection=flow,Minimum=0,Maximum=100,Value=50,SmallChange=3,LargeChange=10};window.Content=bar;
        try
        {
            bar.Measure(new Size(200,200));bar.Arrange(new Rect(0,0,200,200));bar.UpdateLayout();
            // ContextMenuEventArgs 생성자는 WPF 내부 API다. 서비스의 팝업 열기 경로 없이 이벤트만 재현한다.
            var args=(ContextMenuEventArgs)Activator.CreateInstance(typeof(ContextMenuEventArgs),BindingFlags.Instance|BindingFlags.NonPublic,null,[bar,true],null)!;
            bar.RaiseEvent(args);
            var menu=Assert.IsType<ContextMenu>(bar.ContextMenu);menu.PlacementTarget=bar;
            Assert.Same(window.FindResource("PurpleHistoryContextMenuStyle"),menu.Style);
            menu.Measure(new Size(260,double.PositiveInfinity));menu.Arrange(new Rect(new Size(260,menu.DesiredSize.Height)));menu.UpdateLayout();
            var frame=new DispatcherFrame();menu.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);
            var items=menu.Items.OfType<MenuItem>().ToArray();Assert.Equal(7,items.Length);
            Assert.Contains(items,item=>item.Command==ScrollBar.ScrollHereCommand);
            var move=items.Single(item=>item.Command==(orientation==Orientation.Vertical?ScrollBar.LineDownCommand:ScrollBar.LineRightCommand));
            Assert.Same(bar,move.CommandTarget);
            var highlightedKey=(DependencyPropertyKey)typeof(MenuItem).GetField("IsHighlightedPropertyKey",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            move.SetValue(highlightedKey,true);move.UpdateLayout();
            var chrome=Assert.IsType<Border>(move.Template.FindName("HistoryMenuItemChrome",move));
            Assert.Equal(ThemeResources.Get(window,"Selected").Color,Assert.IsType<System.Windows.Media.SolidColorBrush>(chrome.Background).Color);
            Assert.Equal(ThemeResources.Get(window,"Accent").Color,Assert.IsType<System.Windows.Media.SolidColorBrush>(chrome.BorderBrush).Color);
            var command=Assert.IsType<System.Windows.Input.RoutedCommand>(move.Command);Assert.True(command.CanExecute(null,bar));command.Execute(null,bar);Assert.Equal(53,bar.Value);
            ThemeResources.Apply(window,theme==AppTheme.Light?AppTheme.Dark:AppTheme.Light);
            Assert.Equal(ThemeResources.Get(window,"Selected").Color,Assert.IsType<System.Windows.Media.SolidColorBrush>(chrome.Background).Color);
            Assert.False(menu.IsOpen);Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });
}
