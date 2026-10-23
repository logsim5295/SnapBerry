using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SnapBerry.Desktop.Views;

public sealed class DesktopSettingsWindow : Window
{
    private readonly ComboBox theme;private readonly CheckBox startup;private readonly Button save;private readonly TextBlock status;
    private readonly Func<bool,bool,Task> apply;
    public DesktopSettingsWindow(bool dark,bool startAtLogin,bool supportsStartup,Func<bool,bool,Task> apply,string? warning=null)
    {
        this.apply=apply;Title="설정";Width=460;SizeToContent=SizeToContent.Height;CanResize=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var scope=new NameScope();NameScope.SetNameScope(this,scope);
        var panel=new StackPanel{Margin=new Thickness(28),Spacing=14};Content=panel;
        panel.Children.Add(new TextBlock{Text="설정",FontSize=23,FontWeight=FontWeight.SemiBold});
        var description=new TextBlock{Text="작업 환경을 나에게 맞게 설정하세요."};description.Classes.Add("caption");panel.Children.Add(description);
        var appearance=new TextBlock{Text="화면 테마",Margin=new Thickness(0,8,0,0)};appearance.Classes.Add("section-title");panel.Children.Add(appearance);
        theme=new ComboBox{Name="SettingsThemeBox",ItemsSource=new[]{"화이트 모드","다크 모드"},SelectedIndex=dark?1:0,HorizontalAlignment=HorizontalAlignment.Stretch};scope.Register(theme.Name,theme);panel.Children.Add(theme);
        theme.SelectionChanged+=(_,_)=>RequestedThemeVariant=theme.SelectedIndex==1?Avalonia.Styling.ThemeVariant.Dark:Avalonia.Styling.ThemeVariant.Light;
        startup=new CheckBox{Name="StartAtLoginBox",Content="Mac 로그인 시 실행",IsChecked=startAtLogin,IsEnabled=supportsStartup};scope.Register(startup.Name,startup);panel.Children.Add(startup);
        if(!supportsStartup)panel.Children.Add(new TextBlock{Text="자동 실행은 macOS에서 설정할 수 있습니다.",TextWrapping=TextWrapping.Wrap,Opacity=.7});
        status=new TextBlock{Name="SettingsStatus",Text=warning,Foreground=Brushes.IndianRed,TextWrapping=TextWrapping.Wrap};scope.Register(status.Name,status);panel.Children.Add(status);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8,Margin=new Thickness(0,8,0,0)};
        var cancel=new Button{Content="취소",IsCancel=true,MinWidth=76};save=new Button{Name="SaveSettingsButton",Content="저장",IsDefault=true,MinWidth=76};save.Classes.Add("primary");scope.Register(save.Name,save);
        cancel.Click+=(_,_)=>Close(false);save.Click+=async (_,_)=>{if(await TrySaveAsync())Close(true);};buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){Close(false);e.Handled=true;}};
    }
    public async Task<bool> TrySaveAsync()
    {
        if(!save.IsEnabled)return false;save.IsEnabled=false;
        try{await apply(theme.SelectedIndex==1,startup.IsChecked==true);return true;}
        catch(Exception error){status.Text=error.Message;return false;}
        finally{save.IsEnabled=true;}
    }
}
