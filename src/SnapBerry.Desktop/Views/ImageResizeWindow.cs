using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SnapBerry.Core;
using PixelSize=SnapBerry.Core.PixelSize;

namespace SnapBerry.Desktop.Views;

public sealed class ImageResizeWindow : Window
{
    private readonly TextBox width=new(),height=new();
    private readonly CheckBox aspect=new(){Content="비율 유지",IsChecked=true};
    private readonly TextBlock error=new(){Foreground=Brushes.IndianRed,TextWrapping=TextWrapping.Wrap,MinHeight=20};
    private readonly Button apply=new(){Content="적용",IsDefault=true};
    private readonly PixelSize original;
    private PixelSize aspectSize;
    private bool syncing;
    public PixelSize SelectedSize {get;private set;}

    public ImageResizeWindow(PixelSize original,PixelSize current)
    {
        ImageResize.Validate(original);ImageResize.Validate(current);this.original=original;aspectSize=current;SelectedSize=current;
        var names=new NameScope();NameScope.SetNameScope(this,names);
        void Register(string name,Control control){control.Name=name;names.Register(name,control);}
        Title="이미지 크기 변경";Width=420;SizeToContent=SizeToContent.Height;CanResize=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new Thickness(22),Spacing=12};Content=panel;
        panel.Children.Add(new TextBlock{Text=Title,FontSize=22,FontWeight=FontWeight.SemiBold});
        panel.Children.Add(new TextBlock{Text=$"원본 {original.Width} × {original.Height} px",TextWrapping=TextWrapping.Wrap});
        var fields=new Grid{ColumnDefinitions=new("*,*")};panel.Children.Add(fields);
        void Field(TextBox field,string name,string label,int column)
        {
            var stack=new StackPanel{Spacing=5,Margin=new Thickness(column==0?0:10,0,0,0)};stack.Children.Add(new TextBlock{Text=label});Register(name,field);field.MaxLength=10;stack.Children.Add(field);Grid.SetColumn(stack,column);fields.Children.Add(stack);
        }
        Field(width,"ResizeWidth","가로 (px)",0);Field(height,"ResizeHeight","세로 (px)",1);
        // Windows 자동화에서 쓰는 별칭도 같은 입력을 가리킨다.
        names.Register("ResizeWidthInput",width);names.Register("ResizeHeightInput",height);Register("LockAspectRatio",aspect);names.Register("KeepAspectRatio",aspect);panel.Children.Add(aspect);
        Register("ResizeError",error);panel.Children.Add(error);
        panel.Children.Add(new TextBlock{Text="이미지와 주석을 함께 조절합니다. 원본은 보존됩니다.",TextWrapping=TextWrapping.Wrap});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8};
        var reset=new Button{Content="원본 크기"};Register("ResetResizeButton",reset);var cancel=new Button{Content="취소",IsCancel=true};Register("CancelResizeButton",cancel);Register("ApplyResizeButton",apply);
        buttons.Children.Add(reset);buttons.Children.Add(cancel);buttons.Children.Add(apply);panel.Children.Add(buttons);
        SetInputs(current);
        width.PropertyChanged+=(_,e)=>{if(e.Property==TextBox.TextProperty)UpdateInput(true);};height.PropertyChanged+=(_,e)=>{if(e.Property==TextBox.TextProperty)UpdateInput(false);};
        aspect.IsCheckedChanged+=(_,_)=>{if(syncing)return;if(aspect.IsChecked==true&&TryRead(out var currentSize))aspectSize=currentSize;UpdateInput(true);};
        reset.Click+=(_,_)=>{aspectSize=this.original;SetInputs(this.original);UpdateInput(true);TryAccept();};
        cancel.Click+=(_,_)=>Close();apply.Click+=(_,_)=>{if(TryAccept())Close(SelectedSize);};
    }

    public bool TryAccept()
    {
        if(!apply.IsEnabled)return false;
        if(!TryRead(out var size)){Invalid();return false;}
        SelectedSize=size;error.Text="";apply.IsEnabled=true;return true;
    }

    private void UpdateInput(bool editWidth)
    {
        if(syncing)return;syncing=true;
        try
        {
            if(aspect.IsChecked==true)
            {
                if(!int.TryParse((editWidth?width:height).Text,NumberStyles.None,CultureInfo.InvariantCulture,out var value)){Invalid();return;}
                var size=ImageResize.KeepAspectRatio(aspectSize,value,editWidth);
                (editWidth?height:width).Text=(editWidth?size.Height:size.Width).ToString(CultureInfo.InvariantCulture);
            }
            if(TryRead(out _)){error.Text="";apply.IsEnabled=true;}else Invalid();
        }
        catch(ArgumentException){Invalid();}
        finally{syncing=false;}
    }
    private void SetInputs(PixelSize size)
    {
        syncing=true;try{width.Text=size.Width.ToString(CultureInfo.InvariantCulture);height.Text=size.Height.ToString(CultureInfo.InvariantCulture);}finally{syncing=false;}
    }
    private bool TryRead(out PixelSize size)
    {
        size=default;
        if(!int.TryParse(width.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var w)||!int.TryParse(height.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var h))return false;
        size=new(w,h);try{ImageResize.Validate(size);return true;}catch(ArgumentException){return false;}
    }
    private void Invalid(){error.Text="가로·세로는 1~32767 px, 전체 크기는 약 2684만 픽셀 이하여야 합니다.";apply.IsEnabled=false;}
}
