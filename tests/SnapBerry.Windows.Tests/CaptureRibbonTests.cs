using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 캡처 방식별 리본 버튼과 단축키 툴팁 표시를 검증한다.
public class CaptureRibbonTests
{
    [Fact] public void CaptureTooltipsShowSavedHotkeysAndRefreshAfterSettingsChange()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var store=new SettingsService(path);var value=new AppSettings{RegionKey="Ctrl+Alt+A",WindowKey="Ctrl+Alt+B",DesktopKey="Ctrl+Alt+C"};store.Save(value);var w=new EditorWindow(new(Path.Combine(path,"Library")),store);
        try
        {
            void Check(AppSettings settings){Assert.Equal($"영역 캡처 ({settings.RegionKey})",((RibbonButton)w.FindName("RegionCaptureButton")).ToolTip);Assert.Equal($"창 캡처 ({settings.WindowKey})",((RibbonButton)w.FindName("WindowCaptureButton")).ToolTip);Assert.Equal($"전체 화면 캡처 ({settings.DesktopKey})",((RibbonButton)w.FindName("DesktopCaptureButton")).ToolTip);}
            Check(value);var updated=new AppSettings();store.Save(updated);w.ReloadSettings();Check(updated);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void RibbonButtonsRequestTheirCaptureModeAndReplaceOldControls()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            Assert.IsAssignableFrom<Ribbon>(((Grid)w.Content).Children[0]);
            Assert.NotNull(((RibbonGroup)w.FindName("SettingsGroup")).LargeImageSource);
            var settings=Assert.IsType<RibbonButton>(w.FindName("RibbonSettings"));Assert.NotNull(settings.LargeImageSource);var opened=false;w.SettingsRequested=()=>{opened=true;return Task.CompletedTask;};settings.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Assert.True(opened);
            Assert.Null(w.FindName("CaptureButton"));Assert.Null(w.FindName("CaptureModeBox"));var requested=new List<CaptureMode>();w.CaptureRequested=mode=>{requested.Add(mode);return Task.CompletedTask;};
            foreach(var name in new[]{"RegionCaptureButton","WindowCaptureButton","DesktopCaptureButton"})
            {var button=Assert.IsType<RibbonButton>(w.FindName(name));Assert.NotNull(button.LargeImageSource);button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}
            Assert.Equal(new[]{CaptureMode.Region,CaptureMode.Window,CaptureMode.Desktop},requested);
            w.SetBusy(true);Assert.False(((RibbonButton)w.FindName("RegionCaptureButton")).IsEnabled);w.SetBusy(false);Assert.True(((RibbonButton)w.FindName("RegionCaptureButton")).IsEnabled);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
