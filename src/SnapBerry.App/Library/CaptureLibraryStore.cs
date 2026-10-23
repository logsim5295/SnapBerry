using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Security.Cryptography;
using System.Buffers.Binary;
using SnapBerry.Core;
namespace SnapBerry.App.Library;
// 편집 가능한 캡처를 ZIP 기반 .qcapture로 관리한다. gate로 저장·조회·삭제의 파일 접근을 직렬화한다.
public sealed class CaptureLibraryStore(string directory)
{
    internal const int MaxOriginalImageBytes=128*1024*1024;
    internal const int MaxImageAssetBytes=16*1024*1024;
    internal const long MaxTotalImageBytes=256L*1024*1024;
    internal static void ValidateImageAssetSizes(long originalSize,IEnumerable<long> assetSizes)
    {
        if(originalSize>MaxOriginalImageBytes)throw new InvalidDataException("원본 이미지의 용량은 128MB 이하여야 합니다.");
        var total=originalSize;
        foreach(var size in assetSizes)
        {
            if(size>MaxImageAssetBytes)throw new InvalidDataException("추가 이미지의 용량은 16MB 이하여야 합니다. 크기를 줄여 다시 시도해 주세요.");
            total+=size;if(total>MaxTotalImageBytes)throw new InvalidDataException("이력에 포함된 이미지의 총용량은 256MB 이하여야 합니다.");
        }
    }
    private readonly object gate=new();
    private sealed record CachedMetadata(long Length,DateTime LastWriteUtc,CaptureListItem Item);
    private readonly Dictionary<Guid,CachedMetadata> metadata=[];
    private long metadataRevision;
    private static CaptureListItem ListItem(CaptureRecord record)=>new(record.Id,record.CapturedAtUtc,record.ResizeSize?.Width??record.CropBounds?.Width??record.Width,record.ResizeSize?.Height??record.CropBounds?.Height??record.Height){Name=record.Name};
    private void CacheMetadata(string path,CaptureListItem item)
    {var file=new FileInfo(path);metadata[item.Id]=new(file.Length,file.LastWriteTimeUtc,item with{Revision=++metadataRevision});}
    private string PathFor(Guid id)=>Path.Combine(directory,id.ToString("N")+".qcapture");
    public Task SaveAsync(CaptureRecord record)=>Task.Run(()=>
    {
        // 입력 검증을 먼저 끝내고 임시 ZIP을 완성한 뒤 교체한다. 실패 시 기존 이력은 유지된다.
        lock(gate)
        {
            Validate(record);
            var hashes=record.Annotations.Where(x=>x.Kind==AnnotationKind.Sticker).Select(x=>x.AssetHash).Distinct().ToArray();
            ValidateImageAssetSizes(record.OriginalPng.LongLength,hashes.Select(hash=>record.StickerAssets.TryGetValue(hash,out var data)?data.LongLength:throw new InvalidDataException("Sticker image is missing or damaged.")));
            ValidatePng(record.OriginalPng,record.Width,record.Height);
            foreach(var hash in hashes)
            {if(!record.StickerAssets.TryGetValue(hash,out var bytes)||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=hash)throw new InvalidDataException("Sticker image is missing or damaged.");ValidatePng(bytes);}
            Directory.CreateDirectory(directory);var target=PathFor(record.Id);
            if(File.Exists(target)){using var existing=ZipFile.OpenRead(target);ReadManifest(existing,record.Id);}
            var temp=Path.Combine(directory,record.Id.ToString("N")+"."+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create))
                {
                    Write(zip,"document.json",JsonSerializer.SerializeToUtf8Bytes(record));Write(zip,"original.png",record.OriginalPng);
                    foreach(var hash in hashes)Write(zip,$"assets/{hash}.png",record.StickerAssets[hash]);
                }
                File.Move(temp,target,true);
                CacheMetadata(target,ListItem(record));
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    });
    public Task<CaptureRecord> LoadAsync(Guid id)=>Task.Run(()=>
    {
        // 메타데이터·원본 크기·스티커 해시를 함께 검사하여 손상되거나 불완전한 이력을 차단한다.
        lock(gate)
        {
            using var zip=ZipFile.OpenRead(PathFor(id));var record=ReadManifest(zip,id);
            var png=Read(zip,"original.png",MaxOriginalImageBytes);ValidatePng(png,record.Width,record.Height);
            var assets=new Dictionary<string,byte[]>();long total=png.Length;
            foreach(var hash in record.Annotations.Where(x=>x.Kind==AnnotationKind.Sticker).Select(x=>x.AssetHash).Distinct())
            {var data=Read(zip,$"assets/{hash}.png",MaxImageAssetBytes);total+=data.Length;if(total>MaxTotalImageBytes)throw new InvalidDataException("Capture assets are too large.");if(Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()!=hash)throw new InvalidDataException("Sticker image is damaged.");ValidatePng(data);assets[hash]=data;}
            return record with {OriginalPng=png,StickerAssets=assets};
        }
    });
    public Task<IReadOnlyList<CaptureListItem>> ListAsync(int offset,int limit)=>ListAsync(offset,limit,"");
    public Task<IReadOnlyList<CaptureListItem>> ListAsync(int offset,int limit,string query)=>Task.Run<IReadOnlyList<CaptureListItem>>(()=>
    {
        // 최신 캡처부터 최대 50개씩 반환한다. 개별 파일 오류로 전체 목록 조회가 중단되지 않게 한다.
        lock(gate)
        {
            if(!Directory.Exists(directory)){metadata.Clear();return Array.Empty<CaptureListItem>();}var items=new List<CaptureListItem>();var found=new HashSet<Guid>();
            foreach(var path in Directory.EnumerateFiles(directory,"*.qcapture"))
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var id))continue;found.Add(id);
                try
                {
                    // 파일 목록·수정 시각만 확인하고, 변경된 ZIP의 메타데이터만 다시 읽는다.
                    var file=new FileInfo(path);
                    if(metadata.TryGetValue(id,out var cached)&&cached.Length==file.Length&&cached.LastWriteUtc==file.LastWriteTimeUtc)
                    {items.Add(cached.Item);continue;}
                    using var zip=ZipFile.OpenRead(path);var item=ListItem(ReadManifest(zip,id)) with{Revision=++metadataRevision};
                    metadata[id]=new(file.Length,file.LastWriteTimeUtc,item);items.Add(item);
                }
                catch(Exception e)when(e is IOException or InvalidDataException or JsonException or ArgumentException)
                {metadata.Remove(id);items.Add(new(id,File.GetCreationTimeUtc(path),0,0,true));}
            }
            foreach(var id in metadata.Keys.Where(id=>!found.Contains(id)).ToArray())metadata.Remove(id);
            var search=query.Trim();
            return items.Where(x=>search.Length==0||x.Caption.Contains(search,StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.CapturedAtUtc).ThenBy(x=>x.Id).Skip(Math.Max(0,offset)).Take(Math.Clamp(limit,1,50)).ToArray();
        }
    });
    public Task DeleteAsync(Guid id)=>Task.Run(()=>{lock(gate){File.Delete(PathFor(id));metadata.Remove(id);}});
    private static void Write(ZipArchive zip,string name,byte[] bytes){using var stream=zip.CreateEntry(name,CompressionLevel.Fastest).Open();stream.Write(bytes);}
    // 압축 해제 전후 크기를 모두 제한하여 과도한 메모리 할당을 방지한다.
    private static byte[] Read(ZipArchive zip,string name,int limit)
    {var entry=zip.GetEntry(name)??throw new InvalidDataException("Capture data is missing.");if(entry.Length<=0||entry.Length>limit)throw new InvalidDataException("Capture data exceeds limits.");using var stream=entry.Open();using var output=new MemoryStream();var buffer=new byte[8192];int read;while((read=stream.Read(buffer))>0){if(output.Length+read>limit)throw new InvalidDataException();output.Write(buffer,0,read);}return output.ToArray();}
    private static CaptureRecord ReadManifest(ZipArchive zip,Guid id)
    {var r=JsonSerializer.Deserialize<CaptureRecord>(Read(zip,"document.json",8*1024*1024))??throw new InvalidDataException();if(r.Id!=id)throw new InvalidDataException("Capture ID does not match.");Validate(r);return r;}
    internal static void Validate(CaptureRecord r)
    {
        // 디스크에서 읽은 데이터도 신뢰하지 않는다. 좌표·크기·열거형과 자원 참조를 렌더링 전에 검사한다.
        if(r.SchemaVersion!=1)throw new InvalidDataException("Unsupported capture format.");ImageDimensions.GuardSize(r.Width,r.Height);
        if(!CaptureName.IsValid(r.Name))throw new InvalidDataException("Invalid capture name.");
        if(r.CropBounds is PixelRect crop&&!CropPlacement.IsWithin(crop,r.Width,r.Height))throw new InvalidDataException("Invalid crop bounds.");
        if(r.ResizeSize is PixelSize size)ImageResize.Validate(size);
        if(r.Sequence is null||r.Sequence.NextNumeric<1||r.Sequence.NextAlphabetic<1||r.Sequence.NextUppercaseAlphabetic<1||!Enum.IsDefined(r.Sequence.Mode)||!Enum.IsDefined(r.Mode)||r.Annotations is null||r.Annotations.Length>10000)throw new InvalidDataException("Invalid capture metadata.");
        foreach(var a in r.Annotations)
        {
            if(a is null||a.Style is null||a.Text is null||a.Text.Length>AnnotationLimits.MaxTextLength||!Enum.IsDefined(a.Kind)||!Enum.IsDefined(a.Style.LineStyle)||!double.IsFinite(a.X)||!double.IsFinite(a.Y)||!double.IsFinite(a.Width)||!double.IsFinite(a.Height)||Math.Abs(a.X)>1000000||Math.Abs(a.Y)>1000000||Math.Abs(a.Width)>1000000||Math.Abs(a.Height)>1000000||!double.IsFinite(a.Style.FontSize)||a.Style.FontSize<1||a.Style.FontSize>4096||!double.IsFinite(a.Style.Thickness)||a.Style.Thickness<1||a.Style.Thickness>1024||a.MosaicBlockSize<1)throw new InvalidDataException("Invalid annotation.");
            if(a.Kind==AnnotationKind.Sticker&&(a.AssetHash is null||a.AssetHash.Length!=64||a.AssetHash.Any(c=>!"0123456789abcdef".Contains(c))))throw new InvalidDataException("Invalid sticker reference.");
        }
    }
    private static void ValidatePng(byte[] bytes,int? width=null,int? height=null)
    {
        if(bytes.Length<24||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("Invalid PNG.");
        var w=BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4));var h=BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4));ImageDimensions.GuardSize(w,h);
        if(width is not null&&(w!=width||h!=height))throw new InvalidDataException("Image dimensions do not match.");
    }
}
