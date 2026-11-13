using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class FixedRegionSettingsTests
{
    [Fact]
    public void SettingsKeepSaveReachableOnShortWorkAreas()=>Sta.Run(()=>
    {
        var window=new SettingsWindow(new());
        try
        {
            var chrome=Assert.IsType<Border>(window.Content);
            var scroll=Assert.IsType<ScrollViewer>(chrome.Child);
            chrome.Measure(new Size(480,500));chrome.Arrange(new Rect(0,0,480,500));chrome.UpdateLayout();
            Assert.True(scroll.ScrollableHeight>0);
            scroll.ScrollToBottom();chrome.UpdateLayout();
            var save=(Button)window.FindName("SaveSettingsButton");
            var position=save.TransformToAncestor(chrome).Transform(new Point(0,save.ActualHeight));
            Assert.InRange(position.Y,1,500);
            Assert.False(window.IsVisible);
        }
        finally{window.Close();}
    });

    [Theory]
    [InlineData(0,480)]
    [InlineData(-1,480)]
    [InlineData(640,int.MaxValue)]
    public void InvalidSavedAreaIsDiscardedWithoutLosingOtherPreferences(int width,int height)
    {
        var path=Directory.CreateTempSubdirectory().FullName;
        try
        {
            var service=new SettingsService(path);
            service.Save(new(){FixedRegion=new(0,0,width,height),RegionKey="Ctrl+Alt+7"});
            Assert.Null(service.Load().FixedRegion);
            Assert.Equal("Ctrl+Alt+7",service.Load().RegionKey);
        }
        finally{Directory.Delete(path,true);}
    }

    [Fact]
    public void SavedRegionAndShortcutSurviveSettingsRoundTrip()
    {
        var path=Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(path,"settings.json"),"""{"FixedRegionKey":"Ctrl+Alt+8","FixedRegion":{"X":-1200,"Y":80,"Width":640,"Height":480},"RegionKey":"Ctrl+Alt+7"}""");
            var service=new SettingsService(path);
            service.Save(service.Load() with { StartWithWindows=true });
            using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(path,"settings.json")));
            Assert.True(json.RootElement.TryGetProperty("FixedRegion",out var region));
            Assert.Equal(-1200,region.GetProperty("X").GetInt32());
            Assert.Equal(640,region.GetProperty("Width").GetInt32());
            Assert.Equal("Ctrl+Alt+8",json.RootElement.GetProperty("FixedRegionKey").GetString());
            Assert.Equal("Ctrl+Alt+7",service.Load().RegionKey);
        }
        finally { Directory.Delete(path,true); }
    }

    [Fact]
    public void FixedShortcutParticipatesInDuplicateValidationWithoutShowingWindow()=>Sta.Run(()=>
    {
        var window=new SettingsWindow(new());
        try
        {
            var input=Assert.IsType<HotkeyInput>(window.FindName("FixedRegionHotkey"));
            var save=(Button)window.FindName("SaveSettingsButton");
            Assert.True(save.IsEnabled);
            input.Text=((HotkeyInput)window.FindName("RegionHotkey")).Text;
            Assert.False(save.IsEnabled);
            input.Text="Ctrl+Alt+8";
            Assert.True(save.IsEnabled);
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    });
}
