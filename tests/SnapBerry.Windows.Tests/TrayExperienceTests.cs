using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using SnapBerry.App.Shell;
using Xunit;
using Forms=System.Windows.Forms;

namespace SnapBerry.Windows.Tests;

// 최초 트레이 안내의 확인·설정 보존과 메뉴 아이콘 연결이 기존 동작을 바꾸지 않는지 검증한다.
public class TrayExperienceTests
{
    [Fact] public void DefaultCloseShowsBrandedNoticeBeforeHiding()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var settings=new SettingsService(path);
        var w=new EditorWindow(new(Path.Combine(path,"Library")),settings);
        try
        {
            w.Show();w.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,()=>
            {
                var dialog=Assert.Single(w.OwnedWindows.OfType<TrayNoticeDialog>());Assert.True(w.IsVisible);Assert.True(w.IsBusy);
                var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
                if(artifacts is not null)
                {
                    Directory.CreateDirectory(artifacts);var content=(FrameworkElement)dialog.Content;content.UpdateLayout();
                    var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(content);
                    File.WriteAllBytes(Path.Combine(artifacts,"tray-notice-dialog.png"),SnapBerry.App.Export.PngFileWriter.Encode(image));
                }
                ((Button)dialog.FindName("AcknowledgeTrayButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            });
            w.Close();Assert.False(w.IsVisible);Assert.True(settings.Load().HasSeenTrayNotice);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData(true)] [InlineData(false)]
    public void FirstCloseRequiresAcknowledgmentAndPersistsAcrossWindows(bool acknowledge)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));EditorWindow? w=null;
        try
        {
            var settings=new SettingsService(path);settings.Save(new(){RegionKey="Ctrl+Alt+7"});var notices=0;
            w=new(new(Path.Combine(path,"Library")),settings,confirmTrayHide:()=>{notices++;return acknowledge;});w.Show();w.Close();
            Assert.Equal(1,notices);Assert.Equal(!acknowledge,w.IsVisible);Assert.Equal(acknowledge,settings.Load().HasSeenTrayNotice);
            Assert.Equal("Ctrl+Alt+7",settings.Load().RegionKey);
            w.Show();w.Close();Assert.Equal(acknowledge?1:2,notices);w.CloseForExit();
            w=new(new(Path.Combine(path,"Library")),settings,confirmTrayHide:()=>{notices++;return true;});w.Show();w.Close();
            Assert.Equal(acknowledge?1:3,notices);Assert.False(w.IsVisible);
        }
        finally{w?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void ExplicitExitAndBusyCloseDoNotShowNotice()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var notices=0;
        var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),confirmTrayHide:()=>{notices++;return true;});
        try{w.Show();w.SetBusy(true);w.Close();Assert.True(w.IsVisible);Assert.Equal(0,notices);w.CloseForExit();Assert.Equal(0,notices);}
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(true)] [InlineData(false)]
    public void NoticeExplainsTrayAndOnlyAcknowledgmentConfirms(bool acknowledge)=>Sta.Run(()=>
    {
        var dialog=new TrayNoticeDialog();
        dialog.Loaded+=(_,_)=>dialog.Dispatcher.BeginInvoke(()=>
        {
            var text=((TextBlock)dialog.FindName("TrayNoticeMessage")).Text;
            Assert.Contains("알림 영역",text);Assert.Contains("우클릭",text);Assert.Contains("종료",text);
            if(acknowledge)((Button)dialog.FindName("AcknowledgeTrayButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            else dialog.Close();
        });
        Assert.Equal(acknowledge,dialog.ShowDialog()==true);
    });

    [Fact] public void EveryTrayActionHasIconAndKeepsItsClickHandler()=>Sta.Run(()=>
    {
        using var menu=new Forms.ContextMenuStrip();var clicks=0;
        foreach(var title in new[]{"편집기 열기","영역 캡처","창 캡처","전체 화면 캡처","설정","종료"})menu.Items.Add(title,null,(_,_)=>clicks++);
        menu.Items.Insert(4,new Forms.ToolStripSeparator());TrayMenuIcons.Apply(menu);
        foreach(var item in menu.Items.OfType<Forms.ToolStripMenuItem>())
        {
            var bitmap=Assert.IsType<System.Drawing.Bitmap>(item.Image);Assert.Equal(menu.ImageScalingSize,bitmap.Size);
            Assert.Contains(Enumerable.Range(0,bitmap.Width).SelectMany(x=>Enumerable.Range(0,bitmap.Height).Select(y=>bitmap.GetPixel(x,y))),c=>c.A>0);
            item.PerformClick();
        }
        Assert.Equal(6,clicks);Assert.Equal(7,menu.Items.Count);
    });
}
