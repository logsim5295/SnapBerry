using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 리본 아이콘 생성과 버튼별 이미지 자원 연결을 검증한다.
public class RibbonIconTests
{
    [Fact] public void EveryRibbonIconRendersBrandColorsAtLargeAndSmallSizes()=>Sta.Run(()=>
    {
        string[] names=["ImageFile","Paste","Region","Window","Desktop","Select","Arrow","Text","Rectangle","Mosaic","Number","Sticker","Crop","MagicWand","Undo","Redo","Delete","Edit","Copy","Save","ZoomOut","ZoomIn","Fit","History","Properties","Settings","About"];
        foreach(var name in names)foreach(var size in new[]{16,32})
        {
            var icon=RibbonIcons.Create(name);Assert.True(icon.IsFrozen);var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(icon,new Rect(0,0,size,size));
            var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[size*size*4];bitmap.CopyPixels(pixels,size*4,0);var visible=0;var accent=0;
            for(var i=0;i<pixels.Length;i+=4){if(pixels[i+3]>128)visible++;if(pixels[i+3]>240&&((pixels[i+2]>110&&pixels[i+1]<120&&pixels[i]>90)||(pixels[i+2]>210&&pixels[i+1]>150&&pixels[i]<100)))accent++;}
            Assert.True(visible>20,name);Assert.True(accent>2,name);
        }
    });
}
