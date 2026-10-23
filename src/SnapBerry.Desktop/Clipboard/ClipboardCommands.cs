using System.Globalization;
using SnapBerry.Core;
using SnapBerry.Desktop.Editing;

namespace SnapBerry.Desktop.Clipboard;

public readonly record struct ImageObjectSize(double Width,double Height)
{
    public bool IsValid=>double.IsFinite(Width)&&double.IsFinite(Height)&&Width>0&&Height>0&&Width<=1000000&&Height<=1000000;
    public override string ToString()=>FormattableString.Invariant($"{Width:R}|{Height:R}");
    public static ImageObjectSize? Parse(string? text)
    {
        if(text is null||text.Length>100)return null;var parts=text.Split('|');
        return parts.Length==2&&double.TryParse(parts[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var w)&&double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var h)&&new ImageObjectSize(w,h) is {IsValid:true} valid?valid:null;
    }
}
public sealed record ImageClipboardContent(byte[] Png,ImageObjectSize? ObjectSize=null);
public interface IEditorClipboard {Task<ImageClipboardContent?> ReadAsync();Task WriteAsync(ImageClipboardContent content);}

public sealed class ClipboardCommands(IEditorClipboard clipboard)
{
    public async Task CopyAsync(DesktopEditor editor,bool documentOnly=false)
    {
        if(editor.Document is not AnnotationDocument document)return;
        var token=editor.History!.CurrentStateToken;var id=editor.CaptureId;
        var content=!documentOnly&&editor.Selected is {Kind:AnnotationKind.Sticker} selected
            ?new ImageClipboardContent(editor.Assets[selected.AssetHash].ToArray(),new(selected.Width*document.OutputSize.Width/document.Width,selected.Height*document.OutputSize.Height/document.Height))
            :new ImageClipboardContent(editor.RenderPng().ToArray());
        await clipboard.WriteAsync(content);
        if(content.ObjectSize is null&&editor.CaptureId==id)editor.History.MarkExported(token);
    }
    public async Task<bool> PasteAsync(DesktopEditor editor,Func<Task> beforeOpen)
    {
        var content=await clipboard.ReadAsync();if(content is null)return false;
        if(editor.Document is null){await beforeOpen();editor.OpenImage(content.Png,CaptureMode.Clipboard);}
        else editor.InsertImage(content.Png,content.ObjectSize);
        return true;
    }
}
