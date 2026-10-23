using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SnapBerry.App.Editor;

// 색상 선택은 창 안에서만 미리보고, 호출자가 적용 결과를 확정할 때 문서 스타일에 반영한다.
public sealed class ColorPickerWindow : Window
{
    private readonly Grid area=new(){Height=150,ClipToBounds=true,Focusable=true,Cursor=Cursors.Cross};
    private readonly Canvas markerLayer=new(){IsHitTestVisible=false};
    private readonly Ellipse marker=new(){Width=12,Height=12,Stroke=Brushes.White,StrokeThickness=2,Fill=Brushes.Transparent};
    private readonly Slider hueSlider=new(){Minimum=0,Maximum=360,SmallChange=1,LargeChange=30};
    private readonly TextBox hex=new();private readonly TextBox[] rgb=[new(),new(),new()];
    private readonly Border selectedPreview=new();private readonly TextBlock selectedLabel=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
    private readonly WrapPanel palette=new();private readonly TextBlock error=new(){MinHeight=18,FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};
    private readonly Button apply=new(){Content="적용",Width=80,Height=34,IsDefault=true,Margin=new Thickness(8,0,0,0)};
    private double h,s,v;private bool syncing,ready;
    public Color SelectedColor {get;private set;}
    public string SelectedHex=>$"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";

    public ColorPickerWindow(Color initial)
    {
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        error.Foreground=ThemeResources.Get(this,"Danger");
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ScrollBarStyles.xaml",UriKind.Relative)});
        Title="색상 선택";Width=480;SizeToContent=SizeToContent.Height;MaxHeight=Math.Max(400,SystemParameters.WorkArea.Height*.9);ResizeMode=ResizeMode.NoResize;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new FontFamily("Segoe UI");FontSize=13;
        var grid=new Grid{Margin=new Thickness(24)};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel{Orientation=Orientation.Horizontal,Background=Brushes.Transparent,Margin=new Thickness(0,0,0,16)};
        header.Children.Add(new Image{Source=RibbonIcons.Create("Mosaic"),Width=28,Height=28,Margin=new Thickness(0,0,10,0)});
        header.Children.Add(new TextBlock{Text=Title,FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Text"),VerticalAlignment=VerticalAlignment.Center});
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};grid.Children.Add(header);
        var body=new StackPanel();var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);grid.Children.Add(scroll);

        // 색조 위에 흰색 가로 그라데이션과 검정 세로 그라데이션을 합성해 채도·밝기 영역을 만든다.
        area.Children.Add(new Rectangle{Fill=new LinearGradientBrush(Colors.White,Color.FromArgb(0,255,255,255),new Point(0,0),new Point(1,0))});
        area.Children.Add(new Rectangle{Fill=new LinearGradientBrush(Color.FromArgb(0,0,0,0),Colors.Black,new Point(0,0),new Point(0,1))});
        markerLayer.Children.Add(marker);area.Children.Add(markerLayer);RegisterName("SaturationValueArea",area);System.Windows.Automation.AutomationProperties.SetName(area,"채도 및 밝기");area.ToolTip="채도 및 밝기";body.Children.Add(area);
        area.SizeChanged+=(_,_)=>PositionMarker();
        void Pick(Point point){if(area.ActualWidth>0&&area.ActualHeight>0)SetSaturationValue(point.X/area.ActualWidth,1-point.Y/area.ActualHeight);}
        area.MouseLeftButtonDown+=(_,e)=>{area.Focus();Pick(e.GetPosition(area));area.CaptureMouse();e.Handled=true;};
        area.MouseMove+=(_,e)=>{if(area.IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)Pick(e.GetPosition(area));};
        area.MouseLeftButtonUp+=(_,e)=>{if(area.IsMouseCaptured){Pick(e.GetPosition(area));area.ReleaseMouseCapture();e.Handled=true;}};
        area.KeyDown+=(_,e)=>
        {
            var step=Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)?.1:.01;
            if(e.Key==Key.Left)SetSaturationValue(s-step,v);else if(e.Key==Key.Right)SetSaturationValue(s+step,v);else if(e.Key==Key.Up)SetSaturationValue(s,v+step);else if(e.Key==Key.Down)SetSaturationValue(s,v-step);else return;e.Handled=true;
        };
        hueSlider.Style=(Style)FindResource("ColorHueSliderStyle");hueSlider.Margin=new Thickness(0,10,0,10);hueSlider.ToolTip="색조";RegisterName("HueSlider",hueSlider);System.Windows.Automation.AutomationProperties.SetName(hueSlider,"색조");body.Children.Add(hueSlider);
        hueSlider.ValueChanged+=(_,e)=>{if(!ready||syncing)return;h=e.NewValue;UpdateSelection(ColorSelection.FromHsv(h,s,v),updateHsv:false);};

