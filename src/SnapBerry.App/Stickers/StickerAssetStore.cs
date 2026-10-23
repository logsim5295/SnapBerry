using System.Windows.Media.Imaging;
using System.IO;
using System.Security.Cryptography;
namespace SnapBerry.App.Stickers;
// PNG 내용의 SHA-256을 키로 사용해 중복 스티커를 공유하고, 디코딩한 이미지는 별도로 캐시한다.
public sealed class StickerAssetStore
{
    private readonly Dictionary<string,byte[]> data=[];
    private readonly Dictionary<string,BitmapSource> bitmaps=[];
    public IReadOnlyDictionary<string,byte[]> Data=>data;
    public string AddPng(byte[] png)
    {var hash=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();if(!data.ContainsKey(hash))data[hash]=png.ToArray();return hash;}
    public void Import(IReadOnlyDictionary<string,byte[]> source){foreach(var (hash,png) in source){if(AddPng(png)!=hash)throw new InvalidDataException("Sticker image is damaged.");}}
    // 스트림 수명과 분리하여 이미지를 로드하고 동결하므로 저장 작업과 함께 사용해도 안전하다.
    public BitmapSource GetBitmap(string hash)
    {
        if(bitmaps.TryGetValue(hash,out var b))return b;
        if(!data.TryGetValue(hash,out var bytes))throw new InvalidDataException("Sticker image is missing.");
        using var stream=new MemoryStream(bytes);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        b=decoder.Frames[0];b.Freeze();bitmaps[hash]=b;return b;
    }
}
