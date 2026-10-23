using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SnapBerry.App.Editor;

// 창 닫기가 앱 종료와 다름을 최초 한 번 안내한다. 확인 여부 저장은 호출하는 편집기가 담당한다.
public sealed class TrayNoticeDialog : Window
{
    public TrayNoticeDialog(ImageSource? icon=null)
    {
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        Title="트레이 사용 안내";Width=440;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;FontFamily=new FontFamily("Segoe UI");FontSize=13;
        var panel=new StackPanel{Margin=new Thickness(24)};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,16)};
        header.Children.Add(new Image{Source=icon??RibbonIcons.Create("Open"),Width=28,Height=28,Margin=new Thickness(0,0,10,0)});
        header.Children.Add(new TextBlock{Text="트레이에서 계속 실행됩니다",FontSize=18,FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Text"),VerticalAlignment=VerticalAlignment.Center});
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};panel.Children.Add(header);
        var message=new TextBlock{Text="창을 닫아도 SnapBerry는 트레이에서 계속 실행됩니다.\n\n작업표시줄 오른쪽 알림 영역의 SnapBerry 아이콘에서 캡처 기능을 사용할 수 있습니다.\n\n완전히 종료하려면 아이콘을 우클릭한 뒤 ‘종료’를 선택하세요.",TextWrapping=TextWrapping.Wrap,LineHeight=22,Foreground=ThemeResources.Get(this,"Description")};
        RegisterName("TrayNoticeMessage",message);panel.Children.Add(message);
        var acknowledge=new Button{Content="확인",Width=80,Height=34,IsDefault=true,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};
        RegisterName("AcknowledgeTrayButton",acknowledge);acknowledge.Click+=(_,_)=>DialogResult=true;panel.Children.Add(acknowledge);
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=ThemeResources.Get(this,"Selected"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=panel};
        Loaded+=(_,_)=>acknowledge.Focus();PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;DialogResult=false;}};
    }
}
