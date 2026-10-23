using System.IO;
namespace SnapBerry.Windows.Tests;
// 테스트 임시 파일 정리를 공통화하여 백그라운드 저장과 파일 해제 지연에 대응한다.
internal static class TestFiles
{
    public static void DeleteDirectory(string path)
    {
        for(var attempt=0;attempt<20;attempt++)
        {try{if(Directory.Exists(path))Directory.Delete(path,true);return;}catch(IOException)when(attempt<19){Thread.Sleep(25);}}
    }
}
