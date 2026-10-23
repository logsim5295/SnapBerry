using System.Windows;
using System.Windows.Media.Imaging;
using System.Globalization;
using System.Text.Json;
using SnapBerry.App.Library;
using SnapBerry.App.Rendering;
using SnapBerry.Core;
using SnapBerry.App.Export;

namespace SnapBerry.App.Import;

public readonly record struct ClipboardImageSize(double Width,double Height)
{public bool IsValid=>double.IsFinite(Width)&&double.IsFinite(Height)&&Width>0&&Height>0;}
public sealed record ClipboardImageContent(BitmapSource Image,ClipboardImageSize? ObjectSize=null,Annotation? Annotation=null);
public interface IClipboardImageSource
{
    BitmapSource? Read();
    ClipboardImageContent? ReadContent()=>Read() is {} image?new(image):null;
}
public sealed class ClipboardImageSource : IClipboardImageSource
{
    private const string ObjectFormat="SnapBerry.ImageObject.v1";
    private const string AnnotationFormat="SnapBerry.AnnotationObject.v1";
    public BitmapSource? Read()=>ReadContent()?.Image;
    public ClipboardImageContent? ReadContent()=>ReadContent(Clipboard.GetDataObject());
    public static DataObject CreateDataObject(ClipboardImageContent content)
    {
        var data=new DataObject();data.SetData(DataFormats.Bitmap,content.Image);
        data.SetData("PNG",new MemoryStream(PngFileWriter.Encode(content.Image),false));
        if(content.ObjectSize is {IsValid:true} size)
            data.SetData(ObjectFormat,FormattableString.Invariant($"{size.Width:R}|{size.Height:R}"),false);
        if(content.Annotation is {} annotation)data.SetData(AnnotationFormat,JsonSerializer.Serialize(annotation),false);
        return data;
    }
    public static ClipboardImageContent? ReadContent(IDataObject? data)
    {
        var image=ReadImage(data);if(image is null)return null;
        ClipboardImageSize? size=null;
        if(data!.GetDataPresent(ObjectFormat,false)&&data.GetData(ObjectFormat,false) is string {Length:<=100} text)
        {
            var parts=text.Split('|');
            if(parts.Length==2&&double.TryParse(parts[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var width)
                &&double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var height)
                &&new ClipboardImageSize(width,height) is {IsValid:true} valid)size=valid;
        }
        Annotation? annotation=null;
        if(data.GetDataPresent(AnnotationFormat,false)&&data.GetData(AnnotationFormat,false) is string {Length:<=16384} json)
        {
            try
            {
                var value=JsonSerializer.Deserialize<Annotation>(json);
                if(value is {Kind:AnnotationKind.Rectangle or AnnotationKind.Arrow or AnnotationKind.Sticker})
                {
                    CaptureLibraryStore.Validate(new(){Width=1,Height=1,Annotations=[value]});
                    if(value.StickerId is null||value.Style.Color is null||value.Style.Fill is null||value.Style.TextColor is null)
                        throw new InvalidDataException("Invalid annotation strings.");
                    CompositeRenderer.Brush(value.Style.Color);CompositeRenderer.Brush(value.Style.Fill);CompositeRenderer.Brush(value.Style.TextColor);
                    if(value.Kind==AnnotationKind.Arrow||value.Width>0&&value.Height>0)annotation=value;
                }
            }
            catch(Exception e)when(e is JsonException or InvalidDataException or FormatException or ArgumentException or NotSupportedException){}
        }
        return new(image,size,annotation);
    }
    // 메모리 DataObject로도 같은 변환 경로를 검사한다. 시스템 클립보드는 인스턴스 메서드에서만 읽는다.
    public static BitmapSource? ReadImage(IDataObject? data)
    {
        if(data is null)return null;
        // DIB의 알파 정보가 손실되는 경우를 피하도록 이미지 앱들이 제공하는 PNG 형식을 우선한다.
        var png=data.GetDataPresent("PNG",false)?data.GetData("PNG",false):null;
        if(png is byte[] bytes)
        {
            if(bytes.LongLength>ImageImportService.MaxFileBytes)throw new InvalidDataException("클립보드 이미지가 너무 큽니다.");
            using var stream=new MemoryStream(bytes,false);return ImageImportService.Decode(stream);
        }
        if(png is Stream input)
        {
            var position=input.CanSeek?input.Position:(long?)null;
            try
            {
                if(input.CanSeek)input.Position=0;
                using var copy=new MemoryStream();var buffer=new byte[81920];int read;
                while((read=input.Read(buffer,0,buffer.Length))>0)
                {if(copy.Length+read>ImageImportService.MaxFileBytes)throw new InvalidDataException("클립보드 이미지가 너무 큽니다.");copy.Write(buffer,0,read);}
                copy.Position=0;return ImageImportService.Decode(copy);
            }
            finally{if(position is long offset)input.Position=offset;}
        }
        return data.GetDataPresent(DataFormats.Bitmap)?data.GetData(DataFormats.Bitmap) as BitmapSource:null;
    }
}
