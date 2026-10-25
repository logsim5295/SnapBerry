using System.IO;
using System.Text.Json;
using SnapBerry.App.Export;
namespace SnapBerry.App.Settings;
// 설정 파일 입출력을 담당한다. 손상된 JSON은 백업으로 남기고 기본값으로 복구한다.
public sealed class SettingsService(string directory)
{
    private string FilePath => Path.Combine(directory, "settings.json");
    public AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new();
        try
        {
            var value = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
            if (value is null || value.SchemaVersion != 1) throw new JsonException();
            return value with
            {
                Theme=Enum.IsDefined(value.Theme)?value.Theme:AppTheme.Light,
                LastSaveFormat=Enum.IsDefined(value.LastSaveFormat)?value.LastSaveFormat:ExportFormat.Png,
                StickerWindow=value.StickerWindow is {IsValid:true}?value.StickerWindow:null
            };
        }
        catch (JsonException)
        {
            File.Move(FilePath, Path.Combine(directory, $"settings.{Guid.NewGuid():N}.bak"));
            return new();
        }
    }
    public void Save(AppSettings settings)
    {
        // 완성된 임시 파일로 교체하여 중간 쓰기로 기존 설정이 잘리지 않게 한다.
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings));
        File.Move(temp, FilePath, true);
    }
}
