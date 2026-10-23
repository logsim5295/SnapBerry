using SnapBerry.Core;
using System.Text.Json.Serialization;
namespace SnapBerry.App.Library;
// .qcapture 파일의 메타데이터다. 원본 PNG와 스티커 바이트는 JSON이 아니라 ZIP의 별도 항목에 저장한다.
public sealed record CaptureRecord
{
    public int SchemaVersion {get;init;}=1;public Guid Id {get;init;}=Guid.NewGuid();
    public string Name {get;init;}="";
    public DateTimeOffset CapturedAtUtc {get;init;}=DateTimeOffset.UtcNow;public DateTimeOffset UpdatedAtUtc {get;init;}=DateTimeOffset.UtcNow;
    public int Width {get;init;}public int Height {get;init;}public CaptureMode Mode {get;init;}
    public PixelRect? CropBounds {get;init;}
    public PixelSize? ResizeSize {get;init;}
    public Annotation[] Annotations {get;init;}=[];public StepSequenceState Sequence {get;init;}=new();
    [JsonIgnore] public byte[] OriginalPng {get;init;}=[];
    [JsonIgnore] public Dictionary<string,byte[]> StickerAssets {get;init;}=[];
}
// 목록 조회용 경량 정보다. 손상 항목도 목록에 남겨 사용자가 삭제할 수 있게 한다.
public sealed record CaptureListItem(Guid Id,DateTimeOffset CapturedAtUtc,int Width,int Height,bool Damaged=false)
{
    // 같은 저장 경로의 내용 변경도 목록 썸네일 캐시에 전달한다. 저장소 안에서만 유효한 값이다.
    public long Revision {get;init;}
    public string Name {get;init;}="";
    public string CapturedTime=>CapturedAtUtc.ToLocalTime().ToString("MM.dd HH:mm:ss");
    public string Caption=>string.IsNullOrWhiteSpace(Name)?CapturedTime:Name;
    public string Details=>string.IsNullOrWhiteSpace(Name)?Dimensions:$"{CapturedTime}\n{Dimensions}";
    public string Dimensions=>Damaged?"Cannot open":$"{Width} × {Height}";
}
