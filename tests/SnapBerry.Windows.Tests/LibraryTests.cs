using System.IO;
using SnapBerry.Core;
using SnapBerry.App.Library;
using SnapBerry.App.Stickers;
using SnapBerry.App.Export;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 캡처 원본·주석·번호·스티커의 저장 왕복과 실패 시 기존 파일 보존을 검증한다.
public class LibraryTests
{
    private static string Temp()=>Path.Combine(Path.GetTempPath(),"SnapBerry-tests",Guid.NewGuid().ToString("N"));
    private static CaptureRecord Record()=>new(){Width=80,Height=60,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(80,60)),Annotations=[new(){Kind=AnnotationKind.Text,Text="한글"}],Sequence=new(){NextNumeric=3,NextAlphabetic=4}};
    [Theory]
    [InlineData(128L*1024*1024,16L*1024*1024,8,true)]
    [InlineData(128L*1024*1024,16L*1024*1024,9,false)]
    [InlineData(128L*1024*1024+1,1,1,false)]
    [InlineData(1,16L*1024*1024+1,1,false)]
    public void ImageByteBudgetMatchesTheReopenLimits(long original,long asset,int count,bool accepted)
    {
        Action validate=()=>CaptureLibraryStore.ValidateImageAssetSizes(original,Enumerable.Repeat(asset,count));
        if(accepted)validate();else Assert.Throws<InvalidDataException>(validate);
    }
    [Fact] public void OversizedImageAssetCannotReplaceAReadableCapture()=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var store=new CaptureLibraryStore(path);var original=Record();store.SaveAsync(original).GetAwaiter().GetResult();
            var png=PngFileWriter.Encode(RenderingTests.Image(8,6));Array.Resize(ref png,16*1024*1024+1);
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png)).ToLowerInvariant();
            var replacement=original with{Annotations=[new(){Kind=AnnotationKind.Sticker,AssetHash=hash,StickerId="pasted-image"}],StickerAssets=new(){{hash,png}}};
            Assert.Throws<InvalidDataException>(()=>store.SaveAsync(replacement).GetAwaiter().GetResult());
            Assert.Equal("한글",Assert.Single(store.LoadAsync(original.Id).GetAwaiter().GetResult().Annotations).Text);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void CaptureWithoutExportSurvivesRestart()=>Sta.Run(()=>
    {var path=Temp();try{var record=Record();new CaptureLibraryStore(path).SaveAsync(record).GetAwaiter().GetResult();var reopened=new CaptureLibraryStore(path).LoadAsync(record.Id).GetAwaiter().GetResult();Assert.Equal(record.OriginalPng,reopened.OriginalPng);Assert.Equal("한글",reopened.Annotations.Single().Text);Assert.Equal(3,reopened.Sequence.NextNumeric);Assert.Equal(4,reopened.Sequence.NextAlphabetic);}finally{if(Directory.Exists(path))Directory.Delete(path,true);}});
    [Fact] public void StickerAssetsSurviveCatalogRemoval()=>Sta.Run(()=>
    {var path=Temp();try{var assets=new StickerAssetStore();var hash=assets.AddPng(StickerCatalog.CreatePng(StickerCatalog.Load()[0]));var record=Record() with {Annotations=[new(){Kind=AnnotationKind.Sticker,AssetHash=hash,StickerId="old-catalog"}],StickerAssets=new Dictionary<string,byte[]>{{hash,assets.Data[hash]}}};var store=new CaptureLibraryStore(path);store.SaveAsync(record).GetAwaiter().GetResult();Assert.Equal(assets.Data[hash],store.LoadAsync(record.Id).GetAwaiter().GetResult().StickerAssets[hash]);}finally{if(Directory.Exists(path))Directory.Delete(path,true);}});
    [Fact] public void MissingStickerAssetDoesNotOverwriteRecord()=>Sta.Run(()=>
    {var path=Temp();try{var store=new CaptureLibraryStore(path);var r=Record();store.SaveAsync(r).GetAwaiter().GetResult();Assert.ThrowsAny<Exception>(()=>store.SaveAsync(r with {Annotations=[new(){Kind=AnnotationKind.Sticker,AssetHash=new string('a',64)}]}).GetAwaiter().GetResult());Assert.Equal("한글",store.LoadAsync(r.Id).GetAwaiter().GetResult().Annotations[0].Text);}finally{if(Directory.Exists(path))Directory.Delete(path,true);}});
    [Fact] public void DeleteDoesNotRemoveExternalPng()=>Sta.Run(()=>
    {var path=Temp();try{var store=new CaptureLibraryStore(path);var r=Record();store.SaveAsync(r).GetAwaiter().GetResult();var export=Path.Combine(path,"export.png");File.WriteAllBytes(export,r.OriginalPng);store.DeleteAsync(r.Id).GetAwaiter().GetResult();Assert.Empty(store.ListAsync(0,50).GetAwaiter().GetResult());Assert.True(File.Exists(export));}finally{if(Directory.Exists(path))Directory.Delete(path,true);}});
}
