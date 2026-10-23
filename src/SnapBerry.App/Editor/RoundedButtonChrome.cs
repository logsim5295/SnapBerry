using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace SnapBerry.App.Editor;
// 기존 컨트롤 템플릿의 동작을 유지한 채 테두리 모서리만 둥글게 만드는 부착 속성이다.
public static class RoundedButtonChrome
{
    public static readonly DependencyProperty RadiusProperty=DependencyProperty.RegisterAttached("Radius",typeof(double),typeof(RoundedButtonChrome),new PropertyMetadata(0d,RadiusChanged));
    public static double GetRadius(DependencyObject value)=>(double)value.GetValue(RadiusProperty);
    public static void SetRadius(DependencyObject value,double radius)=>value.SetValue(RadiusProperty,radius);
    private static void RadiusChanged(DependencyObject target,DependencyPropertyChangedEventArgs args)
    {
        if(target is not Control control)return;
        control.Loaded-=Loaded;control.Loaded+=Loaded;
        if(control.IsLoaded)Apply(control);
    }
    private static void Loaded(object sender,RoutedEventArgs args)=>Apply((Control)sender);
    private static void Apply(Control control)
    {
        control.ApplyTemplate();Round(control,control,new CornerRadius(GetRadius(control)));
    }
    // 기본 리본의 시각 상태는 유지하고 해당 컨트롤 템플릿이 소유한 테두리만 둥글게 한다.
    private static void Round(DependencyObject parent,Control owner,CornerRadius radius)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=VisualTreeHelper.GetChild(parent,i);
            if(child is Border border&&border.TemplatedParent==owner)border.CornerRadius=radius;
            Round(child,owner,radius);
        }
    }
}
