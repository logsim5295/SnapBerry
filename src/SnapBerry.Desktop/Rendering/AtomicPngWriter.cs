namespace SnapBerry.Desktop.Rendering;

public static class AtomicPngWriter
{
    public static async Task WriteAsync(string path,byte[] png,CancellationToken token=default)
    {
        if(png.Length<8||!png.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("PNG 데이터가 올바르지 않습니다.");
        using(var verified=SkiaDocumentRenderer.Decode(png)){}
        var target=Path.GetFullPath(path);var directory=Path.GetDirectoryName(target)!;Directory.CreateDirectory(directory);
        var temp=Path.Combine(directory,".SnapBerry-"+Guid.NewGuid().ToString("N")+".tmp");
        try{await File.WriteAllBytesAsync(temp,png,token);token.ThrowIfCancellationRequested();File.Move(temp,target,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
