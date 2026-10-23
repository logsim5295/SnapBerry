using System.Windows;
namespace SnapBerry.App.Shell;
// 두 번째 실행 요청이나 트레이 열기를 기존 편집기 창 활성화로 전달한다.
public static class InstanceActivation
{
    public const int Message=0x8051;
    public static bool Handle(Window window,int message)
    {
        if(message!=Message)return false;
        // Hide 이후에는 네이티브 창뿐 아니라 WPF의 표시 상태도 함께 복원해야 한다.
        window.Show();if(window.WindowState==WindowState.Minimized)window.WindowState=WindowState.Normal;window.Activate();return true;
    }
}
