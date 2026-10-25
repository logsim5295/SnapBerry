using SnapBerry.App.Export;

namespace SnapBerry.App.Settings;
public enum AppTheme { Light,Dark }
// 사용자별 JSON 설정이다. 기본값은 신규 설치와 손상된 설정 복구에 공통으로 사용한다.
public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public AppTheme Theme { get; init; } = AppTheme.Light;
    public bool StartWithWindows { get; init; }
    // 첫 닫기 안내를 확인한 사용자에게는 다음 실행부터 안내를 반복하지 않는다.
    public bool HasSeenTrayNotice { get; init; }
    public double HistoryPanelWidth { get; init; } = 230;
    public StickerWindowState? StickerWindow { get; init; }
    public string RegionKey { get; init; } = "Ctrl+Shift+1";
    public string WindowKey { get; init; } = "Ctrl+Shift+2";
    public string DesktopKey { get; init; } = "Ctrl+Shift+3";
    public string LastSaveDirectory { get; init; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public ExportFormat LastSaveFormat { get; init; } = ExportFormat.Png;
}
