using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using SnapBerry.Core;

namespace SnapBerry.Desktop.Views;

public sealed class TextEditWindow : Window
{
    private readonly TextBox input;
    private readonly TextBlock status;
    private readonly Button apply;
    private readonly bool multiline,allowEmpty;
    private readonly int maxLength;
    public string EditedText {get;private set;}

    public TextEditWindow(string initialText,string title="텍스트 편집",bool multiline=true,int maxLength=AnnotationLimits.MaxTextLength,bool allowEmpty=false)
    {
        if(maxLength<1||maxLength>AnnotationLimits.MaxTextLength)throw new ArgumentOutOfRangeException(nameof(maxLength));
        this.multiline=multiline;this.maxLength=maxLength;this.allowEmpty=allowEmpty;EditedText=initialText;
        var names=new NameScope();NameScope.SetNameScope(this,names);
        Title=title;Width=480;Height=multiline?340:250;MinWidth=340;MinHeight=multiline?260:230;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new Grid{Margin=new Thickness(22),RowDefinitions=new("Auto,*,Auto,Auto")};Content=root;
        root.Children.Add(new TextBlock{Text=title,FontSize=22,FontWeight=FontWeight.SemiBold,Margin=new Thickness(0,0,0,12)});
        input=new(){Name="TextEditInput",Text=initialText,AcceptsReturn=multiline,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=multiline?VerticalAlignment.Top:VerticalAlignment.Center,FontSize=18};
        // 초과 길이의 기존·붙여넣기 내용을 잘라 버리지 않고 검증 오류로 보여 준다.
        ScrollViewer.SetVerticalScrollBarVisibility(input,ScrollBarVisibility.Auto);names.Register(input.Name!,input);names.Register("TextEditorInput",input);Grid.SetRow(input,1);root.Children.Add(input);
        status=new(){Name="TextLengthStatus",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8)};names.Register(status.Name!,status);Grid.SetRow(status,2);root.Children.Add(status);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8};
        var cancel=new Button{Name="CancelTextButton",Content="취소",IsCancel=true};apply=new(){Name="ApplyTextButton",Content="적용",IsDefault=!multiline};
        names.Register(cancel.Name!,cancel);names.Register(apply.Name!,apply);buttons.Children.Add(cancel);buttons.Children.Add(apply);Grid.SetRow(buttons,3);root.Children.Add(buttons);
        input.PropertyChanged+=(_,e)=>{if(e.Property==TextBox.TextProperty)Validate();};
        cancel.Click+=(_,_)=>Close();apply.Click+=(_,_)=>{if(TryAccept())Close(EditedText);};
        AddHandler(InputElement.KeyDownEvent,(_,e)=>{if(e.Key==Key.Enter&&(!multiline||e.KeyModifiers.HasFlag(KeyModifiers.Control)||e.KeyModifiers.HasFlag(KeyModifiers.Meta))){e.Handled=true;if(TryAccept())Close(EditedText);}},RoutingStrategies.Tunnel);
        Validate();
    }

    public bool TryAccept()
    {
        if(!Validate())return false;EditedText=input.Text??"";return true;
    }
    private bool Validate()
    {
        var value=input.Text??"";var tooLong=value.Length>maxLength;var invalidLine=!multiline&&value.Any(char.IsControl);var blank=!allowEmpty&&string.IsNullOrWhiteSpace(value);
        apply.IsEnabled=!tooLong&&!invalidLine&&!blank;
        status.Text=tooLong?$"{maxLength}자까지 입력할 수 있습니다. ({value.Length}자 입력됨)":invalidLine?"줄바꿈 없이 한 줄로 입력하세요.":blank?"텍스트를 입력하세요.":$"{value.Length} / {maxLength}자"+(allowEmpty?" · 비우면 날짜로 표시합니다.":"");
        if(apply.IsEnabled)status.ClearValue(TextBlock.ForegroundProperty);else status.Foreground=Brushes.IndianRed;
        return apply.IsEnabled;
    }
}
