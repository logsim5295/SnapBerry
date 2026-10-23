using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.Core;

namespace SnapBerry.App.Editor;

// 출력 픽셀 크기를 편집한다. 취소하면 문서에 아무 변경도 적용하지 않는다.
public sealed class ImageResizeDialog : Window
{
    private readonly TextBox widthInput=new(),heightInput=new();
    private readonly CheckBox keepRatio=new(){Content="비율 유지",IsChecked=true,Margin=new Thickness(0,16,0,0)};
    private readonly TextBlock error=new(){Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};
    private readonly Button apply=new(){Content="적용",Width=80,Height=34,IsDefault=true};
    private PixelSize aspectSize;private bool updating;
    public PixelSize? Result {get;private set;}

    public ImageResizeDialog(PixelSize size)
    {
        aspectSize=size;NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        keepRatio.Style=(Style)FindResource("PurpleCheckBoxStyle");
        error.Foreground=ThemeResources.Get(this,"Danger");
        Title="이미지 크기 변경";Width=410;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        FontFamily=new FontFamily("Segoe UI");FontSize=13;
        var panel=new StackPanel{Margin=new Thickness(24)};
        var header=new TextBlock{Text=Title,FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=Brush("#263B55"),Margin=new Thickness(0,0,0,20)};
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};panel.Children.Add(header);
        var row=new Grid();row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        AddInput("가로 (px)",widthInput,size.Width,0);AddInput("세로 (px)",heightInput,size.Height,1);
        void AddInput(string label,TextBox input,int value,int column)
        {
            var field=new StackPanel{Margin=column==0?new Thickness(0,0,12,0):new Thickness(0)};
            field.Children.Add(new TextBlock{Text=label,Foreground=Brush("#65717C"),Margin=new Thickness(0,0,0,6)});
            input.Style=(Style)FindResource("PurpleTextBoxStyle");input.Height=36;input.Padding=new Thickness(8,4,8,4);input.VerticalContentAlignment=VerticalAlignment.Center;
            input.Text=value.ToString(CultureInfo.InvariantCulture);field.Children.Add(input);Grid.SetColumn(field,column);row.Children.Add(field);
        }
        panel.Children.Add(row);panel.Children.Add(keepRatio);panel.Children.Add(error);
        panel.Children.Add(new TextBlock{Text="이미지와 주석을 함께 조절합니다. 원본은 보존됩니다.",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#75818B"),Margin=new Thickness(0,12,0,0)});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)};
        apply.Style=(Style)FindResource("PurplePrimaryButtonStyle");
        var cancel=new Button{Content="취소",Width=80,Height=34,IsCancel=true,Margin=new Thickness(8,0,0,0)};
        buttons.Children.Add(apply);buttons.Children.Add(cancel);panel.Children.Add(buttons);
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=Brush("#EBD5E6"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=panel};
        RegisterName("ResizeWidthInput",widthInput);RegisterName("ResizeHeightInput",heightInput);RegisterName("KeepAspectRatio",keepRatio);RegisterName("ApplyResizeButton",apply);RegisterName("ResizeError",error);
        widthInput.TextChanged+=(_,_)=>UpdateInput(true);heightInput.TextChanged+=(_,_)=>UpdateInput(false);
        keepRatio.Checked+=(_,_)=>{if(TryRead(out var current))aspectSize=current;UpdateInput(true);};keepRatio.Unchecked+=(_,_)=>UpdateInput(true);
        apply.Click+=(_,_)=>{if(TryRead(out var result)){Result=result;DialogResult=true;}};
        Loaded+=(_,_)=>{widthInput.Focus();widthInput.SelectAll();};UpdateInput(true);
    }

    private void UpdateInput(bool editWidth)
    {
        if(updating)return;updating=true;
        try
        {
            if(keepRatio.IsChecked==true)
            {
                if(!int.TryParse((editWidth?widthInput:heightInput).Text,NumberStyles.None,CultureInfo.InvariantCulture,out var value))throw new ArgumentException("픽셀 크기는 정수로 입력하세요.");
                var size=ImageResize.KeepAspectRatio(aspectSize,value,editWidth);
                (editWidth?heightInput:widthInput).Text=(editWidth?size.Height:size.Width).ToString(CultureInfo.InvariantCulture);
            }
            apply.IsEnabled=TryRead(out _);error.Text=apply.IsEnabled?"":"가로·세로에 유효한 픽셀 크기를 입력하세요.";
        }
        catch(ArgumentException){apply.IsEnabled=false;error.Text="가로·세로는 1~32767 px, 전체 크기는 약 2684만 픽셀 이하여야 합니다.";}
        finally{updating=false;}
    }

    private bool TryRead(out PixelSize size)
    {
        size=default;
        if(!int.TryParse(widthInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var width)||!int.TryParse(heightInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var height))return false;
        size=new(width,height);try{ImageResize.Validate(size);return true;}catch(ArgumentException){return false;}
    }
    private Brush Brush(string value)=>ThemeResources.FromLightColor(this,value);
}
