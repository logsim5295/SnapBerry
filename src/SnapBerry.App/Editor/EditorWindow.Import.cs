using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.App.Import;
using SnapBerry.App.Library;
using SnapBerry.Core;

namespace SnapBerry.App.Editor;

public partial class EditorWindow
{
    private IImagePathPicker imagePicker=null!;
    private IClipboardImageSource clipboardImages=null!;
    private Func<BlankImageOptions?> blankImagePicker=null!;
    private void InitializeImageImport(IImagePathPicker picker,IClipboardImageSource clipboard)
    {
        imagePicker=picker;clipboardImages=clipboard;
        // 리본 그룹이 팝업으로 접혀도 명령은 편집 창에서 실행한다.
        OpenImageButton.CommandTarget=PasteImageButton.CommandTarget=this;
        NewBlankImageButton.CommandTarget=this;
        CommandBindings.Add(new CommandBinding(ApplicationCommands.New,async(_,_)=>await SafeAsync(CreateBlankImageAsync),(_,e)=>{e.CanExecute=!busy;e.Handled=true;}));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open,async(_,_)=>await SafeAsync(OpenImageAsync),(_,e)=>{e.CanExecute=!busy;e.Handled=true;}));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste,async(_,_)=>await SafeAsync(PasteImageAsync),CanPasteImageCommand));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy,CopyImageCommandExecuted,CanCopyImageCommand));
        HistoryList.SelectionChanged+=(_,_)=>CommandManager.InvalidateRequerySuggested();
    }
    private void CanPasteImageCommand(object sender,CanExecuteRoutedEventArgs e)
    {
        e.CanExecute=!busy&&!IsTextInput(e.OriginalSource as DependencyObject);e.Handled=true;
    }
    private static bool IsTextInput(DependencyObject? source)
    {
        for(var node=source;node is not null;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))
            if(node is TextBoxBase or PasswordBox or ComboBox{IsEditable:true})return true;
        return false;
    }
    public Task CreateBlankImageAsync()=>ImportImageAsync(async()=>
    {
        var options=blankImagePicker();
        return(options is null?null:await Task.Run(()=>BlankImageService.Create(options)),null);
    },"빈 화면 생성됨");
    public Task OpenImageAsync()=>ImportImageAsync(async()=>
    {
        var path=imagePicker.Pick(this);return(path is null?null:await ImageImportService.FromFileAsync(path),null);
    },"이미지 열림");
    public Task PasteImageAsync()=>ImportImageAsync(async()=>
    {
        for(var attempt=0;;attempt++)
        {
            try
            {
                var content=clipboardImages.ReadContent();
                if(content is null){StatusText.Text="클립보드에 이미지가 없습니다.";return(null,null);}
                return(ImageImportService.FromBitmap(content.Image),content);
            }
            catch(ExternalException)when(attempt<2){await Task.Delay(100);}
        }
    },"클립보드 이미지 열림",true);
    private async Task ImportImageAsync(Func<Task<(CaptureRecord? Record,ClipboardImageContent? Content)>> read,string success,bool addToCurrent=false)
    {
        if(busy)return;
        var pastePoint=canvas.IsMouseOver?canvas.ToDocumentPoint(Mouse.GetPosition(canvas)):Model.Document is {} d?new PixelPoint(d.CropBounds.X+d.Width/2d,d.CropBounds.Y+d.Height/2d):default;
        var focusPastedObject=false;
        SetBusy(true);canvas.CancelGesture();
        try
        {
            var (record,content)=await read();if(record is null)return;
            if(addToCurrent&&Model.Document is not null)
            {
                Model.Tool=EditorTool.Select;CloseStickerPicker();canvas.Cursor=Cursors.Arrow;
                foreach(var item in ToolsPanel.Items.OfType<ToggleButton>())item.IsChecked=(string)item.Tag==nameof(EditorTool.Select);
                if(content?.Annotation is not null){Model.InsertAnnotation(content,pastePoint);StatusText.Text="개체 붙여넣음";}
                else{Model.InsertImage(record.OriginalPng,content?.ObjectSize);StatusText.Text="이미지 붙여넣음";}
                focusPastedObject=true;
                return;
            }
            // 새 파일의 읽기·검증과 기존 문서의 저장이 성공하기 전에는 편집 문서를 교체하지 않는다.
            await FlushAsync();await store.SaveAsync(record);ShowNewRecord(record);await RefreshLibraryAsync();StatusText.Text=success;
        }
        finally
        {
            SetBusy(false);
            if(focusPastedObject)
            {
                // 비활성화 해제와 이력 포커스 복원이 끝난 뒤 새 개체에 키 입력을 연결한다.
                FocusManager.SetFocusedElement(this,canvas);
                if(IsActive&&IsVisible)canvas.Focus();
            }
        }
    }
}
