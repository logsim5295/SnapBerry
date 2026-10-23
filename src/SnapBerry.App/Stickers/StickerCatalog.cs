using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SnapBerry.App.Stickers;
// PNG 생성기는 플랫폼마다 구현하고 목록과 ID는 공유한다.
public static partial class StickerCatalog
{
    public static byte[] CreatePng(StickerDefinition definition,string? label=null)
    {
        // 키보드 스티커는 라벨 길이에 맞춰 가로폭을 조절하고 나머지는 고정 크기의 투명 PNG로 생성한다.
        label??=definition.Label;
        if(definition.IsKeycap && string.IsNullOrWhiteSpace(label))throw new ArgumentException("Key label cannot be empty.");
        var width=definition.IsKeycap?Math.Clamp(80+label.Length*48,256,1536):256;
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            var teal=new SolidColorBrush(Color.FromRgb(14,152,133));var red=new SolidColorBrush(Color.FromRgb(225,64,86));var ink=new SolidColorBrush(Color.FromRgb(35,43,50));
            if(definition.IsKeycap)
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(211,220,226)),new Pen(new SolidColorBrush(Color.FromRgb(126,141,151)),4),new Rect(8,12,width-16,232),18,18);
                dc.DrawRoundedRectangle(Brushes.White,new Pen(new SolidColorBrush(Color.FromRgb(193,204,212)),2),new Rect(15,16,width-30,210),14,14);
                DrawText(dc,label,ink,78,new Rect(25,20,width-50,190));
            }
            else if(definition.Category=="Work")
            {
                var color=definition.Id switch
                {
                    "work-done" or "work-approved"=>Color.FromRgb(19,152,135),
                    "work-fix" or "work-important"=>Color.FromRgb(225,64,86),
                    "work-new" or "work-pending"=>Color.FromRgb(243,200,61),
                    "work-review"=>Color.FromRgb(55,127,219),
                    _=>Color.FromRgb(146,60,131)
                };
                dc.DrawRoundedRectangle(new SolidColorBrush(color),new Pen(ink,4),new Rect(8,54,240,148),22,22);
                DrawText(dc,label,definition.Id is "work-new" or "work-pending"?ink:Brushes.White,66,new Rect(24,68,208,120));
            }
            else if(definition.Category=="Emoji" && int.Parse(definition.Id.Split('-')[1])<5)
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255,207,51)),new Pen(new SolidColorBrush(Color.FromRgb(231,162,18)),4),new Point(128,128),115,115);
                var mood=definition.Label;
                if(mood=="laugh") {DrawLine(dc,78,95,95,82,ink,8);DrawLine(dc,95,82,112,95,ink,8);DrawLine(dc,148,95,165,82,ink,8);DrawLine(dc,165,82,182,95,ink,8);}
                else {dc.DrawEllipse(ink,null,new Point(87,98),9,14);dc.DrawEllipse(ink,null,new Point(169,98),9,14);}
                if(mood=="surprise")dc.DrawEllipse(ink,null,new Point(128,164),20,28);
                else if(mood=="sad")DrawArc(dc,new Point(77,181),new Point(179,181),new Size(65,65),SweepDirection.Clockwise,ink,9);
                else {DrawArc(dc,new Point(72,151),new Point(184,151),new Size(65,65),SweepDirection.Counterclockwise,ink,9);}
                if(mood=="angry"){DrawLine(dc,69,65,111,81,ink,8);DrawLine(dc,147,81,189,65,ink,8);}
            }
            else
            {
                var color=definition.Id.StartsWith("cross")||definition.Id=="symbol-stop"?red:teal;
                if(definition.Id.EndsWith("circle"))dc.DrawEllipse(color,null,new Point(128,128),114,114);
                if(definition.Id.EndsWith("square"))dc.DrawRoundedRectangle(color,null,new Rect(14,14,228,228),20,20);
                var filled=definition.Id.EndsWith("circle")||definition.Id.EndsWith("square");
                var foreground=filled?Brushes.White:color;
                if(definition.Id.StartsWith("check")){DrawLine(dc,48,133,105,188,foreground,23);DrawLine(dc,105,188,207,68,foreground,23);}
                else if(definition.Id.StartsWith("cross")){DrawLine(dc,66,66,190,190,foreground,24);DrawLine(dc,190,66,66,190,foreground,24);}
                else {if(definition.Category=="Emoji")dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255,239,166)),null,new Point(128,128),112,112);DrawText(dc,label,definition.Category=="Emoji"?red:foreground,160,new Rect(18,8,220,240));}
            }
        }
        var bitmap=new RenderTargetBitmap(width,256,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();
    }
    private static void DrawLine(DrawingContext dc,double x,double y,double x2,double y2,Brush color,double size)=>dc.DrawLine(new Pen(color,size){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new Point(x,y),new Point(x2,y2));
    private static void DrawArc(DrawingContext dc,Point start,Point end,Size size,SweepDirection direction,Brush color,double thickness)
    {var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(start,false,false);c.ArcTo(end,size,0,false,direction,true,false);}dc.DrawGeometry(null,new Pen(color,thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},g);}
    public static void DrawText(DrawingContext dc,string text,Brush color,double size,Rect rect)
    {var t=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI Emoji"),size,color,1);if(t.Width>rect.Width)t.SetFontSize(size*rect.Width/t.Width);dc.DrawText(t,new Point(rect.X+(rect.Width-t.Width)/2,rect.Y+(rect.Height-t.Height)/2));}
}
