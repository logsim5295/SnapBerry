using SnapBerry.App.Library;
using SnapBerry.Desktop.Rendering;
using System.Security.Cryptography;

namespace SnapBerry.Desktop.Editing;

public sealed partial class DesktopEditor
{
    public void ApplyBackgroundRemoval(byte[] png)
    {
        if(Document is null||History is null)throw new InvalidOperationException("이미지를 먼저 열어 주세요.");
        var replacement=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        CaptureLibraryStore.ValidateImageAssetSizes(record!.OriginalPng.LongLength,Document.AssetHashesWithBackground(replacement).Select(h=>h==replacement?png.LongLength:assets[h].LongLength));
        using var image=SkiaDocumentRenderer.Decode(png);
        if(image.Width!=Document.SourceWidth||image.Height!=Document.SourceHeight)throw new InvalidDataException("누끼 이미지의 크기가 원본과 다릅니다.");
        var hash=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();assets[hash]=png.ToArray();
        SelectedId=null;History.SetBackground(hash);
    }
}