        body.Children.Add(Label("기본 색상"));RegisterName("ColorPalette",palette);palette.Margin=new Thickness(0,0,0,12);body.Children.Add(palette);
        foreach(var value in new[]{"#E83F56","#D55E00","#009E73","#0072B2","#CC79A7","#000000","#F28C28","#F3BD35","#00B5D8","#923C83","#FFFFFF","#AAB4BE","#252C33"})
        {
            ColorSelection.TryParseHex(value,out var color);
            var button=new Button{Width=30,Height=30,Margin=new Thickness(2),Padding=new Thickness(3),Tag=value,ToolTip=value,Content=new Border{Width=18,Height=18,Background=new SolidColorBrush(color),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3)}};
            System.Windows.Automation.AutomationProperties.SetName(button,"색상 "+value);button.Click+=(_,_)=>UpdateSelection(color);palette.Children.Add(button);
        }
        var previews=Columns(2);previews.Margin=new Thickness(0,0,0,12);body.Children.Add(previews);
        var original=Color.FromRgb(initial.R,initial.G,initial.B);
        var originalPreview=Preview(original,new TextBlock{Text=$"#{original.R:X2}{original.G:X2}{original.B:X2}",Foreground=Contrast(original),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});RegisterName("OriginalColorPreview",originalPreview);
        var originalPanel=new StackPanel{Margin=new Thickness(0,0,4,0)};originalPanel.Children.Add(Label("기존 색"));originalPanel.Children.Add(originalPreview);previews.Children.Add(originalPanel);
        selectedPreview.Child=selectedLabel;selectedPreview.Height=34;selectedPreview.CornerRadius=new CornerRadius(5);selectedPreview.BorderBrush=Brushes.LightGray;selectedPreview.BorderThickness=new Thickness(1);RegisterName("SelectedColorPreview",selectedPreview);
        var selectedPanel=new StackPanel{Margin=new Thickness(4,0,0,0)};selectedPanel.Children.Add(Label("새 색"));selectedPanel.Children.Add(selectedPreview);Grid.SetColumn(selectedPanel,1);previews.Children.Add(selectedPanel);
        var inputs=Columns(4);body.Children.Add(inputs);Input(inputs,hex,"HEX","HexInput",0,7);Input(inputs,rgb[0],"R","RedInput",1,3);Input(inputs,rgb[1],"G","GreenInput",2,3);Input(inputs,rgb[2],"B","BlueInput",3,3);
        RegisterName("ColorInputError",error);body.Children.Add(error);
        hex.TextChanged+=(_,_)=>{if(!ready||syncing)return;if(ColorSelection.TryParseHex(hex.Text,out var color))UpdateSelection(color,hex);else Invalid("HEX는 #RRGGBB 형식의 6자리 값으로 입력해 주세요.");};
        foreach(var box in rgb)box.TextChanged+=(_,_)=>
        {
            if(!ready||syncing)return;
            if(byte.TryParse(rgb[0].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var r)&&byte.TryParse(rgb[1].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var g)&&byte.TryParse(rgb[2].Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var b))UpdateSelection(Color.FromRgb(r,g,b),box);
            else Invalid("RGB 값은 0부터 255 사이의 정수로 입력해 주세요.");
        };
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
        var cancel=new Button{Content="취소",Width=80,Height=34,IsCancel=true};RegisterName("CancelColorButton",cancel);apply.Style=(Style)FindResource("PurplePrimaryButtonStyle");RegisterName("ApplyColorButton",apply);apply.Click+=(_,_)=>{if(apply.IsEnabled)DialogResult=true;};buttons.Children.Add(cancel);buttons.Children.Add(apply);Grid.SetRow(buttons,2);grid.Children.Add(buttons);
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=ThemeResources.Get(this,"Selected"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=grid};ready=true;UpdateSelection(original);
    }

    internal void SetSaturationValue(double saturation,double value)
    {if(!double.IsFinite(saturation)||!double.IsFinite(value))throw new ArgumentOutOfRangeException(nameof(saturation));s=Math.Clamp(saturation,0,1);v=Math.Clamp(value,0,1);UpdateSelection(ColorSelection.FromHsv(h,s,v),updateHsv:false);}

    private void UpdateSelection(Color color,TextBox? source=null,bool updateHsv=true)
    {
        SelectedColor=Color.FromRgb(color.R,color.G,color.B);if(updateHsv)(h,s,v)=ColorSelection.ToHsv(SelectedColor);
        syncing=true;
        try
        {
            if(hex!=source)hex.Text=SelectedHex;
            byte[] channels=[color.R,color.G,color.B];for(var i=0;i<3;i++)if(rgb[i]!=source)rgb[i].Text=channels[i].ToString(CultureInfo.InvariantCulture);
            hueSlider.Value=h;area.Background=new SolidColorBrush(ColorSelection.FromHsv(h,1,1));selectedPreview.Background=new SolidColorBrush(SelectedColor);selectedLabel.Text=SelectedHex;selectedLabel.Foreground=Contrast(SelectedColor);
            foreach(var button in palette.Children.OfType<Button>()){var selected=(string)button.Tag==SelectedHex;button.BorderBrush=ThemeResources.Get(this,selected?"Accent":"InputBorder");button.BorderThickness=new Thickness(selected?2:1);}
            error.Text="";apply.IsEnabled=true;PositionMarker();
        }
        finally{syncing=false;}
    }
    private void PositionMarker(){Canvas.SetLeft(marker,Math.Clamp(s*area.ActualWidth-6,0,Math.Max(0,area.ActualWidth-12)));Canvas.SetTop(marker,Math.Clamp((1-v)*area.ActualHeight-6,0,Math.Max(0,area.ActualHeight-12)));}
    private void Invalid(string message){error.Text=message;apply.IsEnabled=false;}
    private static Brush Contrast(Color color)=>color.R*.299+color.G*.587+color.B*.114<145?Brushes.White:Brushes.Black;
    private TextBlock Label(string text)=>new(){Text=text,FontSize=12,Foreground=ThemeResources.Get(this,"SecondaryText"),Margin=new Thickness(0,0,0,5)};
    private static Border Preview(Color color,UIElement child)=>new(){Height=34,Background=new SolidColorBrush(color),BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Child=child};
    private static Grid Columns(int count){var grid=new Grid();for(var i=0;i<count;i++)grid.ColumnDefinitions.Add(new());return grid;}
    private void Input(Grid grid,TextBox box,string label,string name,int column,int maxLength)
    {
        var panel=new StackPanel{Margin=new Thickness(column==0?0:4,0,column==3?0:4,0)};panel.Children.Add(Label(label));box.Style=(Style)FindResource("PurpleTextBoxStyle");box.Height=32;box.Padding=new Thickness(6);box.MaxLength=maxLength;box.ToolTip=label=="HEX"?"#RRGGBB":"0–255";RegisterName(name,box);System.Windows.Automation.AutomationProperties.SetName(box,label);panel.Children.Add(box);Grid.SetColumn(panel,column);grid.Children.Add(panel);
    }
}
