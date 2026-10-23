using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnapBerry.App.Editor;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창을 띄우지 않고 색 변환·입력 검증·동기화와 전용 선택 창의 레이아웃을 검증한다.
public class ColorPickerTests
{
    [Theory]
    [InlineData("#FF0000",0)] [InlineData("#FFFF00",60)] [InlineData("#00FF00",120)]
    [InlineData("#00FFFF",180)] [InlineData("#0000FF",240)] [InlineData("#FF00FF",300)]
    public void PrimaryColorsHaveExpectedHueAndRoundTrip(string hex,double hue)
    {
        var color=(Color)ColorConverter.ConvertFromString(hex);var hsv=ColorSelection.ToHsv(color);
        Assert.Equal(hue,hsv.Hue,6);Assert.Equal(1,hsv.Saturation);Assert.Equal(1,hsv.Value);
        Assert.Equal(color,ColorSelection.FromHsv(hsv.Hue,hsv.Saturation,hsv.Value));
    }
    [Theory] [InlineData("#000000")] [InlineData("#FFFFFF")] [InlineData("#808080")] [InlineData("#923C83")] [InlineData("#139887")] [InlineData("#E83F56")]
    public void ArbitraryColorsRoundTripThroughHsv(string hex)
    {
        var color=(Color)ColorConverter.ConvertFromString(hex);var hsv=ColorSelection.ToHsv(color);
        Assert.Equal(color,ColorSelection.FromHsv(hsv.Hue,hsv.Saturation,hsv.Value));
    }
    [Fact] public void HueWrapsAndSaturationValueAreClamped()
    {
        Assert.Equal(Colors.Red,ColorSelection.FromHsv(360,1,1));Assert.Equal(Colors.Magenta,ColorSelection.FromHsv(-60,1,1));
        Assert.Equal(Colors.Red,ColorSelection.FromHsv(0,2,2));Assert.Equal(Colors.Black,ColorSelection.FromHsv(120,1,-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>ColorSelection.FromHsv(double.NaN,1,1));
    }

    [Fact] public void HexRgbHueAndPreviewStaySynchronizedWithoutShowingWindow()=>Sta.Run(()=>
    {
        var w=new ColorPickerWindow(Color.FromRgb(146,60,131));
        try
        {
            var hex=(TextBox)w.FindName("HexInput");var r=(TextBox)w.FindName("RedInput");var g=(TextBox)w.FindName("GreenInput");var b=(TextBox)w.FindName("BlueInput");
            var original=((Border)w.FindName("OriginalColorPreview")).Background.ToString();Assert.Equal("#923C83",w.SelectedHex);
            hex.Text="00ff00";Assert.Equal(Colors.Lime,w.SelectedColor);Assert.Equal("255",g.Text);Assert.Equal("0",r.Text);Assert.Equal(120,((Slider)w.FindName("HueSlider")).Value,6);
            r.Text="255";Assert.Equal("#FFFF00",w.SelectedHex);Assert.Equal("0",b.Text);
            ((Slider)w.FindName("HueSlider")).Value=240;Assert.Equal(Colors.Blue,w.SelectedColor);
            w.SetSaturationValue(0,1);Assert.Equal(Colors.White,w.SelectedColor);w.SetSaturationValue(1,0);Assert.Equal(Colors.Black,w.SelectedColor);
            Assert.Equal(original,((Border)w.FindName("OriginalColorPreview")).Background.ToString());Assert.Null(w.DialogResult);Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });

    [Theory] [InlineData("HexInput","oops")] [InlineData("HexInput","#12")] [InlineData("HexInput","#FF112233")]
    [InlineData("RedInput","256")] [InlineData("GreenInput","-1")] [InlineData("BlueInput","")]
    public void InvalidInputCannotApplyAndPaletteCanRecover(string name,string text)=>Sta.Run(()=>
    {
        var w=new ColorPickerWindow(Colors.Red);
        try
        {
            ((TextBox)w.FindName(name)).Text=text;Assert.False(((Button)w.FindName("ApplyColorButton")).IsEnabled);Assert.Equal(Colors.Red,w.SelectedColor);
            var swatch=((WrapPanel)w.FindName("ColorPalette")).Children.OfType<Button>().Last();swatch.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.True(((Button)w.FindName("ApplyColorButton")).IsEnabled);Assert.False(string.IsNullOrWhiteSpace(w.SelectedHex));Assert.Null(w.DialogResult);Assert.False(w.IsVisible);
        }
        finally{w.Close();}
    });

    [Fact] public void PickerUsesBrandChromeAndFitsOffscreenLayout()=>Sta.Run(()=>
    {
        var w=new ColorPickerWindow(Color.FromRgb(146,60,131));
        try
        {
            Assert.Equal(WindowStyle.None,w.WindowStyle);Assert.True(w.AllowsTransparency);
            var chrome=Assert.IsType<Border>(w.Content);Assert.Equal(8,chrome.CornerRadius.TopLeft);Assert.Equal("#FFEBD5E6",chrome.BorderBrush.ToString());
            var apply=(Button)w.FindName("ApplyColorButton");var cancel=(Button)w.FindName("CancelColorButton");Assert.True(cancel.IsCancel);Assert.Equal("#FF923C83",apply.Background.ToString());
            var buttons=(StackPanel)apply.Parent;Assert.Equal(cancel,buttons.Children[0]);Assert.Equal(apply,buttons.Children[1]);
            chrome.Measure(new Size(w.Width,650));chrome.Arrange(new Rect(new Size(w.Width,Math.Min(650,chrome.DesiredSize.Height))));chrome.UpdateLayout();
            Assert.False(w.IsVisible);Assert.True(((Grid)w.FindName("SaturationValueArea")).ActualWidth>0);
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null)
            {
                System.IO.Directory.CreateDirectory(artifacts);var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(chrome.ActualWidth),(int)Math.Ceiling(chrome.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(chrome);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(artifacts,"color-picker-offscreen.png"),SnapBerry.App.Export.PngFileWriter.Encode(bitmap));
            }
        }
        finally{w.Close();}
    });
}
