using System.IO;
using System.Windows.Media.Imaging;
namespace SnapBerry.App.Export;
// 내부 PNG 인코딩·이미지 디코딩과 모든 형식의 원자적 파일 출력을 공통 처리한다.
public static class PngFileWriter
{
    public static Task WriteAsync(string path,byte[] bytes,bool overwrite)=>Task.Run(()=>
    {
        // 같은 폴더의 임시 파일에 완성한 뒤 교체하여 쓰기 중 실패가 기존 파일을 훼손하지 않게 한다.
        var folder=Path.GetDirectoryName(Path.GetFullPath(path))!;var temp=Path.Combine(folder,$".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try{File.WriteAllBytes(temp,bytes);File.Move(temp,path,overwrite);}finally{if(File.Exists(temp))File.Delete(temp);}
    });
    public static byte[] Encode(BitmapSource bitmap)
    {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray();}
    // 스트림을 닫아도 사용할 수 있게 즉시 로드하고, 스레드 간 사용을 위해 이미지를 동결한다.
    public static BitmapSource Decode(byte[] bytes)
    {using var stream=new MemoryStream(bytes);var image=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];image.Freeze();return image;}
}
