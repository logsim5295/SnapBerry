using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using SnapBerry.App;
using SnapBerry.App.Editor;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 외부 앱·클립보드·실제 창을 실행하지 않고 정보 창의 외형, 상세 정보와 명령 전달을 검증한다.
public class AboutDesignTests
{
    [Fact] public void AboutUsesBrandChromeAndShowsPublicRepositoryLink()=>Sta.Run(()=>
    {
        var w=new AboutWindow();
        try
        {
            Assert.Equal(WindowStyle.None,w.WindowStyle);Assert.True(w.AllowsTransparency);
            var chrome=Assert.IsType<Border>(w.Content);Assert.Equal(8,chrome.CornerRadius.TopLeft);Assert.Equal("#FFEBD5E6",chrome.BorderBrush.ToString());
            Assert.Equal("SnapBerry",((TextBlock)w.FindName("AboutProductName")).Text);Assert.NotNull(((Image)w.FindName("AboutAppIcon")).Source);
            var details=(Expander)w.FindName("AboutDetails");Assert.False(details.IsExpanded);
            Assert.DoesNotContain("github",w.DiagnosticText,StringComparison.OrdinalIgnoreCase);
            var repository=Assert.IsType<Hyperlink>(w.FindName("GitHubLink"));
            Assert.Equal("https://github.com/simlog1234/SnapBerry",repository.NavigateUri.AbsoluteUri);
            Assert.Equal("simlog1234/SnapBerry",Assert.IsType<Run>(repository.Inlines.Single()).Text);
            details.IsExpanded=true;Assert.True(details.IsExpanded);Assert.False(w.IsVisible);
            chrome.Measure(new Size(w.Width,double.PositiveInfinity));chrome.Arrange(new Rect(new Size(w.Width,chrome.DesiredSize.Height)));chrome.UpdateLayout();
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null)
            {
                System.IO.Directory.CreateDirectory(artifacts);var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(chrome.ActualWidth),(int)Math.Ceiling(chrome.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(chrome);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifacts,"about-expanded-offscreen.png"),SnapBerry.App.Export.PngFileWriter.Encode(image));
            }
            var content=(ContentPresenter)details.Template.FindName("ExpanderContent",details);Assert.Equal(Visibility.Visible,content.Visibility);
            var toggle=(System.Windows.Controls.Primitives.ToggleButton)details.Template.FindName("ExpanderHeaderToggle",details);
            toggle.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,false);Assert.False(details.IsExpanded);Assert.Equal(Visibility.Collapsed,content.Visibility);
        }
        finally{w.Close();}
    });

    [Fact] public void GitHubLinkOpensPublicRepositoryOnlyOnRequest()=>Sta.Run(()=>
    {
        string? opened=null;var w=new AboutWindow(openLocation:target=>opened=target);
        try
        {
            Assert.Null(opened);
            var repository=Assert.IsType<Hyperlink>(w.FindName("GitHubLink"));
            var navigation=new RequestNavigateEventArgs(repository.NavigateUri,null){RoutedEvent=Hyperlink.RequestNavigateEvent};
            repository.RaiseEvent(navigation);
            Assert.Equal("https://github.com/simlog1234/SnapBerry",opened);Assert.True(navigation.Handled);
            Assert.Equal("GitHub 저장소 열림",((TextBlock)w.FindName("AboutActionStatus")).Text);Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });

    [Fact] public void DiagnosticsAndFolderCommandsUseSafeExpectedTargets()=>Sta.Run(()=>
    {
        string? copied=null,opened=null;var w=new AboutWindow(copyText:text=>copied=text,openLocation:target=>opened=target);
        try
        {
            ((Button)w.FindName("CopyDiagnosticsButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(w.DiagnosticText,copied);Assert.Contains(w.DevelopmentVersion,copied);Assert.Contains("Windows:",copied);Assert.Contains(".NET:",copied);Assert.Contains("아키텍처:",copied);
            Assert.DoesNotContain(AppDataPaths.RootDirectory,copied,StringComparison.OrdinalIgnoreCase);
            ((Button)w.FindName("OpenStorageFolderButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Equal(AppDataPaths.RootDirectory,opened);
            var mail=(Hyperlink)w.FindName("AboutAuthorLink");mail.RaiseEvent(new RequestNavigateEventArgs(mail.NavigateUri,null){RoutedEvent=Hyperlink.RequestNavigateEvent});
            Assert.Equal("mailto:s921119@gmail.com",opened);Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });

    [Fact] public void ActionFailuresAreReportedWithoutOpeningAnyDialog()=>Sta.Run(()=>
    {
        var w=new AboutWindow(copyText:_=>throw new InvalidOperationException("test"),openLocation:_=>throw new System.ComponentModel.Win32Exception());
        try
        {
            ((Button)w.FindName("CopyDiagnosticsButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Contains("실패",((TextBlock)w.FindName("AboutActionStatus")).Text);
            ((Button)w.FindName("OpenStorageFolderButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Contains("실패",((TextBlock)w.FindName("AboutActionStatus")).Text);Assert.False(w.IsVisible);
            var repository=Assert.IsType<Hyperlink>(w.FindName("GitHubLink"));repository.RaiseEvent(new RequestNavigateEventArgs(repository.NavigateUri,null){RoutedEvent=Hyperlink.RequestNavigateEvent});Assert.Contains("실패",((TextBlock)w.FindName("AboutActionStatus")).Text);Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });
}
