using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using SnapBerry.Core;
using SnapBerry.App.Library;
using SnapBerry.App.Rendering;
using SnapBerry.App.Stickers;
using SnapBerry.App.Export;
using SnapBerry.App.Import;
namespace SnapBerry.App.Editor;
public enum EditorTool { Select,Arrow,Text,Rectangle,Mosaic,Number,Sticker,Crop,MagicWand }
// 편집 문서·선택·도구 기본값·자원과 합성 캐시를 관리하며 UI와 파일 저장 모델을 연결한다.
public sealed partial class EditorViewModel
{
    public AnnotationDocument? Document {get;private set;}public CommandHistory? History {get;private set;}
    public StickerAssetStore Assets {get;private set;}=new();public BitmapSource? Original {get;private set;}
    private CaptureRecord? record;private BitmapSource? composite;
    public Guid? CaptureId=>record?.Id;public Guid? SelectedId {get;set;}public EditorTool Tool {get;set;}=EditorTool.Select;
    public string Name=>record?.Name??"";
    public AnnotationStyle Style {get;set;}=new();public int MosaicSize {get;set;}=12;
    public StickerDefinition? SelectedSticker {get;set;}public double Zoom {get;set;}=1;
    public event EventHandler? Changed;
    public Annotation? Selected=>Document?.Annotations.FirstOrDefault(x=>x.Id==SelectedId);
    internal static Annotation MeasureText(Annotation a)
    {
        if(a.Kind!=AnnotationKind.Text)return a;
        var t=new System.Windows.Media.FormattedText(a.Text,System.Globalization.CultureInfo.CurrentCulture,System.Windows.FlowDirection.LeftToRight,new System.Windows.Media.Typeface("Segoe UI"),a.Style.FontSize,System.Windows.Media.Brushes.Black,1);
        return a with{Width=Math.Max(1,t.Width),Height=Math.Max(1,t.Height)};
    }
    public void UpdateText(Guid id,string text)
    {
        // 번호 값 편집은 표시 문자열뿐 아니라 해당 형식의 다음 순번도 한 명령으로 갱신한다.
        AnnotationLimits.ValidateText(text);var a=Document?.Annotations.FirstOrDefault(x=>x.Id==id)??throw new InvalidOperationException();
        if(a.Kind!=AnnotationKind.StepMarker){History!.Replace(MeasureText(a with{Text=text}));return;}
        var label=text.Trim();var mode=long.TryParse(label,out _)?StepSequenceMode.Numeric:label.All(c=>c is >= 'A' and <= 'Z')?StepSequenceMode.UppercaseAlphabetic:StepSequenceMode.Alphabetic;
        var value=StepLabelFormatter.Parse(label,mode);var next=checked(value+1);var s=Document!.Sequence;
        var sequence=(s with{Mode=mode}).WithNextValue(next);
        var updated=a with{Text=StepLabelFormatter.Format(value,mode)};var index=Document.Annotations.FindIndex(x=>x.Id==id);
        History!.Execute(()=>{Document.Annotations[index]=updated;Document.Sequence=sequence;});
    }
    public void UpdateSelectedStyle(Func<AnnotationStyle,AnnotationStyle> update)
    {Style=update(Style);if(Selected is Annotation a)History!.Replace(MeasureText(a with{Style=update(a.Style)}));}
    public void Open(CaptureRecord value)
    {
        // 검증과 새 문서 구성을 끝낸 뒤 상태를 교체한다. 열기에 실패하면 기존 편집 상태를 유지한다.
        CaptureLibraryStore.Validate(value);CaptureLibraryStore.ValidateImageAssetSizes(value.OriginalPng.LongLength,value.StickerAssets.Values.Select(a=>a.LongLength));
        CompositeRenderer.GuardSize(value.Width,value.Height);var original=PngFileWriter.Decode(value.OriginalPng);
        if(original.PixelWidth!=value.Width||original.PixelHeight!=value.Height)throw new InvalidDataException("Image size does not match.");
        var document=new AnnotationDocument(value.Width,value.Height){Sequence=value.Sequence,CropBounds=value.CropBounds??new(0,0,value.Width,value.Height),ResizeSize=value.ResizeSize,BackgroundAssetHash=value.BackgroundAssetHash};document.Annotations.AddRange(value.Annotations);
        var assets=new StickerAssetStore();assets.Import(value.StickerAssets);foreach(var a in value.Annotations.Where(x=>x.Kind==AnnotationKind.Sticker))assets.GetBitmap(a.AssetHash);
        if(value.BackgroundAssetHash is {} background)
        {
            var image=assets.GetBitmap(background);
            if(image.PixelWidth!=value.Width||image.PixelHeight!=value.Height)throw new InvalidDataException("누끼 이미지의 크기가 원본과 다릅니다.");
        }
        var history=new CommandHistory(document);history.MarkPersisted(history.CurrentStateToken);history.Changed+=(_,_)=>{composite=null;ClearMagicWand();Changed?.Invoke(this,EventArgs.Empty);};
        record=value;Original=original;Document=document;Assets=assets;History=history;SelectedId=null;composite=null;ClearMagicWand();Changed?.Invoke(this,EventArgs.Empty);
    }
    public CaptureRecord Snapshot()
    {
        // 참조 중인 스티커만 포함한다. 원본 PNG는 유지하고 편집 메타데이터를 덧붙인다.
        if(record is null||Document is null)throw new InvalidOperationException("No capture is open.");
        var hashes=Document.ReferencedAssetHashes;
        return record with {SchemaVersion=Document.BackgroundAssetHash is null?1:2,BackgroundAssetHash=Document.BackgroundAssetHash,UpdatedAtUtc=DateTimeOffset.UtcNow,CropBounds=Document.CropBounds==new PixelRect(0,0,Document.SourceWidth,Document.SourceHeight)?null:Document.CropBounds,ResizeSize=Document.ResizeSize,Annotations=Document.Annotations.ToArray(),Sequence=Document.Sequence,StickerAssets=hashes.ToDictionary(h=>h,h=>Assets.Data[h])};
    }
    // 저장이 완료된 이름만 반영한다. 편집 문서와 실행 취소 스택은 교체하지 않는다.
    public void SetCaptureName(string name)
    {if(record is null)throw new InvalidOperationException("No capture is open.");record=record with{Name=CaptureName.Normalize(name)};}
    // 문서 변경 시 캐시를 무효화한다. 선택 변경이나 확대만으로 전체 이미지를 다시 합성하지 않는다.
    public BitmapSource? Background=>Document?.BackgroundAssetHash is {} hash?Assets.GetBitmap(hash):Original;
    public void ApplyBackgroundRemoval(byte[] png)
    {
        if(Document is null||History is null)throw new InvalidOperationException("이미지를 먼저 열어 주세요.");
        var replacement=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        CaptureLibraryStore.ValidateImageAssetSizes(record!.OriginalPng.LongLength,Document.AssetHashesWithBackground(replacement).Select(h=>h==replacement?png.LongLength:Assets.Data[h].LongLength));
        var image=PngFileWriter.Decode(png);
        if(image.PixelWidth!=Document.SourceWidth||image.PixelHeight!=Document.SourceHeight)throw new InvalidDataException("누끼 이미지의 크기가 원본과 다릅니다.");
        var hash=Assets.AddPng(png);SelectedId=null;History.SetBackground(hash);
    }
    public BitmapSource? Composite()=>Document is null||Background is null?null:composite??=new CompositeRenderer().Render(Background,Document.Annotations,Assets,Document.CropBounds,Document.ResizeSize);
    public void CropTo(PixelRect relativeBounds)
    {if(History is null)return;SelectedId=null;History.Crop(relativeBounds);}
    public void InsertSticker(StickerDefinition definition,PixelPoint point)
    {
        if(History is null)return;var bytes=StickerCatalog.CreatePng(definition);var hash=Assets.AddPng(bytes);var image=Assets.GetBitmap(hash);var ratio=(double)image.PixelWidth/image.PixelHeight;
        var a=new Annotation{Kind=AnnotationKind.Sticker,X=point.X,Y=point.Y,Width=ratio>=1?64:64*ratio,Height=ratio>=1?64/ratio:64,AssetHash=hash,StickerId=definition.Id,Text=definition.IsKeycap?definition.Label:""};SelectedId=a.Id;History.Add(a);
    }
    public void InsertImage(byte[] png,ClipboardImageSize? objectSize=null)
    {
        if(Document is null||History is null)throw new InvalidOperationException("No capture is open.");
        CaptureLibraryStore.ValidateImageAssetSizes(record!.OriginalPng.LongLength,[png.LongLength]);
        var hash=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        var sizes=Document.ReferencedAssetHashes.ToDictionary(key=>key,key=>Assets.Data[key].LongLength);
        sizes[hash]=png.LongLength;CaptureLibraryStore.ValidateImageAssetSizes(record.OriginalPng.LongLength,sizes.Values);
        var image=PngFileWriter.Decode(png);CompositeRenderer.GuardSize(image.PixelWidth,image.PixelHeight);
        var output=Document.OutputSize;
        // 출력 픽셀 기준으로 원래 비율을 유지한 뒤 자르기·크기 변경 전의 문서 좌표로 환산한다.
        var fit=Math.Min(1,Math.Min((double)output.Width/image.PixelWidth,(double)output.Height/image.PixelHeight));
        var width=(objectSize is {IsValid:true} size?size.Width:image.PixelWidth*fit)*Document.Width/output.Width;
        var height=(objectSize is {IsValid:true} size2?size2.Height:image.PixelHeight*fit)*Document.Height/output.Height;
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0||width>1000000||height>1000000)
            throw new InvalidDataException("붙여넣을 이미지의 표시 크기가 너무 큽니다.");
        Assets.AddPng(png);
        var annotation=new Annotation{Kind=AnnotationKind.Sticker,StickerId="pasted-image",AssetHash=hash,
            X=Document.CropBounds.X+(Document.Width-width)/2,Y=Document.CropBounds.Y+(Document.Height-height)/2,Width=width,Height=height};
        SelectedId=annotation.Id;History.Add(annotation);
    }
    public void InsertAnnotation(ClipboardImageContent content,PixelPoint center)
    {
        if(Document is null||History is null)throw new InvalidOperationException("No capture is open.");
        var source=content.Annotation??throw new InvalidDataException("복사한 개체 정보가 없습니다.");
        var annotation=source with{Id=Guid.NewGuid(),X=center.X-source.Width/2,Y=center.Y-source.Height/2};
        CaptureLibraryStore.Validate(new(){Width=1,Height=1,Annotations=[annotation]});
        if(annotation.Kind==AnnotationKind.Sticker)
        {
            var png=PngFileWriter.Encode(content.Image);CaptureLibraryStore.ValidateImageAssetSizes(record!.OriginalPng.LongLength,[png.LongLength]);
            var hash=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
            var sizes=Document.ReferencedAssetHashes.ToDictionary(key=>key,key=>Assets.Data[key].LongLength);
            sizes[hash]=png.LongLength;CaptureLibraryStore.ValidateImageAssetSizes(record.OriginalPng.LongLength,sizes.Values);
            annotation=annotation with{AssetHash=Assets.AddPng(png)};
        }
        SelectedId=annotation.Id;History.Add(annotation);
    }
    public void UpdateKeycapLabel(Guid id,string label)
    {
        // 키 라벨은 PNG 안에 그려져 있으므로 새 자원을 생성하고 표시 비율도 다시 계산한다.
        if(string.IsNullOrWhiteSpace(label)||label.Length>64)throw new ArgumentException("Enter a key label of 1–64 characters.");
        var a=Document?.Annotations.FirstOrDefault(x=>x.Id==id)??throw new InvalidOperationException();
        var definition=StickerCatalog.Load().FirstOrDefault(x=>x.Id==a.StickerId&&x.IsKeycap)??throw new InvalidOperationException("This sticker is not a keyboard key.");
        var hash=Assets.AddPng(StickerCatalog.CreatePng(definition,label));var bitmap=Assets.GetBitmap(hash);
        History!.Replace(a with {Text=label,AssetHash=hash,Width=a.Height*bitmap.PixelWidth/bitmap.PixelHeight});
    }
    public void Clear(){ClearMagicWand();record=null;Document=null;History=null;Original=null;composite=null;Assets=new();SelectedId=null;Changed?.Invoke(this,EventArgs.Empty);}
}
