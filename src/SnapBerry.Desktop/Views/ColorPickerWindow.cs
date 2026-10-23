using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SnapBerry.Desktop.Views;

// 미리보기는 대화 상자 안에만 보관한다. 호출자가 승인된 결과를 문서에 적용한다.
public sealed class ColorPickerWindow : Window
{
    private readonly Grid area=new(){Height=160,ClipToBounds=true,Focusable=true,Cursor=new Cursor(StandardCursorType.Cross)};
    private readonly Canvas markerLayer=new(){IsHitTestVisible=false};
    private readonly Ellipse marker=new(){Width=12,Height=12,Stroke=Brushes.White,StrokeThickness=2,Fill=Brushes.Transparent};
    private readonly Slider hue=new(){Minimum=0,Maximum=360,SmallChange=1,LargeChange=30};
    private readonly TextBox hex=new();
    private readonly TextBox[] rgb=[new(),new(),new()];
    private readonly Border preview=new(){Height=38,CornerRadius=new CornerRadius(4)};
    private readonly TextBlock previewLabel=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private readonly WrapPanel palette=new();
    private readonly TextBlock error=new(){Foreground=Brushes.IndianRed,TextWrapping=TextWrapping.Wrap,MinHeight=20};
    private readonly Button apply=new(){Content="적용",IsDefault=true};
    private double h,s,v;
    private bool syncing;
    public string SelectedColor {get;private set;}="#000000";
    public string SelectedHex=>SelectedColor;

