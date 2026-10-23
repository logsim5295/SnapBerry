using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using SnapBerry.App.Library;
using SnapBerry.Core;
using PixelSize=SnapBerry.Core.PixelSize;
using SnapBerry.Desktop.Rendering;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow
{
    private readonly HistoryThumbnailCache historyThumbnails=new(64);
    private IReadOnlyList<CaptureListItem> historyItems=Array.Empty<CaptureListItem>();
    private bool historyLoading,historyHasMore,historyCommandExecuting,historyFeaturesDisposed;
    private int historyOpenVersion;private double historyWidth=240;private Guid? historyPreferredSelection;

    // Callers may supply a confirmation presenter; null always uses the owned, cancel-default dialog.
    public Func<int,Task<bool>>? ConfirmHistoryDeletionAsync {get;set;}
    public int HistoryThumbnailCacheCount=>historyThumbnails.Count;
    public Task RefreshHistoryCommandsAsync()=>RefreshHistoryAsync();

    private void InitializeHistoryFeatures()
    {
        var list=Control<ListBox>("HistoryList");list.SelectionMode=SelectionMode.Multiple;
        list.ItemTemplate=new FuncDataTemplate<CaptureListItem>((item,_)=>CreateHistoryRow(item));
        list.SelectionChanged+=HistorySelectionChanged;
        list.AddHandler(KeyDownEvent,HistoryKeyDown,RoutingStrategies.Tunnel);
        list.AddHandler(PointerPressedEvent,HistoryPointerPressed,RoutingStrategies.Tunnel);
        list.AddHandler(ScrollViewer.ScrollChangedEvent,HistoryScrollChanged,RoutingStrategies.Bubble);
        var copy=new MenuItem{Header="이미지 복사",InputGesture=new KeyGesture(Key.C,KeyModifiers.Meta)};copy.Click+=async (_,_)=>await RunAsync(()=>CopyImageAsync(true));
        var rename=new MenuItem{Header="이름 변경",InputGesture=new KeyGesture(Key.F2)};rename.Click+=(_,_)=>FocusHistoryName();
        var duplicate=new MenuItem{Header="복제",InputGesture=new KeyGesture(Key.D,KeyModifiers.Meta)};duplicate.Click+=async (_,_)=>await RunAsync(DuplicateSelectedHistoryAsync);
        var delete=new MenuItem{Header="삭제",InputGesture=new KeyGesture(Key.Delete)};delete.Click+=async (_,_)=>await RunAsync(DeleteHistoryAsync);
        var menu=new ContextMenu{ItemsSource=new[]{copy,rename,duplicate,delete}};
        menu.Opening+=(_,_)=>
        {
            var selected=list.SelectedItem as CaptureListItem;
            copy.IsEnabled=rename.IsEnabled=duplicate.IsEnabled=!busy&&selected is {Damaged:false};
            delete.IsEnabled=!busy&&SelectedHistoryItems().Length>0;
        };
        list.ContextMenu=menu;
        Wire("RenameButton",()=>RenameSelectedHistoryAsync(Control<TextBox>("NameEntry").Text??""));
        Wire("DeleteHistoryButton",DeleteHistoryAsync);Wire("DuplicateHistoryButton",DuplicateSelectedHistoryAsync);
        Wire("MoreHistoryButton",LoadMoreHistoryAsync);
        Control<TextBox>("SearchBox").TextChanged+=(_,_)=>
        {
            if(closed||historyFeaturesDisposed)return;historyPages=1;historyOpenVersion++;_=RefreshHistoryAsync();
        };
        Control<TextBox>("NameEntry").KeyDown+=async (_,e)=>
        {if(e.Key==Key.Enter&&!busy){e.Handled=true;await RunAsync(()=>RenameSelectedHistoryAsync(Control<TextBox>("NameEntry").Text??""));}};
        Control<Button>("HistoryToggleButton").Click+=(_,_)=>SetHistoryPanelVisible(!Control<Border>("HistoryPanel").IsVisible);
        var splitter=Control<GridSplitter>("HistorySplitter");
        splitter.AddHandler(Thumb.DragCompletedEvent,(_,_)=>SaveHistoryWidth(),RoutingStrategies.Bubble);
        splitter.KeyUp+=(_,_)=>SaveHistoryWidth();
        historyWidth=double.IsFinite(preferences.HistoryPanelWidth)?Math.Clamp(preferences.HistoryPanelWidth,170,600):240;
        ApplyHistoryVisibility(preferences.HistoryVisible);UpdateHistoryCommands();
    }
    private Control CreateHistoryRow(CaptureListItem? item)
    {
        if(item is null)return new TextBlock();
        var preview=new Image{Width=62,Height=48,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center};
        var imageHost=new Border{Width=66,Height=52,CornerRadius=new(4),Background=new SolidColorBrush(Color.FromArgb(18,128,128,128)),Child=preview};
        var text=new StackPanel{Spacing=3,VerticalAlignment=VerticalAlignment.Center,Children=
        {
            new TextBlock{Text=item.Caption,TextTrimming=TextTrimming.CharacterEllipsis,FontWeight=FontWeight.SemiBold},
            new TextBlock{Text=item.Details,FontSize=10,Opacity=.7,TextTrimming=TextTrimming.CharacterEllipsis}
        }};
        var row=new Grid{ColumnDefinitions=new("70,*"),Margin=new(2,6)};Grid.SetColumn(text,1);row.Children.Add(imageHost);row.Children.Add(text);
        preview.AttachedToVisualTree+=async (_,_)=>
        {
            try{await historyThumbnails.AttachAsync(item,preview,()=>RenderHistoryThumbnailAsync(item.Id));}
            catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException or InvalidOperationException or OperationCanceledException){/* A damaged preview does not prevent deleting the entry. */}
        };
        preview.DetachedFromVisualTree+=(_,_)=>preview.Source=null;
        return row;
    }
    private Task<byte[]> RenderHistoryThumbnailAsync(Guid id)=>Task.Run(async ()=>
    {
        var record=await store.LoadAsync(id).ConfigureAwait(false);
        var output=record.ResizeSize??new PixelSize(record.CropBounds?.Width??record.Width,record.CropBounds?.Height??record.Height);
        var ratio=Math.Min(1,Math.Min(132d/output.Width,104d/output.Height));
        var document=new AnnotationDocument(record.Width,record.Height)
        {
            CropBounds=record.CropBounds??new(0,0,record.Width,record.Height),Sequence=record.Sequence,BackgroundAssetHash=record.BackgroundAssetHash,
            ResizeSize=new(Math.Max(1,(int)(output.Width*ratio)),Math.Max(1,(int)(output.Height*ratio)))
        };
        document.Annotations.AddRange(record.Annotations);
        return SkiaDocumentRenderer.Render(record.OriginalPng,document,record.StickerAssets);
    });
    private void HistorySelectionChanged(object? sender,SelectionChangedEventArgs e)
    {
        var selected=Control<ListBox>("HistoryList").SelectedItem as CaptureListItem;
        if(selected is not null)Control<TextBox>("NameEntry").Text=selected.Id==editor.CaptureId?editor.Name:selected.Name;
        UpdateHistoryCommands();
        if(refreshing||busy||closed||historyCommandExecuting)return;
        historyOpenVersion++;
        if(selected is {Damaged:false}&&selected.Id!=editor.CaptureId)_=RunAsync(()=>OpenHistoryAsync(selected));
    }
    private void HistoryPointerPressed(object? sender,PointerPressedEventArgs e)
    {
        if(!e.GetCurrentPoint(Control<ListBox>("HistoryList")).Properties.IsRightButtonPressed)return;
        var source=e.Source as Control;
        var container=source as ListBoxItem??source?.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if(container?.DataContext is not CaptureListItem item)return;
        var list=Control<ListBox>("HistoryList");
        if(list.SelectedItems?.Contains(item)==true)return;
        // Context selection should not start a busy document switch before its menu can open.
        var wasRefreshing=refreshing;refreshing=true;historyOpenVersion++;
        try{list.SelectedItem=item;Control<TextBox>("NameEntry").Text=item.Id==editor.CaptureId?editor.Name:item.Name;}
        finally{refreshing=wasRefreshing;}
    }
    private void HistoryKeyDown(object? sender,KeyEventArgs e)
    {
        if(busy||e.Handled)return;
        var command=(e.KeyModifiers&(KeyModifiers.Meta|KeyModifiers.Control))!=0;
        if(e.Key is Key.Delete or Key.Back){e.Handled=true;_=RunAsync(DeleteHistoryAsync);}
        else if(e.Key==Key.F2){e.Handled=true;FocusHistoryName();}
        else if(command&&e.Key==Key.D){e.Handled=true;_=RunAsync(DuplicateSelectedHistoryAsync);}
    }
    private void FocusHistoryName()
    {
        if(busy||Control<ListBox>("HistoryList").SelectedItem is not CaptureListItem {Damaged:false} item)return;
        var field=Control<TextBox>("NameEntry");field.Text=item.Id==editor.CaptureId?editor.Name:item.Name;field.Focus();field.SelectAll();
    }
    private void HistoryScrollChanged(object? sender,ScrollChangedEventArgs e)
    {
        if(busy||historyLoading||!historyHasMore||e.Source is not ScrollViewer scroll)return;
        var remaining=scroll.Extent.Height-scroll.Viewport.Height-scroll.Offset.Y;
        if(scroll.Extent.Height>scroll.Viewport.Height+1&&remaining<=40)_=RunAsync(LoadMoreHistoryAsync);
    }
    private async Task LoadMoreHistoryAsync()
    {
        if(historyLoading||!historyHasMore)return;historyPages++;await RefreshHistoryAsync();
    }
    private async Task OpenHistoryAsync(CaptureListItem item)
    {
        if(item.Damaged)throw new InvalidDataException("손상된 이력입니다.");
        var openVersion=++historyOpenVersion;var queryVersion=searchVersion;
        var previousDocument=editor.Document;var previousId=editor.CaptureId;
        await FlushAsync();var record=await store.LoadAsync(item.Id);
        if(closed||openVersion!=historyOpenVersion||queryVersion!=searchVersion||editor.CaptureId!=previousId||!ReferenceEquals(editor.Document,previousDocument))return;
        editor.Open(record);Control<TextBox>("NameEntry").Text=record.Name;Fit();Status("이력 열림");
    }
    private async Task RefreshHistoryAsync()
    {
        if(closed||historyFeaturesDisposed)return;
        var version=++searchVersion;var query=Control<TextBox>("SearchBox").Text??"";
        var pages=historyPages;historyLoading=true;UpdateHistoryCommands();
        try
        {
            var items=new List<CaptureListItem>();
            for(var page=0;page<pages;page++)
            {
                var part=await store.ListAsync(page*50,50,query);
                if(closed||version!=searchVersion)return;
                items.AddRange(part);if(part.Count<50)break;
            }
            if(closed||version!=searchVersion)return;
            var list=Control<ListBox>("HistoryList");var selectedIds=SelectedHistoryItems().Select(item=>item.Id).ToHashSet();
            var preferred=historyPreferredSelection;historyPreferredSelection=null;
            var selected=items.FirstOrDefault(item=>item.Id==preferred)
                ??items.FirstOrDefault(item=>item.Id==editor.CaptureId)
                ??items.FirstOrDefault(item=>selectedIds.Contains(item.Id));
            if(selected is null&&editor.Document is null)selected=items.FirstOrDefault(item=>!item.Damaged);
            refreshing=true;
            try
            {
                historyItems=items;historyHasMore=items.Count==pages*50;
                historyThumbnails.KeepValid(items);
                list.ItemsSource=items;list.SelectedItems?.Clear();
                if(selected is not null)list.SelectedItem=selected;
                if(preferred is null&&list.SelectedItems is not null)
                    foreach(var item in items.Where(item=>selectedIds.Contains(item.Id)))if(!list.SelectedItems.Contains(item))list.SelectedItems.Add(item);
            }
            finally{refreshing=false;}
            if(editor.Document is null&&selected is {Damaged:false})await OpenHistoryAsync(selected);
        }
        catch(Exception error){if(!closed&&version==searchVersion)Status("이력 조회 실패: "+error.Message);}
        finally{if(version==searchVersion){historyLoading=false;UpdateHistoryCommands();}}
    }
    private CaptureListItem[] SelectedHistoryItems()=>Control<ListBox>("HistoryList").SelectedItems?.OfType<CaptureListItem>().DistinctBy(item=>item.Id).ToArray()??[];
    private Task DeleteHistoryAsync()=>DeleteSelectedHistoryAsync();
    public async Task<bool> DeleteSelectedHistoryAsync()
    {
        if(historyCommandExecuting||closed)return false;
        var selected=SelectedHistoryItems();if(selected.Length==0)return false;
        historyCommandExecuting=true;historyOpenVersion++;
        try
        {
            if(!await (ConfirmHistoryDeletionAsync?.Invoke(selected.Length)??ShowHistoryDeletionConfirmationAsync(selected.Length)))return false;
            if(closed)return false;
            var ids=selected.Select(item=>item.Id).ToHashSet();
            var anchor=Control<ListBox>("HistoryList").SelectedItem as CaptureListItem;
            var anchorIndex=anchor is null?0:Math.Max(0,historyItems.ToList().FindIndex(item=>item.Id==anchor.Id));
            var next=historyItems.Skip(anchorIndex).Concat(historyItems.Take(anchorIndex).Reverse()).FirstOrDefault(item=>!ids.Contains(item.Id)&&!item.Damaged);
            // Preserve an unrelated active document before SuspendAndDrain cancels the debounce timer.
            if(editor.CaptureId is Guid active&&!ids.Contains(active))await FlushAsync();
            // Suspend every target before any delete. Successful targets stay suspended, so delayed saves cannot resurrect them.
            foreach(var id in ids)await autosave.SuspendAndDrainAsync(id);
            var deleted=new HashSet<Guid>();var errors=new List<Exception>();
            foreach(var id in ids)
            {
                try{await store.DeleteAsync(id);deleted.Add(id);if(editor.CaptureId==id)editor.Clear();}
                catch(Exception error)
                {
                    autosave.Resume(id);if(editor.CaptureId==id&&editor.History is not null)autosave.Queue(editor.Snapshot(),editor.History.CurrentStateToken);
                    errors.Add(error);
                }
            }
            if(anchor is not null&&deleted.Contains(anchor.Id))historyPreferredSelection=next?.Id;
            await RefreshHistoryAsync();
            if(next is not null&&deleted.Contains(anchor?.Id??Guid.Empty)&&editor.CaptureId!=next.Id)
            {
                if(Control<ListBox>("HistoryList").SelectedItem is CaptureListItem current&&current.Id==next.Id)await OpenHistoryAsync(current);
            }
            Status(errors.Count==0?$"이력 {deleted.Count}개 삭제됨":$"삭제 실패 {errors.Count}개: {errors[0].Message}");
            return deleted.Count>0;
        }
        finally{historyCommandExecuting=false;UpdateHistoryCommands();}
    }
    private async Task<bool> ShowHistoryDeletionConfirmationAsync(int count)
    {
        // A background/unshown editor cannot create a dialog. Explicit presenters make headless commands testable.
        if(!IsVisible||closed)return false;
        var dialog=new Window{Title="이력 삭제",Width=380,Height=175,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,RequestedThemeVariant=ActualThemeVariant};
        var panel=new StackPanel{Margin=new(18),Spacing=14};
        panel.Children.Add(new TextBlock{Text=$"선택한 캡처 이력 {count}개를 삭제할까요?\n삭제한 이력은 복구할 수 없습니다.",TextWrapping=TextWrapping.Wrap});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8};
        var cancel=new Button{Content="취소",IsDefault=true,IsCancel=true};var delete=new Button{Content="삭제"};
        cancel.Click+=(_,_)=>dialog.Close(false);delete.Click+=(_,_)=>dialog.Close(true);
        buttons.Children.Add(cancel);buttons.Children.Add(delete);panel.Children.Add(buttons);dialog.Content=panel;
        dialog.Opened+=(_,_)=>cancel.Focus();return await dialog.ShowDialog<bool>(this);
    }
    public async Task RenameSelectedHistoryAsync(string value)
    {
        if(historyCommandExecuting||closed||Control<ListBox>("HistoryList").SelectedItem is not CaptureListItem {Damaged:false} item)return;
        historyCommandExecuting=true;
        try
        {
            var name=CaptureName.Normalize(value);await FlushAsync();
            if(editor.CaptureId==item.Id){editor.Rename(name);await FlushAsync();}
            else{var record=await store.LoadAsync(item.Id);await store.SaveAsync(record with{Name=name,UpdatedAtUtc=DateTimeOffset.UtcNow});}
            Control<TextBox>("NameEntry").Text=name;historyPreferredSelection=item.Id;await RefreshHistoryAsync();Status("이력 이름 저장됨");
        }
        finally{historyCommandExecuting=false;UpdateHistoryCommands();}
    }
    public async Task DuplicateSelectedHistoryAsync()
    {
        if(historyCommandExecuting||closed||Control<ListBox>("HistoryList").SelectedItem is not CaptureListItem {Damaged:false} item)return;
        historyCommandExecuting=true;historyOpenVersion++;
        try
        {
            await FlushAsync();var source=item.Id==editor.CaptureId?editor.Snapshot():await store.LoadAsync(item.Id);
            var now=DateTimeOffset.UtcNow;var duplicate=source with{Id=Guid.NewGuid(),CapturedAtUtc=now,UpdatedAtUtc=now};
            await store.SaveAsync(duplicate);editor.Open(duplicate);Control<TextBox>("NameEntry").Text=duplicate.Name;
            Control<TextBox>("SearchBox").Text="";historyPages=1;historyPreferredSelection=duplicate.Id;
            await FlushAsync();await RefreshHistoryAsync();Fit();Status("이력 복제됨");
        }
        finally{historyCommandExecuting=false;UpdateHistoryCommands();}
    }
    private void UpdateHistoryCommands()
    {
        if(historyFeaturesDisposed)return;
        var selected=Control<ListBox>("HistoryList").SelectedItem as CaptureListItem;
        var enabled=!busy&&!historyCommandExecuting;
        Control<Button>("RenameButton").IsEnabled=Control<Button>("DuplicateHistoryButton").IsEnabled=enabled&&selected is {Damaged:false};
        Control<Button>("DeleteHistoryButton").IsEnabled=enabled&&SelectedHistoryItems().Length>0;
        Control<Button>("MoreHistoryButton").IsEnabled=!busy&&!historyLoading&&historyHasMore;
    }
    public void SetHistoryPanelVisible(bool visible)
    {
        if(!visible)RememberHistoryWidth();ApplyHistoryVisibility(visible);SaveHistoryPreferences(visible);
    }
    private void ApplyHistoryVisibility(bool visible)
    {
        var grid=Control<Grid>("WorkspaceGrid");grid.ColumnDefinitions[0].MinWidth=visible?170:0;
        grid.ColumnDefinitions[0].Width=new(visible?historyWidth:0);grid.ColumnDefinitions[1].Width=new(visible?5:0);
        Control<Border>("HistoryPanel").IsVisible=Control<GridSplitter>("HistorySplitter").IsVisible=visible;
        var toggle=Control<Button>("HistoryToggleButton");var label=visible?"이력 숨기기":"이력 보기";
        DesktopIcons.Set(toggle,"Sidebar",label,showLabel:false);ToolTip.SetTip(toggle,label);
    }
    private void RememberHistoryWidth()
    {
        var column=Control<Grid>("WorkspaceGrid").ColumnDefinitions[0];
        var width=column.Width.IsAbsolute?column.Width.Value:column.ActualWidth;
        if(double.IsFinite(width)&&width>=170)historyWidth=Math.Clamp(width,170,600);
    }
    private void SaveHistoryWidth(){if(!Control<Border>("HistoryPanel").IsVisible)return;RememberHistoryWidth();SaveHistoryPreferences(true);}
    private void SaveHistoryPreferences(bool visible)
    {
        try{var next=preferences with{HistoryVisible=visible,HistoryPanelWidth=historyWidth};next.Save(dataDirectory);preferences=next;}
        catch(Exception error){Status("이력 패널 설정 저장 실패: "+error.Message);}
    }
    private void DisposeHistoryFeatures()
    {
        if(historyFeaturesDisposed)return;historyFeaturesDisposed=true;historyOpenVersion++;searchVersion++;historyThumbnails.Dispose();
    }
}
