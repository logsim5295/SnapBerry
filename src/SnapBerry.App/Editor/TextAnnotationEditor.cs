using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using SnapBerry.Core;
namespace SnapBerry.App.Editor;
// 기존 값을 미리 채우는 공용 내용 편집 창이다. 일반 텍스트는 여러 줄, 번호·키 라벨은 한 줄 입력한다.
public sealed class TextAnnotationEditor : Window
{
    private readonly TextBox input=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(10),FontSize=18};
    public string Value=>input.Text;
    public TextAnnotationEditor(string value,bool multiline=true,int maxLength=AnnotationLimits.MaxTextLength,string? title=null,bool allowEmpty=false)
    {
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        input.Style=(Style)FindResource("PurpleTextBoxStyle");
        Title=title??(multiline?"텍스트 편집":"표시값 편집");Width=460;Height=multiline?320:240;MinWidth=340;MinHeight=multiline?260:230;WindowStartupLocation=WindowStartupLocation.CenterOwner;ResizeMode=ResizeMode.CanResize;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;FontFamily=new FontFamily("Segoe UI");FontSize=13;
        // 제목 표시줄 없이도 가장자리 크기 조절을 유지한다. 입력란과 버튼은 비클라이언트 영역에서 제외된다.
        WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,CornerRadius=new CornerRadius(8),ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
        var grid=new Grid{Margin=new Thickness(24)};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel{Orientation=Orientation.Horizontal,Background=Brushes.Transparent,Margin=new Thickness(0,0,0,20)};
        header.Children.Add(new Image{Source=RibbonIcons.Create("Edit"),Width=28,Height=28,Margin=new Thickness(0,0,10,0)});
        header.Children.Add(new TextBlock{Text=Title,FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Text"),VerticalAlignment=VerticalAlignment.Center});
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};RegisterName("TextEditorHeader",header);grid.Children.Add(header);
        input.Text=value;input.AcceptsReturn=multiline;input.VerticalContentAlignment=multiline?VerticalAlignment.Top:VerticalAlignment.Center;
        if(!multiline)
        {
            input.VerticalAlignment=VerticalAlignment.Center;input.Padding=new Thickness(10,6,10,6);
            input.TextWrapping=TextWrapping.NoWrap;input.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;
        }
        RegisterName("TextEditorInput",input);Grid.SetRow(input,1);grid.Children.Add(input);
        var lengthStatus=new TextBlock{Margin=new Thickness(0,6,0,0),FontSize=12,TextWrapping=TextWrapping.Wrap};RegisterName("TextLengthStatus",lengthStatus);Grid.SetRow(lengthStatus,2);grid.Children.Add(lengthStatus);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0)};RegisterName("TextEditorButtons",buttons);
        var cancel=new Button{Content="취소",Width=80,Height=34,IsCancel=true};var apply=new Button{Content="적용",Width=80,Height=34,Margin=new Thickness(0,0,8,0),Style=(Style)FindResource("PurplePrimaryButtonStyle")};apply.Click+=(_,_)=>{if(apply.IsEnabled)DialogResult=true;};buttons.Children.Add(apply);buttons.Children.Add(cancel);Grid.SetRow(buttons,3);grid.Children.Add(buttons);
        void Validate()
        {
            var tooLong=input.Text.Length>maxLength;var invalidLine=!multiline&&input.Text.Any(char.IsControl);
            apply.IsEnabled=!tooLong&&!invalidLine&&(allowEmpty||!string.IsNullOrWhiteSpace(input.Text));
            lengthStatus.Text=invalidLine?"줄바꿈 없이 한 줄로 입력해 주세요.":tooLong?$"{maxLength}자까지 입력할 수 있습니다. ({input.Text.Length}자 입력됨)":$"{input.Text.Length} / {maxLength}자"+(allowEmpty?" · 비우면 날짜로 표시합니다.":"");
            lengthStatus.Foreground=ThemeResources.Get(this,tooLong?"Danger":"SecondaryText");
        }
        input.TextChanged+=(_,_)=>Validate();Validate();
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=ThemeResources.Get(this,"Selected"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=grid};
        Loaded+=(_,_)=>{input.Focus();input.SelectAll();};PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Enter&&(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)||!multiline)){if(apply.IsEnabled)DialogResult=true;e.Handled=true;}};
    }
}
