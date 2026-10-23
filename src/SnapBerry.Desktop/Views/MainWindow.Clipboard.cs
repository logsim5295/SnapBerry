using Avalonia.Controls;
using Avalonia.VisualTree;
using SnapBerry.App.Library;
using SnapBerry.Desktop.Editing;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow
{
    private bool IsHistorySource(Control? source)=>source==Control<ListBox>("HistoryList")||source?.GetVisualAncestors().Contains(Control<ListBox>("HistoryList"))==true;
    public async Task CopyImageAsync(bool fromHistory=false)
    {
        if(!fromHistory){await clipboardCommands.CopyAsync(editor);Status("이미지 복사됨");return;}
        if(Control<ListBox>("HistoryList").SelectedItem is not CaptureListItem item)return;
        if(item.Damaged)throw new InvalidDataException("손상된 이력입니다.");await FlushAsync();
        if(item.Id==editor.CaptureId)await clipboardCommands.CopyAsync(editor,true);
        else{using var source=new DesktopEditor();source.Open(await store.LoadAsync(item.Id));await clipboardCommands.CopyAsync(source,true);}
        Status("선택한 이력 이미지 복사됨");
    }
    public async Task PasteImageAsync()
    {
        var wasEmpty=editor.Document is null;
        if(!await clipboardCommands.PasteAsync(editor,FlushAsync)){Status("클립보드에 이미지가 없습니다.");return;}
        if(wasEmpty){Control<TextBox>("SearchBox").Text="";Control<TextBox>("NameEntry").Text="";Fit();}
        Control<ComboBox>("ToolBox").SelectedIndex=0;editor.Tool=DesktopTool.Select;
        await FlushAsync();await RefreshHistoryAsync();Status(wasEmpty?"클립보드 이미지 열림":"이미지 붙여넣음");
    }
}
