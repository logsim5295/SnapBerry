using System.IO;
using System.Windows;
using SnapBerry.App.Settings;
using SnapBerry.App.Stickers;
using Xunit;

namespace SnapBerry.Windows.Tests;

public class StickerWindowRestoreTests
{
    [Fact] public void ClosingAndReopeningPreservesSecondaryMonitorGeometryAcrossSettingsReload()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try
        {
            var work=new Rect(-2560,40,2560,1400);var bounds=new Rect(-2200,220,750,600);
            var saved=StickerPickerPlacement.Remember("secondary",bounds,work,1.5,1.5);
            new SettingsService(path).Save(new(){StickerWindow=saved,RegionKey="Ctrl+Alt+7"});
            var loaded=new SettingsService(path).Load();
            Assert.Equal("Ctrl+Alt+7",loaded.RegionKey);
            Assert.Equal("secondary",StickerPickerPlacement.SelectMonitor(loaded.StickerWindow,["primary","secondary"],"primary"));
            Assert.Equal(bounds,StickerPickerPlacement.Restore(loaded.StickerWindow!,work,1.5,1.5));
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }

    [Fact] public void MonitorRearrangementAndDpiChangeKeepLogicalSizeAndRelativePosition()
    {
        var saved=StickerPickerPlacement.Remember("secondary",new(-2200,220,750,600),new(-2560,40,2560,1400),1.5,1.5);
        var restored=StickerPickerPlacement.Restore(saved,new(1920,-200,3840,2100),2,2);
        Assert.Equal(new Rect(2400,40,1000,800),restored);
    }

    [Fact] public void DisconnectedMonitorFallsBackToEditorMonitorAndClampsAllEdges()
    {
        var saved=new StickerWindowState("disconnected",1800,900,500,400);
        Assert.Equal("editor",StickerPickerPlacement.SelectMonitor(saved,["primary","editor"],"editor"));
        Assert.Equal(new Rect(-500,640,500,400),StickerPickerPlacement.Restore(saved,new(-1920,0,1920,1040),1,1));
    }

    [Fact] public void SmallWorkAreaShrinksOversizedWindowAndKeepsTitleVisible()
    {
        var saved=new StickerWindowState("primary",-500,-200,1000,900);
        Assert.Equal(new Rect(40,30,600,400),StickerPickerPlacement.Restore(saved,new(40,30,600,400),2,2));
    }

    [Fact] public void InvalidSavedSizeIsIgnoredWithoutDiscardingOtherSettings()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try
        {
            new SettingsService(path).Save(new(){RegionKey="Ctrl+Alt+7",StickerWindow=new("primary",0,0,-1,400)});
            var loaded=new SettingsService(path).Load();
            Assert.Null(loaded.StickerWindow);Assert.Equal("Ctrl+Alt+7",loaded.RegionKey);
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }

    [Fact] public void SettingsWithoutSavedWindowUseFirstOpenDefaults()
    {
        Assert.Null(new AppSettings().StickerWindow);
        Assert.Equal("editor",StickerPickerPlacement.SelectMonitor(null,["primary","editor"],"editor"));
    }
}
