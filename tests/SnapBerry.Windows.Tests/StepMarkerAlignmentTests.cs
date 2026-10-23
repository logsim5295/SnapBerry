using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Export;
using SnapBerry.App.Rendering;
using SnapBerry.App.Stickers;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 숫자·소문자·대문자의 실제 글리프가 번호 원의 중앙에 배치되는지 검증한다.
public class StepMarkerAlignmentTests
{
    [Theory]
    [InlineData("a")] [InlineData("b")] [InlineData("c")] [InlineData("g")]
    [InlineData("aa")] [InlineData("ab")] [InlineData("1")] [InlineData("10")] [InlineData("100")]
    [InlineData("A")] [InlineData("B")] [InlineData("C")] [InlineData("Z")] [InlineData("AA")]
    public void VisibleGlyphBoundsAreCenteredInTheMarker(string label)=>Sta.Run(()=>
    {
        foreach(var size in new[]{48,96})
        {
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen())CompositeRenderer.DrawAnnotation(dc,new Annotation{Kind=AnnotationKind.StepMarker,X=20,Y=20,Width=size,Height=size,Text=label,Style=new(){Fill="#923C83",TextColor="#FFFFFF"}},new StickerAssetStore());
            var image=new RenderTargetBitmap(size+40,size+40,96,96,PixelFormats.Pbgra32);image.Render(visual);
            var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);
            var points=new List<Point>();
            for(var y=0;y<image.PixelHeight;y++)for(var x=0;x<image.PixelWidth;x++)
            {
                var i=(y*image.PixelWidth+x)*4;
                if(pixels[i]>230&&pixels[i+1]>230&&pixels[i+2]>230&&pixels[i+3]>230)points.Add(new(x,y));
            }
            Assert.NotEmpty(points);
            var cx=(points.Min(p=>p.X)+points.Max(p=>p.X)+1)/2;var cy=(points.Min(p=>p.Y)+points.Max(p=>p.Y)+1)/2;
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
            if(artifacts is not null){Directory.CreateDirectory(artifacts);File.WriteAllBytes(Path.Combine(artifacts,$"step-{label}-{size}.png"),PngFileWriter.Encode(image));}
            Assert.True(Math.Abs(cx-(20+size/2.0))<=1,$"{label}, size={size}: horizontal center={cx}, expected={20+size/2.0}");
            Assert.True(Math.Abs(cy-(20+size/2.0))<=1,$"{label}, size={size}: vertical center={cy}, expected={20+size/2.0}");
        }
    });
}
