using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using SnapBerry.App.Library;

namespace SnapBerry.Desktop.Views;

// Owned by the UI thread. Metadata never allocates images; only realized rows request a preview.
internal sealed class HistoryThumbnailCache(int capacity):IDisposable
{
    private sealed record Entry(Bitmap Bitmap,LinkedListNode<(Guid Id,long Revision)> Node,List<WeakReference<Image>> Targets);
    private readonly Dictionary<(Guid Id,long Revision),Entry> entries=[];
    private readonly Dictionary<(Guid Id,long Revision),Task<byte[]>> pending=[];
    private readonly LinkedList<(Guid Id,long Revision)> recency=[];
    private readonly SemaphoreSlim renderer=new(2,2);
    private bool disposed;private Dictionary<Guid,long>? revisions;
    public int Count=>entries.Count;
    public async Task AttachAsync(CaptureListItem item,Image target,Func<Task<byte[]>> render)
    {
        if(disposed||item.Damaged||!target.IsAttachedToVisualTree()||!IsCurrent(item))return;
        var key=(item.Id,item.Revision);
        if(entries.TryGetValue(key,out var existing)){Use(existing,target);return;}
        if(!pending.TryGetValue(key,out var work))pending[key]=work=RenderAsync(render);
        byte[] bytes;
        try{bytes=await work;}
        finally{if(pending.TryGetValue(key,out var current)&&ReferenceEquals(current,work))pending.Remove(key);}
        if(disposed||!target.IsAttachedToVisualTree()||!IsCurrent(item))return;
        if(!entries.TryGetValue(key,out existing))
        {
            using var stream=new MemoryStream(bytes,false);var bitmap=new Bitmap(stream);
            existing=new(bitmap,recency.AddLast(key),[]);entries.Add(key,existing);
            while(entries.Count>Math.Max(1,capacity)&&recency.First is not null)Remove(recency.First.Value);
        }
        Use(existing,target);
    }
    private async Task<byte[]> RenderAsync(Func<Task<byte[]>> render)
    {
        await renderer.WaitAsync().ConfigureAwait(false);
        try{if(disposed)throw new OperationCanceledException();return await render().ConfigureAwait(false);}
        finally{renderer.Release();}
    }
    private void Use(Entry entry,Image target)
    {
        recency.Remove(entry.Node);recency.AddLast(entry.Node);
        entry.Targets.RemoveAll(reference=>!reference.TryGetTarget(out var image)||!image.IsAttachedToVisualTree());
        if(!entry.Targets.Any(reference=>reference.TryGetTarget(out var image)&&ReferenceEquals(image,target)))entry.Targets.Add(new(target));
        target.Source=entry.Bitmap;
    }
    private bool IsCurrent(CaptureListItem item)=>revisions is null||revisions.TryGetValue(item.Id,out var revision)&&revision==item.Revision;
    public void KeepValid(IEnumerable<CaptureListItem> items)
    {
        revisions=items.ToDictionary(item=>item.Id,item=>item.Revision);
        var valid=revisions.Select(item=>(item.Key,item.Value)).ToHashSet();
        foreach(var key in entries.Keys.Where(key=>!valid.Contains(key)).ToArray())Remove(key);
    }
    private void Remove((Guid Id,long Revision) key)
    {
        if(!entries.Remove(key,out var entry))return;recency.Remove(entry.Node);
        foreach(var reference in entry.Targets)
            if(reference.TryGetTarget(out var image)&&ReferenceEquals(image.Source,entry.Bitmap))image.Source=null;
        entry.Bitmap.Dispose();
    }
    public void Dispose(){if(disposed)return;disposed=true;foreach(var key in entries.Keys.ToArray())Remove(key);pending.Clear();}
}
