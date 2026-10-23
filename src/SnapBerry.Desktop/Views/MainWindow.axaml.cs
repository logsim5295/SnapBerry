using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using SnapBerry.App.Library;
using SnapBerry.Core;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Rendering;
using SnapBerry.Desktop.Clipboard;
using SnapBerry.Desktop.Shortcuts;
using SnapBerry.Desktop.Stickers;
using SnapBerry.Desktop.Shell;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow : Window,IDisposable
{
    private readonly DesktopEditor editor;private readonly EditorSurface surface;private readonly CaptureLibraryStore store;private readonly AutosaveCoordinator autosave;
    private readonly CaptureCoordinator capture;private readonly string dataDirectory;
    private readonly ILoginStartupService? loginStartup;
    private readonly ClipboardCommands clipboardCommands;private readonly GlobalHotkeyService? hotkeys;private DesktopPreferences preferences;
    private bool busy,refreshing,allowClose,closed,syncingProperties;private int historyPages=1,searchVersion;private string? colorBaseline="#E83F56";
    public DesktopEditor Editor=>editor;public bool IsBusy=>busy;
    public MainWindow():this(new DesktopEditor(),new MacCaptureService(),DesktopPreferences.DataDirectory,null,CreateGlobalHotkeys()){}
    public MainWindow(DesktopEditor editor,ICaptureService captureService,string dataDirectory,IEditorClipboard? clipboard=null,GlobalHotkeyService? hotkeys=null,ILoginStartupService? loginStartup=null)
    {
        AvaloniaXamlLoader.Load(this);this.loginStartup=loginStartup??(OperatingSystem.IsMacOS()?new MacLoginStartupService():null);this.editor=editor;this.dataDirectory=dataDirectory;store=new(Path.Combine(dataDirectory,"Library"));capture=new(captureService);
        clipboardCommands=new(clipboard??new AvaloniaEditorClipboard(()=>this.Clipboard));this.hotkeys=hotkeys;preferences=DesktopPreferences.Load(dataDirectory);if(hotkeys is not null)hotkeys.Pressed+=OnGlobalCapture;
        surface=new(editor);surface.SelectionChanged+=RefreshSelection;surface.EditFailed+=error=>Status("편집 실패: "+error.Message);this.FindControl<Panel>("SurfaceHost")!.Children.Add(surface);
        autosave=new(store.SaveAsync);
        autosave.Saved+=(id,token)=>Dispatcher.UIThread.Post(()=>{if(!closed&&editor.CaptureId==id)editor.History?.MarkPersisted(token);});
        autosave.Failed+=error=>Dispatcher.UIThread.Post(()=>{if(!closed)Status("이력 저장 실패: "+error.Message);});
        editor.Changed+=EditorChanged;
        var scroll=Control<ScrollViewer>("ImageScroll");surface.PanRequested+=delta=>scroll.Offset-=delta;
        Wire("OpenButton",()=>OpenImageAsync(false));Wire("AddImageButton",()=>OpenImageAsync(true));
        Wire("CopyButton",()=>CopyImageAsync());Wire("PasteButton",PasteImageAsync);Wire("StickerButton",ShowStickersAsync);Wire("ApplyKeycapButton",UpdateKeycapAsync);Wire("HotkeyButton",ShowHotkeysAsync);
        Wire("RegionButton",()=>CaptureAsync(DesktopCaptureMode.Region));Wire("WindowButton",()=>CaptureAsync(DesktopCaptureMode.Window));Wire("DisplayButton",()=>CaptureAsync(DesktopCaptureMode.Display));
        Wire("SaveButton",ExportAsync);Wire("UndoButton",()=>{surface.CancelGesture();editor.History?.Undo();return Task.CompletedTask;});Wire("RedoButton",()=>{surface.CancelGesture();editor.History?.Redo();return Task.CompletedTask;});
        Wire("DeleteButton",()=>{surface.CancelGesture();editor.DeleteSelected();return Task.CompletedTask;});Wire("DuplicateButton",()=>{surface.CancelGesture();editor.DuplicateSelected();return Task.CompletedTask;});
        Wire("ApplyTextButton",()=>{editor.EditSelectedText(Control<TextBox>("TextEntry").Text??"");return Task.CompletedTask;});
        Wire("ZoomInButton",()=>{surface.Zoom*=1.25;UpdateCommands();return Task.CompletedTask;});Wire("ZoomOutButton",()=>{surface.Zoom/=1.25;UpdateCommands();return Task.CompletedTask;});Wire("FitButton",()=>{Fit();return Task.CompletedTask;});
        var theme=Control<ComboBox>("ThemeBox");theme.SelectedIndex=preferences.Dark?1:0;ApplyTheme(theme.SelectedIndex==1);
        theme.SelectionChanged+=(_,_)=>{ApplyTheme(theme.SelectedIndex==1);try{var next=preferences with{Dark=theme.SelectedIndex==1};next.Save(dataDirectory);preferences=next;}catch(Exception error){Status("테마 저장 실패: "+error.Message);}};
        InitializeHistoryFeatures();InitializeParityFeatures();
        AddHandler(KeyDownEvent,HandleKey,RoutingStrategies.Bubble);Closing+=WindowClosing;Deactivated+=(_,_)=>surface.CancelGesture();
        Closed+=(_,_)=>Dispose();
        Opened+=async (_,_)=>{InitializeHotkeys();await RefreshHistoryAsync();if(editor.Document is not null)Fit();};UpdateCommands();
    }
    private T Control<T>(string name) where T:Control=>this.FindControl<T>(name)??throw new InvalidOperationException(name);
    private void Wire(string name,Func<Task> action)=>Control<Button>(name).Click+=async (_,_)=>await RunAsync(action);
    public async Task RunAsync(Func<Task> action)
    {
        if(busy)return;EndPropertyEditGroup();historyOpenVersion++;surface.CancelGesture();SetBusy(true);
        try{await action();}catch(OperationCanceledException){Status("취소됨");}catch(Exception error){Status("작업 실패: "+error.Message);}finally{SetBusy(false);}
    }
    public void SetBusy(bool value)
    {busy=value;Control<TabControl>("RibbonTabs").IsEnabled=!value;Control<Button>("HistoryToggleButton").IsEnabled=!value;Control<WrapPanel>("CommandPanel").IsEnabled=!value;Control<WrapPanel>("PropertiesPanel").IsEnabled=!value;Control<Border>("HistoryPanel").IsEnabled=!value;surface.IsEnabled=!value;if(stickerPicker is not null)stickerPicker.IsEnabled=!value;UpdateCommands();}
    public void ApplyTheme(bool dark){RequestedThemeVariant=dark?ThemeVariant.Dark:ThemeVariant.Light;if(stickerPicker is not null)stickerPicker.RequestedThemeVariant=RequestedThemeVariant;}
    private void Status(string value)=>Control<TextBlock>("StatusText").Text=value;
    private void EditorChanged(object? sender,EventArgs args)
    {
        if(editor.Document is not null&&editor.History is not null)autosave.Queue(editor.Snapshot(),editor.History.CurrentStateToken);
        RefreshSelection();
    }
    private void UpdateCommands()
    {
        var has=editor.Document is not null;
        foreach(var name in new[]{"SaveButton","AddImageButton","RenameButton","ApplyTextButton","FitButton","ZoomInButton","ZoomOutButton"})Control<Button>(name).IsEnabled=!busy&&has;
        Control<Button>("UndoButton").IsEnabled=!busy&&editor.History?.CanUndo==true;Control<Button>("RedoButton").IsEnabled=!busy&&editor.History?.CanRedo==true;
        Control<Button>("DuplicateButton").IsEnabled=Control<Button>("DeleteButton").IsEnabled=!busy&&editor.Selected is not null;
        Control<WrapPanel>("PropertiesPanel").IsEnabled=!busy&&has;Control<StackPanel>("EmptyPanel").IsVisible=!has;
        Control<Button>("CopyButton").IsEnabled=!busy&&has;Control<Button>("PasteButton").IsEnabled=!busy;UpdateStickerCommands();
        Control<TextBlock>("ZoomText").Text=$"{surface.Zoom:P0}";
        UpdateParityCommands();UpdateHistoryCommands();
        Title=has?$"SnapBerry — {(editor.Name.Length>0?editor.Name:"이름 없는 캡처")}":"SnapBerry";
    }
    private async Task FlushAsync(){await autosave.FlushAsync();if(editor.Document is not null)await editor.SaveAsync(store);}
    private async Task OpenImageAsync(bool insert)
    {
        var files=await StorageProvider.OpenFilePickerAsync(new(){Title=insert?"추가할 이미지 선택":"이미지 열기",AllowMultiple=false,FileTypeFilter=[FilePickerFileTypes.ImageAll]});
        if(files.Count==0)return;await using var input=await files[0].OpenReadAsync();if(input.Length>128L*1024*1024)throw new InvalidDataException("128MB 이하 이미지를 선택해 주세요.");
        using var data=new MemoryStream();await input.CopyToAsync(data);
        if(insert&&editor.Document is not null)editor.InsertImage(data.ToArray());
        else{await FlushAsync();editor.OpenImage(data.ToArray());Control<TextBox>("NameEntry").Text="";Control<TextBox>("SearchBox").Text="";Fit();}
        await FlushAsync();await RefreshHistoryAsync();Status(insert?"이미지 추가됨":"이미지 열림");
    }
    private async Task CaptureAsync(DesktopCaptureMode mode)
    {
        var success=await capture.CaptureAsync(mode,editor,FlushAsync,HideForCapture,RestoreAfterCapture);
        if(!success){Status("캡처 취소됨");return;}
        Control<TextBox>("SearchBox").Text="";Control<TextBox>("NameEntry").Text="";Fit();await FlushAsync();await RefreshHistoryAsync();Status("캡처 완료");
    }
    private async Task ExportAsync()
    {
        if(editor.Document is null)return;
        var file=await StorageProvider.SaveFilePickerAsync(new(){Title="PNG 저장",DefaultExtension="png",SuggestedFileName=ExportFileName.Suggest(editor.Name,DateTime.Now),SuggestedStartLocation=await GetExportDirectoryAsync(),FileTypeChoices=[FilePickerFileTypes.ImagePng],ShowOverwritePrompt=true});
        if(file is null)return;var path=file.TryGetLocalPath()??throw new IOException("로컬 폴더에 저장해 주세요.");await AtomicPngWriter.WriteAsync(path,editor.RenderPng());
        RememberExportDirectory(Path.GetDirectoryName(path)!);
        editor.History!.MarkExported(editor.History.CurrentStateToken);await FlushAsync();await RefreshHistoryAsync();Status("PNG 저장됨");
    }
    private void Fit()
    {if(editor.Document is not AnnotationDocument doc)return;surface.Zoom=Math.Min(1,Math.Min(Math.Max(100,Control<ScrollViewer>("ImageScroll").Bounds.Width-60)/doc.OutputSize.Width,Math.Max(100,Control<ScrollViewer>("ImageScroll").Bounds.Height-60)/doc.OutputSize.Height));UpdateCommands();}
    private void HandleKey(object? sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape){surface.CancelGesture();return;}if(busy)return;
        var textInput=e.Source is TextBox||e.Source is Control c&&c.GetVisualAncestors().OfType<TextBox>().Any();
        var command=(e.KeyModifiers&(KeyModifiers.Meta|KeyModifiers.Control))!=0;
        if(command&&e.Key==Key.O){e.Handled=true;_=RunAsync(()=>OpenImageAsync(false));}
        else if(command&&e.Key==Key.S){e.Handled=true;_=RunAsync(ExportAsync);}
        else if(!textInput&&command&&e.Key==Key.C){e.Handled=true;_=RunAsync(()=>CopyImageAsync(IsHistorySource(e.Source as Control)));}
        else if(!textInput&&command&&e.Key==Key.V){e.Handled=true;_=RunAsync(PasteImageAsync);}
        else if(!textInput&&command&&e.Key==Key.Z){e.Handled=true;surface.CancelGesture();if(e.KeyModifiers.HasFlag(KeyModifiers.Shift))editor.History?.Redo();else editor.History?.Undo();}
        else if(!textInput&&command&&e.Key==Key.D){e.Handled=true;surface.CancelGesture();editor.DuplicateSelected();}
        else if(!textInput&&e.Key is Key.Delete or Key.Back){e.Handled=true;surface.CancelGesture();editor.DeleteSelected();}
    }
    private async void WindowClosing(object? sender,WindowClosingEventArgs e)
    {
        if(allowClose)return;e.Cancel=true;if(busy){Status("작업이 끝난 뒤 다시 종료해 주세요.");return;}
        await RunAsync(async ()=>{await FlushAsync();stickerPicker?.Hide();Hide();});
    }
    public void Dispose(){if(closed)return;allowClose=true;closed=true;if(hotkeys is not null){hotkeys.Pressed-=OnGlobalCapture;hotkeys.Dispose();}DisposeHistoryFeatures();stickerPicker?.Close();editor.Changed-=EditorChanged;autosave.Dispose();surface.Dispose();editor.Dispose();}
}
