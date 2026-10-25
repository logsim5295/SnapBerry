using System.Collections.Concurrent;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;

namespace SnapBerry.App.Rendering;

// WPF 합성은 전용 STA 스레드에서 직렬 처리한다. UI 스레드에는 동결된 작은 비트맵만 전달한다.
internal sealed class HistoryThumbnailLoader(CaptureLibraryStore store):IDisposable
{
    private sealed record Work(CaptureListItem Item,CancellationToken Cancellation,TaskCompletionSource<BitmapSource> Result);
    private readonly BlockingCollection<Work> queue=new();
    private readonly object gate=new();
    private bool started,disposed;

    public Task<BitmapSource> LoadAsync(CaptureListItem item,CancellationToken cancellation)
    {
        var result=new TaskCompletionSource<BitmapSource>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(gate)
        {
            if(disposed)return Task.FromCanceled<BitmapSource>(new CancellationToken(true));
            if(!started)
            {
                var thread=new Thread(Run){IsBackground=true,Name="SnapBerry thumbnails"};
                thread.SetApartmentState(ApartmentState.STA);started=true;thread.Start();
            }
            queue.Add(new(item,cancellation,result));
        }
        return result.Task;
    }
    private void Run()
    {
        try
        {
            foreach(var work in queue.GetConsumingEnumerable())
            {
                try
                {
                    work.Cancellation.ThrowIfCancellationRequested();
                    var bytes=store.LoadThumbnailAsync(work.Item).GetAwaiter().GetResult();
                    BitmapSource? thumbnail=null;
                    if(bytes is not null)
                    {
                        try{thumbnail=PngFileWriter.Decode(bytes);}
                        catch(Exception e)when(e is IOException or InvalidDataException or FormatException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException){}
                    }
                    work.Cancellation.ThrowIfCancellationRequested();
                    if(thumbnail is null)
                    {
                        var record=store.LoadAsync(work.Item.Id).GetAwaiter().GetResult();
                        work.Cancellation.ThrowIfCancellationRequested();
                        var model=new EditorViewModel();model.Open(record);thumbnail=ThumbnailRenderer.Create(model.Composite()!);
                        work.Cancellation.ThrowIfCancellationRequested();
                        store.SaveThumbnailAsync(work.Item,PngFileWriter.Encode(thumbnail)).GetAwaiter().GetResult();
                    }
                    work.Cancellation.ThrowIfCancellationRequested();
                    work.Result.TrySetResult(thumbnail);
                }
                catch(OperationCanceledException){work.Result.TrySetCanceled(work.Cancellation);}
                catch(Exception e){work.Result.TrySetException(e);}
            }
        }
        finally{queue.Dispose();System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();}
    }
    public void Dispose()
    {
        lock(gate)
        {
            if(disposed)return;disposed=true;
            queue.CompleteAdding();if(!started)queue.Dispose();
        }
    }
}
