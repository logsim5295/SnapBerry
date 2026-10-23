using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 팝업·창·클립보드를 열지 않고 공통 템플릿을 적용한 뒤 명령 대상과 탭 전환을 검증한다.
public class RemainingControlThemeTests
{
    [Theory] [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void TextMenuTargetsTheCurrentInputAndRespectsReadOnlyMode(AppTheme theme)=>Sta.Run(()=>
    {
        var window=new Window();
        window.Resources.MergedDictionaries.Add(new(){Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(window,theme);
        var first=new TextBox{Text="첫 번째 입력"};var second=new TextBox{Text="두 번째 입력",IsReadOnly=true};
        var panel=new StackPanel();panel.Children.Add(first);panel.Children.Add(second);window.Content=panel;
        try
        {
            panel.Measure(new Size(400,200));panel.Arrange(new Rect(0,0,400,200));panel.UpdateLayout();
            foreach(var input in new[]{first,second})
            {
                var menu=Assert.IsType<ContextMenu>(input.ContextMenu);menu.PlacementTarget=input;
                menu.Measure(new Size(260,double.PositiveInfinity));menu.Arrange(new Rect(new Size(260,menu.DesiredSize.Height)));menu.UpdateLayout();
                var frame=new DispatcherFrame();menu.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);
                var select=menu.Items.OfType<MenuItem>().Single(x=>x.Command==ApplicationCommands.SelectAll);
                Assert.Same(input,select.CommandTarget);
                Assert.True(ApplicationCommands.SelectAll.CanExecute(null,select.CommandTarget));
                ApplicationCommands.SelectAll.Execute(null,select.CommandTarget);
                Assert.Equal(input.Text,input.SelectedText);
                if(input.IsReadOnly)Assert.False(ApplicationCommands.Cut.CanExecute(null,input));
                menu.PlacementTarget=null;
            }
            Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });

    [Theory] [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void LegalTabsKeepTheirSelectedDocumentAcrossThemeChanges(AppTheme theme)=>Sta.Run(()=>
    {
        var window=new AboutWindow();
        try
        {
            var section=(Expander)window.FindName("AboutLegalInformation");section.IsExpanded=true;
            var tabs=Assert.IsType<TabControl>(section.Content);
            var root=(FrameworkElement)window.Content;window.Content=null;root.Resources=window.Resources;
            void Layout()
            {root.Measure(new Size(480,1000));root.Arrange(new Rect(0,0,480,1000));root.UpdateLayout();}
            foreach(var item in tabs.Items.Cast<TabItem>())
            {
                tabs.SelectedItem=item;ThemeResources.Apply(window,theme);Layout();
                var presenter=Assert.IsType<ContentPresenter>(tabs.Template.FindName("PART_SelectedContentHost",tabs));
                Assert.Same(item.Content,presenter.Content);
                var text=Assert.IsType<TextBox>(item.Content);Assert.NotNull(text.ContextMenu);Assert.True(text.IsReadOnly);
                Assert.NotNull(item.Template.FindName("TabItemChrome",item));
                ThemeResources.Apply(window,theme==AppTheme.Light?AppTheme.Dark:AppTheme.Light);Layout();
                Assert.Same(item,tabs.SelectedItem);Assert.Same(text,presenter.Content);
            }
            Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });
}
