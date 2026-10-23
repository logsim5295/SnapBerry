using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace SnapBerry.Desktop.Views;

// 외부 앱과 클립보드는 사용자 명령 때 주입된 동작으로만 접근한다.
public sealed class AboutWindow : Window
{
    private readonly TextBlock status=new(){Name="AboutActionStatus",TextWrapping=TextWrapping.Wrap,MinHeight=20};
    public string DevelopmentVersion {get;}=(typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??typeof(AboutWindow).Assembly.GetName().Version?.ToString()??"1.1.0").Split('+')[0];
    public string AuthorEmail=>"logsim1234@gmail.com";
    // 사용자 이름·파일 경로·캡처 데이터는 포함하지 않는다.
    public string Diagnostics=>$"SnapBerry {DevelopmentVersion}\n빌드: {typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}\nOS: {RuntimeInformation.OSDescription}\n.NET: {Environment.Version}\n아키텍처: {RuntimeInformation.ProcessArchitecture}";

    public AboutWindow(string dataDirectory,Action<string> open,Action<string> copy)
        :this(dataDirectory,open,text=>{copy(text);return Task.CompletedTask;}){}

    public AboutWindow(string dataDirectory,Action<string> open,Func<string,Task> copy)
    {
        ArgumentNullException.ThrowIfNull(open);ArgumentNullException.ThrowIfNull(copy);
        var names=new NameScope();NameScope.SetNameScope(this,names);
        void Register(string name,Control control){control.Name=name;names.Register(name,control);}
        Title="SnapBerry 정보";Width=540;Height=650;MinWidth=380;MinHeight=340;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new Thickness(22),Spacing=12};Content=new ScrollViewer{Content=panel,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        var product=new TextBlock{Text="SnapBerry",FontSize=26,FontWeight=FontWeight.SemiBold};Register("AboutProductName",product);panel.Children.Add(product);
        var version=new TextBlock{Text=$"개발 버전: {DevelopmentVersion}"};Register("AboutVersion",version);panel.Children.Add(version);
        panel.Children.Add(new TextBlock{Text="만든이: ynsim"});
        var author=new Button{Content=AuthorEmail};Register("AboutAuthorLink",author);panel.Children.Add(author);
        var github=new Button{Content="GitHub · logsim5295/SnapBerry"};Register("GitHubLink",github);panel.Children.Add(github);
        var details=new Expander{Header="상세 정보",HorizontalAlignment=HorizontalAlignment.Stretch,Content=new TextBlock{Text=Diagnostics,TextWrapping=TextWrapping.Wrap}};Register("AboutDetails",details);panel.Children.Add(details);
        var documents=new TabControl{Height=280};Register("AboutLegalDocuments",documents);
        foreach(var (label,resource) in new[]{("이용약관","Terms"),("데이터 처리","DataProcessing"),("외부 구성 요소","ThirdParty")})
        {
            using var stream=typeof(AboutWindow).Assembly.GetManifestResourceStream("SnapBerry.Legal."+resource)??throw new InvalidOperationException("배포 문서를 찾을 수 없습니다: "+resource);
            using var reader=new StreamReader(stream);
            var text=new TextBox{Text=reader.ReadToEnd(),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap};
            ScrollViewer.SetVerticalScrollBarVisibility(text,ScrollBarVisibility.Auto);ScrollViewer.SetHorizontalScrollBarVisibility(text,ScrollBarVisibility.Disabled);
            documents.Items.Add(new TabItem{Header=label,Content=text});
        }
        var legal=new Expander{Header="이용약관 및 데이터 처리 안내",Content=documents,HorizontalAlignment=HorizontalAlignment.Stretch};Register("AboutLegalInformation",legal);panel.Children.Add(legal);
        var actions=new WrapPanel{Orientation=Orientation.Horizontal};
        var copyButton=new Button{Content="진단 정보 복사",Margin=new Thickness(0,0,8,6)};Register("CopyDiagnosticsButton",copyButton);actions.Children.Add(copyButton);
        var folder=new Button{Content="저장 폴더 열기"};Register("OpenStorageFolderButton",folder);actions.Children.Add(folder);panel.Children.Add(actions);Register(status.Name!,status);panel.Children.Add(status);
        async Task ReportAsync(Button button,Func<Task> action,string success)
        {
            button.IsEnabled=false;
            try{await action();status.ClearValue(TextBlock.ForegroundProperty);status.Text=success;}
            catch(Exception exception){status.Foreground=Brushes.IndianRed;status.Text="작업 실패: "+exception.Message;}
            finally{button.IsEnabled=true;}
        }
        Task OpenAsync(string target){open(target);return Task.CompletedTask;}
        copyButton.Click+=async (_,_)=>await ReportAsync(copyButton,()=>copy(Diagnostics),"진단 정보 복사됨");
        folder.Click+=async (_,_)=>await ReportAsync(folder,()=>OpenAsync(dataDirectory),"저장 폴더 열림");
        github.Click+=async (_,_)=>await ReportAsync(github,()=>OpenAsync("https://github.com/logsim5295/SnapBerry"),"GitHub 저장소 열림");
        author.Click+=async (_,_)=>await ReportAsync(author,()=>OpenAsync("mailto:"+AuthorEmail),"메일 앱 열림");
        var close=new Button{Content="닫기",IsCancel=true,IsDefault=true,HorizontalAlignment=HorizontalAlignment.Right};Register("CloseAboutButton",close);close.Click+=(_,_)=>Close();panel.Children.Add(close);
    }
}
