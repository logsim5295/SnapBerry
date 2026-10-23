using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.App.Shell;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창 표시 없이 설정 진입 시 실제 비활성 목록의 렌더링을 확인한다.
public class HistoryListThemeTests
{
    [Theory]
    [InlineData(AppTheme.Dark,false)] [InlineData(AppTheme.Dark,true)]
    [InlineData(AppTheme.Light,false)] [InlineData(AppTheme.Light,true)]
    public void SettingsKeepHistoryBackgroundThemedWhileBusyAndAfterReturning(AppTheme theme,bool populated)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-history-theme-"+Guid.NewGuid().ToString("N"));
        var settings=new SettingsService(path);settings.Save(new(){Theme=theme});
        var editor=new EditorWindow(new(Path.Combine(path,"Library")),settings);
        try
        {
            var list=(ListBox)editor.FindName("HistoryList");
            if(populated)
            {
                list.ItemsSource=new[]{new CaptureEntry(new(Guid.NewGuid(),DateTimeOffset.Now,40,20),null),new CaptureEntry(new(Guid.NewGuid(),DateTimeOffset.Now,30,10),null)};
                list.SelectedIndex=0;
            }
            var root=(FrameworkElement)editor.Content;editor.Content=null;root.Resources=editor.Resources;
            root.SetValue(TextElement.ForegroundProperty,editor.Foreground);
            void Check(string state,AppTheme? expectedTheme=null)
            {
                root.Measure(new Size(1240,820));root.Arrange(new Rect(0,0,1240,820));root.UpdateLayout();
                var frame=new DispatcherFrame();editor.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);root.UpdateLayout();
                var width=(int)list.ActualWidth;var height=(int)list.ActualHeight;Assert.True(width>0&&height>100);
                var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(list);
                var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
                if(folder is not null){Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,$"history-{theme}-{populated}-{state}.png"),PngFileWriter.Encode(bitmap));}
                var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(width/2,height-10,1,1),pixel,4,0);
                var actual=Color.FromArgb(pixel[3],pixel[2],pixel[1],pixel[0]);
                Assert.Equal(ThemeResources.ColorFor(expectedTheme??theme,"Surface"),actual);
                if(populated){Assert.Equal(2,list.Items.Count);Assert.Equal(0,list.SelectedIndex);}
                Assert.False(editor.IsVisible);
            }
            Check("before");
            AppHost.RunSettings(()=>
            {
                Assert.False(list.IsEnabled);Check("settings");
                var dialog=new SettingsWindow(new(){Theme=theme});
                try
                {
                    ((ComboBox)dialog.FindName("ThemeSelector")).SelectedIndex=theme==AppTheme.Dark?0:1;
                    Check("preview-cancel");Assert.False(dialog.IsVisible);
                }
                finally{dialog.Close();}
            },editor.SetBusy,error=>throw new Exception("설정 경로 실패",error));
            Assert.True(list.IsEnabled);Check("after");
            var changedTheme=theme==AppTheme.Dark?AppTheme.Light:AppTheme.Dark;
            AppHost.RunSettings(()=>
            {
                settings.Save(new(){Theme=changedTheme});editor.ReloadSettings();Check("saved-busy",changedTheme);
            },editor.SetBusy,error=>throw new Exception("테마 저장 경로 실패",error));
            Assert.True(list.IsEnabled);Check("saved-after",changedTheme);
            var failure=new IOException("설정 저장 실패 대체");Exception? reported=null;
            AppHost.RunSettings(()=>{Check("error-busy",changedTheme);throw failure;},editor.SetBusy,error=>reported=error);
            Assert.Same(failure,reported);Assert.True(list.IsEnabled);Check("error-after",changedTheme);
        }
        finally{editor.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void HistoryTemplateKeepsVirtualizationScrollingAndMultipleSelection(AppTheme theme)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-history-scroll-theme-"+Guid.NewGuid().ToString("N"));
        var settings=new SettingsService(path);settings.Save(new(){Theme=theme});
        var editor=new EditorWindow(new(Path.Combine(path,"Library")),settings);
        try
        {
            var root=(FrameworkElement)editor.Content;editor.Content=null;root.Resources=editor.Resources;
            var list=(ListBox)editor.FindName("HistoryList");
            list.ItemsSource=Enumerable.Range(0,200).Select(_=>new CaptureEntry(new(Guid.NewGuid(),DateTimeOffset.Now,40,20),null)).ToArray();
            void Layout(){root.Measure(new Size(1240,820));root.Arrange(new Rect(0,0,1240,820));root.UpdateLayout();}
            Layout();
            var scroll=(ScrollViewer)list.Template.FindName("PART_ScrollViewer",list);
            Assert.True(scroll.CanContentScroll);Assert.True(VirtualizingPanel.GetIsVirtualizing(list));
            Assert.True(scroll.ScrollableHeight>0);
            Assert.InRange(Enumerable.Range(0,200).Count(i=>list.ItemContainerGenerator.ContainerFromIndex(i) is not null),1,199);
            list.SelectedItems.Add(list.Items[0]);list.SelectedItems.Add(list.Items[1]);
            scroll.ScrollToVerticalOffset(50);Layout();Assert.True(scroll.VerticalOffset>0);
            editor.SetBusy(true);Layout();editor.SetBusy(false);Layout();
            Assert.Equal(2,list.SelectedItems.Count);Assert.True(scroll.VerticalOffset>0);Assert.False(editor.IsVisible);
        }
        finally{editor.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
