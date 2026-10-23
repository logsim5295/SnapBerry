using System.Globalization;
using System.Windows.Media;

namespace SnapBerry.App.Editor;

// 색상 선택 UI와 독립적인 sRGB/HSV 변환이다. 이 앱의 주석 색상은 기존과 같이 불투명 RGB만 사용한다.
internal static class ColorSelection
{
    internal static (double Hue,double Saturation,double Value) ToHsv(Color color)
    {
        var r=color.R/255d;var g=color.G/255d;var b=color.B/255d;
        var max=Math.Max(r,Math.Max(g,b));var min=Math.Min(r,Math.Min(g,b));var delta=max-min;
        var hue=delta==0?0:max==r?60*((g-b)/delta%6):max==g?60*((b-r)/delta+2):60*((r-g)/delta+4);
        return (hue<0?hue+360:hue,max==0?0:delta/max,max);
    }

    internal static Color FromHsv(double hue,double saturation,double value)
    {
        if(!double.IsFinite(hue)||!double.IsFinite(saturation)||!double.IsFinite(value))throw new ArgumentOutOfRangeException(nameof(hue));
        hue=(hue%360+360)%360;saturation=Math.Clamp(saturation,0,1);value=Math.Clamp(value,0,1);
        var c=value*saturation;var x=c*(1-Math.Abs(hue/60%2-1));var m=value-c;
        var (r,g,b)=hue switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};
        byte Channel(double channel)=>(byte)Math.Clamp(Math.Round((channel+m)*255,MidpointRounding.AwayFromZero),0,255);
        return Color.FromRgb(Channel(r),Channel(g),Channel(b));
    }

    internal static bool TryParseHex(string text,out Color color)
    {
        color=default;text=text.Trim();if(text.StartsWith('#'))text=text[1..];
        if(text.Length!=6||!uint.TryParse(text,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out var rgb))return false;
        color=Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);return true;
    }
}
