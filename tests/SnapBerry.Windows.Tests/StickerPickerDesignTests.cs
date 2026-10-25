using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using SnapBerry.App.Stickers;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창을 표시하지 않고 스티커 도구창의 외형·열 배치와 연속 선택 동작을 검증한다.
public class StickerPickerDesignTests
{
    [Theory] [InlineData(370)] [InlineData(270)]
    public void HeaderPaddingReceivesDragInputWithoutCoveringContent(double width)=>Sta.Run(()=>
    {
        var w=new StickerPicker();
        try
        {
            var chrome=Assert.IsType<Border>(w.Content);
            chrome.Measure(new Size(width,w.Height));chrome.Arrange(new Rect(0,0,width,w.Height));chrome.UpdateLayout();
            var dragArea=Assert.IsType<Border>(w.FindName("StickerHeaderDragArea"));
            // 미표시 Window의 IsVisible 필터를 거치지 않고 렌더링된 입력 영역을 검사한다.
            foreach(var x in new[]{10d,width/2,width-10})
                Assert.Same(dragArea,VisualTreeHelper.HitTest(chrome,new Point(x,10))?.VisualHit);
            var close=(Button)w.FindName("CloseStickerPickerButton");
            var closePoint=close.TranslatePoint(new Point(close.ActualWidth/2,close.ActualHeight/2),chrome);
            var hit=Assert.IsAssignableFrom<DependencyObject>(VisualTreeHelper.HitTest(chrome,closePoint)?.VisualHit);
            while(hit!=close&&hit is Visual)hit=VisualTreeHelper.GetParent(hit);
            Assert.Same(close,hit);
            var categories=(ComboBox)w.FindName("StickerCategories");
            Assert.True(categories.TranslatePoint(new Point(),chrome).Y>=dragArea.TranslatePoint(new Point(0,dragArea.ActualHeight),chrome).Y);
            Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });

    [Fact] public void RoundedPickerKeepsThreeColumnsAndResizableToolWindowBehavior()=>Sta.Run(()=>
    {
        var w=new StickerPicker();
        try
        {
            Assert.Equal(WindowStyle.None,w.WindowStyle);Assert.True(w.AllowsTransparency);Assert.False(w.ShowInTaskbar);Assert.False(w.ShowActivated);
            Assert.Equal(ResizeMode.CanResize,w.ResizeMode);Assert.True(WindowChrome.GetWindowChrome(w).ResizeBorderThickness.Left>0);
            var chrome=Assert.IsType<Border>(w.Content);Assert.Equal(8,chrome.CornerRadius.TopLeft);Assert.Equal("#FFEBD5E6",chrome.BorderBrush.ToString());
            Assert.NotNull(((Image)w.FindName("StickerHeaderIcon")).Source);Assert.Equal("닫기",((Button)w.FindName("CloseStickerPickerButton")).ToolTip);
            chrome.Measure(new Size(w.Width,w.Height));chrome.Arrange(new Rect(0,0,w.Width,w.Height));chrome.UpdateLayout();
            var scroll=(ScrollViewer)w.FindName("StickerScroll");var wrap=(WrapPanel)scroll.Content;var buttons=wrap.Children.OfType<Button>().ToArray();
            Assert.Equal(Visibility.Visible,scroll.ComputedVerticalScrollBarVisibility);
            var first=buttons[0].TranslatePoint(new Point(),wrap);for(var i=1;i<3;i++)Assert.Equal(first.Y,buttons[i].TranslatePoint(new Point(),wrap).Y,1);
            Assert.True(buttons[3].TranslatePoint(new Point(),wrap).Y>first.Y);Assert.False(w.IsVisible);
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null)
            {
                System.IO.Directory.CreateDirectory(artifacts);var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.Width,(int)w.Height,96,96,PixelFormats.Pbgra32);image.Render(chrome);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifacts,"sticker-picker-offscreen.png"),SnapBerry.App.Export.PngFileWriter.Encode(image));
            }
            chrome.Measure(new Size(w.MinWidth,w.Height));chrome.Arrange(new Rect(0,0,w.MinWidth,w.Height));chrome.UpdateLayout();
            Assert.Equal(buttons[0].TranslatePoint(new Point(),wrap).Y,buttons[1].TranslatePoint(new Point(),wrap).Y,1);
            Assert.True(buttons[2].TranslatePoint(new Point(),wrap).Y>buttons[0].TranslatePoint(new Point(),wrap).Y);
        }
        finally{w.Close();}
    });

    [Fact] public void CloseIconClosesOnlyThePickerWithoutShowingIt()=>Sta.Run(()=>
    {
        var w=new StickerPicker();var closed=false;w.Closed+=(_,_)=>closed=true;
        try{((Button)w.FindName("CloseStickerPickerButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Assert.True(closed);Assert.False(w.IsVisible);}
        finally{if(!closed)w.Close();}
    });

    [Theory] [InlineData("Emoji")] [InlineData("Keyboard")] [InlineData("Work")]
    public void StickerSelectionKeepsPickerOpenAndUpdatesPurpleHighlight(string category)=>Sta.Run(()=>
    {
        var w=new StickerPicker();
        try
        {
            var closed=false;w.Closed+=(_,_)=>closed=true;var selections=0;w.StickerSelected+=_=>selections++;
            ((ComboBox)w.FindName("StickerCategories")).SelectedValue=category;
            var buttons=((WrapPanel)((ScrollViewer)w.FindName("StickerScroll")).Content).Children.OfType<Button>().ToArray();
            buttons[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));var first=w.Selected;
            Assert.Equal("#FFEBD5E6",buttons[0].Background.ToString());
            buttons[1].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(2,selections);Assert.NotEqual(first!.Id,w.Selected!.Id);Assert.Equal(category,w.Selected.Category);
            Assert.Equal("#FFEBD5E6",buttons[1].Background.ToString());Assert.Equal("#FF923C83",buttons[1].BorderBrush.ToString());Assert.False(closed);Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });
}
