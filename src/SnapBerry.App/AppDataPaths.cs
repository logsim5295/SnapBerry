namespace SnapBerry.App;

// 배포 EXE 위치와 분리된 사용자별 저장소다. 이력·설정·로그는 LocalAppData 아래에 보관한다.
internal static class AppDataPaths
{
    internal static string RootDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SnapBerry");
    internal static string LibraryDirectory=>Path.Combine(RootDirectory,"Library");
    internal static string LogsDirectory=>Path.Combine(RootDirectory,"Logs");
}
