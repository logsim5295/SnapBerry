namespace SnapBerry.Desktop.Shortcuts;

public sealed record HotkeySettings
{
    public bool Enabled {get;init;}=true;
    public string Region {get;init;}="Ctrl+Shift+1";
    public string Window {get;init;}="Ctrl+Shift+2";
    public string Display {get;init;}="Ctrl+Shift+3";
    public HotkeyChord[] Chords()
    {
        if(!Enabled)return [];var values=new[]{HotkeyChord.Parse(Region),HotkeyChord.Parse(Window),HotkeyChord.Parse(Display)};
        if(values.Distinct().Count()!=3)throw new ArgumentException("캡처 단축키는 서로 다르게 지정해 주세요.");return values;
    }
}

// Carbon의 물리 키 코드와 modifier mask다. 문자열 표기는 설정 UI에서만 해석한다.
public readonly record struct HotkeyChord(uint KeyCode,uint Modifiers)
{
    private static readonly Dictionary<string,uint> Keys=new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"]=0,["S"]=1,["D"]=2,["F"]=3,["H"]=4,["G"]=5,["Z"]=6,["X"]=7,["C"]=8,["V"]=9,["B"]=11,["Q"]=12,["W"]=13,["E"]=14,["R"]=15,["Y"]=16,["T"]=17,
        ["1"]=18,["2"]=19,["3"]=20,["4"]=21,["6"]=22,["5"]=23,["9"]=25,["7"]=26,["8"]=28,["0"]=29,["O"]=31,["U"]=32,["I"]=34,["P"]=35,["L"]=37,["J"]=38,["K"]=40,["N"]=45,["M"]=46,
        ["F1"]=122,["F2"]=120,["F3"]=99,["F4"]=118,["F5"]=96,["F6"]=97,["F7"]=98,["F8"]=100,["F9"]=101,["F10"]=109,["F11"]=103,["F12"]=111
    };
    public static HotkeyChord Parse(string? value)
    {
        var parts=(value??"").Split('+',StringSplitOptions.TrimEntries);uint modifiers=0;
        if(parts.Length<2||!Keys.TryGetValue(parts[^1],out var key))throw new ArgumentException("Ctrl/Option/Command와 영문·숫자·F1~F12를 조합해 주세요.");
        foreach(var part in parts[..^1])
        {
            uint flag=part.ToUpperInvariant() switch{"CTRL" or "CONTROL"=>4096,"SHIFT"=>512,"ALT" or "OPTION"=>2048,"CMD" or "COMMAND"=>256,_=>0};
            if(flag==0||(modifiers&flag)!=0)throw new ArgumentException("단축키의 보조 키를 확인해 주세요.");modifiers|=flag;
        }
        if((modifiers&(4096|2048|256))==0)throw new ArgumentException("Control, Option, Command 중 하나를 포함해 주세요.");return new(key,modifiers);
    }
}