    public ColorPickerWindow(string initialColor)
    {
        if(!TryHex(initialColor,out var initial))throw new ArgumentException("색상은 #RRGGBB 형식이어야 합니다.",nameof(initialColor));
        var names=new NameScope();NameScope.SetNameScope(this,names);
        void Register(string name,Control control){control.Name=name;names.Register(name,control);}
        Title="색상 선택";Width=460;MaxHeight=700;SizeToContent=SizeToContent.Height;CanResize=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var body=new StackPanel{Margin=new Thickness(22),Spacing=10};Content=new ScrollViewer{Content=body,MaxHeight=700};
        body.Children.Add(new TextBlock{Text=Title,FontSize=22,FontWeight=FontWeight.SemiBold});
        area.Children.Add(new Border{Background=Gradient(Colors.White,Color.FromArgb(0,255,255,255),false)});
        area.Children.Add(new Border{Background=Gradient(Color.FromArgb(0,0,0,0),Colors.Black,true)});
        markerLayer.Children.Add(marker);area.Children.Add(markerLayer);Register("SaturationValueArea",area);ToolTip.SetTip(area,"채도 및 밝기 · 방향키로 조절, Shift로 크게 조절");body.Children.Add(area);
        area.SizeChanged+=(_,_)=>PositionMarker();
        void Pick(Point point){if(area.Bounds.Width>0&&area.Bounds.Height>0)SetSaturationValue(point.X/area.Bounds.Width,1-point.Y/area.Bounds.Height);}
        area.PointerPressed+=(_,e)=>{if(!e.GetCurrentPoint(area).Properties.IsLeftButtonPressed)return;area.Focus();Pick(e.GetPosition(area));e.Pointer.Capture(area);e.Handled=true;};
        area.PointerMoved+=(_,e)=>{if(e.Pointer.Captured==area&&e.GetCurrentPoint(area).Properties.IsLeftButtonPressed)Pick(e.GetPosition(area));};
        area.PointerReleased+=(_,e)=>{if(e.Pointer.Captured!=area)return;Pick(e.GetPosition(area));e.Pointer.Capture(null);e.Handled=true;};
        area.KeyDown+=(_,e)=>
        {
            var step=e.KeyModifiers.HasFlag(KeyModifiers.Shift)?.1:.01;
            switch(e.Key){case Key.Left:SetSaturationValue(s-step,v);break;case Key.Right:SetSaturationValue(s+step,v);break;case Key.Up:SetSaturationValue(s,v+step);break;case Key.Down:SetSaturationValue(s,v-step);break;default:return;}e.Handled=true;
        };
        body.Children.Add(new TextBlock{Text="색조"});Register("HueSlider",hue);ToolTip.SetTip(hue,"색조 0–360°");
        var hueStrip=new LinearGradientBrush{StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(1,0,RelativeUnit.Relative)};
        for(var i=0;i<=6;i++)hueStrip.GradientStops.Add(new GradientStop(FromHsv(i*60,1,1),i/6d));
        body.Children.Add(new Border{Height=8,CornerRadius=new CornerRadius(4),Background=hueStrip});body.Children.Add(hue);
        hue.ValueChanged+=(_,_)=>{if(syncing)return;h=hue.Value;UpdateSelection(FromHsv(h,s,v),updateHsv:false);};
        body.Children.Add(new TextBlock{Text="기본 색상"});Register("ColorPalette",palette);body.Children.Add(palette);
        foreach(var value in new[]{"#E83F56","#D55E00","#009E73","#0072B2","#CC79A7","#000000","#F28C28","#F3BD35","#00B5D8","#923C83","#FFFFFF","#AAB4BE","#252C33"})
        {
            TryHex(value,out var color);
            var button=new Button{Tag=value,Width=30,Height=30,Margin=new Thickness(2),Padding=new Thickness(2),Content=new Border{Width=18,Height=18,Background=new SolidColorBrush(color),BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1)}};
            ToolTip.SetTip(button,value);button.Click+=(_,_)=>UpdateSelection(color);palette.Children.Add(button);
        }
        var previews=new Grid{ColumnDefinitions=new("*,*")};
        var original=new Border{Height=38,Margin=new Thickness(0,0,6,0),CornerRadius=new CornerRadius(4),Background=new SolidColorBrush(initial),Child=new TextBlock{Text="기존 "+Hex(initial),Foreground=Contrast(initial),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
        Register("OriginalColorPreview",original);previews.Children.Add(original);preview.Child=previewLabel;Grid.SetColumn(preview,1);previews.Children.Add(preview);Register("SelectedColorPreview",preview);body.Children.Add(previews);
        var fields=new Grid{ColumnDefinitions=new("2*,*,*,*")};body.Children.Add(fields);
        void Input(TextBox field,string label,string name,int column,int maxLength)
        {
            var stack=new StackPanel{Spacing=4,Margin=new Thickness(column==0?0:6,0,0,0)};stack.Children.Add(new TextBlock{Text=label});Register(name,field);field.MaxLength=maxLength;stack.Children.Add(field);Grid.SetColumn(stack,column);fields.Children.Add(stack);
        }
        Input(hex,"HEX","HexInput",0,7);Input(rgb[0],"R","RedInput",1,3);Input(rgb[1],"G","GreenInput",2,3);Input(rgb[2],"B","BlueInput",3,3);
        hex.PropertyChanged+=(_,e)=>{if(e.Property!=TextBox.TextProperty||syncing)return;if(TryHex(hex.Text,out var color))UpdateSelection(color,hex);else Invalid("HEX는 #RRGGBB 형식의 6자리 값으로 입력하세요.");};
        foreach(var field in rgb)field.PropertyChanged+=(_,e)=>
        {
            if(e.Property!=TextBox.TextProperty||syncing)return;
            if(TryRgb(out var color))UpdateSelection(color,field);else Invalid("RGB 값은 0부터 255 사이의 정수로 입력하세요.");
        };
        Register("ColorInputError",error);body.Children.Add(error);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8};
        var cancel=new Button{Content="취소",IsCancel=true};Register("CancelColorButton",cancel);Register("ApplyColorButton",apply);buttons.Children.Add(cancel);buttons.Children.Add(apply);body.Children.Add(buttons);
        cancel.Click+=(_,_)=>Close();apply.Click+=(_,_)=>{if(TryAccept())Close(SelectedColor);};UpdateSelection(initial);
    }

    public bool TryAccept()
    {
        if(!TryHex(hex.Text,out var color)){Invalid("HEX는 #RRGGBB 형식의 6자리 값으로 입력하세요.");return false;}
        if(!TryRgb(out var channels)){Invalid("RGB 값은 0부터 255 사이의 정수로 입력하세요.");return false;}
        if(color!=channels){Invalid("색상 입력을 확인하세요.");return false;}
        UpdateSelection(color);return true;
    }

