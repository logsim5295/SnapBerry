namespace SnapBerry.App.Settings;
// 네이티브 후크와 분리된 우선 단축키 판정기다. 예약 키 차단과 눌림 반복·뗌 처리를 담당한다.
internal sealed class PriorityHotkeyRouter
{
    private readonly Dictionary<int,(uint Modifiers,uint Key)> bindings=[];
    private readonly HashSet<uint> pressed=[];
    internal int Count=>bindings.Count;
    internal static bool IsReserved(uint modifiers,uint key)=>key==0x7B||((modifiers&3)==3&&key==0x2E)||((modifiers&8)!=0&&key==0x4C)||((modifiers&1)!=0&&key is 0x09 or 0x1B)||((modifiers&2)!=0&&key==0x1B);
    internal static bool IsSupported(uint modifiers,uint key)
    {
        // Windows 키 조합과 시스템 예약 조합은 우선 점유하지 않고 안전하게 처리할 수 있는 범위만 허용한다.
        modifiers&=0xF;
        if((modifiers&8)!=0||IsReserved(modifiers,key))return false;
        if(key==0x2C)return true;
        if((modifiers&3)==0||(key==0x73&&(modifiers&1)!=0))return false;
        return key is >=0x30 and <=0x5A or >=0x60 and <=0x7A or >=0x7C and <=0x87;
    }
    internal void Add(int id,uint modifiers,uint key)=>bindings[id]=(modifiers&0xF,key);
    internal void Remove(int id){bindings.Remove(id);if(bindings.Count==0)pressed.Clear();}
    internal bool Process(int message,uint key,uint modifiers,out int? id)
    {
        // 이미 눌린 키의 반복 메시지는 소비만 하고 새 캡처를 발생시키지 않는다.
        id=null;
        if(message is 0x101 or 0x105)return pressed.Remove(key);
        if(message is not (0x100 or 0x104))return false;
        if(pressed.Contains(key))return true;
        foreach(var binding in bindings)
            if(binding.Value==(modifiers&0xF,key)){pressed.Add(key);id=binding.Key;return true;}
        return false;
    }
}
