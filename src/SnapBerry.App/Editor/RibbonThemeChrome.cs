using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SnapBerry.App.Editor;

// 기본 리본의 입력·팝업 템플릿을 유지하며 내부 장식과 접힌 그룹에도 공통 팔레트를 적용한다.
public static class RibbonThemeChrome
{
    // 밝은 테마의 초기 값(false)은 속성 변경 콜백을 발생시키지 않으므로 Loaded도 연결한다.
    static RibbonThemeChrome()=>EventManager.RegisterClassHandler(typeof(RibbonToggleButton),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));
    private static readonly DependencyProperty ClearedBorderProperty=DependencyProperty.RegisterAttached("ClearedBorder",typeof(bool),typeof(RibbonThemeChrome),new PropertyMetadata(false));
    private static readonly IMultiValueConverter MenuChromeConverter=new MenuChromeBrushConverter();
    public static readonly DependencyProperty DarkModeProperty=DependencyProperty.RegisterAttached("DarkMode",typeof(bool),typeof(RibbonThemeChrome),new PropertyMetadata(false,Changed));
    public static bool GetDarkMode(DependencyObject target)=>(bool)target.GetValue(DarkModeProperty);
    public static void SetDarkMode(DependencyObject target,bool value)=>target.SetValue(DarkModeProperty,value);
    private static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs args)
    {
        if(target is not Control control)return;
        control.Loaded-=Loaded;control.Loaded+=Loaded;
        control.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>Apply(control));
    }
    private static void Loaded(object sender,RoutedEventArgs args)
    {if(sender is Control control&&control.TryFindResource("Theme.Accent") is Brush)Apply(control);}
    private static void Apply(Control control)
    {
        control.ApplyTemplate();ApplyTemplateColors(control);
    }
    internal static void ApplyTemplateColors(Control control)
    {
        var dark=GetDarkMode(control);
        void ApplyPart(DependencyObject node)
        {
            // 접힌 그룹은 자체 로컬 스타일을 가진 버튼을 생성하므로 전역 암시적 스타일을 받지 않는다.
            if(control is RibbonGroup&&node is RibbonToggleButton toggle&&toggle.TemplatedParent==control)
            {
                foreach(var (property,key) in new[]{
                    (Control.BackgroundProperty,"Ribbon"),(Control.ForegroundProperty,"Text"),
                    (RibbonToggleButton.MouseOverBackgroundProperty,"Hover"),(RibbonToggleButton.MouseOverBorderBrushProperty,"Accent"),
                    (RibbonToggleButton.CheckedBackgroundProperty,"Selected"),(RibbonToggleButton.CheckedBorderBrushProperty,"Accent"),
                    (RibbonToggleButton.PressedBackgroundProperty,"Selected"),(RibbonToggleButton.PressedBorderBrushProperty,"Accent"),
                    (RibbonToggleButton.FocusedBackgroundProperty,"Hover"),(RibbonToggleButton.FocusedBorderBrushProperty,"Accent")})
                    toggle.SetResourceReference(property,"Theme."+key);
                SetDarkMode(toggle,dark);Apply(toggle);
            }
            if(node is FrameworkElement element&&element.TemplatedParent==control)
            {
                if(element is Border border)
                {
                    if(control is RibbonToggleButton)
                    {
                        // 메뉴 내부 버튼의 OuterBorder는 IsPressed에서 기본 금색을 직접 지정한다.
                        if(control is RibbonToggleButton menuToggle&&control.TemplatedParent is RibbonMenuButton&&border.Name=="OuterBorder")
                        {
                            BindingOperations.SetBinding(border,Border.BackgroundProperty,MenuChromeBinding(menuToggle,false));
                            BindingOperations.SetBinding(border,Border.BorderBrushProperty,MenuChromeBinding(menuToggle,true));return;
                        }
                        // 바깥 테두리의 Checked/Hover 색만 드러내고 기본 노랑·주황 광택은 제거한다.
                        if(border.Name is "InnerBorder" or "MiddleBorder")
                        {border.Background=Brushes.Transparent;border.BorderBrush=Brushes.Transparent;return;}
                        if(control.TemplatedParent is RibbonGroup&&border.Name.StartsWith("Image",StringComparison.Ordinal)&&border.Name.EndsWith("Border",StringComparison.Ordinal))
                        {border.SetResourceReference(Border.BackgroundProperty,"Theme.Surface");border.SetResourceReference(Border.BorderBrushProperty,"Theme.Accent");return;}
                    }
                    if(border.Name=="groupsBorder")
                    {if(dark)border.SetResourceReference(Border.BackgroundProperty,"Theme.Ribbon");else border.ClearValue(Border.BackgroundProperty);}
                    if(border.Name=="PART_HotBackground")
                    {if(dark)border.Background=Brushes.Transparent;else border.ClearValue(Border.BackgroundProperty);}
                    if(border.Name is "InnerBorder" or "MiddleBorder"||(bool)border.GetValue(ClearedBorderProperty)||border.BorderBrush is SolidColorBrush{Color:{R:255,G:255,B:255,A:96}})
                    {
                        if(dark){border.SetValue(ClearedBorderProperty,true);border.BorderBrush=Brushes.Transparent;}
                        else{border.ClearValue(Border.BorderBrushProperty);border.ClearValue(ClearedBorderProperty);}
                    }
                }
                if(element is Rectangle rect&&rect.Name is "OverlayRect" or "InnerOverlayRect")
                {if(dark)rect.Fill=Brushes.Transparent;else rect.ClearValue(Shape.FillProperty);}
            }
        }
        // RibbonTab은 선택 전 시각 자식을 노출하지 않을 수 있다. 템플릿 이름 범위는 이미 완성되어 있다.
        foreach(var name in new[]{"groupsBorder","PART_HotBackground","InnerBorder","MiddleBorder","OverlayRect","InnerOverlayRect"})
            if(control.Template?.FindName(name,control) is DependencyObject part)ApplyPart(part);
        void Visit(DependencyObject node)
        {ApplyPart(node);for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Visit(VisualTreeHelper.GetChild(node,i));}
        Visit(control);
    }

    private static MultiBinding MenuChromeBinding(RibbonToggleButton toggle,bool border)
    {
        var binding=new MultiBinding{Converter=MenuChromeConverter,ConverterParameter=border,Mode=BindingMode.OneWay};
        foreach(var path in new[]{"IsPressed","IsChecked","IsMouseOver","IsKeyboardFocusWithin","Background","BorderBrush"})
            binding.Bindings.Add(new Binding(path){Source=toggle});
        foreach(var key in new[]{"Hover","Selected","Accent"})binding.Bindings.Add(new Binding{Source=ThemeResources.Get(toggle,key)});
        return binding;
    }

    private sealed class MenuChromeBrushConverter : IMultiValueConverter
    {
        public object Convert(object[] values,Type targetType,object parameter,System.Globalization.CultureInfo culture)
        {
            var pressedOrChecked=values[0] is true||values[1] is true;
            var active=pressedOrChecked||values[2] is true||values[3] is true;
            if(active)return values[parameter is true?8:pressedOrChecked?7:6];
            return values[parameter is true?5:4] is Brush brush?brush:Brushes.Transparent;
        }
        public object[] ConvertBack(object value,Type[] targetTypes,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
    }
}
