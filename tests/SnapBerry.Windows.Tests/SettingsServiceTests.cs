using System.IO;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;
// 설정 저장·로드와 손상된 JSON의 백업 및 기본값 복구를 검증한다.
public class SettingsServiceTests
{
    [Fact] public void CorruptSettingsAreBackedUp()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            File.WriteAllText(Path.Combine(path, "settings.json"), "not json");
            var settings = new SettingsService(path).Load();
            Assert.False(settings.StartWithWindows);
            Assert.Equal(1, settings.SchemaVersion);
            Assert.Single(Directory.GetFiles(path, "settings.*.bak"));
        }
        finally { Directory.Delete(path, true); }
    }
    [Fact] public void SettingsRoundTripPreservesHotkeys()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var service = new SettingsService(path);
            service.Save(new AppSettings { RegionKey = "Ctrl+Alt+7", LastSaveDirectory = "D:\\Images" });
            Assert.Equal("Ctrl+Alt+7", service.Load().RegionKey);
            Assert.Equal("D:\\Images", service.Load().LastSaveDirectory);
        }
        finally { Directory.Delete(path, true); }
    }
}