    public void SetSaturationValue(double saturation,double value)
    {
        if(!double.IsFinite(saturation)||!double.IsFinite(value))throw new ArgumentOutOfRangeException(nameof(saturation));
        s=Math.Clamp(saturation,0,1);v=Math.Clamp(value,0,1);UpdateSelection(FromHsv(h,s,v),updateHsv:false);
    }

    private void UpdateSelection(Color color,TextBox? source=null,bool updateHsv=true)
    {
        SelectedColor=Hex(color);if(updateHsv)(h,s,v)=ToHsv(color);syncing=true;
        try
        {
            if(source!=hex)hex.Text=SelectedColor;byte[] values=[color.R,color.G,color.B];
            for(var i=0;i<3;i++)if(source!=rgb[i])rgb[i].Text=values[i].ToString(CultureInfo.InvariantCulture);
            hue.Value=h;area.Background=new SolidColorBrush(FromHsv(h,1,1));preview.Background=new SolidColorBrush(color);previewLabel.Text="선택 "+SelectedColor;previewLabel.Foreground=Contrast(color);
            foreach(var button in palette.Children.OfType<Button>())button.BorderThickness=new Thickness(Equals(button.Tag,SelectedColor)?3:1);
            error.Text="";apply.IsEnabled=true;PositionMarker();
        }
        finally{syncing=false;}
    }
    private void PositionMarker(){Canvas.SetLeft(marker,Math.Clamp(s*area.Bounds.Width-6,0,Math.Max(0,area.Bounds.Width-12)));Canvas.SetTop(marker,Math.Clamp((1-v)*area.Bounds.Height-6,0,Math.Max(0,area.Bounds.Height-12)));}
    private void Invalid(string message){error.Text=message;apply.IsEnabled=false;}
    private bool TryRgb(out Color color)
    {
        color=default;
        if(!byte.TryParse(rgb[0].Text,NumberStyles.None,CultureInfo.InvariantCulture,out var r)||!byte.TryParse(rgb[1].Text,NumberStyles.None,CultureInfo.InvariantCulture,out var g)||!byte.TryParse(rgb[2].Text,NumberStyles.None,CultureInfo.InvariantCulture,out var b))return false;
        color=Color.FromRgb(r,g,b);return true;
    }
    private static bool TryHex(string? value,out Color color)
    {
        color=default;var text=(value??"").Trim();if(text.StartsWith('#'))text=text[1..];
        if(text.Length!=6||!uint.TryParse(text,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out var packed))return false;
        color=Color.FromRgb((byte)(packed>>16),(byte)(packed>>8),(byte)packed);return true;
    }
    private static string Hex(Color color)=>$"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private static IBrush Contrast(Color color)=>color.R*.299+color.G*.587+color.B*.114<145?Brushes.White:Brushes.Black;
    private static LinearGradientBrush Gradient(Color start,Color end,bool vertical)=>new(){StartPoint=new RelativePoint(0,0,RelativeUnit.Relative),EndPoint=new RelativePoint(vertical?0:1,vertical?1:0,RelativeUnit.Relative),GradientStops=[new GradientStop(start,0),new GradientStop(end,1)]};
    private static (double Hue,double Saturation,double Value) ToHsv(Color color)
    {
        var r=color.R/255d;var g=color.G/255d;var b=color.B/255d;var max=Math.Max(r,Math.Max(g,b));var min=Math.Min(r,Math.Min(g,b));var delta=max-min;
        var hue=delta==0?0:max==r?60*((g-b)/delta%6):max==g?60*((b-r)/delta+2):60*((r-g)/delta+4);
        return (hue<0?hue+360:hue,max==0?0:delta/max,max);
    }
    private static Color FromHsv(double hue,double saturation,double value)
    {
        hue=(hue%360+360)%360;var c=value*saturation;var x=c*(1-Math.Abs(hue/60%2-1));var m=value-c;
        var (r,g,b)=hue switch{<60=>(c,x,0d),<120=>(x,c,0d),<180=>(0d,c,x),<240=>(0d,x,c),<300=>(x,0d,c),_=>(c,0d,x)};
        byte Channel(double channel)=>(byte)Math.Clamp(Math.Round((channel+m)*255,MidpointRounding.AwayFromZero),0,255);
        return Color.FromRgb(Channel(r),Channel(g),Channel(b));
    }
}
