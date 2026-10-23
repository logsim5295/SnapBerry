using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창 표시·실제 키보드 포커스 변경 없이 기본 이력과 삭제 후 문서 전환을 검증한다.
public class HistoryDefaultSelectionTests
{
    private sealed class HeadlessSource : PresentationSource
    {
        public override Visual RootVisual {get;set;}=new DrawingVisual();
        public override bool IsDisposed=>false;
        protected override CompositionTarget GetCompositionTargetCore()=>null!;
    }
    private static void Pump(Task task)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();
        task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
    }
    private static void Refresh(EditorWindow window)=>Pump((Task)typeof(EditorWindow)
        .GetMethod("RefreshLibraryAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null)!);
    private static void Delete(EditorWindow window,params Guid[] ids)=>Pump((Task)typeof(EditorWindow)
        .GetMethod("DeleteHistoryAsync",BindingFlags.Instance|BindingFlags.NonPublic,null,[typeof(Guid[])],null)!.Invoke(window,[ids])!);
    private static ListBox List(EditorWindow window)=>(ListBox)window.FindName("HistoryList");
    private static void Select(EditorWindow window,Guid id)=>List(window).SelectedItem=List(window).Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==id);
    private static void AssertSelected(EditorWindow window,Guid id)
    {
        Assert.Equal(id,window.Model.CaptureId);
        Assert.Equal(id,Assert.IsType<CaptureEntry>(List(window).SelectedItem).Item.Id);
    }
    private static void FocusSelectedItem(EditorWindow window)
    {
        var list=List(window);list.Measure(new Size(300,400));list.Arrange(new Rect(0,0,300,400));list.UpdateLayout();
        var item=Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem));
        // 숨겨진 창의 논리 포커스만 지정한다. 실제 데스크톱 키보드 포커스는 변경하지 않는다.
        FocusManager.SetFocusedElement(window,item);
    }
    private static void AssertHistoryFocused(EditorWindow window)
    {
        var focused=FocusManager.GetFocusedElement(window);
        Assert.True(ReferenceEquals(focused,List(window))||focused is ListBoxItem item
            &&ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item),List(window)),"Focus must belong to the current history list, not a removed item.");
        if(focused is ListBoxItem selected)Assert.Same(List(window).SelectedItem,selected.DataContext);
        Assert.False(window.IsVisible);
    }
    private static void PressDeleteAtLogicalFocus(EditorWindow window)=>PressHistoryKeyAtLogicalFocus(window,Key.Delete);
    private static void PressHistoryKeyAtLogicalFocus(EditorWindow window,Key pressed)
    {
        var focused=Assert.IsAssignableFrom<UIElement>(FocusManager.GetFocusedElement(window));
        var key=new KeyEventArgs(Keyboard.PrimaryDevice,new HeadlessSource(),0,pressed){RoutedEvent=Keyboard.KeyDownEvent};
        focused.RaiseEvent(key);Assert.True(key.Handled,$"{pressed} must reach the history list from its focused element.");
        async Task Wait()
        {var end=DateTime.UtcNow.AddSeconds(10);while(window.IsBusy){Assert.True(DateTime.UtcNow<end);await Task.Delay(1);}}
        Pump(Wait());
    }
    private static void WithHistory(int count,Action<EditorWindow,CaptureLibraryStore,CaptureRecord[],string> action,bool confirmed=true,Func<string,string?>? editName=null)
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-history-selection-"+Guid.NewGuid().ToString("N"));
        var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
        var png=PngFileWriter.Encode(RenderingTests.Image(40,30));
        var records=Enumerable.Range(0,count).Select(i=>new CaptureRecord
        {Width=40,Height=30,OriginalPng=png,CapturedAtUtc=DateTimeOffset.UtcNow.AddDays(-i)}).ToArray();
        foreach(var record in records.Reverse())store.SaveAsync(record).GetAwaiter().GetResult();
        var window=new EditorWindow(store,new SettingsService(path),confirmDelete:_=>confirmed,editHistoryName:editName);
        try{action(window,store,records,path);}
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    }

    [Fact] public void InitialHistoryOpensNewestCapture()=>Sta.Run(()=>WithHistory(3,(window,_,records,_)=>
    {
        Refresh(window);AssertSelected(window,records[0].Id);
        Assert.Equal(40,window.Model.Original!.PixelWidth);
    }));

    [Fact] public void EmptyHistoryLeavesEditorEmpty()=>Sta.Run(()=>WithHistory(0,(window,_,_,_)=>
    {
        Refresh(window);Assert.Null(window.Model.CaptureId);Assert.Empty(List(window).Items);
    }));

    [Theory] [InlineData(false)] [InlineData(true)]
    public void UnreadableCapturesAreSkippedWhenChoosingDefaultOrNext(bool deleting)=>Sta.Run(()=>WithHistory(3,(window,store,records,path)=>
    {
        var damaged=deleting?records[1]:records[0];
        // manifest는 유효하지만 원본 PNG를 읽을 수 없는 경우도 자동 선택에서 제외한다.
        using(var zip=System.IO.Compression.ZipFile.Open(Path.Combine(path,"Library",damaged.Id.ToString("N")+".qcapture"),System.IO.Compression.ZipArchiveMode.Update))
        {zip.GetEntry("original.png")!.Delete();using var writer=new StreamWriter(zip.CreateEntry("original.png").Open());writer.Write("invalid png");}
        Refresh(window);
        if(deleting)Delete(window,records[0].Id);
        AssertSelected(window,records[deleting?2:1].Id);
    }));

    [Fact] public void DeletingSearchResultOpensNextMatchingCapture()=>Sta.Run(()=>WithHistory(3,(window,store,records,_)=>
    {
        store.SaveAsync(records[0] with{Name="검색 대상"}).GetAwaiter().GetResult();
        store.SaveAsync(records[2] with{Name="검색 대상"}).GetAwaiter().GetResult();
        Refresh(window);((TextBox)window.FindName("HistorySearchBox")).Text="검색 대상";Refresh(window);
        Delete(window,records[0].Id);AssertSelected(window,records[2].Id);Assert.Single(List(window).Items);
    }));

    [Fact] public void RefreshPreservesOpenedCaptureAndSelection()=>Sta.Run(()=>WithHistory(3,(window,_,records,_)=>
    {
        Refresh(window);Pump(window.OpenRecordAsync(records[1].Id));Select(window,records[1].Id);
        Refresh(window);AssertSelected(window,records[1].Id);
    }));

    [Theory] [InlineData(0,1)] [InlineData(1,2)] [InlineData(2,1)]
    public void DeletingActiveCaptureOpensNextOrPrevious(int deleted,int expected)=>Sta.Run(()=>WithHistory(3,(window,store,records,_)=>
    {
        Refresh(window);Pump(window.OpenRecordAsync(records[deleted].Id));Select(window,records[deleted].Id);
        Delete(window,records[deleted].Id);AssertSelected(window,records[expected].Id);
        Assert.DoesNotContain(store.ListAsync(0,50).GetAwaiter().GetResult(),x=>x.Id==records[deleted].Id);
    }));

    [Fact] public void DeletingSelectedCaptureOpensNextEvenWhenAnotherCaptureWasOpen()=>Sta.Run(()=>WithHistory(3,(window,_,records,_)=>
    {
        Refresh(window);Select(window,records[1].Id);Delete(window,records[1].Id);
        AssertSelected(window,records[2].Id);
    }));

    [Fact] public void DeletingMultipleCapturesSkipsDeletedRows()=>Sta.Run(()=>WithHistory(5,(window,_,records,_)=>
    {
        Refresh(window);Pump(window.OpenRecordAsync(records[2].Id));Select(window,records[2].Id);
        List(window).SelectedItems.Add(List(window).Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==records[0].Id));
        List(window).SelectedItems.Add(List(window).Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==records[3].Id));
        Delete(window,records[0].Id,records[2].Id,records[3].Id);AssertSelected(window,records[4].Id);
    }));

    [Fact] public void DeletingLastCaptureClearsEditorAndSelection()=>Sta.Run(()=>WithHistory(1,(window,_,records,_)=>
    {
        Refresh(window);Delete(window,records[0].Id);
        Assert.Null(window.Model.CaptureId);Assert.Null(List(window).SelectedItem);Assert.Empty(List(window).Items);
    }));

    [Fact] public void ConsecutiveDeletesKeepFocusInTheCurrentHistory()=>Sta.Run(()=>WithHistory(3,(window,_,records,_)=>
    {
        Refresh(window);FocusSelectedItem(window);
        PressDeleteAtLogicalFocus(window);AssertSelected(window,records[1].Id);AssertHistoryFocused(window);
        // 다음 문서의 자동 저장으로 목록이 다시 만들어져도 연속 Delete가 이어져야 한다.
        Pump(window.FlushAsync());Refresh(window);AssertHistoryFocused(window);
        PressDeleteAtLogicalFocus(window);AssertSelected(window,records[2].Id);AssertHistoryFocused(window);
        PressDeleteAtLogicalFocus(window);Assert.Empty(List(window).Items);AssertHistoryFocused(window);
    }));

    [Fact] public void BackgroundRefreshKeepsFocusInTheCurrentHistory()=>Sta.Run(()=>WithHistory(2,(window,_,records,_)=>
    {
        Refresh(window);FocusSelectedItem(window);
        Refresh(window);AssertSelected(window,records[0].Id);AssertHistoryFocused(window);
    }));

    [Fact] public void RefreshDuringHistoryOperationRestoresFocusAfterReenabling()=>Sta.Run(()=>WithHistory(2,(window,_,records,_)=>
    {
        Refresh(window);FocusSelectedItem(window);
        window.SetBusy(true);
        // 실제 WPF 창에서는 비활성화된 항목의 키보드 포커스가 다른 컨트롤로 이동한다.
        // 숨겨진 창에서는 논리 포커스만 옮겨 같은 상태를 재현한다.
        FocusManager.SetFocusedElement(window,window);
        window.SetBusy(true); // 중복 busy 설정이 원래 입력 출처를 잊으면 안 된다.
        Refresh(window);window.SetBusy(false);
        AssertHistoryFocused(window);PressDeleteAtLogicalFocus(window);
        AssertSelected(window,records[1].Id);
    }));

    [Fact] public void RenameRefreshAllowsImmediateF2AndDeleteWithoutReselecting()=>Sta.Run(()=>
    {
        var calls=0;
        WithHistory(2,(window,store,records,_)=>
        {
            Refresh(window);FocusSelectedItem(window);
            PressHistoryKeyAtLogicalFocus(window,Key.F2);
            Assert.Equal("이름 1",store.LoadAsync(records[0].Id).GetAwaiter().GetResult().Name);
            AssertHistoryFocused(window);
            PressHistoryKeyAtLogicalFocus(window,Key.F2);
            Assert.Equal("이름 2",store.LoadAsync(records[0].Id).GetAwaiter().GetResult().Name);
            AssertHistoryFocused(window);PressDeleteAtLogicalFocus(window);
            AssertSelected(window,records[1].Id);Assert.Equal(2,calls);
        },editName:_=>$"이름 {++calls}");
    });

    [Fact] public void OpeningHistoryWithPendingAutosaveKeepsImmediateHistoryKeys()=>Sta.Run(()=>WithHistory(3,(window,store,records,_)=>
    {
        Refresh(window);
        window.Model.History!.Add(new(){Kind=SnapBerry.Core.AnnotationKind.Rectangle,Width=10,Height=10});
        Select(window,records[1].Id);FocusSelectedItem(window);
        Pump(window.OpenRecordAsync(records[1].Id));
        AssertSelected(window,records[1].Id);AssertHistoryFocused(window);
        PressHistoryKeyAtLogicalFocus(window,Key.F2);AssertHistoryFocused(window);
        Assert.Equal("전환 후 이름",store.LoadAsync(records[1].Id).GetAwaiter().GetResult().Name);
        Assert.Single(store.LoadAsync(records[0].Id).GetAwaiter().GetResult().Annotations);
        PressDeleteAtLogicalFocus(window);AssertSelected(window,records[2].Id);
    },editName:_=>"전환 후 이름"));

    [Theory] [InlineData(null)] [InlineData("")]
    public void CancelledOrUnchangedRenameRestoresFocusAfterDialog(string? result)=>Sta.Run(()=>
    {
        EditorWindow? owner=null;
        WithHistory(2,(window,_,records,_)=>
        {
            owner=window;Refresh(window);FocusSelectedItem(window);
            PressHistoryKeyAtLogicalFocus(window,Key.F2);AssertHistoryFocused(window);
            PressDeleteAtLogicalFocus(window);AssertSelected(window,records[1].Id);
        },editName:_=>
        {
            // 모달 입력창이 닫힐 때의 포커스 이동도 실제 창 없이 재현한다.
            FocusManager.SetFocusedElement(owner!,owner!);return result;
        });
    });

    [Fact] public void BusyRefreshDoesNotMoveSearchFocusIntoHistory()=>Sta.Run(()=>WithHistory(2,(window,_,_,_)=>
    {
        Refresh(window);var search=(TextBox)window.FindName("HistorySearchBox");
        FocusManager.SetFocusedElement(window,search);
        window.SetBusy(true);Refresh(window);window.SetBusy(false);
        Assert.Same(search,FocusManager.GetFocusedElement(window));Assert.False(window.IsVisible);
    }));

    [Fact] public void BackgroundRefreshPreservesSearchFocus()=>Sta.Run(()=>WithHistory(2,(window,_,_,_)=>
    {
        Refresh(window);var search=(TextBox)window.FindName("HistorySearchBox");FocusManager.SetFocusedElement(window,search);
        Refresh(window);Assert.Same(search,FocusManager.GetFocusedElement(window));Assert.False(window.IsVisible);
    }));

    [Fact] public void CancellingDeletionPreservesCaptureAndSelection()=>Sta.Run(()=>WithHistory(2,(window,_,records,_)=>
    {
        Refresh(window);Select(window,records[0].Id);Delete(window,records[0].Id);
        AssertSelected(window,records[0].Id);Assert.Equal(2,List(window).Items.Count);
    },confirmed:false));

    [Fact] public void DeletingUnselectedContextCapturePreservesOpenCapture()=>Sta.Run(()=>WithHistory(3,(window,_,records,_)=>
    {
        Refresh(window);Select(window,records[0].Id);Delete(window,records[1].Id);
        AssertSelected(window,records[0].Id);
    }));

    [Fact] public void DeletingAtPageBoundaryOpensNextPageCapture()=>Sta.Run(()=>WithHistory(51,(window,_,records,_)=>
    {
        Refresh(window);Pump(window.OpenRecordAsync(records[49].Id));Select(window,records[49].Id);
        Delete(window,records[49].Id);AssertSelected(window,records[50].Id);
    }));

    [Fact] public void FailedDeletionPreservesActiveCapture()=>Sta.Run(()=>WithHistory(2,(window,_,records,path)=>
    {
        Refresh(window);Select(window,records[0].Id);
        using var locked=File.OpenRead(Path.Combine(path,"Library",records[0].Id.ToString("N")+".qcapture"));
        Delete(window,records[0].Id);AssertSelected(window,records[0].Id);Assert.Equal(2,List(window).Items.Count);
    }));
}
