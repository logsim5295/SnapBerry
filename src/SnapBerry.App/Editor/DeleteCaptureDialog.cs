using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SnapBerry.App.Editor;

// 이력 삭제 전용 확인창이다. 기본 포커스와 Enter는 삭제, Esc·닫기는 취소로 처리한다.
public sealed class DeleteCaptureDialog : Window
{
    public DeleteCaptureDialog(int count)
    {
        if(count<1)throw new ArgumentOutOfRangeException(nameof(count));
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        Title="캡처 이력 삭제";Width=420;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;FontFamily=new FontFamily("Segoe UI");FontSize=13;

        var panel=new StackPanel{Margin=new Thickness(24)};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,16)};
        var icon=new Image{Source=RibbonIcons.Create("Delete"),Width=28,Height=28,Margin=new Thickness(0,0,10,0)};
        RegisterName("DeleteDialogIcon",icon);header.Children.Add(icon);
        header.Children.Add(new TextBlock{Text="캡처를 삭제할까요?",FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=Brush("#263B55"),VerticalAlignment=VerticalAlignment.Center});
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};panel.Children.Add(header);

        var message=new TextBlock{Text=$"선택한 캡처 {count}개와 편집 내용이 삭제됩니다.\n삭제 후 복구할 수 없습니다.",TextWrapping=TextWrapping.Wrap,LineHeight=22,Foreground=Brush("#4E5C68")};
        RegisterName("DeleteMessage",message);panel.Children.Add(message);
        panel.Children.Add(new TextBlock{Text="내보낸 이미지 파일은 유지됩니다.",TextWrapping=TextWrapping.Wrap,Foreground=Brush("#75818B"),Margin=new Thickness(0,10,0,0)});

        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
        var cancel=new Button{Content="취소",Width=80,Height=34,IsCancel=true};
        var delete=new Button{Content="삭제",Width=80,Height=34,IsDefault=true,Margin=new Thickness(8,0,0,0),Style=(Style)FindResource("PurpleDeleteButtonStyle")};
        RegisterName("CancelDeleteButton",cancel);RegisterName("ConfirmDeleteButton",delete);
        cancel.Click+=(_,_)=>DialogResult=false;delete.Click+=(_,_)=>DialogResult=true;
        buttons.Children.Add(cancel);buttons.Children.Add(delete);panel.Children.Add(buttons);
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=Brush("#EBD5E6"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=panel};
        FocusManager.SetFocusedElement(this,delete);Loaded+=(_,_)=>delete.Focus();
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;DialogResult=false;}};
    }

    private Brush Brush(string value)=>ThemeResources.FromLightColor(this,value);
}
