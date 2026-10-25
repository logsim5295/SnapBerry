using System.Text.Json.Serialization;

namespace SnapBerry.App.Settings;

// 모니터 작업 영역 기준의 상대 위치와 크기를 DIP로 보관한다.
public sealed record StickerWindowState(string MonitorName,double OffsetX,double OffsetY,double Width,double Height)
{
    [JsonIgnore] public bool IsValid=>!string.IsNullOrWhiteSpace(MonitorName)
        &&double.IsFinite(OffsetX)&&double.IsFinite(OffsetY)
        &&double.IsFinite(Width)&&Width>0&&double.IsFinite(Height)&&Height>0;
}
