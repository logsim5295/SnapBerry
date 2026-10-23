using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using SnapBerry.Core;
using SnapBerry.Desktop.Capture;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Shell;
using SnapBerry.Desktop.Shortcuts;
using SnapBerry.Desktop.Views;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class ParityViewTests
{
    private sealed class NoCapture:ICaptureService
    {public Task<byte[]?> CaptureAsync(DesktopCaptureMode mode,CancellationToken token=default)=>throw new InvalidOperationException("OS capture prohibited");}
    private sealed class Login:ILoginStartupService
    {
        public bool IsEnabled{get;private set;}public List<bool> Changes {get;}=[];
        public void SetEnabled(bool value){IsEnabled=value;Changes.Add(value);}
    }
    [AvaloniaFact] public void PropertiesFollowToolAndSelectionAndEditsAreUndoable()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-view-"+Guid.NewGuid());using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            editor.Open(EditorTests.Record());Assert.False(window.FindControl<Border>("PropertiesBorder")!.IsVisible);
            window.FindControl<ComboBox>("ToolBox")!.SelectedIndex=3;
            Assert.True(window.FindControl<WrapPanel>("LineStyleControls")!.IsVisible);Assert.False(window.FindControl<WrapPanel>("FontControls")!.IsVisible);
            editor.Commit(new(){Kind=AnnotationKind.Rectangle,X=2,Y=2,Width=30,Height=20});
            window.FindControl<ComboBox>("LineStyleBox")!.SelectedIndex=2;Assert.Equal(StrokePattern.Dot,editor.Selected!.Style.LineStyle);
            editor.History!.Undo();Assert.Equal(StrokePattern.Solid,editor.Selected!.Style.LineStyle);
            window.FindControl<ComboBox>("ToolBox")!.SelectedIndex=4;editor.Commit(new(){Kind=AnnotationKind.Mosaic,Width=30,Height=20});
            window.FindControl<Slider>("MosaicSlider")!.Value=32;Assert.Equal(32,editor.Selected!.MosaicBlockSize);
            editor.History.Undo();Assert.Equal(12,editor.Selected!.MosaicBlockSize);
            window.SetBusy(true);Assert.False(window.FindControl<Border>("CommandBar")!.IsEnabled);
            window.SetBusy(false);Assert.True(window.FindControl<Border>("CommandBar")!.IsEnabled);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact] public async Task SettingsPreserveHistoryPreferencesAndRollBackStartupOnWriteFailure()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-settings-"+Guid.NewGuid());var login=new Login();using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path,loginStartup:login);
        try
        {
            window.SetHistoryPanelVisible(false);await window.ApplyAppearanceSettingsAsync(true,true);var saved=DesktopPreferences.Load(path);
            Assert.True(saved.Dark);Assert.True(saved.StartAtLogin);Assert.False(saved.HistoryVisible);Assert.True(login.IsEnabled);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
        var file=Path.GetTempFileName();var failedLogin=new Login();using var otherEditor=new DesktopEditor();using var other=new MainWindow(otherEditor,new NoCapture(),file,loginStartup:failedLogin);
        try
        {await Assert.ThrowsAnyAsync<IOException>(()=>other.ApplyAppearanceSettingsAsync(true,true));Assert.False(failedLogin.IsEnabled);Assert.Equal(new[]{true,false},failedLogin.Changes);}
        finally{other.Dispose();other.Close();File.Delete(file);}
    }
    [AvaloniaFact] public void StartingANewMosaicClearsTheOldSelectionAndShowsTheActualBlockSize()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-mosaic-"+Guid.NewGuid());using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            editor.Open(EditorTests.Record());window.FindControl<ComboBox>("ToolBox")!.SelectedIndex=4;
            editor.Commit(new(){Kind=AnnotationKind.Mosaic,X=0,Y=0,Width=10,Height=10,MosaicBlockSize=32});
            Assert.Equal(32,window.FindControl<Slider>("MosaicSlider")!.Value);
            var surface=(EditorSurface)window.FindControl<Panel>("SurfaceHost")!.Children[0];surface.Gesture.Begin(new(40,30));
            Assert.Null(editor.SelectedId);window.RefreshSelection();Assert.Equal(surface.Gesture.MosaicSize,window.FindControl<Slider>("MosaicSlider")!.Value);
            surface.Gesture.End(new(60,50));Assert.Equal(12,editor.Selected!.MosaicBlockSize);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [Theory] [InlineData("회의 캡처","회의 캡처.png")] [InlineData("sample.PNG","sample.png")] [InlineData("a/b:c","a_b_c.png")] [InlineData("CON","_CON.png")] [InlineData(" ","Capture_20261022_123456.png")]
    public void ExportUsesTheCaptureNameWithoutCreatingPaths(string name,string expected)
        =>Assert.Equal(expected,ExportFileName.Suggest(name,new DateTime(2026,10,22,12,34,56)));
    [AvaloniaFact] public async Task SavingStartupOffRemovesTheOwnedEntryFromAnOlderAppLocation()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-relocated-"+Guid.NewGuid());var first=Path.Combine(path,"Old","SnapBerry");var moved=Path.Combine(path,"New","SnapBerry");
        var original=new MacLoginStartupService(path,first,501,_=>new(0,"","disabled services = {\n}"));original.SetEnabled(true);
        var relocated=new MacLoginStartupService(path,moved,501,_=>new(0,"","disabled services = {\n}"));Assert.False(relocated.IsEnabled);
        using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path,loginStartup:relocated);
        try{await window.ApplyAppearanceSettingsAsync(false,false);Assert.False(File.Exists(Path.Combine(path,"Library","LaunchAgents","com.ynsim.snapberry.plist")));}
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact] public async Task ColorPickerEditsRgbWithoutRejectingOrDiscardingExistingOpacity()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-alpha-"+Guid.NewGuid());using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            editor.Open(EditorTests.Record());editor.Commit(new(){Kind=AnnotationKind.Rectangle,Style=new(){Color="#80112233",Fill="#80112233"}});
            window.Show();Dispatcher.UIThread.RunJobs();window.FindControl<Button>("ColorPickerButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var picker=Assert.Single(window.OwnedWindows.OfType<ColorPickerWindow>());Assert.Equal("#112233",picker.SelectedColor);
            picker.FindControl<TextBox>("HexInput")!.Text="#00FF00";Assert.True(picker.TryAccept());picker.Close(picker.SelectedColor);
            var until=DateTime.UtcNow.AddSeconds(5);while(window.IsBusy){Assert.True(DateTime.UtcNow<until);await Task.Delay(1,TestContext.Current.CancellationToken);}
            Assert.Equal("#8000FF00",editor.Selected!.Style.Color);editor.History!.Undo();Assert.Equal("#80112233",editor.Selected!.Style.Color);
        }
        finally{window.SetBusy(false);await window.RequestExitAsync();window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact] public async Task HotkeyDialogSuspendsBindingsAndRestoresThemAfterCancel()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-keycapture-"+Guid.NewGuid());var backend=new GlobalHotkeyTests.Backend();var hotkeys=new GlobalHotkeyService(backend);
        using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path,hotkeys:hotkeys);
        try
        {
            window.Show();Dispatcher.UIThread.RunJobs();Assert.Equal(3,backend.Active.Count);var pending=window.RequestHotkeySettingsAsync();
            Assert.Empty(backend.Active);var dialog=Assert.Single(window.OwnedWindows.OfType<HotkeySettingsWindow>());dialog.Close(false);await pending;Assert.Equal(3,backend.Active.Count);
        }
        finally{window.SetBusy(false);await window.RequestExitAsync();window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact] public async Task VerticalWheelChangesViewZoomButHorizontalWheelDoesNotResizeOrZoomTheImage()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-wheel-"+Guid.NewGuid());using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            editor.Open(EditorTests.Record());window.Show();Dispatcher.UIThread.RunJobs();var surface=(EditorSurface)window.FindControl<Panel>("SurfaceHost")!.Children[0];
            var position=surface.TranslatePoint(new(20,20),window)!.Value;var zoom=surface.Zoom;
            window.MouseWheel(position,new(1,0));Assert.Equal(zoom,surface.Zoom);
            window.MouseWheel(position,new(0,1));Assert.True(surface.Zoom>zoom);Assert.Null(editor.Document!.ResizeSize);
        }
        finally{window.SetBusy(false);await window.RequestExitAsync();window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [AvaloniaFact] public async Task DraggingTheMosaicSliderIsOneUndoAction()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-parity-drag-"+Guid.NewGuid());using var editor=new DesktopEditor();using var window=new MainWindow(editor,new NoCapture(),path);
        try
        {
            editor.Open(EditorTests.Record());window.FindControl<ComboBox>("ToolBox")!.SelectedIndex=4;editor.Commit(new(){Kind=AnnotationKind.Mosaic,X=0,Y=0,Width=10,Height=10});
            window.Show();Dispatcher.UIThread.RunJobs();var slider=window.FindControl<Slider>("MosaicSlider")!;
            var start=slider.TranslatePoint(new(25,slider.Bounds.Height/2),window)!.Value;var middle=start.WithX(start.X+40);var end=start.WithX(start.X+85);
            window.MouseDown(start,MouseButton.Left);window.MouseMove(middle,RawInputModifiers.LeftMouseButton);window.MouseMove(end,RawInputModifiers.LeftMouseButton);window.MouseUp(end,MouseButton.Left);
            Assert.True(editor.Selected!.MosaicBlockSize>12);editor.History!.Undo();Assert.Equal(12,editor.Selected!.MosaicBlockSize);
        }
        finally{window.SetBusy(false);await window.RequestExitAsync();window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
}
