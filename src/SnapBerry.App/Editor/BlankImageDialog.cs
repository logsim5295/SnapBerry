using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.App.Import;
using SnapBerry.Core;

namespace SnapBerry.App.Editor;

public sealed class BlankImageDialog : Window
{
    private readonly TextBox widthInput=new(),heightInput=new();
    private readonly CheckBox transparent=new(){Content="투명 배경",Margin=new Thickness(0,12,0,0)};
    private readonly Button colorButton=new(){Height=36,HorizontalContentAlignment=HorizontalAlignment.Left};
    private readonly Border colorSample=new(){Width=22,Height=22,BorderBrush=Brushes.Gray,BorderThickness=new Thickness(1),Margin=new Thickness(0,0,10,0)};
    private readonly TextBlock colorLabel=new(){VerticalAlignment=VerticalAlignment.Center};
    private readonly TextBlock error=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0)};
    private readonly Button create=new(){Content="만들기",Width=80,Height=34,IsDefault=true};
    private Color background=Colors.White;
    public BlankImageOptions? Result {get;private set;}

    public BlankImageDialog()
    {
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        Title="빈 화면 만들기";Width=410;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        FontFamily=new FontFamily("Segoe UI");FontSize=13;
        var panel=new StackPanel{Margin=new Thickness(24)};
        var header=new TextBlock{Text=Title,FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Text"),Margin=new Thickness(0,0,0,20)};
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};panel.Children.Add(header);
        var row=new Grid();row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new());
        AddInput("가로 (px)",widthInput,"1280",0);AddInput("세로 (px)",heightInput,"720",1);panel.Children.Add(row);
        void AddInput(string label,TextBox input,string value,int column)
        {
            var field=new StackPanel{Margin=column==0?new Thickness(0,0,12,0):new Thickness(0)};
            field.Children.Add(new TextBlock{Text=label,Foreground=ThemeResources.Get(this,"SecondaryText"),Margin=new Thickness(0,0,0,6)});
            input.Style=(Style)FindResource("PurpleTextBoxStyle");input.Height=36;input.Padding=new Thickness(8,4,8,4);input.VerticalContentAlignment=VerticalAlignment.Center;input.Text=value;
            System.Windows.Automation.AutomationProperties.SetName(input,label);
            field.Children.Add(input);Grid.SetColumn(field,column);row.Children.Add(field);
        }
        panel.Children.Add(new TextBlock{Text="배경색",Foreground=ThemeResources.Get(this,"SecondaryText"),Margin=new Thickness(0,16,0,6)});
        var colorContent=new StackPanel{Orientation=Orientation.Horizontal};colorContent.Children.Add(colorSample);colorContent.Children.Add(colorLabel);colorButton.Content=colorContent;
        colorButton.ToolTip="배경색 선택";System.Windows.Automation.AutomationProperties.SetName(colorButton,"배경색 선택");
        colorButton.Click+=(_,_)=>
        {
            var picker=new ColorPickerWindow(background){Owner=this,Icon=Icon};
            if(picker.ShowDialog()==true){background=picker.SelectedColor;UpdateColor();}
        };
        transparent.Style=(Style)FindResource("PurpleCheckBoxStyle");
        transparent.Checked+=(_,_)=>colorButton.IsEnabled=false;transparent.Unchecked+=(_,_)=>colorButton.IsEnabled=true;
        panel.Children.Add(colorButton);panel.Children.Add(transparent);error.Foreground=ThemeResources.Get(this,"Danger");panel.Children.Add(error);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)};
        create.Style=(Style)FindResource("PurplePrimaryButtonStyle");
        buttons.Children.Add(create);buttons.Children.Add(new Button{Content="취소",Width=80,Height=34,IsCancel=true,Margin=new Thickness(8,0,0,0)});panel.Children.Add(buttons);
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=ThemeResources.Get(this,"Selected"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=panel};
        RegisterName("BlankWidthInput",widthInput);RegisterName("BlankHeightInput",heightInput);RegisterName("BlankTransparent",transparent);
        RegisterName("BlankColorButton",colorButton);RegisterName("CreateBlankButton",create);RegisterName("BlankError",error);
        widthInput.TextChanged+=(_,_)=>ValidateInput();heightInput.TextChanged+=(_,_)=>ValidateInput();
        create.Click+=(_,_)=>{if(TryRead(out var size)){Result=new(size,transparent.IsChecked==true?Colors.Transparent:background);DialogResult=true;}};
        Loaded+=(_,_)=>{widthInput.Focus();widthInput.SelectAll();};UpdateColor();ValidateInput();
    }
    private void UpdateColor()
    {colorSample.Background=new SolidColorBrush(background);colorLabel.Text=$"#{background.R:X2}{background.G:X2}{background.B:X2}";}
    private void ValidateInput()
    {
        create.IsEnabled=TryRead(out _);
        error.Text=create.IsEnabled?"":"가로·세로는 1~32767 px 정수, 전체 크기는 약 2684만 픽셀 이하여야 합니다.";
    }
    private bool TryRead(out PixelSize size)
    {
        size=default;
        if(!int.TryParse(widthInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var width)||!int.TryParse(heightInput.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var height))return false;
        size=new(width,height);try{ImageResize.Validate(size);return true;}catch(ArgumentException){return false;}
    }
}
