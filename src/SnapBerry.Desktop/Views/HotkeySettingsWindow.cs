using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;
using SnapBerry.Desktop.Shortcuts;

namespace SnapBerry.Desktop.Views;

public sealed class HotkeySettingsWindow : Window
{
    private readonly CheckBox enabled;private readonly TextBox region,window,display;
    private readonly TextBlock error=new(){Name="HotkeyError",TextWrapping=Avalonia.Media.TextWrapping.Wrap,Foreground=Avalonia.Media.Brushes.IndianRed};
    private readonly Button save;
    private readonly Func<HotkeySettings,Task> apply;
    public HotkeySettingsWindow(HotkeySettings settings,Func<HotkeySettings,Task> apply)
    {
        var scope=new NameScope();NameScope.SetNameScope(this,scope);
        this.apply=apply;Title="전역 캡처 단축키";Width=500;SizeToContent=SizeToContent.Height;CanResize=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new Thickness(28),Spacing=12};Content=panel;
        panel.Children.Add(new TextBlock{Text="캡처 단축키",FontSize=23,FontWeight=Avalonia.Media.FontWeight.SemiBold});
        var description=new TextBlock{Text="SnapBerry가 열려 있지 않은 화면에서도 빠르게 캡처하세요.",TextWrapping=Avalonia.Media.TextWrapping.Wrap};description.Classes.Add("caption");panel.Children.Add(description);
        enabled=new(){Name="EnableGlobalHotkeys",Content="다른 앱에서도 캡처 단축키 사용",IsChecked=settings.Enabled,Margin=new Thickness(0,8)};panel.Children.Add(enabled);
        scope.Register(enabled.Name!,enabled);
        TextBox Field(string name,string label,string value,string defaultValue)
        {
            var caption=new TextBlock{Text=label,Margin=new Thickness(0,4,0,0)};caption.Classes.Add("section-title");panel.Children.Add(caption);
            var row=new Grid{ColumnDefinitions=new("*,Auto")};var field=new TextBox{Name=name,Text=value,MaxLength=80,IsReadOnly=true,FontFamily=new Avalonia.Media.FontFamily("Menlo, Consolas, monospace"),MinHeight=34};ToolTip.SetTip(field,"입력란을 선택하고 사용할 키 조합을 누르세요.");row.Children.Add(field);scope.Register(name,field);
            var reset=new Button{Name="Reset"+name,Content="기본값",Margin=new Thickness(8,0,0,0)};Grid.SetColumn(reset,1);row.Children.Add(reset);scope.Register(reset.Name!,reset);panel.Children.Add(row);
            var restore=value;
            field.GotFocus+=(_,_)=>restore=field.Text??"";
            reset.Click+=(_,_)=>{field.Text=defaultValue;restore=defaultValue;error.Text="";};
            field.AddHandler(InputElement.KeyDownEvent,(_,e)=>
            {
                if(e.Key==Key.Escape){field.Text=restore;error.Text="";e.Handled=true;return;}
                // Tab은 기본 탐색을 유지하고 보조 키만 누른 경우에는 아직 조합을 확정하지 않는다.
                if(e.Key==Key.Tab)return;
                e.Handled=true;
                if(e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)return;
                var key=e.Key>=Key.A&&e.Key<=Key.Z?e.Key.ToString():e.Key>=Key.D0&&e.Key<=Key.D9?((int)e.Key-(int)Key.D0).ToString():e.Key>=Key.NumPad0&&e.Key<=Key.NumPad9?((int)e.Key-(int)Key.NumPad0).ToString():e.Key>=Key.F1&&e.Key<=Key.F12?e.Key.ToString():null;
                var parts=new List<string>();
                if(e.KeyModifiers.HasFlag(KeyModifiers.Meta))parts.Add("Command");
                if(e.KeyModifiers.HasFlag(KeyModifiers.Control))parts.Add("Ctrl");
                if(e.KeyModifiers.HasFlag(KeyModifiers.Alt))parts.Add("Option");
                if(e.KeyModifiers.HasFlag(KeyModifiers.Shift))parts.Add("Shift");
                if(key is null){error.Text="영문·숫자·F1~F12 키를 사용하세요.";return;}
                parts.Add(key);var chord=string.Join('+',parts);
                try{HotkeyChord.Parse(chord);field.Text=chord;error.Text="";}
                catch(ArgumentException exception){error.Text=exception.Message;}
            },RoutingStrategies.Tunnel|RoutingStrategies.Bubble);
            return field;
        }
        var defaults=new HotkeySettings();
        region=Field("RegionHotkey","영역 캡처",settings.Region,defaults.Region);window=Field("WindowHotkey","창 캡처",settings.Window,defaults.Window);display=Field("DisplayHotkey","전체 화면",settings.Display,defaults.Display);
        var hint=new TextBlock{Text="입력란에서 원하는 키 조합을 누르세요.\n예: Ctrl+Shift+1, Command+Option+P, Ctrl+F12",TextWrapping=Avalonia.Media.TextWrapping.Wrap};hint.Classes.Add("caption");panel.Children.Add(hint);
        panel.Children.Add(error);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8,Margin=new Thickness(0,8,0,0)};var cancel=new Button{Content="취소",IsCancel=true,MinWidth=76};save=new(){Content="저장",Name="SaveHotkeys",IsDefault=true,MinWidth=76};save.Classes.Add("primary");buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);
        scope.Register(error.Name!,error);scope.Register(save.Name!,save);
        cancel.Click+=(_,_)=>Close(false);save.Click+=async (_,_)=>{if(await TrySaveAsync())Close(true);};
    }
    public async Task<bool> TrySaveAsync()
    {
        if(!save.IsEnabled)return false;save.IsEnabled=false;
        try{var value=new HotkeySettings{Enabled=enabled.IsChecked==true,Region=region.Text??"",Window=window.Text??"",Display=display.Text??""};value.Chords();await apply(value);return true;}
        catch(Exception exception){error.Text=exception.Message;return false;}
        finally{save.IsEnabled=true;}
    }
}
