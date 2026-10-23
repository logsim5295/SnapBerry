using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SnapBerry.App.Editor;

// WPF가 생성한 방향별 메뉴의 명령·현지화·우클릭 위치는 유지하고 외형만 바꾼다.
public static class ScrollBarMenuTheme
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(ScrollBarMenuTheme),new PropertyMetadata(false,Changed));
    public static bool GetEnabled(DependencyObject target)=>(bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target,bool value)=>target.SetValue(EnabledProperty,value);

    private static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs args)
    {
        if(target is not ScrollBar bar)return;
        bar.ContextMenuOpening-=Opening;
        if((bool)args.NewValue)bar.ContextMenuOpening+=Opening;
    }

    private static void Opening(object sender,ContextMenuEventArgs args)
    {
        if(args.Handled||sender is not ScrollBar bar||bar.ContextMenu is not {} menu)return;
        // 명시적으로 지정하거나 비활성화한 메뉴는 기본 메뉴를 꾸미는 경로에서 제외한다.
        if(DependencyPropertyHelper.GetValueSource(bar,FrameworkElement.ContextMenuProperty).BaseValueSource!=BaseValueSource.Default)return;
        menu.Style=(Style)bar.FindResource("PurpleHistoryContextMenuStyle");
        menu.ItemContainerStyle=null;
        // 팝업이 열리기 전에도 소유 창의 팔레트를 찾도록 공유 브러시를 연결한다.
        // 브러시 내부 바인딩을 유지하므로 메뉴가 열린 상태의 테마 전환도 반영된다.
        foreach(var key in new[]{"Surface","Text","SecondaryText","Selected","Accent","AccentText","Border"})
            menu.Resources["Theme."+key]=ThemeResources.Get(bar,key);
        foreach(var item in menu.Items)
        {
            if(item is MenuItem entry)entry.Style=(Style)bar.FindResource("PurpleHistoryMenuItemStyle");
            else if(item is Separator separator)separator.Style=(Style)bar.FindResource("PurpleMenuSeparatorStyle");
        }
    }
}
