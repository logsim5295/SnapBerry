using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Data;
using SnapBerry.App.Settings;

namespace SnapBerry.App.Editor;

// 창별 팔레트를 공유 자원으로 제공한다. 이미지·주석·색상 견본에는 테마 색상을 적용하지 않는다.
public static class ThemeResources
{
    private const string ModeKey="SnapBerry.Theme";
    private static readonly (string Key,string Light,string Dark)[] Palette=
    [
        ("WindowBackground","#F3F5F7","#181A20"),("Surface","#FFFFFF","#22252D"),
        ("Text","#263B55","#E9EDF3"),("SecondaryText","#65717C","#B1BAC7"),
        ("MutedText","#75818B","#A3ADBC"),("Description","#4E5C68","#B1BAC7"),
        ("Canvas","#EEF1F4","#15171C"),("Thumbnail","#F0F3F5","#1B1E25"),
        ("Ribbon","#FAFBFC","#252832"),("Border","#DBE2E7","#414653"),
        ("InputBorder","#DCE2E7","#48505E"),
        ("Hover","#F5EAF3","#392C3D"),("Selected","#EBD5E6","#51334E"),
        ("Accent","#923C83","#C880BA"),("AccentText","#743069","#E6ACDC"),
        ("AccentPressed","#602657","#98558D"),("ScrollPressed","#D6B0CE","#6F4C69"),
        ("AccentHover","#B76AA9","#D79CCC"),
        ("Danger","#A83855","#FF8C9F"),("Primary","#923C83","#923C83"),
        ("PrimaryText","#FFFFFF","#FFFFFF"),("PrimaryHover","#743069","#743069")
    ];
    public static AppTheme Current=>Application.Current?.Properties[ModeKey] is AppTheme mode?mode:AppTheme.Light;
    public static void Initialize(Window window,AppTheme? theme=null)
    {
        Apply(window,theme??Current);
        window.SetResourceReference(Control.ForegroundProperty,"Theme.Text");
        window.SourceInitialized+=(_,_)=>ApplyTitleBar(window,GetTheme(window));
    }
    public static AppTheme GetTheme(FrameworkElement window)=>window.Resources[ModeKey] is AppTheme mode?mode:AppTheme.Light;
    public static void Apply(Window window,AppTheme theme)
    {
        if(!Enum.IsDefined(theme))theme=AppTheme.Light;
        window.Resources[ModeKey]=theme;
        window.Resources["Theme.IsDark"]=theme==AppTheme.Dark;
        foreach(var (key,light,dark) in Palette)
        {
            var color=(Color)ColorConverter.ConvertFromString(theme==AppTheme.Dark?dark:light);
            // 바인딩된 브러시는 WPF 자원 조회 시 동결되지 않아 C#로 만든 창도 즉시 갱신된다.
            if(window.Resources["Theme.Source."+key] is ThemeColor source)source.Value=color;
            else
            {
                source=new(){Value=color};window.Resources["Theme.Source."+key]=source;
                var brush=new SolidColorBrush();BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding(nameof(ThemeColor.Value)){Source=source});
                window.Resources["Theme."+key]=brush;
            }
        }
        foreach(var (key,value) in new (object,string)[]{(SystemColors.WindowBrushKey,"Surface"),(SystemColors.WindowTextBrushKey,"Text"),
            (SystemColors.ControlBrushKey,"Surface"),(SystemColors.ControlTextBrushKey,"Text"),(SystemColors.ControlLightBrushKey,"Ribbon"),
            (SystemColors.ControlLightLightBrushKey,"Surface"),(SystemColors.ControlDarkBrushKey,"InputBorder"),
            (SystemColors.HighlightBrushKey,"Selected"),(SystemColors.HighlightTextBrushKey,"Text"),(SystemColors.GrayTextBrushKey,"MutedText"),
            (SystemColors.InfoBrushKey,"Surface"),(SystemColors.InfoTextBrushKey,"Text")})
            window.Resources[key]=Get(window,value);
        ApplyTitleBar(window,theme);
    }
    public static void ApplyToApplication(AppTheme theme)
    {
        if(Application.Current is not {} app)return;
        app.Properties[ModeKey]=theme;
        foreach(Window window in app.Windows)
            if(window.Resources.Contains(ModeKey))Apply(window,theme);
    }
    public static SolidColorBrush Get(FrameworkElement window,string key)=>(SolidColorBrush)window.FindResource("Theme."+key);
    public static Color ColorFor(AppTheme theme,string key)
    {var item=Palette.First(x=>x.Key==key);return(Color)ColorConverter.ConvertFromString(theme==AppTheme.Dark?item.Dark:item.Light);}
    public static Brush FromLightColor(FrameworkElement window,string color)=>Get(window,Palette.First(x=>x.Light.Equals(color,StringComparison.OrdinalIgnoreCase)).Key);
    private static void ApplyTitleBar(Window window,AppTheme theme)
    {
        var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
        var dark=theme==AppTheme.Dark?1:0;DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
    }
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    [Obfuscation(Exclude=true,ApplyToMembers=true)]
    private sealed class ThemeColor : INotifyPropertyChanged
    {
        private Color value;
        public Color Value {get=>value;set{this.value=value;PropertyChanged?.Invoke(this,new(nameof(Value)));}}
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
