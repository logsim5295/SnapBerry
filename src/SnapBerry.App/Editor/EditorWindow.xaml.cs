using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SnapBerry.Core;
using SnapBerry.App.Library;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.App.Stickers;
using SnapBerry.App.Rendering;
using SnapBerry.App.Import;
namespace SnapBerry.App.Editor;
// 목록 메타데이터에 표시용 썸네일을 결합한다. 실제 편집 데이터는 열 때 저장소에서 읽는다.
public sealed record CaptureEntry(CaptureListItem Item,BitmapSource? Thumbnail)
{public string Caption=>Item.Caption;public string Dimensions=>Item.Dimensions;public string Details=>Item.Details;}
// 리본·속성 패널·이력·캔버스를 조정하는 화면 계층이다. 편집 상태 자체는 Model에 위임한다.
public partial class EditorWindow : Window
{
    public EditorViewModel Model {get;}=new();
    private readonly EditorCanvas canvas;private readonly CaptureLibraryStore store;private readonly AutosaveCoordinator autosave;
    private readonly ScrollPanController imagePan;
    private readonly SettingsService settingsService;private AppSettings settings;
    private bool ready,busy,updatingNumber,updatingProperties,allowClose,refreshing,initialSizeApplied;private int historyPages=1;
    private readonly ObservableCollection<CaptureEntry> historyItems=[];
    private double historyWidth=230;
    private bool hasMoreHistory;private ScrollViewer? historyScroll;
    private Task? libraryRefresh;private bool refreshRequested;private Guid? selectAfterRefresh;
    private bool restoreHistoryFocusAfterBusy;
    private CommandHistory? propertyDragHistory;
    private (EditorTool Tool,Guid? Selection)? propertiesContext;
    private StickerPicker? stickerPicker;
    public Func<CaptureMode,Task>? CaptureRequested {get;set;}public Func<Task>? SettingsRequested {get;set;}
    public bool IsBusy=>busy;
    public Action<string>? DiagnosticStage {get;set;}
    private readonly IExportPathPicker exportPicker;
    private readonly Action<ClipboardImageContent> writeClipboardImage;
    private readonly Func<int,bool> deleteConfirmation;
    private readonly Func<bool> showTrayNotice;private bool trayNoticeAcknowledged;
    private readonly Size preferredStartupSize;
    public EditorWindow(CaptureLibraryStore library,SettingsService settingsStore,IExportPathPicker? picker=null,Func<int,bool>? confirmDelete=null,Func<bool>? confirmTrayHide=null,IImagePathPicker? imagePicker=null,IClipboardImageSource? clipboardImages=null,Func<string,string?>? editHistoryName=null,Action<ClipboardImageContent>? copyImage=null)
    {
        store=library;settingsService=settingsStore;exportPicker=picker??new ExportPathPicker();writeClipboardImage=copyImage??(content=>Clipboard.SetDataObject(ClipboardImageSource.CreateDataObject(content),true));settings=settingsService.Load();InitializeComponent();
        ThemeResources.Initialize(this,settings.Theme);ThemeResources.ApplyToApplication(settings.Theme);
        deleteConfirmation=confirmDelete??(count=>new DeleteCaptureDialog(count){Owner=this,Icon=Icon}.ShowDialog()==true);
        historyNameEditor=editHistoryName??(value=>{var dialog=new TextAnnotationEditor(value,false,CaptureName.MaxLength,"이력 이름 변경",true){Owner=this,Icon=Icon};return dialog.ShowDialog()==true?dialog.Value:null;});
        showTrayNotice=confirmTrayHide??(()=>new TrayNoticeDialog(Icon){Owner=this,Icon=Icon}.ShowDialog()==true);trayNoticeAcknowledged=settings.HasSeenTrayNotice;
        preferredStartupSize=new(Width,Height);
        historyWidth=double.IsFinite(settings.HistoryPanelWidth)&&settings.HistoryPanelWidth>=170?settings.HistoryPanelWidth:230;LibraryColumn.Width=new GridLength(historyWidth);
        HistorySplitter.DragCompleted+=(_,_)=>SaveHistoryWidth();
        using(var stream=typeof(EditorWindow).Assembly.GetManifestResourceStream("SnapBerry.App.ico")!)
        {Icon=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Icon.Freeze();}
        SourceInitialized+=(_,_)=>
        {
            var source=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            source.CompositionTarget.RenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
            DiagnosticStage?.Invoke($"editor.renderTarget.mode={source.CompositionTarget.RenderMode}");
        };
        canvas=new(){Model=Model};CanvasHost.Children.Add(canvas);HistoryList.ItemsSource=historyItems;InitializeRibbonIcons();InitializeLineStyleItems();UpdateCaptureTooltips();ImageScroll.PreviewMouseWheel+=ImageMouseWheel;
        imagePan=new(ImageScroll,()=>!busy&&Model.Document is not null,canvas.CancelGesture);
        InitializeImageImport(imagePicker??new ImagePathPicker(),clipboardImages??new ClipboardImageSource());
        foreach(var slider in new[]{ThicknessSlider,MosaicSlider})
        {slider.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler(PropertyDragStarted));slider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler(PropertyDragCompleted));}
        autosave=new(store.SaveAsync);
        autosave.Saved+=(id,token)=>Dispatcher.BeginInvoke(()=>{if(Model.CaptureId==id){Model.History?.MarkPersisted(token);StatusText.Text="이력 저장됨";}_=RefreshLibraryAsync();});
        autosave.Failed+=e=>Dispatcher.BeginInvoke(()=>StatusText.Text="이력 저장 실패: "+e.Message);
        // 문서 변경은 화면 갱신과 자동 저장 예약으로 이어진다. 선택 변경은 별도 이벤트로 구분한다.
        Model.Changed+=(_,_)=>
        {
            try{imagePan.Cancel();canvas.Refresh();UpdateCommands();UpdateNumber();UpdateProperties();if(Model.History is not null){autosave.Queue(Model.Snapshot(),Model.History.CurrentStateToken);StatusText.Text="저장 중…";}}
            catch(Exception e){StatusText.Text=e.Message;}
        };
        Model.MagicWandChanged+=(_,_)=>{canvas.Refresh();UpdateCommands();UpdateProperties();if(Model.Tool==EditorTool.MagicWand)StatusText.Text=Model.HasPixelSelection?$"{Model.MagicWandCount:N0} 픽셀 선택됨 · Delete로 지우기 · Esc로 취소":"마술봉: 원본 이미지의 색상을 클릭하세요.";};
        canvas.TextRequested+=EditText;canvas.SelectionChanged+=()=>{UpdateProperties();UpdateCommands();};ready=true;UpdateCommands();UpdateProperties();
        Loaded+=async(_,_)=>{FitInitialWindow();DiagnosticStage?.Invoke("library.load.begin");await SafeAsync(RefreshLibraryAsync);DiagnosticStage?.Invoke("library.load.end");};Closing+=WindowClosing;PreviewKeyDown+=WindowKeyDown;
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)imagePan.Cancel();UpdateStickerPickerVisibility();};Deactivated+=(_,_)=>imagePan.Cancel();
    }
    private void FitInitialWindow()
    {
        // 모니터 작업 영역의 물리 픽셀을 DIP로 환산해 높은 배율에서도 창 크기를 제한한다.
        if(initialSizeApplied)return;initialSizeApplied=true;
        var hwnd=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var info=new SnapBerry.App.Interop.NativeMethods.MonitorInfo{Size=System.Runtime.InteropServices.Marshal.SizeOf<SnapBerry.App.Interop.NativeMethods.MonitorInfo>()};
        if(!SnapBerry.App.Interop.NativeMethods.GetMonitorInfo(SnapBerry.App.Interop.NativeMethods.MonitorFromWindow(hwnd,2),ref info))return;
        var dpi=VisualTreeHelper.GetDpi(this);var work=info.Work;
        var size=InitialWindowSize.Calculate(preferredStartupSize.Width,preferredStartupSize.Height,work.Right-work.Left,work.Bottom-work.Top,dpi.DpiScaleX,dpi.DpiScaleY);
        MinWidth=Math.Min(MinWidth,size.Width);MinHeight=Math.Min(MinHeight,size.Height);Width=size.Width;Height=size.Height;
        LibraryColumn.Width=new GridLength(Math.Min(historyWidth,Math.Max(170,Width-230)));
        // 음수 모니터 원점에 배율이 두 번 적용되지 않도록 최종 위치는 네이티브 좌표로 지정한다.
        if(SnapBerry.App.Interop.NativeMethods.GetWindowRect(hwnd,out var rect))
            SnapBerry.App.Interop.NativeMethods.SetWindowPos(hwnd,IntPtr.Zero,work.Left+(work.Right-work.Left-rect.Right+rect.Left)/2,work.Top+(work.Bottom-work.Top-rect.Bottom+rect.Top)/2,0,0,0x15);
        DiagnosticStage?.Invoke($"editor.initialSize width={Width:F1} height={Height:F1} dpiX={dpi.DpiScaleX:F2} dpiY={dpi.DpiScaleY:F2}");
    }
    private async Task SafeAsync(Func<Task> action)
    {try{await action();}catch(Exception e){StatusText.Text=e.Message;MessageBox.Show(this,e.Message,"SnapBerry",MessageBoxButton.OK,MessageBoxImage.Warning);}}
    public async Task FlushAsync(){imagePan.Cancel();EndPropertyDrag();canvas.CancelGesture();await autosave.FlushAsync();}
    public void SetBusy(bool value)
    {
        // 목록을 비활성화하면 WPF가 키보드 포커스를 다른 곳으로 옮긴다.
        // 작업 중 목록 항목이 교체될 수도 있으므로 비활성화 전에 출처를 기억한다.
        if(value&&!busy)restoreHistoryFocusAfterBusy=HistoryList.IsKeyboardFocusWithin||IsHistoryCopySource(FocusManager.GetFocusedElement(this) as DependencyObject);
        var restoreFocus=!value&&busy&&restoreHistoryFocusAfterBusy;
        if(value){imagePan.Cancel();EndPropertyDrag();}busy=value;EmptyRegionCaptureButton.IsEnabled=RegionCaptureButton.IsEnabled=WindowCaptureButton.IsEnabled=DesktopCaptureButton.IsEnabled=!value;MainRibbon.IsEnabled=!value;canvas.IsEnabled=!value;HistoryList.IsEnabled=HistorySearchPanel.IsEnabled=!value;UpdateCommands();UpdateStickerPickerVisibility();
        // 비동기 작업이 끝나면 다음 사용자 입력을 기다리지 않고 명령 버튼의 활성화 상태를 갱신한다.
        CommandManager.InvalidateRequerySuggested();
        if(!value)restoreHistoryFocusAfterBusy=false;
        if(restoreFocus)RestoreHistoryFocus();
    }
    public async Task OpenNewAsync(CaptureRecord record)
    {
        ShowNewRecord(record);autosave.Queue(Model.Snapshot(),Model.History!.CurrentStateToken);await autosave.FlushAsync();await RefreshLibraryAsync();
    }
    private void ShowNewRecord(CaptureRecord record)
    {EndPropertyDrag();Model.Open(record);HistoryList.SelectedItems.Clear();HistorySearchBox.Clear();selectAfterRefresh=record.Id;Fit();}
    public async Task OpenRecordAsync(Guid id)
    {
        // 다른 이력을 열기 전에 현재 문서의 저장을 완료한다. 같은 문서를 다시 열어 수정을 덮지 않는다.
        if(busy||Model.CaptureId==id)return;SetBusy(true);try{await OpenRecordCoreAsync(id);}finally{SetBusy(false);}
    }
    private async Task OpenRecordCoreAsync(Guid id)
    {
        if(Model.CaptureId==id)return;
        await FlushAsync();var record=await store.LoadAsync(id);Model.Open(record);Fit();StatusText.Text="이력 열림";
    }
    private async Task SelectHistoryEntryAsync(CaptureEntry entry)
    {
        await OpenRecordCoreAsync(entry.Item.Id);
        var current=historyItems.FirstOrDefault(x=>x.Item.Id==entry.Item.Id);
        HistoryList.SelectedItem=current;if(current is not null)HistoryList.ScrollIntoView(current);
    }
    private void RestoreHistoryFocus()
    {
        if(!HistoryList.IsEnabled||HistoryPanel.Visibility!=Visibility.Visible)return;
        HistoryList.UpdateLayout();
        var target=(HistoryList.SelectedItem is {} selected?HistoryList.ItemContainerGenerator.ContainerFromItem(selected) as UIElement:null)??HistoryList;
        FocusManager.SetFocusedElement(this,target);
        // 다른 앱으로 전환했거나 트레이로 숨긴 동안에는 실제 키보드 포커스를 가져오지 않는다.
        if(IsActive&&IsVisible)target.Focus();
    }
    private Task RefreshLibraryAsync()
    {
        // 진행 중인 목록 갱신도 기다린다. 겹친 요청은 마지막 상태로 한 번 더 읽어 반영한다.
        refreshRequested=true;if(refreshing)return libraryRefresh!;
        refreshing=true;return libraryRefresh=RefreshLibraryLoopAsync();
    }
    private async Task RefreshLibraryLoopAsync()
    {
        try{while(refreshRequested){refreshRequested=false;await RefreshLibraryPageAsync();}}
        finally{refreshing=false;}
    }
    private async Task RefreshLibraryPageAsync()
    {
        // 페이지 단위로 읽되 기존 선택과 스크롤 위치를 보존하고 가능한 썸네일은 재사용한다.
        try
        {
            var offset=historyScroll?.VerticalOffset??0;var lastPageCount=0;
            var version=historySearchVersion;var query=HistorySearchBox.Text.Trim();
            var old=historyItems.ToDictionary(x=>x.Item.Id);var items=new List<CaptureEntry>();
            for(var page=0;page<historyPages;page++)
            {
                var entries=await store.ListAsync(page*50,50,query);if(version!=historySearchVersion)return;lastPageCount=entries.Count;foreach(var entry in entries)
                {
                    if(version!=historySearchVersion)return;
                    BitmapSource? thumbnail=null;
                    if(entry.Id==Model.CaptureId&&Model.Composite() is BitmapSource image)thumbnail=ThumbnailRenderer.Create(image);
                    else if(old.TryGetValue(entry.Id,out var cached)&&cached.Item==entry)thumbnail=cached.Thumbnail;
                    else if(!entry.Damaged)
                    {try{var record=await store.LoadAsync(entry.Id);if(version!=historySearchVersion)return;var vm=new EditorViewModel();vm.Open(record);thumbnail=ThumbnailRenderer.Create(vm.Composite()!);}catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException){}}
                    items.Add(new(entry,thumbnail));
                }
                if(entries.Count<50)break;
            }
            if(version!=historySearchVersion)return;
            // 디스크를 읽는 동안 사용자가 바꾼 선택도 마지막 시점의 값으로 보존한다.
            var selected=HistoryList.SelectedItems.Cast<CaptureEntry>().Select(x=>x.Item.Id).ToHashSet();
            if(selectAfterRefresh is Guid target)selected=[target];
            var restoreHistoryFocus=!busy&&(HistoryList.IsKeyboardFocusWithin||IsHistoryCopySource(FocusManager.GetFocusedElement(this) as DependencyObject));
            historyItems.Clear();foreach(var item in items){historyItems.Add(item);if(selected.Contains(item.Item.Id))HistoryList.SelectedItems.Add(item);}
            if(selectAfterRefresh is Guid id&&(query.Length>0||items.Any(x=>x.Item.Id==id)))selectAfterRefresh=null;
            HistoryEmptyText.Text=query.Length>0?"검색 결과가 없습니다.":"캡처 이력이 없습니다.";
            HistoryEmptyText.Visibility=items.Count==0?Visibility.Visible:Visibility.Collapsed;
            hasMoreHistory=lastPageCount==50;historyScroll?.ScrollToVerticalOffset(offset);
            if(restoreHistoryFocus)RestoreHistoryFocus();
            // 아직 문서와 선택이 없으면 최신의 열 수 있는 캡처를 기본으로 표시한다.
            if(!busy&&Model.CaptureId is null&&selected.Count==0&&selectAfterRefresh is null&&query.Length==0
                &&items.FirstOrDefault(x=>!x.Item.Damaged&&x.Thumbnail is not null) is CaptureEntry latest)
            {
                SetBusy(true);try{await SelectHistoryEntryAsync(latest);}finally{SetBusy(false);}
            }
        }
        catch(Exception e){StatusText.Text="이력 목록: "+e.Message;}
    }
    private void UpdateCommands()
    {
        // 문서 유무·선택 종류·작업 중 상태에 따라 의미 있는 명령만 활성화한다.
        if(!ready)return;
        var hasDocument=Model.Document is not null;
        MagicWandButton.IsEnabled=!busy&&hasDocument;DeleteMagicWandButton.IsEnabled=!busy&&Model.HasPixelSelection;
        ImageSizeButton.IsEnabled=!busy&&hasDocument;
        ImageSizeButton.Content=Model.Document is { } document?$"{document.OutputSize.Width} × {document.OutputSize.Height} px":"이미지 없음";
        ToolsPanel.IsEnabled=!busy&&hasDocument;PropertiesGroup.IsEnabled=PropertiesPanel.IsEnabled=!busy&&hasDocument&&Model.Tool!=EditorTool.Crop;
        if(!PropertiesPanel.IsEnabled){ColorMenuButton.IsDropDownOpen=false;ThicknessMenuButton.IsDropDownOpen=false;}
        if(!hasDocument)CloseStickerPicker();
        EmptyEditorState.Visibility=hasDocument?Visibility.Collapsed:Visibility.Visible;
        UndoButton.IsEnabled=!busy&&Model.History?.CanUndo==true;RedoButton.IsEnabled=!busy&&Model.History?.CanRedo==true;
        CopyButton.IsEnabled=SaveButton.IsEnabled=!busy&&hasDocument;DeleteButton.IsEnabled=!busy&&(Model.Selected is not null||Model.HasPixelSelection);EditButton.IsEnabled=!busy&&CanEditContent(Model.Selected);ZoomText.Text=$"{Model.Zoom:P0}";
    }
    private void UpdateProperties()
    {
        // 선택 객체에 지원되는 속성만 표시하고, 값 동기화 중에는 변경 이벤트의 재진입을 막는다.
        if(!ready)return;var t=Model.Tool;
        var context=(t,Model.SelectedId);
        if(propertiesContext!=context){ColorMenuButton.IsDropDownOpen=false;ThicknessMenuButton.IsDropDownOpen=false;EndPropertyDrag();propertiesContext=context;}
        var editsColor=t==EditorTool.Select?Model.Selected?.Kind is AnnotationKind.Arrow or AnnotationKind.Rectangle or AnnotationKind.Text or AnnotationKind.StepMarker:t is EditorTool.Arrow or EditorTool.Rectangle or EditorTool.Text or EditorTool.Number;
        var editsThickness=t==EditorTool.Select?Model.Selected?.Kind is AnnotationKind.Arrow or AnnotationKind.Rectangle:t is EditorTool.Arrow or EditorTool.Rectangle;
        ColorControls.Visibility=editsColor?Visibility.Visible:Visibility.Collapsed;
        CurrentColorControls.Visibility=ColorControls.Visibility;
        ThicknessControls.Visibility=editsThickness?Visibility.Visible:Visibility.Collapsed;
        CurrentColorControls.IsEnabled=editsColor;
        ThicknessControls.IsEnabled=t!=EditorTool.Select||Model.Selected?.Kind is AnnotationKind.Arrow or AnnotationKind.Rectangle;
        var editsLine=t is EditorTool.Arrow or EditorTool.Rectangle||(t==EditorTool.Select&&Model.Selected?.Kind is AnnotationKind.Arrow or AnnotationKind.Rectangle);
        LineStyleControls.Visibility=editsLine?Visibility.Visible:Visibility.Collapsed;LineStyleControls.IsEnabled=editsLine;
        FontControls.Visibility=t==EditorTool.Text||(t==EditorTool.Select&&Model.Selected?.Kind==AnnotationKind.Text)?Visibility.Visible:Visibility.Collapsed;MosaicControls.Visibility=t==EditorTool.Mosaic||(t==EditorTool.Select&&Model.Selected?.Kind==AnnotationKind.Mosaic)?Visibility.Visible:Visibility.Collapsed;
        MagicWandControls.Visibility=t==EditorTool.MagicWand?Visibility.Visible:Visibility.Collapsed;
        NumberControls.Visibility=t==EditorTool.Number?Visibility.Visible:Visibility.Collapsed;StickerChooseButton.Visibility=t==EditorTool.Sticker?Visibility.Visible:Visibility.Collapsed;
        NumberTextColorButton.Visibility=t==EditorTool.Number||(t==EditorTool.Select&&Model.Selected?.Kind==AnnotationKind.StepMarker)?Visibility.Visible:Visibility.Collapsed;
        PropertiesGroup.Visibility=Model.Document is not null&&PropertiesPanel.Children.OfType<UIElement>().Any(child=>child.Visibility==Visibility.Visible)?Visibility.Visible:Visibility.Collapsed;
        updatingProperties=true;var style=EditsSelection?Model.Selected?.Style??Model.Style:Model.Style;ThicknessSlider.Value=style.Thickness;
        ThicknessMenuButton.Label=$"두께 {style.Thickness:0.#}px";
        LineStyleBox.SelectedIndex=(int)style.LineStyle;
        CurrentColorSwatch.Fill=(Brush)new BrushConverter().ConvertFromString(style.Color)!;CurrentColorText.Text=style.Color;
        ColorMenuButton.ToolTip=$"색상 {style.Color}";
        var swatch=new DrawingImage(new GeometryDrawing(CurrentColorSwatch.Fill,new Pen(Brushes.Gray,1),new RectangleGeometry(new Rect(1,1,14,14),3,3)));swatch.Freeze();
        ColorMenuButton.SmallImageSource=ColorMenuButton.LargeImageSource=swatch;
        foreach(var button in ColorControls.Children.OfType<Button>().Where(b=>b.Tag is string))
        {var active=string.Equals((string)button.Tag,style.Color,StringComparison.OrdinalIgnoreCase);button.BorderBrush=ThemeResources.Get(this,active?"Accent":"InputBorder");button.BorderThickness=new Thickness(active?3:1);}
        FontSizeBox.SelectedItem=FontSizeBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item=>double.Parse((string)item.Content)==style.FontSize);
        FontSizeBox.Text=style.FontSize.ToString(System.Globalization.CultureInfo.CurrentCulture);
        MosaicSlider.Value=Model.Selected?.Kind==AnnotationKind.Mosaic?Model.Selected.MosaicBlockSize:Model.MosaicSize;updatingProperties=false;
    }
    private void UpdateNumber()
    {
        if(!ready||Model.Document is null)return;updatingNumber=true;var s=Model.Document.Sequence;NumberModeBox.SelectedIndex=(int)s.Mode;NextNumberBox.Text=StepLabelFormatter.Format(s.GetNextValue(),s.Mode);updatingNumber=false;
    }
    private void ToolClick(object sender,RoutedEventArgs e)
    {
        if(busy||Model.Document is null||sender is not ToggleButton button)return;EndPropertyDrag();canvas.CancelGesture();Model.ClearMagicWand();Model.Tool=Enum.Parse<EditorTool>((string)button.Tag);foreach(var item in ToolsPanel.Items.OfType<ToggleButton>())item.IsChecked=item==button;
        if(Model.Tool is EditorTool.Crop or EditorTool.MagicWand)Model.SelectedId=null;
        UpdateCommands();UpdateProperties();canvas.Cursor=Model.Tool is EditorTool.Select or EditorTool.Crop?Cursors.Arrow:Cursors.Cross;canvas.InvalidateVisual();
        if(Model.Tool==EditorTool.Sticker)ChooseStickerClick(sender,e);else CloseStickerPicker();
    }
    private async void CaptureClick(object sender,RoutedEventArgs e)=>await SafeAsync(async()=>{if(!busy&&CaptureRequested is not null&&sender is FrameworkElement{Tag:string mode})await CaptureRequested(Enum.Parse<CaptureMode>(mode));});
    private async void SettingsClick(object sender,RoutedEventArgs e)=>await SafeAsync(async()=>{if(SettingsRequested is not null)await SettingsRequested();});
    private void AboutClick(object sender,RoutedEventArgs e)
    {if(busy)return;SetBusy(true);try{new AboutWindow{Owner=this,Icon=Icon}.ShowDialog();}finally{SetBusy(false);}}
    private void UndoClick(object sender,RoutedEventArgs e){canvas.CancelGesture();Model.History?.Undo();}private void RedoClick(object sender,RoutedEventArgs e){canvas.CancelGesture();Model.History?.Redo();}
    private void DeleteAnnotationClick(object sender,RoutedEventArgs e)=>DeleteSelectionSafely();
    private static bool CanEditContent(Annotation? annotation)=>annotation is not null&&(annotation.Kind is AnnotationKind.Text or AnnotationKind.StepMarker||(annotation.Kind==AnnotationKind.Sticker&&annotation.StickerId.StartsWith("key-")));
    private void EditAnnotationClick(object sender,RoutedEventArgs e){if(Model.Selected is Annotation a&&CanEditContent(a))EditText(new(a.X,a.Y),a);}
    private void EditText(PixelPoint position,Annotation? annotation)
    {
        // 일반 텍스트·번호·키보드 스티커는 같은 입력 창을 쓰되 각 모델의 갱신 방식으로 전달한다.
        if(busy)return;var id=Model.CaptureId;SetBusy(true);
        try
        {
            var dialog=new TextAnnotationEditor(annotation?.Text??"",annotation is null||annotation.Kind==AnnotationKind.Text,annotation?.Kind==AnnotationKind.Sticker?64:AnnotationLimits.MaxTextLength){Owner=this};if(dialog.ShowDialog()!=true||Model.CaptureId!=id)return;
            if(annotation?.Kind==AnnotationKind.Sticker){Model.UpdateKeycapLabel(annotation.Id,dialog.Value);return;}
            if(annotation is not null)Model.UpdateText(annotation.Id,dialog.Value);
            else
            {
                var text=new FormattedText(dialog.Value,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),Model.Style.FontSize,Brushes.Black,1);
                var a=new Annotation{Kind=AnnotationKind.Text,X=position.X,Y=position.Y,Width=text.Width,Height=text.Height,Text=dialog.Value,Style=Model.Style};Model.SelectedId=a.Id;Model.History!.Add(a);
            }
        }
        catch(Exception ex){StatusText.Text=ex.Message;}
        finally{SetBusy(false);}
    }
    private bool EditsSelection=>Model.Tool==EditorTool.Select||(Model.Tool==EditorTool.Arrow&&Model.Selected?.Kind==AnnotationKind.Arrow)||(Model.Tool==EditorTool.Rectangle&&Model.Selected?.Kind==AnnotationKind.Rectangle)||(Model.Tool==EditorTool.Number&&Model.Selected?.Kind==AnnotationKind.StepMarker)||(Model.Tool==EditorTool.Text&&Model.Selected?.Kind==AnnotationKind.Text);
    // 기존 객체를 편집하는 상황이면 명령 이력에 기록하고, 아니면 새 객체의 기본값을 바꾼다.
    private void ApplyStyle(Func<AnnotationStyle,AnnotationStyle> update)
    {if(EditsSelection)Model.UpdateSelectedStyle(update);else Model.Style=update(Model.Style);UpdateProperties();}
    private void PickColorClick(object sender,RoutedEventArgs e)
    {
        if(busy||!ColorPickerButton.IsEnabled)return;canvas.CancelGesture();SetBusy(true);
        try
        {
            ColorMenuButton.IsDropDownOpen=false;
            var color=(Color)ColorConverter.ConvertFromString(EditsSelection?Model.Selected?.Style.Color??Model.Style.Color:Model.Style.Color);
            var picker=new ColorPickerWindow(color){Owner=this,Icon=Icon};
            if(picker.ShowDialog()==true){var value=picker.SelectedHex;ApplyStyle(s=>s with{Color=value,Fill=value});}
        }
        finally{SetBusy(false);}
    }
    private void ColorClick(object sender,RoutedEventArgs e){if(!ColorControls.IsEnabled)return;var color=(string)((Button)sender).Tag;ApplyStyle(s=>s with{Color=color,Fill=color});ColorMenuButton.IsDropDownOpen=false;}
    private void ThicknessChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(ready&&!updatingProperties&&ThicknessControls.IsEnabled)ApplyStyle(s=>s with{Thickness=e.NewValue});}
    private void PropertyDragStarted(object sender,DragStartedEventArgs e)
    {EndPropertyDrag();propertyDragHistory=Model.History;propertyDragHistory?.BeginGroup();}
    private void PropertyDragCompleted(object sender,DragCompletedEventArgs e)=>EndPropertyDrag();
    private void EndPropertyDrag(){var history=propertyDragHistory;propertyDragHistory=null;history?.EndGroup();}
    private void LineStyleChanged(object sender,SelectionChangedEventArgs e)
    {if(ready&&!updatingProperties&&LineStyleControls.IsEnabled&&LineStyleBox.SelectedItem is ComboBoxItem{Tag:StrokePattern pattern})ApplyStyle(s=>s with{LineStyle=pattern});}
    private void FontChanged(object sender,SelectionChangedEventArgs e){if(ready&&!updatingProperties&&FontSizeBox.SelectedItem is ComboBoxItem item)ApplyStyle(s=>s with{FontSize=double.Parse((string)item.Content)});}
    private void FontSizeKeyDown(object sender,KeyEventArgs e)
    {if(e.Key==Key.Enter){ApplyFontSize();e.Handled=true;}else if(e.Key==Key.Escape){UpdateProperties();e.Handled=true;}}
    private void FontSizeLostFocus(object sender,KeyboardFocusChangedEventArgs e)
    {if(!FontSizeBox.IsKeyboardFocusWithin&&!FontSizeBox.IsDropDownOpen)ApplyFontSize();}
    private void ApplyFontSize()
    {
        // 직접 입력은 Enter나 포커스 이동 시 확정한다. 저장 형식이 허용하는 유한한 크기만 적용한다.
        if(!ready||updatingProperties||busy||!FontSizeBox.IsEnabled||FontControls.Visibility!=Visibility.Visible)return;
        if(double.TryParse(FontSizeBox.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.CurrentCulture,out var size)&&double.IsFinite(size)&&size>=1&&size<=4096)
        {
            var current=EditsSelection?Model.Selected?.Style??Model.Style:Model.Style;
            if(current.FontSize!=size)ApplyStyle(s=>s with{FontSize=size});else UpdateProperties();
        }
        else{StatusText.Text="글자 크기는 1부터 4096 사이의 숫자로 입력해 주세요.";UpdateProperties();}
    }
    private void NumberTextColorClick(object sender,RoutedEventArgs e)=>ApplyStyle(s=>s with{TextColor=s.TextColor=="#FFFFFF"?"#252C33":"#FFFFFF"});
    private void MosaicChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {if(!ready||updatingProperties)return;Model.MosaicSize=(int)e.NewValue;if(Model.Selected is Annotation a&&a.Kind==AnnotationKind.Mosaic)Model.History?.Replace(a with{MosaicBlockSize=Model.MosaicSize});}
    private void NumberModeChanged(object sender,SelectionChangedEventArgs e){if(!ready||updatingNumber||Model.Document is null||!Enum.IsDefined((StepSequenceMode)NumberModeBox.SelectedIndex))return;Model.History?.SetSequence(Model.Document.Sequence with{Mode=(StepSequenceMode)NumberModeBox.SelectedIndex});}
    private void NextNumberChanged(object sender,KeyboardFocusChangedEventArgs e)=>ApplyNextNumber();
    private void NextNumberKeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter){ApplyNextNumber();e.Handled=true;}}
    private void ApplyNextNumber()
    {if(!ready||updatingNumber||Model.Document is null)return;try{var s=Model.Document.Sequence;var value=StepLabelFormatter.Parse(NextNumberBox.Text,s.Mode);var next=s.WithNextValue(value);if(s!=next)Model.History?.SetSequence(next);}catch(Exception){StatusText.Text="다음 순번을 확인해 주세요.";UpdateNumber();}}
    private void ChooseStickerClick(object sender,RoutedEventArgs e)
    {
        if(busy||Model.Document is null||Model.Tool!=EditorTool.Sticker)return;
        if(stickerPicker is not null){stickerPicker.Show();stickerPicker.Activate();return;}
        var picker=new StickerPicker(Model.SelectedSticker){Owner=this,Icon=Icon,WindowStartupLocation=WindowStartupLocation.Manual};
        picker.Left=Left+Math.Max(0,ActualWidth-picker.Width-16);picker.Top=Top+Math.Max(0,Math.Min(180,ActualHeight-picker.Height-32));
        picker.StickerSelected+=definition=>{if(busy||Model.Tool!=EditorTool.Sticker)return;canvas.CancelGesture();Model.SelectedSticker=definition;Model.SelectedId=null;StickerChooseButton.Content=definition.Name;canvas.InvalidateVisual();UpdateCommands();};
        picker.Closed+=(_,_)=>{if(stickerPicker==picker)stickerPicker=null;};stickerPicker=picker;picker.Show();
    }
    private void CloseStickerPicker(){var picker=stickerPicker;stickerPicker=null;picker?.Close();}
    private void UpdateStickerPickerVisibility()
    {if(stickerPicker is null)return;if(IsVisible&&!busy&&Model.Document is not null&&Model.Tool==EditorTool.Sticker)stickerPicker.Show();else stickerPicker.Hide();}
    private async void CopyClick(object sender,RoutedEventArgs e)=>await SafeAsync(CopyAsync);
    public async Task CopyAsync()
    {
        if(busy||Model.Document is null)return;SetBusy(true);try
        {canvas.CancelGesture();var image=Model.Composite()!;var token=Model.History!.CurrentStateToken;await WriteClipboardImageAsync(image);Model.History.MarkExported(token);StatusText.Text=Model.History.IsPersistenceDirty?"복사됨 · 이력 저장 대기 중":"클립보드에 복사됨";}
        finally{SetBusy(false);}
    }
    private async void SaveClick(object sender,RoutedEventArgs e)=>await SafeAsync(SaveAsync);
    public async Task SaveAsync()
    {
        if(busy||Model.Document is null)return;SetBusy(true);
        try
        {
            canvas.CancelGesture();var id=Model.CaptureId;
            var fileName=ExportPathPicker.SuggestFileName(Model.Name,DateTime.Now,settings.LastSaveFormat);
            var path=exportPicker.Pick(this,settings.LastSaveDirectory,fileName);
            if(path is null||id!=Model.CaptureId)return;
            // 직접 입력한 확장자도 같은 규칙으로 처리하여 파일명과 실제 형식이 어긋나지 않게 한다.
            var format=ImageFileWriter.GetFormat(path);var token=Model.History!.CurrentStateToken;
            var image=Model.Composite()!;var bytes=await Task.Run(()=>ImageFileWriter.Encode(image,format));
            await ImageFileWriter.WriteAsync(path,bytes,true);Model.History.MarkExported(token);
            settings=settings with{LastSaveDirectory=Path.GetDirectoryName(path)!,LastSaveFormat=format};
            try{settingsService.Save(settings);}catch(IOException){}
            var saved=$"{ImageFileWriter.GetDisplayName(format)} 저장됨";
            StatusText.Text=Model.History.IsPersistenceDirty?saved+" · 이력 저장 대기 중":saved;
        }
        finally{SetBusy(false);}
    }
    private async void HistorySingleClick(object sender,MouseButtonEventArgs e)
    {
        // 수정키를 누른 다중 선택이나 내부 버튼 클릭은 문서 열기로 처리하지 않는다.
        if(Keyboard.Modifiers!=ModifierKeys.None||busy)return;
        for(var node=e.OriginalSource as DependencyObject;node is not null&&node is not ListBoxItem;node=node is Visual?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node))if(node is ButtonBase)return;
        if(e.OriginalSource is DependencyObject origin&&ItemsControl.ContainerFromElement(HistoryList,origin) is ListBoxItem container&&container.DataContext is CaptureEntry item)await SafeAsync(()=>OpenRecordAsync(item.Item.Id));
    }
    private async void HistoryKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.F2&&HistoryList.SelectedItems.Count==1&&HistoryList.SelectedItem is CaptureEntry named&&!named.Item.Damaged){e.Handled=true;await SafeAsync(()=>RenameHistoryAsync(named.Item.Id));}
        else if(e.Key==Key.Enter&&HistoryList.SelectedItem is CaptureEntry item){e.Handled=true;await SafeAsync(()=>OpenRecordAsync(item.Item.Id));}
        else if(e.Key==Key.Delete){e.Handled=true;await SafeAsync(DeleteHistoryAsync);}
    }
    private void HistoryItemLoaded(object sender,RoutedEventArgs e)
    {
        // 가상화된 목록 항목의 현재 DataContext를 참조하여 재사용된 컨테이너도 올바른 이력을 가리킨다.
        if(sender is not ListBoxItem container||container.ContextMenu is not null)return;
        var menu=new ContextMenu{Style=(Style)FindResource("PurpleHistoryContextMenuStyle")};menu.SetBinding(DataContextProperty,new System.Windows.Data.Binding("PlacementTarget.DataContext"){RelativeSource=new(System.Windows.Data.RelativeSourceMode.Self)});
        // 리본과 같은 아이콘 생성기를 사용해 메뉴에서도 색상과 형태를 일치시킨다.
        var delete=new MenuItem{Header="삭제",InputGestureText="Delete",Icon=new Image{Source=RibbonIcons.Create("Delete"),Width=18,Height=18,Tag="Delete"}};delete.Click+=DeleteHistoryMenuClick;
        var duplicate=new MenuItem{Header="복제",Icon=new Image{Source=RibbonIcons.Create("Copy"),Width=18,Height=18,Tag="Copy"}};duplicate.Click+=DuplicateHistoryMenuClick;
        var rename=new MenuItem{Header="이름 변경",InputGestureText="F2",Icon=new Image{Source=RibbonIcons.Create("Edit"),Width=18,Height=18,Tag="Edit"}};rename.Click+=RenameHistoryMenuClick;
        menu.Items.Add(delete);menu.Items.Add(rename);menu.Items.Add(duplicate);menu.Opened+=HistoryMenuOpened;container.ContextMenu=menu;
    }
    private void HistoryMenuOpened(object sender,RoutedEventArgs e)
    {
        if(sender is not ContextMenu menu)return;
        foreach(var item in menu.Items.OfType<MenuItem>())item.IsEnabled=!busy&&menu.DataContext is CaptureEntry entry&&((string)item.Header=="삭제"||!entry.Item.Damaged);
    }
    private async void DeleteHistoryMenuClick(object sender,RoutedEventArgs e)
    {if(((MenuItem)sender).DataContext is CaptureEntry entry)await SafeAsync(()=>DeleteHistoryAsync([entry.Item.Id]));}
    private async void DuplicateHistoryMenuClick(object sender,RoutedEventArgs e)
    {if(((MenuItem)sender).DataContext is CaptureEntry entry&&!entry.Item.Damaged)await SafeAsync(()=>DuplicateHistoryAsync(entry.Item.Id));}
    private async Task DuplicateHistoryAsync(Guid id)
    {
        // 현재 문서는 최신 스냅샷을, 다른 이력은 저장본을 복제한다. 새 ID로 저장해 원본과 분리한다.
        if(busy)return;SetBusy(true);
        try
        {
            await FlushAsync();
            var source=Model.CaptureId==id?Model.Snapshot():await store.LoadAsync(id);
            var now=DateTimeOffset.UtcNow;var copy=source with{Id=Guid.NewGuid(),CapturedAtUtc=now,UpdatedAtUtc=now};
            await store.SaveAsync(copy);ShowNewRecord(copy);await FlushAsync();
            while(refreshing)await Task.Delay(10);
            await RefreshLibraryAsync();HistoryList.SelectedItem=historyItems.FirstOrDefault(x=>x.Item.Id==copy.Id);
            StatusText.Text="이력 복제됨";
        }
        finally{SetBusy(false);}
    }
    private Task DeleteHistoryAsync()=>DeleteHistoryAsync(HistoryList.SelectedItems.Cast<CaptureEntry>().Select(x=>x.Item.Id).ToArray());
    private async Task DeleteHistoryAsync(Guid[] ids)
    {
        // 활성 문서는 자동 저장을 중단하고 삭제한다. 실패하면 저장을 재개하여 편집 내용을 보호한다.
        if(busy||ids.Length==0)return;
        SetBusy(true);var failed=new List<string>();try
        {
            if(!deleteConfirmation(ids.Length))return;
            var selected=HistoryList.SelectedItem as CaptureEntry;
            var anchor=selected is not null&&ids.Contains(selected.Item.Id)?selected:historyItems.FirstOrDefault(x=>x.Item.Id==Model.CaptureId&&ids.Contains(x.Item.Id));
            var preceding=anchor is null?Array.Empty<Guid>():historyItems.TakeWhile(x=>x.Item.Id!=anchor.Item.Id).Select(x=>x.Item.Id).ToArray();
            var deleted=new HashSet<Guid>();
            foreach(var id in ids)
            {
                var active=Model.CaptureId==id;if(active)await autosave.SuspendAndDrainAsync(id);
                try{await store.DeleteAsync(id);deleted.Add(id);if(active)Model.Clear();}
                catch(Exception e){failed.Add(e.Message);if(active){autosave.Resume(id);if(Model.History?.IsPersistenceDirty==true)autosave.Queue(Model.Snapshot(),Model.History.CurrentStateToken);}}
            }
            await RefreshLibraryAsync();
            if(anchor is not null&&deleted.Contains(anchor.Item.Id))
            {
                // 삭제 전 위치 다음의 생존 항목을 열고, 끝에 도달하면 가까운 이전 항목으로 이동한다.
                var index=preceding.Count(id=>!deleted.Contains(id));
                var next=historyItems.Skip(index).Concat(historyItems.Take(index).Reverse()).FirstOrDefault(x=>!x.Item.Damaged&&x.Thumbnail is not null);
                if(next is not null)await SelectHistoryEntryAsync(next);
            }
            StatusText.Text=failed.Count==0?"이력 삭제됨":$"삭제 실패 {failed.Count}개: {failed[0]}";
        }
        finally{SetBusy(false);RestoreHistoryFocus();}
    }
    private async void HistoryScrollChanged(object sender,ScrollChangedEventArgs e)
    {
        // 스크롤 하단에 도달하면 다음 페이지를 요청한다. 작업 중에는 중복 로드를 하지 않는다.
        if(e.OriginalSource is not ScrollViewer scroll)return;historyScroll=scroll;
        if(refreshing||busy||!hasMoreHistory||scroll.ScrollableHeight<=0||scroll.VerticalOffset<scroll.ScrollableHeight-2)return;
        historyPages++;await SafeAsync(RefreshLibraryAsync);
    }
    private void ToggleHistoryClick(object sender,RoutedEventArgs e)
    {
        if(HistoryPanel.Visibility==Visibility.Collapsed)
        {
            LibraryColumn.MinWidth=170;LibraryColumn.Width=new GridLength(Math.Clamp(historyWidth,170,Math.Max(170,WorkspaceGrid.ActualWidth-206)));
            HistorySplitterColumn.Width=new GridLength(6);HistoryPanel.Visibility=HistorySplitter.Visibility=Visibility.Visible;
        }
        else
        {
            historyWidth=LibraryColumn.ActualWidth;LibraryColumn.MinWidth=0;LibraryColumn.Width=new GridLength(0);HistorySplitterColumn.Width=new GridLength(0);
            HistoryPanel.Visibility=HistorySplitter.Visibility=Visibility.Collapsed;
        }
    }
    private void Fit(){if(Model.Document is null)return;var width=Math.Max(200,ImageScroll.ActualWidth-60);var height=Math.Max(150,ImageScroll.ActualHeight-60);SetZoom(Math.Min(1,Math.Min(width/Model.Document.OutputSize.Width,height/Model.Document.OutputSize.Height)));}
    private void ResizeImageClick(object sender,RoutedEventArgs e)
    {
        if(busy||Model.Document is null)return;canvas.CancelGesture();SetBusy(true);
        try
        {
            var dialog=new ImageResizeDialog(Model.Document.OutputSize){Owner=this};
            if(dialog.ShowDialog()==true&&dialog.Result is PixelSize size){Model.History!.Resize(size);Fit();StatusText.Text="이미지 크기 변경";}
        }
        catch(Exception error){MessageBox.Show(this,error.Message,"이미지 크기 변경",MessageBoxButton.OK,MessageBoxImage.Warning);}
        finally{SetBusy(false);}
    }
    private void SetZoom(double zoom){imagePan.Cancel();canvas.CancelGesture();Model.Zoom=Math.Clamp(zoom,.1,4);canvas.Refresh();UpdateCommands();}
    private void ImageMouseWheel(object sender,MouseWheelEventArgs e)
    {
        if(Model.Document is null)return;e.Handled=true;if(busy||imagePan.IsActive||e.Delta==0)return;
        canvas.CancelGesture();SetZoom(Model.Zoom*Math.Pow(1.25,-e.Delta/120.0));
    }
    private void FitClick(object sender,RoutedEventArgs e)=>Fit();private void ZoomOutClick(object sender,RoutedEventArgs e)=>SetZoom(Model.Zoom/1.25);private void ZoomInClick(object sender,RoutedEventArgs e)=>SetZoom(Model.Zoom*1.25);
    private void WindowKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape&&Model.HasPixelSelection){Model.ClearMagicWand();e.Handled=true;return;}
        if(e.Key==Key.Escape&&imagePan.IsActive){imagePan.Cancel();e.Handled=true;return;}
        if(e.Key==Key.Escape&&!busy&&stickerPicker is not null){canvas.CancelGesture();CloseStickerPicker();e.Handled=true;return;}
        if(IsTextInput(e.OriginalSource as DependencyObject)||busy)return;
        // 캔버스 밖의 리본·속성 컨트롤에 포커스가 있어도 선택 개체를 삭제한다.
        // 이력 목록의 Delete는 기존 이력 삭제 처리로 전달한다.
        if(e.Key==Key.Delete&&Model.HasPixelSelection&&!IsHistoryCopySource(e.OriginalSource as DependencyObject)){DeleteSelectionSafely();e.Handled=true;return;}
        if(e.Key==Key.Delete&&!IsHistoryCopySource(e.OriginalSource as DependencyObject)&&Model.Selected is not null)
        {DeleteSelectionSafely();e.Handled=true;return;}
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {if(e.Key==Key.Z){canvas.CancelGesture();Model.History?.Undo();e.Handled=true;}else if(e.Key==Key.Y){canvas.CancelGesture();Model.History?.Redo();e.Handled=true;}else if(e.Key==Key.S){SaveClick(this,new());e.Handled=true;}}
    }
    private void SaveHistoryWidth()
    {
        // 목록을 접은 상태에서는 너비 0이 아니라 마지막으로 펼쳤던 너비를 저장한다.
        var width=HistoryPanel.Visibility==Visibility.Visible&&LibraryColumn.ActualWidth>0?LibraryColumn.ActualWidth:historyWidth;
        try{settings=settingsService.Load() with{HistoryPanelWidth=width};settingsService.Save(settings);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException){StatusText.Text="이력 너비 저장 실패: "+e.Message;}
    }
    private void WindowClosing(object? sender,CancelEventArgs e)
    {
        imagePan.Cancel();
        if(allowClose)return;e.Cancel=true;if(busy)return;
        SaveHistoryWidth();CloseStickerPicker();
        if(!trayNoticeAcknowledged)
        {
            // 확인하지 않고 안내를 닫으면 편집기를 그대로 둔다. 안내 중에는 단축키와 중복 작업을 막는다.
            SetBusy(true);
            try
            {
                if(!showTrayNotice())return;trayNoticeAcknowledged=true;
                try{settings=settingsService.Load() with{HasSeenTrayNotice=true};settingsService.Save(settings);}
                catch(Exception error)when(error is IOException or UnauthorizedAccessException){StatusText.Text="트레이 안내 설정 저장 실패: "+error.Message;}
            }
            finally{SetBusy(false);}
        }
        Hide();
    }
    public void CloseForExit(){Model.ClearMagicWand();EndPropertyDrag();SaveHistoryWidth();CloseStickerPicker();allowClose=true;autosave.Dispose();Close();}
    public void ReloadSettings(){settings=settingsService.Load();ThemeResources.Apply(this,settings.Theme);ThemeResources.ApplyToApplication(settings.Theme);InitializeRibbonIcons();UpdateCaptureTooltips();}
    private void UpdateCaptureTooltips()
    {
        RegionCaptureButton.ToolTip=$"영역 캡처 ({settings.RegionKey})";
        WindowCaptureButton.ToolTip=$"창 캡처 ({settings.WindowKey})";
        DesktopCaptureButton.ToolTip=$"전체 화면 캡처 ({settings.DesktopKey})";
    }
    private void InitializeLineStyleItems()
    {
        string[] labels=["실선","파선","점선","일점쇄선","이점쇄선"];
        foreach(var pattern in Enum.GetValues<StrokePattern>())
        {
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,54,14));
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(146,60,131)),2){DashStyle=CompositeRenderer.GetDashStyle(pattern),DashCap=PenLineCap.Round},new Point(2,7),new Point(52,7));
            }
            var source=new DrawingImage(visual.Drawing);source.Freeze();var panel=new StackPanel{Orientation=Orientation.Horizontal};
            panel.Children.Add(new Image{Source=source,Width=54,Height=14,Margin=new Thickness(0,0,8,0)});
            panel.Children.Add(new TextBlock{Text=labels[(int)pattern],VerticalAlignment=VerticalAlignment.Center});
            LineStyleBox.Items.Add(new ComboBoxItem{Tag=pattern,Content=panel});
        }
        LineStyleBox.SelectedIndex=0;
    }
    private void InitializeRibbonIcons()
    {
        RibbonSettings.LargeImageSource=RibbonSettings.SmallImageSource=RibbonIcons.Create("Settings",settings.Theme);
        RibbonAbout.LargeImageSource=RibbonAbout.SmallImageSource=RibbonIcons.Create("About",settings.Theme);
        SettingsGroup.LargeImageSource=SettingsGroup.SmallImageSource=RibbonSettings.LargeImageSource;
        PropertiesGroup.LargeImageSource=PropertiesGroup.SmallImageSource=RibbonIcons.Create("Properties",settings.Theme);
        foreach(var button in ToolsPanel.Items.OfType<RibbonToggleButton>())button.LargeImageSource=button.SmallImageSource=RibbonIcons.Create((string)button.Tag,settings.Theme);
        foreach(var (button,name) in new[]{(OpenImageButton,"ImageFile"),(PasteImageButton,"Paste"),(RegionCaptureButton,"Region"),(WindowCaptureButton,"Window"),(DesktopCaptureButton,"Desktop"),(UndoButton,"Undo"),(RedoButton,"Redo"),(DeleteButton,"Delete"),(EditButton,"Edit"),(CopyButton,"Copy"),(SaveButton,"Save"),(RibbonZoomOut,"ZoomOut"),(RibbonZoomIn,"ZoomIn"),(RibbonFit,"Fit"),(RibbonHistory,"History")})button.LargeImageSource=button.SmallImageSource=RibbonIcons.Create(name,settings.Theme);
    }
}
