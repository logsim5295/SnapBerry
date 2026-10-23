using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 대화상자는 표시하지 않고 입력 이벤트와 오프스크린 배치만 검사한다.
public class ImageResizeDialogTests
{
    [Fact] public void DimensionInputsKeepRatioAllowIndependentSizesAndRejectInvalidValues()=>Sta.Run(()=>
    {
        var dialog=new ImageResizeDialog(new(1920,1080));
        try
        {
            var width=(TextBox)dialog.FindName("ResizeWidthInput");var height=(TextBox)dialog.FindName("ResizeHeightInput");
            var ratio=(CheckBox)dialog.FindName("KeepAspectRatio");var apply=(Button)dialog.FindName("ApplyResizeButton");
            Assert.True(ratio.IsChecked);Assert.Equal("1920",width.Text);Assert.Equal("1080",height.Text);
            width.Text="960";Assert.Equal("540",height.Text);height.Text="270";Assert.Equal("480",width.Text);
            ratio.IsChecked=false;width.Text="1000";Assert.Equal("270",height.Text);Assert.True(apply.IsEnabled);
            foreach(var invalid in new[]{"0","-1","1.5","","999999999999999999999"}){width.Text=invalid;Assert.False(apply.IsEnabled);}
            width.Text="10000";height.Text="10000";Assert.False(apply.IsEnabled);
            width.Text="800";height.Text="600";Assert.True(apply.IsEnabled);
            ratio.IsChecked=true;width.Text="400";Assert.Equal("300",height.Text);
            // 비율 유지 계산이 제한을 넘으면 이전 세로값으로 잘못 적용하지 않는다.
            width.Text="32767";Assert.False(apply.IsEnabled);width.Text="800";Assert.True(apply.IsEnabled);
            Assert.Null(dialog.Result);
        }
        finally{dialog.Close();}
    });

    [Fact] public void DialogContentFitsWithoutShowingAWindow()=>Sta.Run(()=>
    {
        var dialog=new ImageResizeDialog(new(1920,1080));
        try
        {
            var content=(FrameworkElement)dialog.Content;content.Measure(new Size(410,double.PositiveInfinity));content.Arrange(new Rect(content.DesiredSize));content.UpdateLayout();
            Assert.InRange(content.ActualHeight,230,500);
            var folder=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(folder is not null)
            {
                Directory.CreateDirectory(folder);var image=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);
                image.Render(content);File.WriteAllBytes(Path.Combine(folder,"image-resize-dialog.png"),PngFileWriter.Encode(image));
            }
        }
        finally{dialog.Close();}
    });
}
