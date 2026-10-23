using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 창·클립보드·화면 캡처 없이 이름 저장과 전체 이력 검색을 검증한다.
public class HistoryNameTests
{
    // 네이티브 창 핸들 없이 키 이벤트 인자만 구성한다.
    private sealed class HeadlessSource : PresentationSource
    {
        public override Visual RootVisual {get;set;}=new DrawingVisual();
        public override bool IsDisposed=>false;
        protected override CompositionTarget GetCompositionTargetCore()=>null!;
    }
    private static CaptureRecord Record(string name="")=>new()
        {Width=40,Height=30,Name=name,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(40,30))};
    private static string Temp()=>Path.Combine(Path.GetTempPath(),"SnapBerry-names-"+Guid.NewGuid().ToString("N"));
    private static IReadOnlyList<CaptureListItem> Search(CaptureLibraryStore store,int offset,string query)
        =>store.ListAsync(offset,50,query).GetAwaiter().GetResult();
    private static Task Invoke(EditorWindow window,string method,params object[] args)
        =>(Task)typeof(EditorWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,args)!;
    private static void Pump(Task task)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();
        task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
    }
    private static void WithWindow(Func<string,string?> edit,Action<EditorWindow,CaptureLibraryStore,string> action)
    {
        var path=Temp();var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
        var window=new EditorWindow(store,new SettingsService(path),editHistoryName:edit);
        try{action(window,store,path);}finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    }
    [Fact] public void NameSurvivesRestartAndAppearsInTheList()=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var record=Record("업무 보고서");new CaptureLibraryStore(path).SaveAsync(record).GetAwaiter().GetResult();
            var store=new CaptureLibraryStore(path);var reopened=store.LoadAsync(record.Id).GetAwaiter().GetResult();
            Assert.Equal("업무 보고서",reopened.Name);
            Assert.Equal("업무 보고서",Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult()).Caption);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void SearchFiltersAllNamesBeforePagingAndIgnoresCase()=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var store=new CaptureLibraryStore(path);var now=DateTimeOffset.UtcNow;
            for(var i=0;i<105;i++)
            {
                var record=Record(i<52?"보고 Project "+i:"다른 자료") with{CapturedAtUtc=now.AddMinutes(i)};
                store.SaveAsync(record).GetAwaiter().GetResult();
            }
            Assert.Equal(50,Search(store,0,"  project  ").Count);
            Assert.Equal(2,Search(store,50,"보고").Count);
            Assert.Empty(Search(store,0,"없는 이름"));
            Assert.Equal(50,Search(store,0,"  ").Count);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData("10.20")] [InlineData("12:34")] [InlineData("12:34:56")] [InlineData("10.20 12:34:56")] [InlineData("  10.20  ")]
    public void SearchFindsUnnamedHistoryByItsDisplayedLocalDateAndTime(string query)=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var store=new CaptureLibraryStore(path);
            var captured=new DateTimeOffset(new DateTime(2026,10,20,12,34,56,DateTimeKind.Local)).ToUniversalTime();
            var unnamed=Record() with{CapturedAtUtc=captured};
            store.SaveAsync(unnamed).GetAwaiter().GetResult();
            store.SaveAsync(Record() with{CapturedAtUtc=captured.AddDays(-1).AddHours(-3)}).GetAwaiter().GetResult();
            store.SaveAsync(Record("별도 제목") with{CapturedAtUtc=captured}).GetAwaiter().GetResult();
            var found=Assert.Single(Search(new CaptureLibraryStore(path),0,query));
            Assert.Equal(unnamed.Id,found.Id);Assert.Equal("10.20 12:34:56",found.Caption);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(true)] [InlineData(false)]
    public void RenameUsesClickedRecordAndPreservesPendingEditsAndUndo(bool active)=>Sta.Run(()=>
        WithWindow(value=>{Assert.Equal("원래 이름",value);return "  업무 결과  ";},(window,store,_)=>
    {
        var target=Record("원래 이름");var other=Record("다른 문서");
        store.SaveAsync(target).GetAwaiter().GetResult();store.SaveAsync(other).GetAwaiter().GetResult();
        window.Model.Open(active?target:other);var history=window.Model.History!;
        var annotation=new Annotation{Kind=AnnotationKind.Text,Text="최신 편집"};history.Add(annotation);
        history.Add(new(){Kind=AnnotationKind.Rectangle});history.Undo();var token=history.CurrentStateToken;
        Pump(Invoke(window,"RenameHistoryAsync",target.Id));
        Assert.Same(history,window.Model.History);Assert.Equal(token,history.CurrentStateToken);
        Assert.True(history.CanUndo);Assert.True(history.CanRedo);
        var saved=store.LoadAsync(target.Id).GetAwaiter().GetResult();Assert.Equal("업무 결과",saved.Name);
        Assert.Equal(target.OriginalPng,saved.OriginalPng);
        Assert.Equal(active?target.Id:other.Id,window.Model.CaptureId);
        Assert.Equal(active?"업무 결과":"다른 문서",window.Model.Snapshot().Name);
        Assert.Contains(store.LoadAsync(window.Model.CaptureId!.Value).GetAwaiter().GetResult().Annotations,a=>a.Text=="최신 편집");
        // 이름 변경 전 대기 저장과 이후 Undo/Redo 저장 모두 새 이름을 유지해야 한다.
        history.Redo();Pump(window.FlushAsync());history.Undo();Pump(window.FlushAsync());
        Assert.Equal(active?"업무 결과":"다른 문서",store.LoadAsync(window.Model.CaptureId.Value).GetAwaiter().GetResult().Name);
        Assert.Empty(Search(store,0,"원래 이름"));Assert.Single(Search(store,0,"업무 결과"));
    }));

    [Theory] [InlineData(null,"원래 이름")] [InlineData("  ","")]
    public void CancelAndClearNameBehaveWithoutReplacingTheDocument(string? input,string expected)=>Sta.Run(()=>
        WithWindow(_=>input,(window,store,_)=>
    {
        var target=Record("원래 이름");Pump(window.OpenNewAsync(target));var document=window.Model.Document;
        Pump(Invoke(window,"RenameHistoryAsync",target.Id));Assert.Same(document,window.Model.Document);
        Assert.Equal(expected,store.LoadAsync(target.Id).GetAwaiter().GetResult().Name);
        var item=Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult());
        Assert.Equal(expected.Length==0?item.CapturedTime:expected,item.Caption);
    }));

    [Fact] public void FailedRenameKeepsTheStoredAndActiveName()=>Sta.Run(()=>
    {
        FileStream? locked=null;string? file=null;
        try
        {
            WithWindow(_=>{locked=new FileStream(file!,FileMode.Open,FileAccess.Read,FileShare.Read);return "실패한 이름";},(window,store,path)=>
            {
                var target=Record("기존 이름");Pump(window.OpenNewAsync(target));
                file=Path.Combine(path,"Library",target.Id.ToString("N")+".qcapture");
                try{var error=Xunit.Record.Exception(()=>Pump(Invoke(window,"RenameHistoryAsync",target.Id)));Assert.True(error is IOException or UnauthorizedAccessException,error?.ToString());}
                finally{locked?.Dispose();locked=null;}
                Assert.False(window.IsBusy);Assert.Equal("기존 이름",window.Model.Snapshot().Name);
                Assert.Equal("기존 이름",store.LoadAsync(target.Id).GetAwaiter().GetResult().Name);
                Assert.Equal("기존 이름",Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult()).Caption);
            });
        }
        finally{locked?.Dispose();}
    });

    [Fact] public void SearchKeepsTheEditorAndUsesTheLatestQueryAndNewCaptureClearsIt()=>Sta.Run(()=>
        WithWindow(_=>"변경된 이름",(window,store,_)=>
    {
        var first=Record("Alpha 보고");var second=Record("Beta 보고");
        Pump(window.OpenNewAsync(first));store.SaveAsync(second).GetAwaiter().GetResult();
        var search=(TextBox)window.FindName("HistorySearchBox");var list=(ListBox)window.FindName("HistoryList");
        var document=window.Model.Document;
        search.Text="Alpha";search.Text="beta";Pump(Invoke(window,"RefreshLibraryAsync"));
        Assert.Equal(second.Id,Assert.Single(list.Items.Cast<CaptureEntry>()).Item.Id);
        Assert.Same(document,window.Model.Document);
        Pump(Invoke(window,"RenameHistoryAsync",second.Id));Assert.Empty(list.Items.Cast<CaptureEntry>());
        Assert.Equal(Visibility.Visible,((TextBlock)window.FindName("HistoryEmptyText")).Visibility);
        Assert.Same(document,window.Model.Document);
        var added=Record();Pump(window.OpenNewAsync(added));Pump(Invoke(window,"RefreshLibraryAsync"));
        Assert.Equal("",search.Text);Assert.Equal(3,list.Items.Count);
        Assert.Equal(added.Id,Assert.IsType<CaptureEntry>(list.SelectedItem).Item.Id);
    }));

    [Theory] [InlineData("bad\nname")] [InlineData("bad\tname")] [InlineData(null)]
    public void InvalidNamesDoNotOverwriteExistingHistory(string? name)=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var store=new CaptureLibraryStore(path);var record=Record("유효한 이름");store.SaveAsync(record).GetAwaiter().GetResult();
            Assert.Throws<InvalidDataException>(()=>store.SaveAsync(record with{Name=name!}).GetAwaiter().GetResult());
            Assert.Throws<InvalidDataException>(()=>store.SaveAsync(record with{Name=new string('x',101)}).GetAwaiter().GetResult());
            Assert.Equal(record.Name,store.LoadAsync(record.Id).GetAwaiter().GetResult().Name);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void NameDialogAllowsClearingAndEnforcesTheSameSingleLineLimit()=>Sta.Run(()=>
    {
        var dialog=new TextAnnotationEditor("",false,CaptureName.MaxLength,"이력 이름 변경",true);
        try
        {
            var input=(TextBox)dialog.FindName("TextEditorInput");
            var apply=(Button)((StackPanel)dialog.FindName("TextEditorButtons")).Children[0];
            Assert.Equal("이력 이름 변경",dialog.Title);Assert.True(apply.IsEnabled);
            input.Text=new string('x',100);Assert.True(apply.IsEnabled);Assert.Equal(input.Text,CaptureName.Normalize(input.Text));
            input.Text=new string('x',101);Assert.False(apply.IsEnabled);
            input.Text="bad\nname";Assert.False(apply.IsEnabled);
            input.Text="   ";Assert.True(apply.IsEnabled);Assert.Equal("",CaptureName.Normalize(input.Text));
        }
        finally{dialog.Close();}
    });

    [Theory] [InlineData(true)] [InlineData(false)]
    public void ContextMenuAndF2RenameTheirTargetWithoutShowingAWindow(bool contextMenu)=>Sta.Run(()=>
    {
        var calls=0;
        WithWindow(_=>{calls++;return "새 이름";},(window,store,_)=>
        {
            var target=Record("대상");var active=Record("편집 중");
            Pump(window.OpenNewAsync(active));store.SaveAsync(target).GetAwaiter().GetResult();Pump(Invoke(window,"RefreshLibraryAsync"));
            var list=(ListBox)window.FindName("HistoryList");
            var entry=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==target.Id);
            if(contextMenu)
            {
                // 선택 항목과 우클릭 항목이 다를 때도 우클릭 항목의 이름만 바꾼다.
                var container=new ListBoxItem{DataContext=entry};
                typeof(EditorWindow).GetMethod("HistoryItemLoaded",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[container,new RoutedEventArgs()]);
                var menu=container.ContextMenu;menu.DataContext=entry;
                typeof(EditorWindow).GetMethod("HistoryMenuOpened",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[menu,new RoutedEventArgs()]);
                var rename=menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="이름 변경");
                Assert.True(rename.IsEnabled);Assert.Equal("F2",rename.InputGestureText);
                rename.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            else
            {
                list.SelectedItems.Clear();list.SelectedItem=entry;Assert.Single(list.SelectedItems.Cast<CaptureEntry>());
                var key=new KeyEventArgs(Keyboard.PrimaryDevice,new HeadlessSource(),0,Key.F2){RoutedEvent=Keyboard.KeyDownEvent};
                typeof(EditorWindow).GetMethod("HistoryKeyDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[list,key]);
                Assert.True(key.Handled);
            }
            async Task Wait(){var end=DateTime.UtcNow.AddSeconds(10);while(window.IsBusy){Assert.True(DateTime.UtcNow<end);await Task.Delay(10);}}
            Pump(Wait());Assert.Equal(1,calls);Assert.False(window.IsVisible);
            Assert.Equal("새 이름",store.LoadAsync(target.Id).GetAwaiter().GetResult().Name);
            Assert.Equal(active.Id,window.Model.CaptureId);
        });
    });

    [Fact] public void DamagedHistoryDoesNotOfferRename()=>Sta.Run(()=>WithWindow(_=>throw new Exception("입력창을 열면 안 됩니다."),(window,_,_)=>
    {
        var container=new ListBoxItem{DataContext=new CaptureEntry(new(Guid.NewGuid(),DateTimeOffset.UtcNow,0,0,true),null)};
        typeof(EditorWindow).GetMethod("HistoryItemLoaded",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[container,new RoutedEventArgs()]);
        var menu=container.ContextMenu;menu.DataContext=container.DataContext;
        typeof(EditorWindow).GetMethod("HistoryMenuOpened",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[menu,new RoutedEventArgs()]);
        Assert.False(menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="이름 변경").IsEnabled);
        Assert.True(menu.Items.OfType<MenuItem>().Single(x=>(string)x.Header=="삭제").IsEnabled);
    }));
    [Fact] public void OldManifestWithoutNameKeepsItsDateCaption()=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var record=Record();var store=new CaptureLibraryStore(path);store.SaveAsync(record).GetAwaiter().GetResult();
            using(var zip=ZipFile.Open(Path.Combine(path,record.Id.ToString("N")+".qcapture"),ZipArchiveMode.Update))
            {
                var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(record))!;json.AsObject().Remove("Name");
                zip.GetEntry("document.json")!.Delete();using var writer=new StreamWriter(zip.CreateEntry("document.json").Open());writer.Write(json.ToJsonString());
            }
            var loaded=new CaptureLibraryStore(path).LoadAsync(record.Id).GetAwaiter().GetResult();
            Assert.Equal(record.OriginalPng,loaded.OriginalPng);
            Assert.Equal(record.CapturedAtUtc.ToLocalTime().ToString("MM.dd HH:mm:ss"),Assert.Single(new CaptureLibraryStore(path).ListAsync(0,50).GetAwaiter().GetResult()).Caption);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
}
