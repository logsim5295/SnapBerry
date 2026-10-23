using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using SnapBerry.Core;

namespace SnapBerry.Desktop.Clipboard;

public sealed class AvaloniaEditorClipboard(Func<IClipboard?> getClipboard) : IEditorClipboard
{
    public static readonly DataFormat<byte[]> PngFormat=DataFormat.CreateBytesApplicationFormat("SnapBerry.ImagePng.v1");
    public static readonly DataFormat<string> SizeFormat=DataFormat.CreateStringApplicationFormat("SnapBerry.ImageObject.v1");
    public async Task<ImageClipboardContent?> ReadAsync()
    {
        var clipboard=getClipboard()??throw new InvalidOperationException("클립보드를 사용할 수 없습니다.");
        using var data=await clipboard.TryGetDataAsync();return data is null?null:await ReadDataAsync(data);
    }
    public static async Task<ImageClipboardContent?> ReadDataAsync(IAsyncDataTransfer data)
    {
        foreach(var item in data.Items)
        {
            var raw=await item.TryGetValueAsync(PngFormat);byte[] png;
            if(raw is not null){if(raw.Length>128*1024*1024)throw new InvalidDataException("클립보드 이미지가 너무 큽니다.");png=raw.ToArray();}
            else
            {
                using var bitmap=await item.TryGetBitmapAsync();if(bitmap is null)continue;
                ImageDimensions.GuardSize(bitmap.PixelSize.Width,bitmap.PixelSize.Height);
                using var output=new MemoryStream();bitmap.Save(output,PngBitmapEncoderOptions.Default);png=output.ToArray();
            }
            return new(png,ImageObjectSize.Parse(await item.TryGetValueAsync(SizeFormat)));
        }
        return null;
    }
    public static DataTransfer CreateData(ImageClipboardContent content)
    {
        var png=content.Png.ToArray();var item=new DataTransferItem();
        item.Set(PngFormat,png);item.Set(DataFormat.Bitmap,()=>new Bitmap(new MemoryStream(png,false)));
        if(content.ObjectSize is {IsValid:true} size)item.Set(SizeFormat,size.ToString());
        var data=new DataTransfer();data.Add(item);return data;
    }
    public async Task WriteAsync(ImageClipboardContent content)
    {
        var clipboard=getClipboard()??throw new InvalidOperationException("클립보드를 사용할 수 없습니다.");
        var data=CreateData(content);
        try{await clipboard.SetDataAsync(data);}catch{((IDisposable)data).Dispose();throw;}
        // 성공 후의 수명과 해제는 Avalonia가 관리한다. 지연 이미지 제공자를 미리 Dispose하지 않는다.
    }
}
