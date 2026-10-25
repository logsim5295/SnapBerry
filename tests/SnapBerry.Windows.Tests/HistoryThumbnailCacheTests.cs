using System.IO;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Rendering;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 임시 라이브러리와 메모리 비트맵만 사용하며 창이나 데스크톱을 열지 않는다.
public class HistoryThumbnailCacheTests
{
    private static void WithStore(Action<string,CaptureLibraryStore,CaptureRecord,byte[]> test)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-thumbnail-"+Guid.NewGuid().ToString("N"));
        var record=new CaptureRecord{Width=240,Height=160,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(240,160))};
        var preview=PngFileWriter.Encode(ThumbnailRenderer.Create(RenderingTests.Image(240,160)));
        var store=new CaptureLibraryStore(path);
        try{store.SaveAsync(record).GetAwaiter().GetResult();test(path,store,record,preview);}
        finally{TestFiles.DeleteDirectory(path);}
    });
    private static CaptureListItem Item(CaptureLibraryStore store)=>Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult());

    [Fact] public void CacheSurvivesReopeningLibrary()=>WithStore((path,store,_,preview)=>
    {
        store.SaveThumbnailAsync(Item(store),preview).GetAwaiter().GetResult();
        var reopened=new CaptureLibraryStore(path);
        Assert.Equal(preview,reopened.LoadThumbnailAsync(Item(reopened)).GetAwaiter().GetResult());
    });

    [Fact] public void SavingCaptureInvalidatesPreviewAndRejectsLateOldPreview()=>WithStore((path,store,record,preview)=>
    {
        var old=Item(store);store.SaveThumbnailAsync(old,preview).GetAwaiter().GetResult();
        store.SaveAsync(record with{Name="변경"}).GetAwaiter().GetResult();
        store.SaveThumbnailAsync(old,preview).GetAwaiter().GetResult();
        Assert.Null(store.LoadThumbnailAsync(Item(store)).GetAwaiter().GetResult());
        var reopened=new CaptureLibraryStore(path);
        Assert.Null(reopened.LoadThumbnailAsync(Item(reopened)).GetAwaiter().GetResult());
    });

    [Fact] public void ExternalFileChangeInvalidatesPreview()=>WithStore((path,store,record,preview)=>
    {
        var item=Item(store);store.SaveThumbnailAsync(item,preview).GetAwaiter().GetResult();
        var file=Path.Combine(path,record.Id.ToString("N")+".qcapture");
        File.SetLastWriteTimeUtc(file,File.GetLastWriteTimeUtc(file).AddSeconds(5));
        Assert.Null(store.LoadThumbnailAsync(item).GetAwaiter().GetResult());
        Assert.Null(store.LoadThumbnailAsync(Item(store)).GetAwaiter().GetResult());
    });

    [Fact] public void DeletedCaptureDoesNotLeaveItsPreview()=>WithStore((path,store,record,preview)=>
    {
        var item=Item(store);store.SaveThumbnailAsync(item,preview).GetAwaiter().GetResult();
        Assert.Single(Directory.GetFiles(path,"*.thumb",SearchOption.AllDirectories));
        store.DeleteAsync(record.Id).GetAwaiter().GetResult();
        Assert.Empty(Directory.GetFiles(path,"*.thumb",SearchOption.AllDirectories));
        Assert.Null(store.LoadThumbnailAsync(item).GetAwaiter().GetResult());
    });

    [Fact] public void CorruptOrUnavailableCacheDoesNotBlockLibrary()=>WithStore((path,store,record,preview)=>
    {
        var item=Item(store);store.SaveThumbnailAsync(item,preview).GetAwaiter().GetResult();
        var file=Assert.Single(Directory.GetFiles(path,"*.thumb",SearchOption.AllDirectories));
        File.WriteAllBytes(file,[1,2,3]);
        Assert.Null(store.LoadThumbnailAsync(item).GetAwaiter().GetResult());
        using(var locked=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            Assert.Null(store.LoadThumbnailAsync(item).GetAwaiter().GetResult());
            store.SaveThumbnailAsync(item,preview).GetAwaiter().GetResult();
            Assert.Equal(record.Id,store.LoadAsync(record.Id).GetAwaiter().GetResult().Id);
        }
    });

    [Fact] public void LoaderCreatesCacheAndReusesItWithoutReadingOriginal()=>WithStore((path,store,record,_)=>
    {
        using(var loader=new HistoryThumbnailLoader(store))
        {
            var bitmap=loader.LoadAsync(Item(store),CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            Assert.Equal(120,bitmap.PixelWidth);Assert.Equal(80,bitmap.PixelHeight);Assert.True(bitmap.IsFrozen);
        }
        var reopened=new CaptureLibraryStore(path);var item=Item(reopened);
        using var locked=new FileStream(Path.Combine(path,record.Id.ToString("N")+".qcapture"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        using var cachedLoader=new HistoryThumbnailLoader(reopened);
        var cached=cachedLoader.LoadAsync(item,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.Equal(120,cached.PixelWidth);Assert.True(cached.IsFrozen);
    });

    [Fact] public void LoaderRegeneratesUndecodableCachedPng()=>WithStore((path,store,_,preview)=>
    {
        var item=Item(store);store.SaveThumbnailAsync(item,preview[..24]).GetAwaiter().GetResult();
        using var loader=new HistoryThumbnailLoader(store);
        var bitmap=loader.LoadAsync(item,CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Assert.Equal(120,bitmap.PixelWidth);
        Assert.True(store.LoadThumbnailAsync(item).GetAwaiter().GetResult()!.Length>24);
    });

    [Fact] public void CanceledThumbnailDoesNotReadOrWriteFiles()=>WithStore((path,store,_,_)=>
    {
        using var loader=new HistoryThumbnailLoader(store);
        Assert.ThrowsAny<OperationCanceledException>(()=>loader.LoadAsync(Item(store),new CancellationToken(true)).GetAwaiter().GetResult());
        Assert.Empty(Directory.GetFiles(path,"*.thumb",SearchOption.AllDirectories));
    });
}
