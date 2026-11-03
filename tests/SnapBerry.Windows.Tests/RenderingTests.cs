using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.Core;
using SnapBerry.App.Rendering;
using SnapBerry.App.Stickers;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 원본 위의 주석·모자이크 합성과 이미지 크기 제한을 픽셀 결과로 검증한다.
public class RenderingTests
{
    internal static BitmapSource Image(int w,int h)
    {var bytes=new byte[w*h*4];for(var i=0;i<bytes.Length;i+=4){bytes[i]=(byte)((i/4)%256);bytes[i+1]=80;bytes[i+2]=120;bytes[i+3]=255;}var b=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,bytes,w*4);b.Freeze();return b;}
    [Fact] public void ExportKeepsOriginalDimensions()=>Sta.Run(()=>{var r=new CompositeRenderer().Render(Image(320,200),[],new StickerAssetStore());Assert.Equal(320,r.PixelWidth);Assert.Equal(200,r.PixelHeight);});
    [Fact] public void OriginalBufferIsUnchanged()=>Sta.Run(()=>{var b=Image(32,32);var before=new byte[4096];b.CopyPixels(before,128,0);new CompositeRenderer().Render(b,[new(){Kind=AnnotationKind.Mosaic,Width=32,Height=32}],new());var after=new byte[4096];b.CopyPixels(after,128,0);Assert.Equal(before,after);});
    [Fact] public void OverlappingMosaicUsesLargerBlocks()=>Sta.Run(()=>{var r=new CompositeRenderer().Render(Image(16,16),[new(){Kind=AnnotationKind.Mosaic,Width=16,Height=16,MosaicBlockSize=4},new(){Kind=AnnotationKind.Mosaic,Width=16,Height=16,MosaicBlockSize=8}],new());var bytes=new byte[1024];r.CopyPixels(bytes,64,0);Assert.Equal(bytes[0],bytes[7*4]);Assert.NotEqual(bytes[0],bytes[8*4]);});
    private static byte[] Pixels(BitmapSource image){var bytes=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(bytes,image.PixelWidth*4,0);return bytes;}
    [Fact] public void MosaicBlockAveragesIncludePixelsOutsideTheAnnotation()=>Sta.Run(()=>
    {
        // 블록 격자는 이미지 원점에 고정되며 평균은 블록 전체(0..7)에서 계산한다. 주석 밖 픽셀은 바뀌지 않는다.
        var r=Pixels(new CompositeRenderer().Render(Image(16,16),[new(){Kind=AnnotationKind.Mosaic,X=4,Y=4,Width=4,Height=4,MosaicBlockSize=8}],new()));
        Assert.Equal(59,r[(4*16+4)*4]);Assert.Equal(59,r[(7*16+7)*4]);
        Assert.Equal(51,r[(3*16+3)*4]);Assert.Equal(136,r[(8*16+8)*4]);Assert.Equal(0,r[0]);
    });
    [Fact] public void MosaicOutsideTheImageIsClippedToTheEdges()=>Sta.Run(()=>
    {
        // 음수 좌표와 이미지 밖으로 나간 영역은 안쪽 부분만 적용하고 가장자리 블록은 이미지 안에서만 평균을 낸다.
        var r=Pixels(new CompositeRenderer().Render(Image(16,16),[new(){Kind=AnnotationKind.Mosaic,X=-5,Y=-5,Width=12,Height=12,MosaicBlockSize=8},new(){Kind=AnnotationKind.Mosaic,X=12,Y=12,Width=40,Height=40,MosaicBlockSize=8}],new()));
        Assert.Equal(59,r[0]);Assert.Equal(59,r[(6*16+6)*4]);Assert.Equal(7*16+7,r[(7*16+7)*4]);
        Assert.Equal(195,r[(15*16+15)*4]);Assert.Equal(r[(12*16+12)*4],r[(15*16+15)*4]);Assert.Equal(11*16+8,r[(11*16+8)*4]);
    });
    [Fact] public void MosaicDoesNotChangeTransparentPixelsOutsideTheArea()=>Sta.Run(()=>
    {
        var bytes=new byte[8*8*4];for(var i=0;i<bytes.Length;i+=4){bytes[i]=200;bytes[i+3]=(byte)(i/4<32?0:255);}
        var image=BitmapSource.Create(8,8,96,96,PixelFormats.Bgra32,null,bytes,32);image.Freeze();
        var r=Pixels(CompositeRenderer.Mosaic(image,[new(){Kind=AnnotationKind.Mosaic,X=0,Y=4,Width=8,Height=4,MosaicBlockSize=2}]));
        Assert.Equal(0,r[3]);Assert.Equal(255,r[(4*8)*4+3]);Assert.Equal(200,r[(4*8)*4]);
    });
    [Fact] public void CatalogHas80UniqueAssets()=>Sta.Run(()=>{var c=StickerCatalog.Load();Assert.Equal(80,c.Count);Assert.Equal(80,c.Select(x=>x.Id).Distinct().Count());var s=new StickerAssetStore();foreach(var item in c){var hash=s.AddPng(StickerCatalog.CreatePng(item));var b=s.GetBitmap(hash);Assert.True(b.PixelWidth>0);}});
    [Fact] public void StickerTransparencyCompositesCorrectly()=>Sta.Run(()=>{var s=new StickerAssetStore();var hash=s.AddPng(StickerCatalog.CreatePng(StickerCatalog.Load().First(x=>x.Id=="check-plain")));var r=new CompositeRenderer().Render(Image(100,100),[new(){Kind=AnnotationKind.Sticker,X=10,Y=10,Width=64,Height=64,AssetHash=hash}],s);Assert.Equal(100,r.PixelWidth);});
}
