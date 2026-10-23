using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SnapBerry.App.Library;
using SnapBerry.Core;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Views;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class HistoryParityTests
{
    [AvaloniaFact]
    public async Task FirstHistoryRefreshOpensTheNewestCaptureWithoutAWindow()
    {
        using var fixture=await HistoryFixture.CreateAsync(3);var window=fixture.Window;
        await InvokeAsync(window,"RefreshHistoryAsync");
        Assert.Equal(fixture.Records[0].Id,fixture.Editor.CaptureId);
        Assert.Equal(fixture.Records[0].Id,Assert.IsType<CaptureListItem>(fixture.List.SelectedItem).Id);
        Assert.False(window.IsVisible);
    }
    [AvaloniaFact]
    public async Task BatchDeleteRequiresConfirmationThenOpensTheAdjacentSurvivorWithoutResurrection()
    {
        using var fixture=await HistoryFixture.CreateAsync(3);await InvokeAsync(fixture.Window,"RefreshHistoryAsync");
        fixture.Editor.Commit(new(){Kind=AnnotationKind.Rectangle,X=3,Y=4,Width=8,Height=9});
        fixture.Select(fixture.Records[0].Id,fixture.Records[1].Id);
        SetConfirmation(fixture.Window,_=>Task.FromResult(false));
        Assert.False(await InvokeAsync<bool>(fixture.Window,"DeleteSelectedHistoryAsync"));
        Assert.Equal(3,(await fixture.Store.ListAsync(0,50)).Count);
        SetConfirmation(fixture.Window,count=>Task.FromResult(count==2));
        Assert.True(await InvokeAsync<bool>(fixture.Window,"DeleteSelectedHistoryAsync"));
        await Task.Delay(650);
        Assert.Equal(fixture.Records[2].Id,fixture.Editor.CaptureId);
        Assert.Equal(fixture.Records[2].Id,Assert.Single(await fixture.Store.ListAsync(0,50)).Id);
    }
    [AvaloniaFact]
    public async Task UnshownWindowNeverOpensAConfirmationDialogOrDeletesByDefault()
    {
        using var fixture=await HistoryFixture.CreateAsync(1);await InvokeAsync(fixture.Window,"RefreshHistoryAsync");
        Assert.False(await InvokeAsync<bool>(fixture.Window,"DeleteSelectedHistoryAsync"));
        Assert.Single(await fixture.Store.ListAsync(0,50));Assert.False(fixture.Window.IsVisible);
    }
    [AvaloniaFact]
    public async Task DuplicateFlushesTheSelectedCaptureAndPreservesEditableAssetsAndName()
    {
        using var fixture=await HistoryFixture.CreateAsync(1);await InvokeAsync(fixture.Window,"RefreshHistoryAsync");
        fixture.Editor.InsertImage(EditorTests.Png(12,8));fixture.Editor.Rename("편집한 캡처");
        var before=fixture.Editor.Snapshot();
        await InvokeAsync(fixture.Window,"DuplicateSelectedHistoryAsync");
        var records=await fixture.Store.ListAsync(0,50);Assert.Equal(2,records.Count);
        var duplicate=await fixture.Store.LoadAsync(fixture.Editor.CaptureId!.Value);
        Assert.NotEqual(before.Id,duplicate.Id);Assert.True(duplicate.CapturedAtUtc>before.CapturedAtUtc);
        Assert.Equal("편집한 캡처",duplicate.Name);Assert.Equal(before.Annotations,duplicate.Annotations);
        Assert.Equal(before.OriginalPng,duplicate.OriginalPng);
        var hash=Assert.Single(before.StickerAssets).Key;Assert.Equal(before.StickerAssets[hash],duplicate.StickerAssets[hash]);
        var savedOriginal=await fixture.Store.LoadAsync(before.Id);Assert.Equal(before.Annotations,savedOriginal.Annotations);
    }
    [AvaloniaFact]
    public async Task HistoryRenamePersistsAndSearchRefreshCannotReopenAnUnmatchedCapture()
    {
        using var fixture=await HistoryFixture.CreateAsync(2);await InvokeAsync(fixture.Window,"RefreshHistoryAsync");
        await InvokeAsync(fixture.Window,"RenameSelectedHistoryAsync","검색 대상");
        Assert.Equal("검색 대상",(await fixture.Store.LoadAsync(fixture.Records[0].Id)).Name);
        fixture.Window.FindControl<TextBox>("SearchBox")!.Text="does-not-match";
        Dispatcher.UIThread.RunJobs(); // TextBox posts TextChanged; settle it before awaiting the explicit refresh.
        await InvokeAsync(fixture.Window,"RefreshHistoryAsync");
        Assert.Empty(fixture.List.Items.Cast<CaptureListItem>());Assert.Null(fixture.List.SelectedItem);
        Assert.Equal(fixture.Records[0].Id,fixture.Editor.CaptureId);
    }
    [AvaloniaFact]
    public async Task CollapsingHistoryPersistsItsWidthAndRestoresIt()
    {
        using var fixture=await HistoryFixture.CreateAsync(1);
        var grid=fixture.Window.FindControl<Grid>("WorkspaceGrid")!;
        grid.ColumnDefinitions[0].Width=new Avalonia.Controls.GridLength(312);
        Invoke(fixture.Window,"SetHistoryPanelVisible",false);
        Assert.False(fixture.Window.FindControl<Border>("HistoryPanel")!.IsVisible);
        Assert.Equal(0,grid.ColumnDefinitions[0].Width.Value);
        var saved=DesktopPreferences.Load(fixture.Path);
        Assert.False((bool)typeof(DesktopPreferences).GetProperty("HistoryVisible")!.GetValue(saved)!);
        Assert.Equal(312,(double)typeof(DesktopPreferences).GetProperty("HistoryPanelWidth")!.GetValue(saved)!);
        Invoke(fixture.Window,"SetHistoryPanelVisible",true);
        Assert.True(fixture.Window.FindControl<Border>("HistoryPanel")!.IsVisible);
        Assert.Equal(312,grid.ColumnDefinitions[0].Width.Value);
    }
    [AvaloniaFact]
    public async Task PagingUsesMetadataWithoutDecodingTheWholeHistoryCollection()
    {
        using var fixture=await HistoryFixture.CreateAsync(55);await InvokeAsync(fixture.Window,"RefreshHistoryAsync");
        Assert.Equal(50,fixture.List.Items.Count);
        await InvokeAsync(fixture.Window,"LoadMoreHistoryAsync");
        Assert.Equal(55,fixture.List.Items.Count);
        Assert.Equal(0,(int)typeof(MainWindow).GetProperty("HistoryThumbnailCacheCount")!.GetValue(fixture.Window)!);
        fixture.Window.FindControl<TextBox>("SearchBox")!.Text="캡처 54";
        Dispatcher.UIThread.RunJobs();
        await InvokeAsync(fixture.Window,"RefreshHistoryAsync");
        Assert.Equal(fixture.Records[54].Id,Assert.Single(fixture.List.Items.Cast<CaptureListItem>()).Id);
    }
    [AvaloniaFact]
    public async Task ThumbnailCacheEvictsOldBitmapsInvalidatesRevisionsAndClearsImageReferences()
    {
        var type=typeof(MainWindow).Assembly.GetType("SnapBerry.Desktop.Views.HistoryThumbnailCache")!;
        var cache=Activator.CreateInstance(type,[2])!;
        var images=Enumerable.Range(0,3).Select(_=>new Image()).ToArray();
        var panel=new StackPanel();foreach(var image in images)panel.Children.Add(image);
        // The assembly uses UseHeadless: Show only registers this memory window, with no OS window/focus.
        var host=new Window{Content=panel};host.Show();
        var records=Enumerable.Range(0,3).Select(_=>new CaptureListItem(Guid.NewGuid(),DateTimeOffset.UtcNow,1,1){Revision=1}).ToArray();
        var png=EditorTests.Png(1,1);
        async Task Attach(CaptureListItem item,Image image)=>await (Task)type.GetMethod("AttachAsync")!.Invoke(cache,[item,image,(Func<Task<byte[]>>)(()=>Task.FromResult(png))])!;
        try
        {
            await Attach(records[0],images[0]);await Attach(records[1],images[1]);await Attach(records[2],images[2]);
            Assert.Null(images[0].Source);Assert.NotNull(images[1].Source);Assert.NotNull(images[2].Source);
            Assert.Equal(2,(int)type.GetProperty("Count")!.GetValue(cache)!);
            var previous=images[2].Source;var revised=records[2] with{Revision=2};await Attach(revised,images[2]);
            Assert.NotSame(previous,images[2].Source);
            type.GetMethod("KeepValid")!.Invoke(cache,[new[]{revised}]);
            Assert.Equal(1,(int)type.GetProperty("Count")!.GetValue(cache)!);Assert.Null(images[1].Source);
            ((IDisposable)cache).Dispose();Assert.Null(images[2].Source);
        }
        finally{((IDisposable)cache).Dispose();host.Close();}
    }
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AThumbnailFinishingAfterRevisionChangeOrDisposalNeverReattaches(bool dispose)
    {
        var type=typeof(MainWindow).Assembly.GetType("SnapBerry.Desktop.Views.HistoryThumbnailCache")!;
        var cache=Activator.CreateInstance(type,[2])!;var image=new Image();
        var host=new Window{Content=image};host.Show(); // UseHeadless memory window only.
        var item=new CaptureListItem(Guid.NewGuid(),DateTimeOffset.UtcNow,1,1){Revision=1};
        var completion=new TaskCompletionSource<byte[]>();
        var work=(Task)type.GetMethod("AttachAsync")!.Invoke(cache,[item,image,(Func<Task<byte[]>>)(()=>completion.Task)])!;
        try
        {
            if(dispose)((IDisposable)cache).Dispose();
            else type.GetMethod("KeepValid")!.Invoke(cache,[new[]{item with{Revision=2}}]);
            completion.SetResult(EditorTests.Png(1,1));await work;
            Assert.Null(image.Source);Assert.Equal(0,(int)type.GetProperty("Count")!.GetValue(cache)!);
        }
        finally{((IDisposable)cache).Dispose();host.Close();}
    }
    [AvaloniaFact]
    public async Task AHistoryLoadFinishingAfterANewDocumentWasOpenedCannotReplaceIt()
    {
        using var fixture=await HistoryFixture.CreateAsync(1);
        var ownedStore=typeof(MainWindow).GetField("store",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(fixture.Window)!;
        var gate=typeof(CaptureLibraryStore).GetField("gate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(ownedStore)!;
        var item=Assert.Single(await fixture.Store.ListAsync(0,50));var replacement=EditorTests.Record() with{Name="방금 연 이미지"};
        Task load;
        // Hold the real store boundary: the pending disk load cannot finish before the new document opens.
        Monitor.Enter(gate);
        try
        {
            load=InvokeAsync(fixture.Window,"OpenHistoryAsync",item);
            fixture.Editor.Open(replacement);
        }
        finally{Monitor.Exit(gate);}
        await load;
        Assert.Equal(replacement.Id,fixture.Editor.CaptureId);Assert.Equal("방금 연 이미지",fixture.Editor.Name);
    }
    private static object? Invoke(MainWindow window,string name,params object?[] args)=>
        (typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
          ??throw new InvalidOperationException("Missing history command: "+name)).Invoke(window,args);
    private static Task InvokeAsync(MainWindow window,string name,params object?[] args)=>(Task)Invoke(window,name,args)!;
    private static Task<T> InvokeAsync<T>(MainWindow window,string name,params object?[] args)=>(Task<T>)Invoke(window,name,args)!;
    private static void SetConfirmation(MainWindow window,Func<int,Task<bool>> confirm)=>
        (typeof(MainWindow).GetProperty("ConfirmHistoryDeletionAsync")??throw new InvalidOperationException("Missing injectable history confirmation")).SetValue(window,confirm);
    private sealed class HistoryFixture:IDisposable
    {
        public required string Path {get;init;}public required CaptureLibraryStore Store {get;init;}
        public required MainWindow Window {get;init;}public required DesktopEditor Editor {get;init;}
        public required CaptureRecord[] Records {get;init;}public ListBox List=>Window.FindControl<ListBox>("HistoryList")!;
        public static async Task<HistoryFixture> CreateAsync(int count)
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SnapBerry-history-"+Guid.NewGuid());
            var store=new CaptureLibraryStore(System.IO.Path.Combine(path,"Library"));
            var records=Enumerable.Range(0,count).Select(i=>EditorTests.Record() with{Name="캡처 "+i,CapturedAtUtc=DateTimeOffset.UtcNow.AddDays(-i-1)}).ToArray();
            foreach(var record in records)await store.SaveAsync(record);
            var editor=new DesktopEditor();return new(){Path=path,Store=store,Editor=editor,Window=new(editor,new NoCapture(),path),Records=records};
        }
        public void Select(params Guid[] ids)
        {
            Window.SetBusy(true);
            try{List.SelectedItems!.Clear();foreach(var item in List.Items.Cast<CaptureListItem>().Where(item=>ids.Contains(item.Id)))List.SelectedItems.Add(item);}
            finally{Window.SetBusy(false);}
        }
        public void Dispose(){Window.Dispose();Window.Close();if(Directory.Exists(Path))Directory.Delete(Path,true);}
    }
    private sealed class NoCapture:ICaptureService
    {
        public Task<byte[]?> CaptureAsync(DesktopCaptureMode mode,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("실제 캡처 금지");
    }
}
