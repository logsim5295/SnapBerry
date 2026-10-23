using System.Security.Cryptography;
using SnapBerry.App.Library;
using SnapBerry.Core;
using SnapBerry.Desktop.Rendering;
using SnapBerry.Desktop.Clipboard;

namespace SnapBerry.Desktop.Editing;

public enum DesktopTool { Select,Arrow,Text,Rectangle,Mosaic,Number,Image,Crop,MagicWand }

public sealed partial class DesktopEditor : IDisposable
{
    private CaptureRecord? record;private Dictionary<string,byte[]> assets=[];private byte[]? composite;
    public AnnotationDocument? Document {get;private set;}public CommandHistory? History {get;private set;}
    public Guid? CaptureId=>record?.Id;public string Name=>record?.Name??"";
    public Guid? SelectedId {get;set;}public Annotation? Selected=>Document?.Annotations.FirstOrDefault(a=>a.Id==SelectedId);
    public AnnotationStyle Style {get;set;}=new();public DesktopTool Tool {get;set;}=DesktopTool.Select;
    public IReadOnlyDictionary<string,byte[]> Assets=>assets;public event EventHandler? Changed;
    public void OpenImage(byte[] png,CaptureMode mode=CaptureMode.ImageFile)
    {
        using var decoded=SkiaDocumentRenderer.Decode(png);var normalized=SkiaDocumentRenderer.Encode(decoded);
        Open(new(){Width=decoded.Width,Height=decoded.Height,OriginalPng=normalized,Mode=mode});
    }
    public void Open(CaptureRecord value)
    {
        CaptureLibraryStore.Validate(value);CaptureLibraryStore.ValidateImageAssetSizes(value.OriginalPng.LongLength,value.StickerAssets.Values.Select(a=>a.LongLength));
        ImageDimensions.GuardSize(value.Width,value.Height);using var image=SkiaDocumentRenderer.Decode(value.OriginalPng);
        if(image.Width!=value.Width||image.Height!=value.Height||!CaptureName.IsValid(value.Name))throw new InvalidDataException("이미지 정보가 올바르지 않습니다.");
        var document=new AnnotationDocument(value.Width,value.Height){CropBounds=value.CropBounds??new(0,0,value.Width,value.Height),ResizeSize=value.ResizeSize,Sequence=value.Sequence,BackgroundAssetHash=value.BackgroundAssetHash};
        document.Annotations.AddRange(value.Annotations);var nextAssets=value.StickerAssets.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToArray());
        foreach(var a in document.Annotations.Where(a=>a.Kind==AnnotationKind.Sticker))
        {
            if(!nextAssets.TryGetValue(a.AssetHash,out var bytes)||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=a.AssetHash)throw new InvalidDataException("추가 이미지가 손상되었습니다.");
            using var asset=SkiaDocumentRenderer.Decode(bytes);
        }
        if(document.BackgroundAssetHash is {} background)
        {
            if(!nextAssets.TryGetValue(background,out var bytes)||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=background)throw new InvalidDataException("누끼 이미지가 손상되었습니다.");
        }
        var rendered=SkiaDocumentRenderer.Render(value.OriginalPng,document,nextAssets);
        var history=new CommandHistory(document);history.MarkPersisted(history.CurrentStateToken);history.Changed+=OnHistoryChanged;
        if(History is not null)History.Changed-=OnHistoryChanged;
        ClearMagicWand();record=value with{OriginalPng=value.OriginalPng.ToArray()};Document=document;assets=nextAssets;History=history;SelectedId=null;composite=rendered;Changed?.Invoke(this,EventArgs.Empty);
    }
    private void OnHistoryChanged(object? sender,EventArgs args){ClearMagicWand();composite=null;if(Selected is null)SelectedId=null;Changed?.Invoke(this,EventArgs.Empty);}
    public void Commit(Annotation annotation)
    {
        if(History is null)return;
        if(annotation.Kind==AnnotationKind.Text)
        {AnnotationLimits.ValidateText(annotation.Text);var size=SkiaDocumentRenderer.MeasureText(annotation.Text,annotation.Style.FontSize);annotation=annotation with{Width=size.Width,Height=size.Height};}
        SelectedId=annotation.Id;History.Add(annotation);
    }
    public void ReplaceSelected(Annotation annotation)
    {
        if(Selected is null)return;
        if(annotation.Kind==AnnotationKind.Text){AnnotationLimits.ValidateText(annotation.Text);var size=SkiaDocumentRenderer.MeasureText(annotation.Text,annotation.Style.FontSize);annotation=annotation with{Width=size.Width,Height=size.Height};}
        History!.Replace(annotation);
    }
    public void DeleteSelected(){if(DeleteMagicWandSelection())return;if(SelectedId is Guid id){SelectedId=null;History?.Remove(id);}}
    public void DuplicateSelected(){if(Selected is Annotation a)Commit(a with{Id=Guid.NewGuid(),X=a.X+12,Y=a.Y+12});}
    public void InsertStep(PixelPoint point){if(History is null)return;History.InsertStep(point,Style);SelectedId=Document!.Annotations.Last().Id;Changed?.Invoke(this,EventArgs.Empty);}
    public void InsertImage(byte[] png,ImageObjectSize? outputSize=null)
        =>InsertImageAsset(png,outputSize,"pasted-image","");
    private void InsertImageAsset(byte[] png,ImageObjectSize? outputSize,string stickerId,string text)
    {
        if(Document is null||History is null)throw new InvalidOperationException("이미지를 먼저 열어 주세요.");
        CaptureLibraryStore.ValidateImageAssetSizes(record!.OriginalPng.LongLength,[png.LongLength]);
        if(outputSize is {IsValid:false})throw new InvalidDataException("붙여넣을 이미지의 표시 크기가 올바르지 않습니다.");
        using var bitmap=SkiaDocumentRenderer.Decode(png);var normalized=SkiaDocumentRenderer.Encode(bitmap);var hash=Convert.ToHexString(SHA256.HashData(normalized)).ToLowerInvariant();
        var sizes=Document.ReferencedAssetHashes.ToDictionary(key=>key,key=>assets[key].LongLength);sizes[hash]=normalized.LongLength;
        CaptureLibraryStore.ValidateImageAssetSizes(record!.OriginalPng.LongLength,sizes.Values);
        var output=Document.OutputSize;var ratio=Math.Min(1,Math.Min((double)output.Width/bitmap.Width,(double)output.Height/bitmap.Height));
        var width=(outputSize?.Width??bitmap.Width*ratio)*Document.Width/output.Width;var height=(outputSize?.Height??bitmap.Height*ratio)*Document.Height/output.Height;
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0||width>1000000||height>1000000)throw new InvalidDataException("붙여넣을 이미지의 표시 크기가 너무 큽니다.");
        assets[hash]=normalized;Commit(new(){Kind=AnnotationKind.Sticker,StickerId=stickerId,Text=text,AssetHash=hash,X=Document.CropBounds.X+(Document.Width-width)/2,Y=Document.CropBounds.Y+(Document.Height-height)/2,Width=width,Height=height});
    }
    public void Rename(string value){if(record is null)return;record=record with{Name=CaptureName.Normalize(value)};Changed?.Invoke(this,EventArgs.Empty);}
    public CaptureRecord Snapshot()
    {
        if(record is null||Document is null)throw new InvalidOperationException("열린 이미지가 없습니다.");
        var hashes=Document.ReferencedAssetHashes;
        return record with{SchemaVersion=Document.BackgroundAssetHash is null?1:2,BackgroundAssetHash=Document.BackgroundAssetHash,UpdatedAtUtc=DateTimeOffset.UtcNow,CropBounds=Document.CropBounds==new PixelRect(0,0,record.Width,record.Height)?null:Document.CropBounds,ResizeSize=Document.ResizeSize,Annotations=Document.Annotations.ToArray(),Sequence=Document.Sequence,StickerAssets=hashes.ToDictionary(h=>h,h=>assets[h])};
    }
    public byte[] RenderPng()=>composite??=SkiaDocumentRenderer.Render(record!.OriginalPng,Document!,assets);
    public async Task SaveAsync(CaptureLibraryStore store)
    {
        if(History is null)return;var token=History.CurrentStateToken;var id=CaptureId;await store.SaveAsync(Snapshot());if(CaptureId==id)History.MarkPersisted(token);
    }
    public void Clear(){ClearMagicWand();if(History is not null)History.Changed-=OnHistoryChanged;History=null;Document=null;record=null;assets=[];composite=null;SelectedId=null;Changed?.Invoke(this,EventArgs.Empty);}
    public void Dispose(){ClearMagicWand();if(History is not null)History.Changed-=OnHistoryChanged;}
}
