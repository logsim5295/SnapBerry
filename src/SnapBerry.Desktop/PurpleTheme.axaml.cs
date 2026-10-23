using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace SnapBerry.Desktop;

// Override Fluent's state resources instead of replacing its templates: native menus,
// localized editing/scroll commands, validation and disabled states keep their behavior.
public sealed class PurpleTheme : Styles
{
    public PurpleTheme()
    {
        AvaloniaXamlLoader.Load(this);
        Resources.ThemeDictionaries[ThemeVariant.Light]=Palette(false);
        Resources.ThemeDictionaries[ThemeVariant.Dark]=Palette(true);
    }

    private static ResourceDictionary Palette(bool dark)
    {
        var resources=new ResourceDictionary();
        var accent=Color.Parse(dark?"#C880BA":"#923C83");
        var hover=Color.Parse(dark?"#392C3D":"#F5EAF3");
        var selected=Color.Parse(dark?"#51334E":"#EBD5E6");
        var pressed=Color.Parse(dark?"#98558D":"#602657");
        var accentHover=Color.Parse(dark?"#D79CCC":"#B76AA9");
        void Brushes(Color color,params string[] keys)
        {
            var brush=new SolidColorBrush(color).ToImmutable();
            foreach(var key in keys)resources[key]=brush;
        }

        void Neutral(string light,string night,params string[] keys)=>Brushes(Color.Parse(dark?night:light),keys);
        Neutral("#FAFAFC","#252528","Mac.Window");
        Neutral("#FCFCFD","#2C2C30","Mac.Toolbar");
        Neutral("#F1F1F5","#222225","Mac.Sidebar");
        Neutral("#E9E9EF","#19191C","Mac.Canvas");
        Neutral("#FFFFFF","#343439","Mac.Card","ButtonBackground");
        Neutral("#DEDEE5","#45454D","Mac.Border","ButtonBorderBrush");
        Neutral("#686874","#B0B0BC","Mac.Secondary");
        Neutral("#FFFFFF","#211E23","Mac.OnAccent");
        Neutral("#F0F0F4","#303035","ButtonBackgroundDisabled");
        Neutral("#E5E5EB","#38383F","ButtonBorderBrushDisabled");

        Brushes(hover,"Theme.Hover","SystemControlHighlightListLowBrush",
            "ButtonBackgroundPointerOver","RepeatButtonBackgroundPointerOver","ToggleButtonBackgroundPointerOver","ToggleButtonBackgroundIndeterminatePointerOver",
            "ComboBoxBackgroundPointerOver","ComboBoxItemBackgroundPointerOver","MenuFlyoutItemBackgroundPointerOver",
            "TextControlBackgroundPointerOver","TextControlButtonBackgroundPointerOver",
            "CheckBoxCheckBackgroundFillUncheckedPointerOver",
            "ExpanderHeaderBackgroundPointerOver","ExpanderChevronBackgroundPointerOver",
            "TabItemHeaderBackgroundUnselectedPointerOver","ScrollBarButtonBackgroundPointerOver");
        Brushes(selected,"Theme.Selected","SystemControlHighlightListMediumBrush","SystemControlHighlightListAccentLowBrush",
            "SystemControlHighlightListAccentMediumBrush","SystemControlHighlightListAccentHighBrush",
            "ButtonBackgroundPressed","RepeatButtonBackgroundPressed","ToggleButtonBackgroundPressed","ToggleButtonBackgroundIndeterminatePressed",
            "ComboBoxBackgroundPressed","ComboBoxBackgroundUnfocused","ComboBoxItemBackgroundPressed",
            "ComboBoxItemBackgroundSelected","ComboBoxItemBackgroundSelectedPointerOver","ComboBoxItemBackgroundSelectedPressed",
            "MenuFlyoutItemBackgroundPressed","CheckBoxCheckBackgroundFillUncheckedPressed",
            "ExpanderHeaderBackgroundPressed","ExpanderChevronBackgroundPressed",
            "TabItemHeaderBackgroundSelected","TabItemHeaderBackgroundUnselectedPressed",
            "TabItemHeaderBackgroundSelectedPointerOver","TabItemHeaderBackgroundSelectedPressed",
            "ScrollBarButtonBackgroundPressed","SliderTrackFill","SliderTrackFillPointerOver","SliderTrackFillPressed");
        Brushes(accent,"Theme.Accent","SystemControlFocusVisualPrimaryBrush",
            "ButtonBorderBrushPointerOver","ButtonBorderBrushPressed",
            "ToggleButtonBorderBrushPointerOver","ToggleButtonBorderBrushPressed",
            "ComboBoxBorderBrushPointerOver","ComboBoxBorderBrushPressed","ComboBoxBackgroundBorderBrushFocused",
            "ComboBoxBackgroundBorderBrushUnfocused","TextControlBorderBrushPointerOver","TextControlBorderBrushFocused",
            "TextControlSelectionHighlightColor","TextControlButtonBorderBrushPointerOver","TextControlButtonBorderBrushPressed",
            "CheckBoxCheckBackgroundStrokeUncheckedPointerOver","CheckBoxCheckBackgroundStrokeUncheckedPressed",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver","CheckBoxCheckBackgroundStrokeCheckedPressed",
            "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver","CheckBoxCheckBackgroundStrokeIndeterminatePressed",
            "CheckBoxCheckBackgroundFillChecked","CheckBoxCheckBackgroundFillCheckedPointerOver",
            "CheckBoxCheckBackgroundFillIndeterminate","CheckBoxCheckBackgroundFillIndeterminatePointerOver",
            "ExpanderHeaderBorderBrushPointerOver","ExpanderHeaderBorderBrushPressed",
            "ExpanderChevronBorderBrushPointerOver","ExpanderChevronBorderBrushPressed",
            "TabItemHeaderSelectedPipeFill","TabItemHeaderForegroundSelected","TabItemHeaderForegroundSelectedPointerOver",
            "TabItemHeaderForegroundSelectedPressed","ScrollBarButtonArrowForeground","ScrollBarButtonArrowForegroundPointerOver",
            "ScrollBarButtonArrowForegroundPressed","ScrollBarPanningThumbBackground",
            "SliderThumbBackground","SliderThumbBackgroundPointerOver",
            "SliderTrackValueFill","SliderTrackValueFillPointerOver","SliderTrackValueFillPressed");
        Brushes(hover,"SystemControlFocusVisualSecondaryBrush");
        Brushes(accentHover,"ScrollBarThumbFillPointerOver");
        Brushes(pressed,"Theme.AccentPressed","ScrollBarThumbFillPressed","SliderThumbBackgroundPressed",
            "CheckBoxCheckBackgroundFillCheckedPressed","CheckBoxCheckBackgroundFillIndeterminatePressed");
        // These Fluent resource keys are colors, although Background consumes them as brushes.
        resources["ScrollBarThumbBackgroundColor"]=accent;
        resources["ScrollBarPanningThumbBackgroundColor"]=accent;
        return resources;
    }
}
