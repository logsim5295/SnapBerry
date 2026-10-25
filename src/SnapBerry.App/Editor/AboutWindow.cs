using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;
namespace SnapBerry.App.Editor;
// 제품 정보와 문의·진단 기능을 모은다. 외부 앱 실행과 클립보드 복사는 사용자 명령 때만 수행한다.
public sealed class AboutWindow : Window
{
    public string DevelopmentVersion {get;}=(typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??typeof(AboutWindow).Assembly.GetName().Version?.ToString()??"1.1.0").Split('+')[0];
    public string AuthorEmail=>"logsim1234@gmail.com";
    // 진단에는 환경 정보만 포함하며 사용자 이름·경로·캡처 데이터는 포함하지 않는다.
    internal string DiagnosticText=>$"SnapBerry {DevelopmentVersion}\n빌드: {typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}\nWindows: {RuntimeInformation.OSDescription}\n.NET: {Environment.Version}\n아키텍처: {RuntimeInformation.ProcessArchitecture}";
    public AboutWindow(Action<string>? copyText=null,Action<string>? openLocation=null)
    {
        NameScope.SetNameScope(this,new NameScope());
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/SnapBerry;component/Editor/ButtonStyles.xaml",UriKind.Relative)});
        ThemeResources.Initialize(this);
        Title="SnapBerry 정보";Width=480;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;FontFamily=new FontFamily("Segoe UI");FontSize=13;
        var panel=new StackPanel{Margin=new Thickness(24)};
        var headerRow=new Grid{Margin=new Thickness(0,0,0,18)};headerRow.ColumnDefinitions.Add(new());headerRow.ColumnDefinitions.Add(new(){Width=GridLength.Auto});panel.Children.Add(headerRow);
        var header=new StackPanel{Orientation=Orientation.Horizontal,Background=Brushes.Transparent};headerRow.Children.Add(header);
        BitmapSource appIcon;using(var stream=typeof(AboutWindow).Assembly.GetManifestResourceStream("SnapBerry.App.ico")!){appIcon=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames.OrderByDescending(frame=>frame.PixelWidth).First();appIcon.Freeze();}
        var icon=new Image{Source=appIcon,Width=40,Height=40,Margin=new Thickness(0,0,12,0)};RegisterName("AboutAppIcon",icon);header.Children.Add(icon);
        var title=new StackPanel();var name=new TextBlock{Text="SnapBerry",FontSize=22,FontWeight=FontWeights.SemiBold,Foreground=Brush("#263B55")};RegisterName("AboutProductName",name);title.Children.Add(name);
        var version=new TextBlock{Text=$"개발 버전: {DevelopmentVersion}",Foreground=Brush("#65717C"),Margin=new Thickness(0,4,0,0)};RegisterName("AboutVersion",version);title.Children.Add(version);header.Children.Add(title);
        header.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragMove();};
        var close=new Button{Content=new TextBlock{Text="\uE8BB",FontFamily=new FontFamily("Segoe Fluent Icons"),FontSize=12},Width=32,Height=32,ToolTip="닫기",IsCancel=true,IsDefault=true,VerticalAlignment=VerticalAlignment.Top,Foreground=ThemeResources.Get(this,"Accent")};RegisterName("CloseAboutButton",close);System.Windows.Automation.AutomationProperties.SetName(close,"정보 창 닫기");close.Click+=(_,_)=>Close();Grid.SetColumn(close,1);headerRow.Children.Add(close);
        var author=new TextBlock{Foreground=Brush("#4E5C68")};author.Inlines.Add(new Run("만든이: ynsim · "));var email=new Hyperlink(new Run(AuthorEmail)){NavigateUri=new Uri($"mailto:{AuthorEmail}"),Foreground=Brush("#923C83")};RegisterName("AboutAuthorLink",email);author.Inlines.Add(email);panel.Children.Add(author);
        var repository=new TextBlock{Foreground=Brush("#4E5C68"),Margin=new Thickness(0,8,0,0)};repository.Inlines.Add(new Run("GitHub: "));
        var github=new Hyperlink(new Run("logsim5295/SnapBerry")){NavigateUri=new Uri("https://github.com/logsim5295/SnapBerry"),Foreground=Brush("#923C83"),ToolTip="GitHub 저장소 열기"};RegisterName("GitHubLink",github);repository.Inlines.Add(github);panel.Children.Add(repository);
        var details=new Expander{Header="상세 정보",Style=(Style)FindResource("PurpleExpanderStyle"),Margin=new Thickness(0,18,0,0)};
        details.Content=new TextBlock{Text=$"Windows: {RuntimeInformation.OSDescription}\n.NET: {Environment.Version}\n아키텍처: {RuntimeInformation.ProcessArchitecture}",Foreground=Brush("#4E5C68"),TextWrapping=TextWrapping.Wrap,LineHeight=22};RegisterName("AboutDetails",details);panel.Children.Add(details);
        var legal=new Expander{Header="이용약관 및 데이터 처리 안내",Style=(Style)FindResource("PurpleExpanderStyle"),Margin=new Thickness(0,12,0,0)};
        var documents=new TabControl{Style=(Style)FindResource("PurpleTabControlStyle"),Height=250,Background=ThemeResources.Get(this,"Surface"),Foreground=Brush("#4E5C68")};
        foreach(var (label,resource) in new[]{("이용약관","Terms"),("데이터 처리","DataProcessing"),("외부 구성 요소","ThirdParty")})
        {
            using var stream=typeof(AboutWindow).Assembly.GetManifestResourceStream("SnapBerry.Legal."+resource)??throw new InvalidOperationException("배포 문서를 찾을 수 없습니다.");
            using var reader=new StreamReader(stream);
            var text=new TextBox{Text=reader.ReadToEnd(),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(10),Background=ThemeResources.Get(this,"Surface"),Foreground=Brush("#4E5C68"),BorderThickness=new Thickness(0)};
            System.Windows.Automation.AutomationProperties.SetName(text,label);
            documents.Items.Add(new TabItem{Header=label,Content=text});
        }
        legal.Content=documents;RegisterName("AboutLegalInformation",legal);panel.Children.Add(legal);
        var actions=new Grid{Margin=new Thickness(0,20,0,0)};actions.ColumnDefinitions.Add(new());actions.ColumnDefinitions.Add(new(){Width=new GridLength(8)});actions.ColumnDefinitions.Add(new());
        Button ActionButton(string label,string imageName)
        {
            var content=new StackPanel{Orientation=Orientation.Horizontal};content.Children.Add(new Image{Source=RibbonIcons.Create(imageName),Width=18,Height=18,Margin=new Thickness(0,0,8,0)});content.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});
            return new Button{Content=content,Height=34};
        }
        var copy=ActionButton("진단 정보 복사","Copy");RegisterName("CopyDiagnosticsButton",copy);actions.Children.Add(copy);
        var folder=ActionButton("저장 폴더 열기","Open");RegisterName("OpenStorageFolderButton",folder);Grid.SetColumn(folder,2);actions.Children.Add(folder);panel.Children.Add(actions);
        var status=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brush("#923C83"),Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed};RegisterName("AboutActionStatus",status);panel.Children.Add(status);
        void Report(Action action,string success)
        {
            try{action();status.Text=success;status.Foreground=Brush("#923C83");}
            catch(Exception error)when(error is ExternalException or IOException or UnauthorizedAccessException or InvalidOperationException){status.Text="작업 실패: "+error.Message;status.Foreground=Brush("#A83855");}
            status.Visibility=Visibility.Visible;
        }
        void Open(string target)
        {
            if(openLocation is not null){openLocation(target);return;}
            if(target==AppDataPaths.RootDirectory)Directory.CreateDirectory(target);
            Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
        }
        copy.Click+=(_,_)=>Report(()=>{if(copyText is not null)copyText(DiagnosticText);else Clipboard.SetText(DiagnosticText);},"진단 정보 복사됨");
        folder.Click+=(_,_)=>Report(()=>Open(AppDataPaths.RootDirectory),"저장 폴더 열림");
        email.RequestNavigate+=(_,e)=>{e.Handled=true;Report(()=>Open(e.Uri.AbsoluteUri),"메일 앱 열림");};
        github.RequestNavigate+=(_,e)=>{e.Handled=true;Report(()=>Open(e.Uri.AbsoluteUri),"GitHub 저장소 열림");};
        MaxHeight=Math.Max(360,SystemParameters.WorkArea.Height-48);
        Content=new Border{Background=ThemeResources.Get(this,"Surface"),BorderBrush=Brush("#EBD5E6"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Child=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};
    }

    private Brush Brush(string color)=>ThemeResources.FromLightColor(this,color);
}
