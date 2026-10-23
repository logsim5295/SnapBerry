using SnapBerry.App.Stickers;

namespace SnapBerry.Desktop.Stickers;

public static class DesktopStickerCatalog
{
    private static readonly IReadOnlyList<StickerDefinition> Definitions=StickerCatalog.Load().Concat(new[]{
        new StickerDefinition("key-mac-command","Command","Keyboard",true,"⌘"),new("key-mac-option","Option","Keyboard",true,"⌥"),new("key-mac-control","Control","Keyboard",true,"⌃"),new("key-mac-shift","Shift","Keyboard",true,"⇧"),
        new("key-mac-copy","Command+C","Keyboard",true,"⌘ C"),new("key-mac-paste","Command+V","Keyboard",true,"⌘ V"),new("key-mac-undo","Command+Z","Keyboard",true,"⌘ Z"),new("key-mac-save","Command+S","Keyboard",true,"⌘ S")}).ToArray();
    public static IReadOnlyList<StickerDefinition> Load()=>Definitions;
    public static string CategoryName(string category)=>category switch{"Emoji"=>"이모지","Keyboard"=>"키보드","Check / X"=>"체크 / 엑스","Symbols"=>"기호","Directions"=>"방향","Work"=>"업무",_=>category};
    public static IEnumerable<StickerDefinition> Search(string? category,string query)
    {
        var text=query.Trim();return Definitions.Where(x=>(string.IsNullOrEmpty(category)||x.Category==category)&&(text.Length==0||x.Name.Contains(text,StringComparison.OrdinalIgnoreCase)||x.Label.Contains(text,StringComparison.OrdinalIgnoreCase)||CategoryName(x.Category).Contains(text,StringComparison.OrdinalIgnoreCase)));
    }
}
