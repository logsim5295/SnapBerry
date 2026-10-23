using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Media;
namespace SnapBerry.App.Editor;
// 일반 창 제목 표시줄과 중복되는 리본 제목 영역을 제거하여 상단 빈 공간을 없앤다.
public sealed class CompactRibbon : Ribbon
{
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // 실제 창이 제목 표시줄을 소유하므로 리본의 내부 제목 행은 높이까지 제거한다.
        if(GetTemplateChild("PART_TitlePanel") is FrameworkElement title&&VisualTreeHelper.GetParent(title) is FrameworkElement host)
        {
            host.Visibility=Visibility.Collapsed;
            if(VisualTreeHelper.GetParent(host) is Grid grid&&grid.RowDefinitions.Count>Grid.GetRow(host))grid.RowDefinitions[Grid.GetRow(host)].Height=new GridLength(0);
        }
        if(GetTemplateChild("titleBarBackground") is FrameworkElement background)background.Visibility=Visibility.Collapsed;
    }
}
