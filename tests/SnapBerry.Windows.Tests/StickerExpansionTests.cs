using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Stickers;
using SnapBerry.App.Export;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 추가 스티커의 ID 안정성과 자원 생성·편집·저장을 검증한다.
public class StickerExpansionTests
{
    [Fact]
    public void WorkCategoryOffersEightSelectableStickers()=>Sta.Run(()=>
    {
        var picker=new StickerPicker();
        try
        {
            var dock=(DockPanel)picker.FindName("StickerContent");
            var categories=(ComboBox)dock.Children[0];
            categories.SelectedValue="Work";
            var wrap=(WrapPanel)((ScrollViewer)dock.Children[1]).Content;
            Assert.Equal(8,wrap.Children.Count);
            var selected=0;picker.StickerSelected+=_=>selected++;
            foreach(var button in wrap.Children.OfType<Button>())button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(8,selected);
            Assert.Equal("work-pending",picker.Selected!.Id);
        }
        finally{picker.Close();}
    });

    [Fact]
    public void ExpandedCatalogPreservesLegacyIdsAndAdds32Stickers()
    {
        var catalog=StickerCatalog.Load();
        Assert.Equal(80,catalog.Count);
        Assert.Equal(80,catalog.Select(x=>x.Id).Distinct().Count());
        Assert.Equal("Ctrl",catalog.Single(x=>x.Id=="key-12").Label);
        Assert.Equal("→",catalog.Single(x=>x.Id=="key-25").Label);
        Assert.Equal("↑",catalog.Single(x=>x.Id=="direction-40").Label);
        Assert.Equal("↘",catalog.Single(x=>x.Id=="direction-47").Label);
        Assert.Equal(24,catalog.Count(x=>x.Category=="Emoji"));
        Assert.Equal(26,catalog.Count(x=>x.Category=="Keyboard"));
        Assert.Equal(8,catalog.Count(x=>x.Category=="Work"));
    }

    [Fact]
    public void AddedStickersRenderVisiblePngsAndKeycapsRemainEditable()=>Sta.Run(()=>
    {
        var stickers=StickerCatalog.Load().Skip(48).ToArray();
        Assert.Equal(32,stickers.Length);
        var hashes=new HashSet<string>();
        foreach(var sticker in stickers)
        {
            var png=StickerCatalog.CreatePng(sticker);
            Assert.True(hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png))),sticker.Id);
            var bitmap=PngFileWriter.Decode(png);
            var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];
            bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
            Assert.Contains(Enumerable.Range(0,pixels.Length/4),i=>pixels[i*4+3]>0);
            if(sticker.IsKeycap)
            {
                Assert.StartsWith("key-",sticker.Id);
                Assert.NotEqual(png,StickerCatalog.CreatePng(sticker,"Custom key"));
            }
        }
        var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");
        if(artifacts is not null)
        {
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1024,640));
                for(var i=0;i<stickers.Length;i++)
                {
                    var x=i%8*128;var y=i/8*160;
                    dc.DrawImage(PngFileWriter.Decode(StickerCatalog.CreatePng(stickers[i])),new Rect(x+8,y+8,112,112));
                    StickerCatalog.DrawText(dc,stickers[i].Name,Brushes.Black,14,new Rect(x+4,y+122,120,30));
                }
            }
            var sheet=new RenderTargetBitmap(1024,640,96,96,PixelFormats.Pbgra32);sheet.Render(visual);
            Directory.CreateDirectory(artifacts);File.WriteAllBytes(Path.Combine(artifacts,"sticker-expansion.png"),PngFileWriter.Encode(sheet));
        }
    });
}
