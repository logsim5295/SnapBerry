using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SnapBerry.Desktop.Views;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Shell;
using Xunit;

namespace SnapBerry.Desktop.Tests;

// TestAppBuilder uses Avalonia.Headless: Show and flyouts only create memory surfaces.
public class PurpleThemeTests
{
    private static string Accent(bool dark)=>dark?"#C880BA":"#923C83";
    private static string Hover(bool dark)=>dark?"#392C3D":"#F5EAF3";
    private static string Selected(bool dark)=>dark?"#51334E":"#EBD5E6";
    private static void Equal(string expected,IBrush? brush)=>Assert.Equal(Color.Parse(expected),Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color);
    private static void State(Control control,string state,bool enabled=true)=>((IPseudoClasses)control.Classes).Set(state,enabled);
    private static T Part<T>(Control control,string? name=null) where T:Control=>control.GetVisualDescendants().OfType<T>().First(x=>x.TemplatedParent==control&&(name is null||x.Name==name));
    private static Window Host(bool dark,params Control[] controls)
    {
        var window=new Window{Width=700,Height=700,RequestedThemeVariant=dark?ThemeVariant.Dark:ThemeVariant.Light,Content=new StackPanel()};
        foreach(var control in controls)((StackPanel)window.Content).Children.Add(control);
        window.Show();Dispatcher.UIThread.RunJobs();return window;
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void ButtonsAndActiveToolsKeepPurpleInCombinedStatesAndThemeChanges(bool dark)
    {
        var button=new Button{Content="색상"};var tool=new Button{Content="화살표"};tool.Classes.Add("active");
        var window=Host(dark,button,tool);
        try
        {
            Equal(Selected(dark),Part<ContentPresenter>(tool).Background);
            State(button,":pointerover");Equal(Hover(dark),Part<ContentPresenter>(button).Background);
            State(button,":pressed");Equal(Selected(dark),Part<ContentPresenter>(button).Background);
            Equal(Accent(dark),Part<ContentPresenter>(button).BorderBrush);
            window.RequestedThemeVariant=dark?ThemeVariant.Light:ThemeVariant.Dark;Dispatcher.UIThread.RunJobs();
            Equal(Selected(!dark),Part<ContentPresenter>(button).Background);
            Equal(Selected(!dark),Part<ContentPresenter>(tool).Background);
            button.IsEnabled=false;Assert.False(button.IsEffectivelyEnabled);
            Assert.NotEqual(Color.Parse(Selected(!dark)),Assert.IsAssignableFrom<ISolidColorBrush>(Part<ContentPresenter>(button).Background).Color);
        }
        finally{window.Close();}
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void TabsListsAndComboItemsUsePurpleForHoverPressedAndSelected(bool dark)
    {
        var tab=new TabItem{Header="보기"};var tabs=new TabControl{Items={new TabItem{Header="홈"},tab}};
        var listItem=new ListBoxItem{Content="이력"};var list=new ListBox{Items={listItem}};
        var comboItem=new ComboBoxItem{Content="파선"};var window=Host(dark,tabs,list,comboItem);
        try
        {
            foreach(var (control,chrome) in new (Control,Control)[]{(tab,Part<Border>(tab,"PART_LayoutRoot")),(listItem,Part<ContentPresenter>(listItem)),(comboItem,Part<ContentPresenter>(comboItem))})
            {
                State(control,":pointerover");Equal(Hover(dark),chrome is Border b?b.Background:((ContentPresenter)chrome).Background);
                State(control,":pressed");Equal(Selected(dark),chrome is Border b2?b2.Background:((ContentPresenter)chrome).Background);
                State(control,":pressed",false);State(control,":pointerover",false);
            }
            tabs.SelectedIndex=1;list.SelectedIndex=0;comboItem.IsSelected=true;
            Equal(Selected(dark),tab.Background);Equal(Accent(dark),Part<Border>(tab,"PART_SelectedPipe").Background);
            Equal(Selected(dark),Part<ContentPresenter>(listItem).Background);Equal(Selected(dark),Part<ContentPresenter>(comboItem).Background);
        }
        finally{window.Close();}
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void CheckBoxComboAndTextInputsHavePurpleInteractionAndSelection(bool dark)
    {
        var check=new CheckBox{Content="비율 유지"};var combo=new ComboBox{Items={"실선","파선"},SelectedIndex=0};var text=new TextBox{Text="선택 텍스트"};var number=new NumericUpDown{Value=3};
        var window=Host(dark,check,combo,text,number);
        try
        {
            State(check,":pointerover");Equal(Accent(dark),Part<Border>(check,"NormalRectangle").BorderBrush);
            check.IsChecked=true;Equal(Accent(dark),Part<Border>(check,"NormalRectangle").Background);
            State(combo,":pointerover");Equal(Hover(dark),Part<Border>(combo,"Background").Background);
            State(combo,":pressed");Equal(Selected(dark),Part<Border>(combo,"Background").Background);
            State(text,":pointerover");Equal(Accent(dark),Part<Border>(text,"PART_BorderElement").BorderBrush);
            text.Focus();Equal(Accent(dark),text.SelectionBrush);Equal(Accent(dark),text.CaretBrush);Equal(Accent(dark),Part<Border>(text,"PART_BorderElement").BorderBrush);
            var inner=Part<TextBox>(number,"PART_TextBox");Equal(Accent(dark),inner.SelectionBrush);
            var spinner=Part<ButtonSpinner>(number,"PART_Spinner");var spin=spinner.GetVisualDescendants().OfType<RepeatButton>().First();
            State(spin,":pointerover");Equal(Hover(dark),Part<ContentPresenter>(spin).Background);
            inner.Focus();Equal(Accent(dark),spinner.BorderBrush);
        }
        finally{window.Close();}
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void SliderScrollbarsAndSplitterUsePurpleThroughDragging(bool dark)
    {
        var slider=new Slider{Value=30,Width=200};var bar=new ScrollBar{Orientation=Orientation.Horizontal,Maximum=100,ViewportSize=20,Width=200,AllowAutoHide=false};var splitter=new GridSplitter();
        var window=Host(dark,slider,bar,splitter);
        try
        {
            State(slider,":pointerover");Equal(Accent(dark),Part<Thumb>(slider).Background);
            State(slider,":pressed");AssertPurple(Part<Thumb>(slider).Background);
            var thumb=bar.GetVisualDescendants().OfType<Thumb>().Single();AssertPurple(Part<Border>(thumb).Background);
            State(thumb,":pointerover");AssertPurple(Part<Border>(thumb).Background);
            State(thumb,":pressed");AssertPurple(Part<Border>(thumb).Background);
            State(splitter,":pointerover");Equal(Accent(dark),splitter.Background);
            State(splitter,":pressed");AssertPurple(splitter.Background);
        }
        finally{window.Close();}
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void AppMenusAndDefaultTextAndScrollbarFlyoutsKeepPurpleWithoutReplacingCommands(bool dark)
    {
        var item=new MenuItem{Header="이름 변경"};var text=new TextBox{Text="abc"};var bar=new ScrollBar{Maximum=100,ViewportSize=20,AllowAutoHide=false};
        var window=Host(dark,text,bar);
        try
        {
            var menu=new ContextMenu{Items={item}};text.ContextMenu=menu;menu.Open(text);Dispatcher.UIThread.RunJobs();item.IsSelected=true;
            var chrome=Part<Border>(item,"PART_LayoutRoot");Equal(Hover(dark),chrome.Background);
            State(item,":pressed");State(chrome,":pointerover");Equal(Selected(dark),chrome.Background);menu.Close();text.ContextMenu=null;
            foreach(var owner in new Control[]{text,bar})
            {
                var flyout=Assert.IsType<MenuFlyout>(owner.ContextFlyout);flyout.ShowAt(owner);Dispatcher.UIThread.RunJobs();
                var entry=flyout.Items.OfType<MenuItem>().First();Assert.NotNull(entry.Command);Assert.NotNull(entry.Header);
                entry.IsEnabled=true;entry.IsSelected=true;Equal(Hover(dark),Part<Border>(entry,"PART_LayoutRoot").Background);
                State(entry,":pressed");State(Part<Border>(entry,"PART_LayoutRoot"),":pointerover");Equal(Selected(dark),Part<Border>(entry,"PART_LayoutRoot").Background);
                flyout.Hide();
            }
        }
        finally{window.Close();}
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void ResizeAndSettingsDialogsInheritPurpleAndKeepTheirFunctionalColors(bool dark)
    {
        foreach(var dialog in new Window[]{new ImageResizeWindow(new(800,400),new(400,200)),new DesktopSettingsWindow(dark,true,true,(_,_)=>Task.CompletedTask),new ColorPickerWindow("#E83F56"),new TextEditWindow("테스트","텍스트 편집"),new HotkeySettingsWindow(new(),_=>Task.CompletedTask),new AboutWindow("unused",_=>{},(Action<string>)(_=>{})),new StickerPickerWindow(_=>{})})
        try
        {
            dialog.RequestedThemeVariant=dark?ThemeVariant.Dark:ThemeVariant.Light;dialog.Show();Dispatcher.UIThread.RunJobs();
            foreach(var check in dialog.GetVisualDescendants().OfType<CheckBox>()){check.IsChecked=true;Equal(Accent(dark),Part<Border>(check,"NormalRectangle").Background);}
            foreach(var button in ((Control)dialog.Content!).GetVisualDescendants().OfType<Button>().Where(b=>b.GetType()==typeof(Button)&&b.TemplatedParent is null))
            {
                State(button,":pointerover");var chrome=Part<ContentPresenter>(button);
                Assert.True(chrome.Background is not null,$"{dialog.GetType().Name}/{button.Name}/{button.Content} enabled={button.IsEffectivelyEnabled}");Equal(Hover(dark),chrome.Background);
                State(button,":pressed");Equal(Selected(dark),chrome.Background);
            }
            foreach(var expander in dialog.GetVisualDescendants().OfType<Expander>())
            {
                var header=Part<ToggleButton>(expander,"ExpanderHeader");State(header,":pointerover");
                Equal(Hover(dark),Part<Border>(header,"ToggleButtonBackground").Background);
                State(header,":pressed");Equal(Selected(dark),Part<Border>(header,"ToggleButtonBackground").Background);
            }
            if(dialog is ColorPickerWindow picker){Assert.Equal("#E83F56",picker.SelectedColor);Equal("#E83F56",picker.FindControl<Border>("OriginalColorPreview")!.Background);}
        }
        finally{dialog.Close();}
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public async Task ActualEditorKeepsPurpleAfterToolTabAndThemeChangesWhileNativeMenusStayNative(bool dark)
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-purple-"+Guid.NewGuid());using var editor=new DesktopEditor();var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            window.ApplyTheme(dark);editor.Open(EditorTests.Record());window.Show();Dispatcher.UIThread.RunJobs();
            var tools=window.FindControl<WrapPanel>("ToolButtons")!.Children.OfType<Button>().Where(x=>x.Tag is string).ToArray();
            foreach(var tool in tools)
            {
                tool.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
                Equal(Selected(dark),Part<ContentPresenter>(tool).Background);
                State(tool,":pointerover");State(tool,":pressed");Equal(Selected(dark),Part<ContentPresenter>(tool).Background);
                State(tool,":pressed",false);State(tool,":pointerover",false);
            }
            var splitter=window.FindControl<GridSplitter>("HistorySplitter")!;State(splitter,":pointerover");Equal(Accent(dark),splitter.Background);
            window.FindControl<Button>("ViewOptionsButton")!.Flyout!.ShowAt(window.FindControl<Button>("ViewOptionsButton")!);
            window.ApplyTheme(!dark);Dispatcher.UIThread.RunJobs();Equal(Accent(!dark),splitter.Background);
            Assert.All(DesktopMenu.Create(window).Items,x=>Assert.True(x is NativeMenuItem or NativeMenuItemSeparator));
            Assert.All(new[]{"SystemAccentColor","SystemAccentColorLight1","SystemAccentColorDark1"},key=>
            {
                Assert.True(window.TryFindResource(key,out var value));var color=Assert.IsType<Color>(value);Assert.True(color.R>color.G&&color.B>color.G,$"{key}: {color}");
            });
        }
        finally{await window.RequestExitAsync();window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }

    private sealed class NoCapture : ICaptureService
    {
        public Task<byte[]?> CaptureAsync(DesktopCaptureMode mode,CancellationToken cancellationToken=default)=>throw new Exception("실제 캡처 금지");
    }

    private static void AssertPurple(IBrush? brush)
    {
        var color=Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
        Assert.True(color.R>color.G&&color.B>color.G,$"Expected purple, got {color}");
    }
}
