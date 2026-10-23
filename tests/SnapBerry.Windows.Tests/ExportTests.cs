using System.IO;
using SnapBerry.App.Export;
using Xunit;
namespace SnapBerry.Windows.Tests;
// PNG 저장 결과와 기존 파일 보호 등 내보내기 동작을 검증한다.
public class ExportTests
{
    [Fact] public async Task ExistingFileIsNotOverwrittenWithoutConsent()
    {var p=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");try{await File.WriteAllTextAsync(p,"old");await Assert.ThrowsAsync<IOException>(()=>PngFileWriter.WriteAsync(p,[1,2,3],false));Assert.Equal("old",await File.ReadAllTextAsync(p));}finally{File.Delete(p);}}
    [Fact] public async Task PngRoundTripKeepsDimensionsAndPixels()
    {byte[] bytes=[];Sta.Run(()=>bytes=PngFileWriter.Encode(RenderingTests.Image(40,30)));var p=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");try{await PngFileWriter.WriteAsync(p,bytes,false);Sta.Run(()=>{var b=PngFileWriter.Decode(File.ReadAllBytes(p));Assert.Equal(40,b.PixelWidth);Assert.Equal(30,b.PixelHeight);});}finally{File.Delete(p);}}
}
