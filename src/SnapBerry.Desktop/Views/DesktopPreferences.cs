using System.Text.Json;
using SnapBerry.Desktop.Shortcuts;

namespace SnapBerry.Desktop.Views;

public sealed record DesktopPreferences(bool Dark=false)
{
    public HotkeySettings Hotkeys {get;init;}=new();
    public bool HistoryVisible {get;init;}=true;
    public double HistoryPanelWidth {get;init;}=240;
    public bool StartAtLogin {get;init;}
    public string LastSaveDirectory {get;init;}="";
    public static string DataDirectory=>OperatingSystem.IsMacOS()
        ?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Application Support","SnapBerry")
        :Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SnapBerry-Desktop");
    public static DesktopPreferences Load(string directory)
    {try{var value=JsonSerializer.Deserialize<DesktopPreferences>(File.ReadAllText(Path.Combine(directory,"desktop-settings.json")))??new();return value with{Hotkeys=value.Hotkeys??new()};}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){return new();}}
    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,"desktop-settings.json");var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
