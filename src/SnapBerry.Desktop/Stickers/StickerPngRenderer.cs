using System.Text;
using SkiaSharp;
using SnapBerry.App.Stickers;
using SnapBerry.Desktop.Rendering;

namespace SnapBerry.Desktop.Stickers;

public static class StickerPngRenderer
{
    public static byte[] CreatePng(StickerDefinition definition,string? label=null)
    {
        label??=definition.Label;if(definition.IsKeycap&&(string.IsNullOrWhiteSpace(label)||label.Length>64||label.Any(char.IsControl)))throw new ArgumentException("키보드 라벨은 줄바꿈 없이 1~64자로 입력해 주세요.");
        var width=definition.IsKeycap?Math.Clamp(80+label.Length*48,256,1536):256;
        using var bitmap=new SKBitmap(width,256);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.Transparent);
        var teal=new SKColor(14,152,133);var red=new SKColor(225,64,86);var ink=new SKColor(35,43,50);
        using var fill=new SKPaint{IsAntialias=true};using var line=new SKPaint{IsAntialias=true,Style=SKPaintStyle.Stroke,StrokeCap=SKStrokeCap.Round,StrokeJoin=SKStrokeJoin.Round};
        void Round(float left,float top,float right,float bottom,float radius,SKColor color){fill.Color=color;canvas.DrawRoundRect(new SKRect(left,top,right,bottom),radius,radius,fill);}
        void Stroke(float x,float y,float x2,float y2,SKColor color,float thickness){line.Color=color;line.StrokeWidth=thickness;canvas.DrawLine(x,y,x2,y2,line);}
        if(definition.IsKeycap)
        {
            Round(8,12,width-8,244,18,new(126,141,151));Round(12,16,width-12,240,16,new(211,220,226));Round(15,16,width-15,226,14,SKColors.White);
            Text(canvas,label,ink,78,new(25,20,width-25,210));
        }
        else if(definition.Category=="Work")
        {
            var color=definition.Id switch{"work-done" or "work-approved"=>teal,"work-fix" or "work-important"=>red,"work-new" or "work-pending"=>new SKColor(243,200,61),"work-review"=>new SKColor(55,127,219),_=>new SKColor(146,60,131)};
            Round(8,54,248,202,22,ink);Round(12,58,244,198,18,color);Text(canvas,label,definition.Id is "work-new" or "work-pending"?ink:SKColors.White,66,new(24,68,232,188));
        }
        else if(definition.Category=="Emoji"&&int.TryParse(definition.Id.Split('-')[1],out var index)&&index<5)
        {
            fill.Color=new(231,162,18);canvas.DrawCircle(128,128,117,fill);fill.Color=new(255,207,51);canvas.DrawCircle(128,128,113,fill);
            if(label=="laugh"){Stroke(78,95,95,82,ink,8);Stroke(95,82,112,95,ink,8);Stroke(148,95,165,82,ink,8);Stroke(165,82,182,95,ink,8);}
            else{fill.Color=ink;canvas.DrawOval(new SKRect(78,84,96,112),fill);canvas.DrawOval(new SKRect(160,84,178,112),fill);}
            if(label=="surprise"){fill.Color=ink;canvas.DrawOval(new SKRect(108,136,148,192),fill);}
            else{using var mouth=new SKPath();mouth.MoveTo(72,label=="sad"?181:151);mouth.QuadTo(128,label=="sad"?125:220,184,label=="sad"?181:151);line.Color=ink;line.StrokeWidth=9;canvas.DrawPath(mouth,line);}
            if(label=="angry"){Stroke(69,65,111,81,ink,8);Stroke(147,81,189,65,ink,8);}
        }
        else
        {
            var color=definition.Id.StartsWith("cross")||definition.Id=="symbol-stop"?red:teal;var filled=definition.Id.EndsWith("circle")||definition.Id.EndsWith("square");
            fill.Color=color;if(definition.Id.EndsWith("circle"))canvas.DrawCircle(128,128,114,fill);if(definition.Id.EndsWith("square"))Round(14,14,242,242,20,color);
            var foreground=filled?SKColors.White:color;
            if(definition.Id.StartsWith("check")){Stroke(48,133,105,188,foreground,23);Stroke(105,188,207,68,foreground,23);}
            else if(definition.Id.StartsWith("cross")){Stroke(66,66,190,190,foreground,24);Stroke(190,66,66,190,foreground,24);}
            else{if(definition.Category=="Emoji"){fill.Color=new(255,239,166);canvas.DrawCircle(128,128,112,fill);}Text(canvas,label,definition.Category=="Emoji"?red:foreground,160,new(18,8,238,248));}
        }
        return SkiaDocumentRenderer.Encode(bitmap);
    }
    private static void Text(SKCanvas canvas,string text,SKColor color,float size,SKRect bounds)
    {
        var nonAscii=text.EnumerateRunes().FirstOrDefault(r=>r.Value>127);
        using var typeface=nonAscii.Value>0?SKFontManager.Default.MatchCharacter(nonAscii.Value):SKFontManager.Default.MatchFamily("Arial");
        using var font=new SKFont(typeface??SKTypeface.Default,size);using var paint=new SKPaint{Color=color,IsAntialias=true};
        font.MeasureText(text,out var measured);if(measured.Width>bounds.Width)font.Size*=bounds.Width/measured.Width;
        font.MeasureText(text,out measured);canvas.DrawText(text,bounds.Left+(bounds.Width-measured.Width)/2-measured.Left,bounds.Top+(bounds.Height-measured.Height)/2-measured.Top,SKTextAlign.Left,font,paint);
    }
}
