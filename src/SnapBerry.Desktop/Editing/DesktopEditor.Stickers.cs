using System.Security.Cryptography;
using SnapBerry.App.Library;
using SnapBerry.App.Stickers;
using SnapBerry.Desktop.Clipboard;
using SnapBerry.Desktop.Stickers;
using SnapBerry.Desktop.Rendering;

namespace SnapBerry.Desktop.Editing;

public sealed partial class DesktopEditor
{
    public void InsertSticker(StickerDefinition definition)
    {
        if(Document is null)return;var png=StickerPngRenderer.CreatePng(definition);using var bitmap=SkiaDocumentRenderer.Decode(png);var ratio=(double)bitmap.Width/bitmap.Height;
        InsertImageAsset(png,new ImageObjectSize(ratio>=1?64:64*ratio,ratio>=1?64/ratio:64),definition.Id,definition.IsKeycap?definition.Label:"");
    }
    public void UpdateKeycapLabel(string label)
    {
        if(Selected is not {} selected)return;var definition=DesktopStickerCatalog.Load().FirstOrDefault(x=>x.Id==selected.StickerId&&x.IsKeycap)??throw new InvalidOperationException("키보드 스티커를 선택해 주세요.");
        var png=StickerPngRenderer.CreatePng(definition,label);var hash=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        var sizes=Document!.Annotations.Where(a=>a.Kind==Core.AnnotationKind.Sticker&&a.Id!=selected.Id).Select(a=>a.AssetHash).Distinct().Concat(Document.BackgroundAssetHash is {} background?[background]:[]).Distinct().Select(key=>assets[key].LongLength).Append(png.LongLength);
        CaptureLibraryStore.ValidateImageAssetSizes(record!.OriginalPng.LongLength,sizes);using var bitmap=SkiaDocumentRenderer.Decode(png);assets[hash]=png;
        History!.Replace(selected with{AssetHash=hash,Text=label,Width=selected.Height*bitmap.Width/bitmap.Height});
    }
}
