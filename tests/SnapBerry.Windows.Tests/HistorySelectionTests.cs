using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 한 번 클릭 열기, 삭제 키 대상, 선택 강조와 스크롤 기반 추가 로드를 검증한다.
public class HistorySelectionTests
{
    private static void PumpUntil(Func<bool> ready)
    {
        var frame=new DispatcherFrame();var started=DateTime.UtcNow;var timeout=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};
        timer.Tick+=(_,_)=>{if(ready()||(timeout=DateTime.UtcNow-started>TimeSpan.FromSeconds(3)))frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);timer.Stop();Assert.False(timeout,"Single-click history activation timed out.");
    }
    [Fact] public void OneClickOpensHistoryItem()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? window=null;
        try
        {
            var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var record=new CaptureRecord{Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))};store.SaveAsync(record).GetAwaiter().GetResult();
            window=new(store,new SettingsService(path));window.Show();var list=(ListBox)window.FindName("HistoryList");PumpUntil(()=>list.Items.Count==1);list.SelectedIndex=0;list.UpdateLayout();var item=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.PreviewMouseLeftButtonUpEvent,Source=item});PumpUntil(()=>window.Model.CaptureId==record.Id);
            Assert.Equal(record.Id,window.Model.CaptureId);
            foreach(var focus in new IInputElement[]{item,(Button)window.FindName("HistoryToggleButton")})
            {
                Keyboard.Focus(focus);window.UpdateLayout();var chrome=Assert.IsType<Border>(item.Template.FindName("HistoryItemChrome",item));Assert.Equal("#FFEBD5E6",chrome.Background.ToString());Assert.Equal("#FF923C83",chrome.BorderBrush.ToString());
            }
            list.SelectedIndex=-1;window.UpdateLayout();Assert.NotEqual("#FFEBD5E6",((Border)item.Template.FindName("HistoryItemChrome",item)).Background.ToString());
        }
        finally{window?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData(true,false)] [InlineData(false,false)] [InlineData(true,true)] [InlineData(false,true)]
    public void HistoryHasNoDeleteButtonAndDeleteKeyTargetsSelection(bool confirmed,bool targetActive)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? window=null;
        try
        {
            var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var a=new CaptureRecord{Width=40,Height=30,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))};var b=a with{Id=Guid.NewGuid()};store.SaveAsync(a).GetAwaiter().GetResult();store.SaveAsync(b).GetAwaiter().GetResult();
            window=new(store,new SettingsService(path),confirmDelete:_=>confirmed);window.Show();var list=(ListBox)window.FindName("HistoryList");PumpUntil(()=>list.Items.Count==2&&!window.IsBusy&&window.Model.CaptureId is not null);var active=targetActive?b:a;window.Model.Open(active);list.UpdateLayout();
            var target=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==b.Id);list.SelectedItem=target;var item=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(target);
            Keyboard.Focus(item);window.UpdateLayout();Assert.DoesNotContain(Descendants(item).OfType<Button>(),x=>x.Name=="DeleteCaptureButton");
            list.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,Key.Delete){RoutedEvent=Keyboard.KeyDownEvent});PumpUntil(()=>!window.IsBusy&&list.Items.Count==(confirmed?1:2));Assert.Equal(confirmed?a.Id:active.Id,window.Model.CaptureId);Assert.Contains(list.Items.Cast<CaptureEntry>(),x=>x.Item.Id==a.Id);
        }
        finally{window?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}}
    [Fact] public void ScrollingLoadsOlderCapturesWithoutMoreButton()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? window=null;
        try
        {
            var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var png=PngFileWriter.Encode(RenderingTests.Image(40,30));
            for(var i=0;i<51;i++)store.SaveAsync(new(){Width=40,Height=30,OriginalPng=png}).GetAwaiter().GetResult();
            window=new(store,new SettingsService(path));window.Show();var list=(ListBox)window.FindName("HistoryList");PumpUntil(()=>list.Items.Count==50);Assert.Null(window.FindName("MoreHistoryButton"));window.UpdateLayout();
            var scroll=Descendants(list).OfType<ScrollViewer>().First();Assert.Equal(ScrollBarVisibility.Auto,scroll.VerticalScrollBarVisibility);scroll.ScrollToBottom();PumpUntil(()=>list.Items.Count==51);
        }
        finally{window?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
