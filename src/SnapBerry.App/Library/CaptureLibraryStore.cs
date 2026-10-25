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
            var hashes=record.ReferencedAssetHashes.ToArray();
            ValidateImageAssetSizes(record.OriginalPng.LongLength,hashes.Select(hash=>record.StickerAssets.TryGetValue(hash,out var data)?data.LongLength:throw new InvalidDataException("Sticker image is missing or damaged.")));
            ValidatePng(record.OriginalPng,record.Width,record.Height);
            foreach(var hash in hashes)
            {if(!record.StickerAssets.TryGetValue(hash,out var bytes)||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=hash)throw new InvalidDataException("Sticker image is missing or damaged.");ValidatePng(bytes,hash==record.BackgroundAssetHash?record.Width:null,hash==record.BackgroundAssetHash?record.Height:null);}
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
                DeleteThumbnail(record.Id);
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
            foreach(var hash in record.ReferencedAssetHashes)
            {var data=Read(zip,$"assets/{hash}.png",MaxImageAssetBytes);total+=data.Length;if(total>MaxTotalImageBytes)throw new InvalidDataException("Capture assets are too large.");if(Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()!=hash)throw new InvalidDataException("Sticker image is damaged.");ValidatePng(data,hash==record.BackgroundAssetHash?record.Width:null,hash==record.BackgroundAssetHash?record.Height:null);assets[hash]=data;}
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
    public Task DeleteAsync(Guid id)=>Task.Run(()=>{lock(gate){File.Delete(PathFor(id));metadata.Remove(id);DeleteThumbnail(id);}});
    // 원본과 별도로 보관하는 재생성 가능한 WPF 썸네일이다. 파일 크기·수정 시각과
    // 조회 revision을 함께 검사하여 재시작 후 재사용과 저장 중 오래된 결과 차단을 모두 보장한다.
    private string ThumbnailPath(Guid id)=>Path.Combine(directory,"thumbnails-wpf-v1",id.ToString("N")+".thumb");
    private CachedMetadata? CurrentMetadata(CaptureListItem item)
    {
        if(item.Damaged||!metadata.TryGetValue(item.Id,out var cached)||cached.Item.Revision!=item.Revision)return null;
        var file=new FileInfo(PathFor(item.Id));
        return file.Exists&&file.Length==cached.Length&&file.LastWriteTimeUtc==cached.LastWriteUtc?cached:null;
    }
    private void DeleteThumbnail(Guid id)
    {try{File.Delete(ThumbnailPath(id));}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}}
    public Task<byte[]?> LoadThumbnailAsync(CaptureListItem item)=>Task.Run<byte[]?>(()=>
    {
        lock(gate)
        {
            try
            {
                var current=CurrentMetadata(item);if(current is null)return null;
                var path=ThumbnailPath(item.Id);var file=new FileInfo(path);
                if(!file.Exists||file.Length<=16||file.Length>256*1024)return null;
                var bytes=File.ReadAllBytes(path);
                if(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(0,8))!=current.Length||BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8,8))!=current.LastWriteUtc.Ticks)return null;
                var png=bytes[16..];ValidateThumbnail(png);return png;
            }
            catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException){return null;}
        }
    });
    public Task SaveThumbnailAsync(CaptureListItem item,byte[] png)=>Task.Run(()=>
    {
        lock(gate)
        {
            string? temp=null;
            try
            {
                var current=CurrentMetadata(item);if(current is null)return;ValidateThumbnail(png);
                var path=ThumbnailPath(item.Id);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var bytes=new byte[16+png.Length];BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(0,8),current.Length);
                BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8,8),current.LastWriteUtc.Ticks);png.CopyTo(bytes,16);
                temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllBytes(temp,bytes);File.Move(temp,path,true);
            }
            catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException){}
            finally{if(temp is not null)try{File.Delete(temp);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}}
        }
    });
    private static void ValidateThumbnail(byte[] png)
    {
        if(png.Length>256*1024-16)throw new InvalidDataException("Thumbnail exceeds limits.");
        ValidatePng(png);
        if(BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4))>120||BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4))>120)throw new InvalidDataException("Thumbnail dimensions exceed limits.");
    }
    private static void Write(ZipArchive zip,string name,byte[] bytes){using var stream=zip.CreateEntry(name,CompressionLevel.Fastest).Open();stream.Write(bytes);}
    // 압축 해제 전후 크기를 모두 제한하여 과도한 메모리 할당을 방지한다.
    private static byte[] Read(ZipArchive zip,string name,int limit)
    {var entry=zip.GetEntry(name)??throw new InvalidDataException("Capture data is missing.");if(entry.Length<=0||entry.Length>limit)throw new InvalidDataException("Capture data exceeds limits.");using var stream=entry.Open();using var output=new MemoryStream();var buffer=new byte[8192];int read;while((read=stream.Read(buffer))>0){if(output.Length+read>limit)throw new InvalidDataException();output.Write(buffer,0,read);}return output.ToArray();}
    private static CaptureRecord ReadManifest(ZipArchive zip,Guid id)
    {var r=JsonSerializer.Deserialize<CaptureRecord>(Read(zip,"document.json",8*1024*1024))??throw new InvalidDataException();if(r.Id!=id)throw new InvalidDataException("Capture ID does not match.");Validate(r);return r;}
    internal static void Validate(CaptureRecord r)
    {
        // 디스크에서 읽은 데이터도 신뢰하지 않는다. 좌표·크기·열거형과 자원 참조를 렌더링 전에 검사한다.
        if(r.SchemaVersion is not (1 or 2)||r.BackgroundAssetHash is not null&&(r.SchemaVersion!=2||!AnnotationDocument.IsAssetHash(r.BackgroundAssetHash)))throw new InvalidDataException("Unsupported capture format.");ImageDimensions.GuardSize(r.Width,r.Height);
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
