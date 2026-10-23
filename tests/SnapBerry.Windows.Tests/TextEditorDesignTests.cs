using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using SnapBerry.App.Editor;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 메모리에서만 텍스트·표시값 편집 창을 구성해 브랜드 외형과 기존 값·버튼·크기 조절 설정을 검증한다.
public class TextEditorDesignTests
{
    [Theory] [InlineData(true)] [InlineData(false)]
    public void RoundedEditorKeepsValueInputModeAndButtonOrder(bool multiline)=>Sta.Run(()=>
    {
        var value=multiline?"기존 텍스트\n두 번째 줄":"AA";var w=new TextAnnotationEditor(value,multiline);
        try
        {
            Assert.Equal(WindowStyle.None,w.WindowStyle);Assert.True(w.AllowsTransparency);Assert.Equal(ResizeMode.CanResize,w.ResizeMode);
            var chrome=Assert.IsType<Border>(w.Content);Assert.Equal(8,chrome.CornerRadius.TopLeft);
            Assert.Equal("#FFFFFFFF",chrome.Background.ToString());Assert.Equal("#FFEBD5E6",chrome.BorderBrush.ToString());
            Assert.True(WindowChrome.GetWindowChrome(w).ResizeBorderThickness.Left>0);
            var header=Assert.IsType<StackPanel>(w.FindName("TextEditorHeader"));Assert.NotNull(header.Background);
            Assert.NotNull(header.Children.OfType<Image>().Single().Source);Assert.Equal(w.Title,header.Children.OfType<TextBlock>().Single().Text);
            var input=Assert.IsType<TextBox>(w.FindName("TextEditorInput"));Assert.Equal(value,w.Value);Assert.Equal(multiline,input.AcceptsReturn);
            var buttons=Assert.IsType<StackPanel>(w.FindName("TextEditorButtons"));
            var apply=Assert.IsType<Button>(buttons.Children[0]);var cancel=Assert.IsType<Button>(buttons.Children[1]);
            Assert.Equal("적용",apply.Content);Assert.Equal("취소",cancel.Content);Assert.True(cancel.IsCancel);
            Assert.Equal("#FF923C83",apply.Background.ToString());input.Text=" ";apply.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.Null(w.DialogResult);
            input.Text=value;chrome.Measure(new Size(w.Width,w.Height));chrome.Arrange(new Rect(0,0,w.Width,w.Height));chrome.UpdateLayout();
            Assert.True(input.ActualHeight>0);Assert.False(w.IsVisible);
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null)
            {
                System.IO.Directory.CreateDirectory(artifacts);var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.Width,(int)w.Height,96,96,PixelFormats.Pbgra32);image.Render(chrome);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifacts,$"text-editor-{multiline}-offscreen.png"),SnapBerry.App.Export.PngFileWriter.Encode(image));
                Assert.False(w.IsVisible);
            }
        }
        finally{w.Close();}
    });
}
