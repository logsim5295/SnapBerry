using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using SnapBerry.Core;
using SnapBerry.Desktop.Shortcuts;
using SnapBerry.Desktop.Views;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class DialogParityTests
{
    [AvaloniaFact]
    public void InvalidHexCannotApplyAndRgbChangesSynchronizeTheSelectedColor()
    {
        var dialog=Create("ColorPickerWindow","#123456");
        try
        {
            dialog.FindControl<TextBox>("HexInput")!.Text="#oops";
            Assert.False(Accept(dialog));
            Assert.False(dialog.FindControl<Button>("ApplyColorButton")!.IsEnabled);
            dialog.FindControl<TextBox>("HexInput")!.Text="#FF0000";
            dialog.FindControl<TextBox>("GreenInput")!.Text="128";
            Assert.True(Accept(dialog));Assert.Equal("#FF8000",Property<string>(dialog,"SelectedColor"));
            dialog.FindControl<TextBox>("BlueInput")!.Text="256";Assert.False(Accept(dialog));
        }
        finally{dialog.Close();}
    }

    [AvaloniaFact]
    public void HueAndSaturationValueSelectionProduceCanonicalRgbAndFollowTheTheme()
    {
        var dialog=Create("ColorPickerWindow","#FF0000");
        try
        {
            dialog.RequestedThemeVariant=ThemeVariant.Dark;
            dialog.FindControl<Slider>("HueSlider")!.Value=120;
            dialog.GetType().GetMethod("SetSaturationValue")!.Invoke(dialog,[1d,1d]);
            Assert.Equal("#00FF00",Property<string>(dialog,"SelectedColor"));
            Assert.Equal(ThemeVariant.Dark,dialog.FindControl<TextBox>("HexInput")!.ActualThemeVariant);
            var palette=dialog.FindControl<WrapPanel>("ColorPalette")!;
            Click(palette.Children.OfType<Button>().Single(x=>Equals(x.Tag,"#000000")));
            Assert.Equal("#000000",Property<string>(dialog,"SelectedColor"));
        }
        finally{dialog.Close();}
    }

    [AvaloniaFact]
    public void ResizeLocksAspectRejectsOversizedOutputAndResetRestoresOriginalPixels()
    {
        var dialog=Create("ImageResizeWindow",new PixelSize(800,400),new PixelSize(400,200));
        try
        {
            dialog.FindControl<TextBox>("ResizeWidth")!.Text="600";
            Assert.True(Accept(dialog));Assert.Equal(new PixelSize(600,300),Property<PixelSize>(dialog,"SelectedSize"));
            dialog.FindControl<CheckBox>("LockAspectRatio")!.IsChecked=false;
            dialog.FindControl<TextBox>("ResizeWidth")!.Text="32767";
            dialog.FindControl<TextBox>("ResizeHeight")!.Text="32767";
            Assert.False(Accept(dialog));
            Click(dialog.FindControl<Button>("ResetResizeButton")!);
            Assert.True(Accept(dialog));Assert.Equal(new PixelSize(800,400),Property<PixelSize>(dialog,"SelectedSize"));
        }
        finally{dialog.Close();}
    }

    [AvaloniaFact]
    public void MultilineTextPreservesWhitespaceAndRejectsBlankOrOversizedAnnotations()
    {
        var dialog=Create("TextEditWindow","original","텍스트 편집");
        try
        {
            var field=dialog.FindControl<TextBox>("TextEditInput")!;
            Assert.True(field.AcceptsReturn);
            field.Text=" ";Assert.False(Accept(dialog));
            field.Text="  first\nsecond  ";Assert.True(Accept(dialog));Assert.Equal("  first\nsecond  ",Property<string>(dialog,"EditedText"));
            field.Text=new string('x',8193);Assert.False(Accept(dialog));
        }
        finally{dialog.Close();}
    }

    [AvaloniaFact]
    public void AboutActionsAreExplicitAndDiagnosticsNeverContainTheStoragePath()
    {
        var opened=new List<string>();var copied=new List<string>();
        var dialog=Create("AboutWindow","/Users/private/Captures",(Action<string>)opened.Add,(Action<string>)copied.Add);
        try
        {
            Assert.Empty(opened);Assert.Empty(copied);
            Click(dialog.FindControl<Button>("CopyDiagnosticsButton")!);
            Assert.Single(copied);Assert.Contains("SnapBerry",copied[0]);Assert.DoesNotContain("/Users/private",copied[0]);
            Click(dialog.FindControl<Button>("OpenStorageFolderButton")!);Assert.Equal("/Users/private/Captures",Assert.Single(opened));
            Click(dialog.FindControl<Button>("GitHubLink")!);Assert.Equal("https://github.com/simlog1234/SnapBerry",opened[1]);
            Assert.All(dialog.FindControl<TabControl>("AboutLegalDocuments")!.Items.OfType<TabItem>(),item=>Assert.NotEmpty(((TextBox)item.Content!).Text!));
        }
        finally{dialog.Close();}
    }

    [AvaloniaFact]
    public void AboutFailedActionsReportTheErrorWithoutEscapingTheClickHandler()
    {
        var dialog=Create("AboutWindow","/tmp/test",(Action<string>)(_=>throw new IOException("unavailable")),(Action<string>)(_=>throw new IOException("copy failed")));
        try{Click(dialog.FindControl<Button>("CopyDiagnosticsButton")!);Assert.Contains("copy failed",dialog.FindControl<TextBlock>("AboutActionStatus")!.Text);}
        finally{dialog.Close();}
    }

    [AvaloniaFact]
    public async Task HotkeyFieldsCaptureKeyPressesEscapeRestoresAndResetUsesDefaults()
    {
        HotkeySettings? applied=null;var dialog=new HotkeySettingsWindow(new(){Region="Ctrl+F9"},value=>{applied=value;return Task.CompletedTask;});
        try
        {
            var field=dialog.FindControl<TextBox>("RegionHotkey")!;
            field.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.P,KeyModifiers=KeyModifiers.Meta|KeyModifiers.Alt});
            Assert.Equal("Command+Option+P",field.Text);
            field.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});Assert.Equal("Ctrl+F9",field.Text);
            Click(dialog.FindControl<Button>("ResetRegionHotkey")!);Assert.Equal("Ctrl+Shift+1",field.Text);
            Assert.True(await dialog.TrySaveAsync());Assert.Equal("Ctrl+Shift+1",applied!.Region);
        }
        finally{dialog.Close();}
    }

    private static Window Create(string name,params object[] arguments)
    {
        var type=typeof(HotkeySettingsWindow).Assembly.GetType("SnapBerry.Desktop.Views."+name);
        Assert.NotNull(type);
        var constructor=type.GetConstructors().Single(c=>c.GetParameters().Length>=arguments.Length&&c.GetParameters().Skip(arguments.Length).All(p=>p.IsOptional)&&c.GetParameters().Take(arguments.Length).Select((p,i)=>p.ParameterType.IsInstanceOfType(arguments[i])).All(match=>match));
        return (Window)constructor.Invoke(arguments.Concat(constructor.GetParameters().Skip(arguments.Length).Select(p=>p.DefaultValue)).ToArray());
    }
    private static bool Accept(Window window)=>(bool)window.GetType().GetMethod("TryAccept")!.Invoke(window,null)!;
    private static T Property<T>(Window window,string name)=>(T)window.GetType().GetProperty(name)!.GetValue(window)!;
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
