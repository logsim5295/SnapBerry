namespace SnapBerry.App.Stickers;
public sealed record StickerDefinition(string Id,string Name,string Category,bool IsKeycap=false,string Label="");
// 스티커 정의 목록과 PNG 생성기를 제공한다. 이력에는 생성된 PNG도 저장해 목록 변경에 영향을 받지 않는다.
public static partial class StickerCatalog
{
    public static IReadOnlyList<StickerDefinition> Load()
    {
        var result=new List<StickerDefinition>();
        string[] names=["Smile","Laugh","Sad","Angry","Surprise","Heart","Star","Thumbs up","Thumbs down","Clap","Idea","Celebrate"];
        string[] labels=["smile","laugh","sad","angry","surprise","♥","★","👍","👎","👏","💡","🎉"];
        for(var i=0;i<12;i++)result.Add(new($"emoji-{i}",names[i],"Emoji",false,labels[i]));
        foreach(var key in new[]{"Ctrl","Shift","Alt","Win","Enter","Esc","Tab","Space","Backspace","Delete","↑","↓","←","→"})result.Add(new($"key-{result.Count}",key,"Keyboard",true,key));
        foreach(var type in new[]{"check","cross"})foreach(var shape in new[]{"plain","circle","square"})result.Add(new($"{type}-{shape}",$"{type} {shape}","Check / X",false,type=="check"?"✓":"×"));
        var symbols=new[]{("warning","!"),("information","i"),("question","?"),("stop","■"),("circle","●"),("square","■"),("triangle","▲"),("diamond","◆")};
        foreach(var (id,glyph) in symbols)result.Add(new($"symbol-{id}",id,"Symbols",false,glyph));
        foreach(var glyph in new[]{"↑","↓","←","→","↖","↗","↙","↘"})result.Add(new($"direction-{result.Count}",glyph,"Directions",false,glyph));
        // 기존 항목의 순번 기반 ID가 달라지지 않도록 새 스티커는 목록 뒤에 추가한다.
        var emotions=new[]{("윙크","\U0001F609"),("고민","\U0001F914"),("감사","\U0001F64F"),("응원","\U0001F4AA"),("멋짐","\U0001F60E"),("졸림","\U0001F634"),("눈물","\U0001F622"),("사랑","\U0001F970"),("안도","\U0001F60C"),("머쓱","\U0001F605"),("비밀","\U0001F910"),("자신감","\U0001F60F")};
        for(var i=0;i<emotions.Length;i++)result.Add(new($"emoji-{i+12}",emotions[i].Item1,"Emoji",false,emotions[i].Item2));
        var keys=new[]{("home","Home"),("end","End"),("page-up","Page Up"),("page-down","Page Down"),("print-screen","Print Screen"),("insert","Insert"),("copy","Ctrl+C"),("paste","Ctrl+V"),("undo","Ctrl+Z"),("save","Ctrl+S"),("switch","Alt+Tab"),("save-as","Ctrl+Shift+S")};
        foreach(var (id,label) in keys)result.Add(new($"key-{id}",label,"Keyboard",true,label));
        var work=new[]{("todo","TODO"),("done","DONE"),("new","NEW"),("fix","FIX"),("important","중요"),("review","검토"),("approved","승인"),("pending","보류")};
        foreach(var (id,label) in work)result.Add(new($"work-{id}",label,"Work",false,label));
        return result;
    }
}
