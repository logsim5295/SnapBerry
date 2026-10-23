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
    [Fact] public void CatalogHas80UniqueAssets()=>Sta.Run(()=>{var c=StickerCatalog.Load();Assert.Equal(80,c.Count);Assert.Equal(80,c.Select(x=>x.Id).Distinct().Count());var s=new StickerAssetStore();foreach(var item in c){var hash=s.AddPng(StickerCatalog.CreatePng(item));var b=s.GetBitmap(hash);Assert.True(b.PixelWidth>0);}});
    [Fact] public void StickerTransparencyCompositesCorrectly()=>Sta.Run(()=>{var s=new StickerAssetStore();var hash=s.AddPng(StickerCatalog.CreatePng(StickerCatalog.Load().First(x=>x.Id=="check-plain")));var r=new CompositeRenderer().Render(Image(100,100),[new(){Kind=AnnotationKind.Sticker,X=10,Y=10,Width=64,Height=64,AssetHash=hash}],s);Assert.Equal(100,r.PixelWidth);});
}
