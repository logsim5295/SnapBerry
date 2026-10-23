using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Windows.Media;
using SnapBerry.App.Editor;
namespace SnapBerry.App.Settings;
// 키 직접 입력·기본값 복원·중복 및 외부 점유 검증을 제공하고, 확정된 설정만 호출자에게 반환한다.
public sealed class SettingsWindow : Window
{
    public AppSettings? Result {get;private set;}
    public SettingsWindow(AppSettings settings,Func<string,string?>? checkAvailability=null,Func<string,HotkeyRegistrationResult>? inspectBinding=null)
    {
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this,settings.Theme);
        Title="설정";Width=480;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;FontFamily=new FontFamily("Segoe UI");FontSize=13;
        var panel=new StackPanel{Margin=new Thickness(24)};
        var header=new StackPanel{Orientation=Orientation.Horizontal,Background=Brushes.Transparent,Margin=new Thickness(0,0,0,24)};
        header.Children.Add(new Image{Source=RibbonIcons.Create("Settings"),Width=28,Height=28,Margin=new Thickness(0,0,10,0)});
        header.Children.Add(new TextBlock{Text="설정",FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Text"),VerticalAlignment=VerticalAlignment.Center});
        // 기본 제목 표시줄 대신 제목 영역을 드래그해 창을 이동할 수 있게 한다.
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};RegisterName("SettingsHeader",header);panel.Children.Add(header);
        panel.Children.Add(new TextBlock{Text="화면 테마",FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Accent"),Margin=new Thickness(0,0,0,8)});
        var theme=new ComboBox{Style=(Style)FindResource("PurpleComboBoxStyle"),Height=34,Margin=new Thickness(0,0,0,20)};
        theme.Items.Add(new ComboBoxItem{Content="화이트 모드"});theme.Items.Add(new ComboBoxItem{Content="다크 모드"});theme.SelectedIndex=settings.Theme==AppTheme.Dark?1:0;
        RegisterName("ThemeSelector",theme);System.Windows.Automation.AutomationProperties.SetName(theme,"화면 테마");
        theme.SelectionChanged+=(_,_)=>ThemeResources.Apply(this,theme.SelectedIndex==1?AppTheme.Dark:AppTheme.Light);panel.Children.Add(theme);
        panel.Children.Add(new TextBlock{Text="캡처 단축키",FontWeight=FontWeights.SemiBold,Foreground=ThemeResources.Get(this,"Accent"),Margin=new Thickness(0,0,0,14)});
        var fields=new List<(HotkeyInput Box,TextBlock Error)>();Button? save=null;
        void Validate()
        {
            // 입력 형식, 앱 내부 중복, OS 등록 가능 여부 순으로 검사하고 경고는 저장을 막지 않는다.
            var parsed=new (uint,uint)?[fields.Count];var errors=new string?[fields.Count];var warnings=new string?[fields.Count];
            for(var i=0;i<fields.Count;i++){errors[i]=fields[i].Box.CaptureError;try{parsed[i]=HotkeyService.Parse(fields[i].Box.Text);}catch(ArgumentException ex){errors[i]??=ex.Message;}}
            for(var i=0;i<fields.Count;i++)
            {
                if(parsed[i] is not null&&parsed.Where((_,j)=>j!=i).Contains(parsed[i]))errors[i]="다른 캡처 기능의 단축키와 중복됩니다.";
                if(errors[i] is null)errors[i]=checkAvailability?.Invoke(fields[i].Box.Text);
                if(errors[i] is null&&inspectBinding is not null){var result=inspectBinding(fields[i].Box.Text);if(!result.Success)errors[i]=result.Error??"등록할 수 없는 단축키입니다.";else warnings[i]=result.Warning;}
                fields[i].Error.Text=errors[i]??warnings[i]??"";fields[i].Error.Visibility=errors[i] is null&&warnings[i] is null?Visibility.Collapsed:Visibility.Visible;fields[i].Error.Foreground=ThemeResources.Get(this,errors[i] is null?"Accent":"Danger");
            }
            if(save is not null)save.IsEnabled=errors.All(x=>x is null);
        }
        HotkeyInput Input(string title,string name,string text,string defaultValue)
        {
            panel.Children.Add(new TextBlock{Text=title,Margin=new Thickness(0,0,0,5)});var row=new Grid();row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
            var box=new HotkeyInput{Style=(Style)FindResource("PurpleTextBoxStyle"),Text=text,Height=34,Padding=new Thickness(7),ToolTip="클릭한 뒤 원하는 단축키를 누르세요. Esc로 입력을 취소합니다."};RegisterName(name,box);row.Children.Add(box);
            var reset=new Button{Content=new Image{Source=RibbonIcons.Create("Undo"),Width=18,Height=18},Width=34,Height=34,Margin=new Thickness(8,0,0,0),ToolTip=$"기본값으로 초기화: {defaultValue}"};RegisterName(name+"Reset",reset);Grid.SetColumn(reset,1);row.Children.Add(reset);panel.Children.Add(row);
            var error=new TextBlock{Foreground=ThemeResources.Get(this,"Danger"),FontSize=12,TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};RegisterName(name+"Error",error);panel.Children.Add(error);panel.Children.Add(new Border{Height=12});fields.Add((box,error));
            reset.Click+=(_,_)=>box.SetBindingText(defaultValue);box.TextChanged+=(_,_)=>Validate();box.CaptureStatusChanged+=Validate;return box;
        }
        var defaults=new AppSettings();var region=Input("영역 캡처","RegionHotkey",settings.RegionKey,defaults.RegionKey);var window=Input("창 캡처","WindowHotkey",settings.WindowKey,defaults.WindowKey);var desktop=Input("전체 화면 캡처","DesktopHotkey",settings.DesktopKey,defaults.DesktopKey);
        var startup=new CheckBox{Style=(Style)FindResource("PurpleCheckBoxStyle"),Content="Windows 시작 시 실행",IsChecked=settings.StartWithWindows,Margin=new Thickness(0,4,0,12)};panel.Children.Add(startup);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
        var cancel=new Button{Content="취소",Width=80,Height=34,IsCancel=true};RegisterName("CancelSettingsButton",cancel);cancel.Click+=(_,_)=>DialogResult=false;
        save=new Button{Content="저장",Height=34,Width=80,Margin=new Thickness(8,0,0,0),Style=(Style)FindResource("PurplePrimaryButtonStyle")};RegisterName("SaveSettingsButton",save);save.Click+=(_,_)=>
        {Validate();if(!save.IsEnabled)return;Result=settings with{Theme=theme.SelectedIndex==1?AppTheme.Dark:AppTheme.Light,RegionKey=region.Text,WindowKey=window.Text,DesktopKey=desktop.Text,StartWithWindows=startup.IsChecked==true};DialogResult=true;};buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=ThemeResources.Get(this,"Selected"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=panel};Validate();
    }
    // 트레이에서 열 때 숨김·최소화 편집기를 소유자로 연결하지 않아 편집기가 함께 복원되지 않게 한다.
    internal void ConfigureOwner(Window editor)
    {
        var useEditor=editor.IsVisible&&editor.WindowState!=WindowState.Minimized;
        Owner=useEditor?editor:null;
        WindowStartupLocation=useEditor?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen;
    }
}
// 현재 사용자 Run 레지스트리로 트레이 자동 시작을 관리한다. 기존 등록 키 이름은 호환성을 위해 유지한다.
public static class StartupService
{
    public static void SetEnabled(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if(enabled){var path=Environment.ProcessPath??throw new InvalidOperationException("실행 경로를 확인할 수 없습니다.");key.SetValue("QuickCapture",$"\"{path}\" --tray");}else key.DeleteValue("QuickCapture",false);
    }
}
