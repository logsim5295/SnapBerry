using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using Forms=System.Windows.Forms;

namespace SnapBerry.App.Shell;

// WPF 리본 그림을 WinForms 트레이 메뉴용 비트맵으로 변환하고 메뉴 수명에 맞춰 자원을 해제한다.
internal static class TrayMenuIcons
{
    internal static void Apply(Forms.ContextMenuStrip menu)
    {
        // 글꼴과 같은 화면 배율을 적용하고 표시할 픽셀 크기로 직접 렌더링한다.
        menu.ImageScalingSize=menu.LogicalToDeviceUnits(new System.Drawing.Size(16,16));
        var size=menu.ImageScalingSize.Width;
        var images=new List<System.Drawing.Bitmap>();menu.Disposed+=(_,_)=>{foreach(var image in images)image.Dispose();};
        foreach(var item in menu.Items.OfType<Forms.ToolStripMenuItem>())
        {
            var name=item.Text switch{"편집기 열기"=>"Open","영역 캡처"=>"Region","창 캡처"=>"Window","전체 화면 캡처"=>"Desktop","설정"=>"Settings","종료"=>"Exit",_=>throw new ArgumentOutOfRangeException(nameof(item.Text))};
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(RibbonIcons.Create(name),new Rect(0,0,size,size));
            var source=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);source.Render(visual);
            using var stream=new MemoryStream(PngFileWriter.Encode(source));using var decoded=new System.Drawing.Bitmap(stream);
            // 스트림이 닫힌 후에도 메뉴에서 사용할 수 있도록 독립된 이미지로 복사한다.
            var bitmap=new System.Drawing.Bitmap(decoded);images.Add(bitmap);item.Image=bitmap;
        }
    }
}
