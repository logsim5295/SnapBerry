using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SnapBerry.Desktop.Views;

// Vector silhouettes are independent of SF Symbols or installed icon fonts.
internal static class DesktopIcons
{
    private static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        ["Sidebar"] = "M2,3 H22 V21 H2 Z M4,5 V19 H8 V5 Z M10,5 V19 H20 V5 Z",
        ["Open"] = "M2,5 H10 L12,8 H22 V11 H6 L3,21 H1 V5 Z M7,13 H24 L21,21 H4 Z",
        ["Paste"] = "M8,2 H16 V6 H8 Z M5,4 H7 V8 H17 V4 H20 V22 H4 V4 Z M7,11 V13 H17 V11 Z M7,16 V18 H15 V16 Z",
        ["Region"] = "M2,2 H9 V4 H4 V9 H2 Z M15,2 H22 V9 H20 V4 H15 Z M2,15 H4 V20 H9 V22 H2 Z M20,15 H22 V22 H15 V20 H20 Z M8,8 H16 V16 H8 Z",
        ["Window"] = "M2,3 H22 V21 H2 Z M4,9 V19 H20 V9 Z M5,5 V7 H7 V5 Z M9,5 V7 H11 V5 Z",
        ["Display"] = "M1,3 H23 V18 H14 V21 H18 V23 H6 V21 H10 V18 H1 Z M3,5 V16 H21 V5 Z",
        ["Copy"] = "M7,2 H22 V18 H18 V22 H2 V6 H7 Z M9,4 V6 H18 V16 H20 V4 Z M4,8 V20 H16 V8 Z",
        ["Save"] = "M3,2 H18 L22,6 V22 H2 V2 Z M6,4 V10 H17 V4 Z M6,14 V20 H18 V14 Z M13,5 H15 V9 H13 Z",
        ["View"] = "M2,4 H22 V7 H2 Z M2,11 H22 V14 H2 Z M2,18 H22 V21 H2 Z M6,2 H9 V9 H6 Z M15,9 H18 V16 H15 Z M8,16 H11 V23 H8 Z",
        ["Hotkey"] = "M1,5 H23 V20 H1 Z M3,7 V18 H21 V7 Z M5,9 H7 V11 H5 Z M9,9 H11 V11 H9 Z M13,9 H15 V11 H13 Z M17,9 H19 V11 H17 Z M6,14 H18 V16 H6 Z",
        ["Settings"] = "M10,1 H14 L15,5 L19,4 L22,8 L19,11 L23,13 L21,18 L17,17 L16,22 H11 L10,18 L6,19 L3,15 L6,12 L2,10 L4,5 L8,6 Z M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 Z",
        ["About"] = "M12,1 A11,11 0 1 0 12,23 A11,11 0 1 0 12,1 Z M11,5 H13 V8 H11 Z M10,10 H13 V17 H15 V19 H9 V17 H11 V12 H10 Z",
        ["선택"] = "M4,2 L20,13 L13,14 L16,21 L13,23 L10,16 L5,21 Z",
        ["화살표"] = "M3,19 L16,6 H9 V3 H22 V16 H19 V9 L6,22 Z",
        ["텍스트"] = "M2,3 H22 V8 H19 V6 H14 V20 H17 V23 H7 V20 H10 V6 H5 V8 H2 Z",
        ["사각형"] = "M2,3 H22 V21 H2 Z M4,5 V19 H20 V5 Z",
        ["모자이크"] = "M2,2 H8 V8 H2 Z M10,2 H16 V8 H10 Z M18,2 H24 V8 H18 Z M2,10 H8 V16 H2 Z M10,10 H16 V16 H10 Z M18,10 H24 V16 H18 Z M2,18 H8 V24 H2 Z M10,18 H16 V24 H10 Z M18,18 H24 V24 H18 Z",
        ["번호"] = "M12,1 A11,11 0 1 0 12,23 A11,11 0 1 0 12,1 Z M8,8 L12,5 H14 V18 H17 V20 H8 V18 H11 V9 L9,10 Z",
        ["스티커"] = "M3,2 H21 V15 L14,22 H3 Z M5,4 V20 H12 V13 H19 V4 Z M14,15 V18 L17,15 Z",
        ["자르기"] = "M5,1 H8 V16 H23 V19 H8 V24 H5 V19 H0 V16 H5 Z M10,5 H19 V14 H16 V8 H10 Z",
        ["Add"] = "M2,4 H15 V7 H5 V19 H18 V11 H21 V22 H2 Z M18,1 H21 V5 H25 V8 H21 V12 H18 V8 H14 V5 H18 Z",
        ["Undo"] = "M9,3 V7 H14 A9,9 0 0 1 21,21 L18,19 A6,6 0 0 0 14,10 H9 V14 L1,8 Z",
        ["마술봉"] = "M2,20 L17,5 L21,9 L6,24 Z M6,1 L8,5 L12,7 L8,9 L6,13 L4,9 L0,7 L4,5 Z M19,15 H21 V18 H24 V20 H21 V23 H19 V20 H16 V18 H19 Z",
        ["Redo"] = "M15,3 L23,8 L15,14 V10 H10 A6,6 0 0 0 6,19 L3,21 A9,9 0 0 1 10,7 H15 Z",
        ["Delete"] = "M8,2 H16 V5 H22 V8 H2 V5 H8 Z M5,10 H19 L18,23 H6 Z M8,12 V20 H10 V12 Z M14,12 V20 H16 V12 Z",
        ["Edit"] = "M3,17 L16,4 L21,9 L8,22 L2,23 Z M18,2 L20,0 L25,5 L23,7 Z",
    };

    public static void Set(Button button, string icon, string? label = null, bool showLabel = true)
    {
        label ??= button.Content as string ?? icon;
        AutomationProperties.SetName(button, label);
        if (ToolTip.GetTip(button) is null) ToolTip.SetTip(button, label);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new PathIcon { Data = Geometry.Parse(Paths[icon]), Width = 15, Height = 15, IsHitTestVisible = false });
        if (showLabel) content.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        button.Content = content;
    }

    public static void Apply(MainWindow window)
    {
        foreach (var (name, icon) in new[] { ("OpenButton","Open"), ("PasteButton","Paste"), ("RegionButton","Region"), ("WindowButton","Window"),
            ("DisplayButton","Display"), ("CopyButton","Copy"), ("SaveButton","Save"), ("ViewOptionsButton","View"), ("HotkeyButton","Hotkey"),
            ("SettingsButton","Settings"), ("AboutButton","About"), ("AddImageButton","Add"), ("UndoButton","Undo"), ("RedoButton","Redo"),
            ("DuplicateButton","Copy"), ("DeleteButton","Delete"), ("EditTextButton","Edit") })
        {
            var button = window.FindControl<Button>(name)!;
            Set(button, icon, showLabel: !button.Classes.Contains("icon"));
            // Keep native accessibility clients and the macOS feature probe compatible with compact labels.
            if (name is "OpenButton" or "WindowButton")
                AutomationProperties.SetName(button, name == "OpenButton" ? "이미지 열기" : "창 캡처");
        }
        foreach (var button in window.FindControl<WrapPanel>("ToolButtons")!.Children.OfType<Button>())
            Set(button, (string)button.Content!);
    }
}
