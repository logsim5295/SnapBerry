namespace SnapBerry.Core;
// 저장 형식에도 사용되는 열거형이므로 기존 값의 순서를 바꾸지 않는다.
public enum AnnotationKind { Arrow, Text, Rectangle, Mosaic, StepMarker, Sticker }
public enum StepSequenceMode { Numeric, Alphabetic, UppercaseAlphabetic }
public enum StrokePattern { Solid, Dash, Dot, DashDot, DashDotDot }
public enum CaptureMode { Region, Window, Desktop, ImageFile, Clipboard }
// 주석 위치는 확대율이나 모니터 배율과 무관한 원본 이미지 좌표로 관리한다.
public readonly record struct PixelPoint(double X,double Y);
public readonly record struct PixelRect(int X,int Y,int Width,int Height)
{
    public static PixelRect FromPoints(PixelPoint a,PixelPoint b) => new((int)Math.Floor(Math.Min(a.X,b.X)),(int)Math.Floor(Math.Min(a.Y,b.Y)),(int)Math.Ceiling(Math.Abs(a.X-b.X)),(int)Math.Ceiling(Math.Abs(a.Y-b.Y)));
    public PixelRect Translate(int x,int y) => this with { X=X+x,Y=Y+y };
}
// 원본 좌표와 화면 좌표 사이의 확대 및 평행 이동을 표현한다.
public readonly record struct ViewTransform(double Zoom,double X,double Y)
{
    public PixelPoint ToView(PixelPoint p)=>new(p.X*Zoom+X,p.Y*Zoom+Y);
    public PixelPoint ToImage(PixelPoint p)=>new((p.X-X)/Zoom,(p.Y-Y)/Zoom);
}
public sealed record AnnotationStyle { public string Color {get;init;}="#E83F56";public string Fill {get;init;}="#E83F56";public string TextColor {get;init;}="#FFFFFF";public double Thickness {get;init;}=3;public double FontSize {get;init;}=24;public StrokePattern LineStyle {get;init;}=StrokePattern.Solid; }
// 불변 레코드를 교체하는 방식으로 편집하여 이전 실행 취소 상태를 보존한다.
// 화살표의 Width/Height는 끝점까지의 방향 벡터이므로 음수도 허용한다.
public sealed record Annotation
{
    public Guid Id {get;init;}=Guid.NewGuid();public AnnotationKind Kind {get;init;}
    public double X {get;init;}public double Y {get;init;}public double Width {get;init;}=60;public double Height {get;init;}=40;
    public string Text {get;init;}="";public int MosaicBlockSize {get;init;}=12;
    public string StickerId {get;init;}="";public string AssetHash {get;init;}="";
    public AnnotationStyle Style {get;init;}=new();
}
// 숫자·소문자·대문자는 각각 독립된 다음 순번을 유지한다.
public sealed record StepSequenceState
{
    public StepSequenceMode Mode {get;init;}public long NextNumeric {get;init;}=1;public long NextAlphabetic {get;init;}=1;public long NextUppercaseAlphabetic {get;init;}=1;
    public long GetNextValue()=>Mode switch{StepSequenceMode.Numeric=>NextNumeric,StepSequenceMode.Alphabetic=>NextAlphabetic,StepSequenceMode.UppercaseAlphabetic=>NextUppercaseAlphabetic,_=>throw new ArgumentOutOfRangeException(nameof(Mode))};
    public StepSequenceState WithNextValue(long value)
    {
        if(value<1)throw new ArgumentOutOfRangeException(nameof(value));
        return Mode switch{StepSequenceMode.Numeric=>this with{NextNumeric=value},StepSequenceMode.Alphabetic=>this with{NextAlphabetic=value},StepSequenceMode.UppercaseAlphabetic=>this with{NextUppercaseAlphabetic=value},_=>throw new ArgumentOutOfRangeException(nameof(Mode))};
    }
}
// 알파벳 순번은 0이 없는 26진 표기이며 z 다음은 aa, Z 다음은 AA다.
public static class StepLabelFormatter
{
    public static string Format(long value,StepSequenceMode mode)
    {
        if(value<1)throw new ArgumentOutOfRangeException(nameof(value));
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        if(mode==StepSequenceMode.Numeric)return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var first=mode==StepSequenceMode.UppercaseAlphabetic?'A':'a';
        var result="";while(value>0){value--;result=(char)(first+value%26)+result;value/=26;}return result;
    }
    public static long Parse(string text,StepSequenceMode mode)
    {
        if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));
        if(mode==StepSequenceMode.Numeric && long.TryParse(text,out var n) && n>0)return n;
        if(mode==StepSequenceMode.Numeric)throw new FormatException("Enter a positive number.");
        long value=0;if(string.IsNullOrWhiteSpace(text))throw new FormatException("Enter a letter.");
        var first=mode==StepSequenceMode.UppercaseAlphabetic?'A':'a';var last=mode==StepSequenceMode.UppercaseAlphabetic?'Z':'z';
        foreach(var c in text){if(c<first||c>last)throw new FormatException(mode==StepSequenceMode.UppercaseAlphabetic?"Use uppercase letters.":"Use lowercase letters.");value=checked(value*26+c-first+1);}return value;
    }
}
// 원본 크기와 주석은 유지하고 잘라낼 영역만 변경하는 비파괴 편집 문서다.
public sealed class AnnotationDocument(int width,int height)
{
    public int SourceWidth {get;}=width;public int SourceHeight {get;}=height;
    private PixelRect cropBounds=new(0,0,width,height);
    public PixelRect CropBounds
    {
        get=>cropBounds;
        set{if(!CropPlacement.IsWithin(value,SourceWidth,SourceHeight))throw new ArgumentOutOfRangeException(nameof(value));cropBounds=value;}
    }
    public int Width=>CropBounds.Width;public int Height=>CropBounds.Height;
    private PixelSize? resizeSize;
    public PixelSize? ResizeSize {get=>resizeSize;set{if(value is PixelSize size)ImageResize.Validate(size);resizeSize=value;}}
    public PixelSize OutputSize=>ResizeSize??new(Width,Height);
    private string? backgroundAssetHash;
    public string? BackgroundAssetHash
    {
        get=>backgroundAssetHash;
        set{if(value is not null&&!IsAssetHash(value))throw new ArgumentException("Invalid background image reference.",nameof(value));backgroundAssetHash=value;}
    }
    public IEnumerable<string> ReferencedAssetHashes=>Annotations.Where(a=>a.Kind==AnnotationKind.Sticker).Select(a=>a.AssetHash).Concat(BackgroundAssetHash is {} hash?[hash]:[]).Distinct();
    public IEnumerable<string> AssetHashesWithBackground(string hash)=>Annotations.Where(a=>a.Kind==AnnotationKind.Sticker).Select(a=>a.AssetHash).Append(hash).Distinct();
    public static bool IsAssetHash(string? hash)=>hash is {Length:64}&&hash.All(c=>"0123456789abcdef".Contains(c));
    public List<Annotation> Annotations {get;}=[];public StepSequenceState Sequence {get;set;}=new();
}
