using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.App.Stickers;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 비모달 선택 창의 스티커 변경·연속 삽입과 편집기 도구 전환 연동을 검증한다.
public class StickerWorkflowTests
{
    private static void Tool(EditorWindow w,string name)=>((ItemsControl)w.FindName("ToolsPanel")).Items.OfType<ToggleButton>().Single(x=>(string)x.Tag==name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static void Escape(Window w)=>w.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(w),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
    [Fact] public void PaletteChangesStickerAndAllowsRepeatedCanvasInsertion()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),confirmTrayHide:()=>true);
        try
        {
            w.Model.Open(new(){Width=400,Height=300,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(400,300))});w.Show();Tool(w,"Sticker");var picker=w.OwnedWindows.OfType<StickerPicker>().Single();Assert.False(w.IsBusy);Assert.True(w.IsEnabled);Assert.True(picker.IsVisible);
            Tool(w,"Sticker");Assert.Same(picker,w.OwnedWindows.OfType<StickerPicker>().Single());
            var dock=(DockPanel)picker.FindName("StickerContent");((ComboBox)dock.Children[0]).SelectedIndex=1;var wrap=(WrapPanel)((ScrollViewer)dock.Children[1]).Content;var buttons=wrap.Children.OfType<Button>().ToArray();var canvas=(EditorCanvas)((Grid)w.FindName("CanvasHost")).Children[0];
            void Insert(){canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});foreach(var annotation in w.Model.Document!.Annotations.ToArray())w.Model.History!.Replace(annotation with{X=10000,Y=10000});}
            buttons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));var first=w.Model.SelectedSticker;Assert.NotNull(first);Insert();Insert();Assert.Equal(2,w.Model.Document!.Annotations.Count);Assert.All(w.Model.Document.Annotations,x=>Assert.Equal(first.Id,x.StickerId));
            buttons[1].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Assert.NotEqual(first.Id,w.Model.SelectedSticker!.Id);Insert();Assert.Equal(3,w.Model.Document.Annotations.Count);Assert.Equal(w.Model.SelectedSticker.Id,w.Model.Document.Annotations.Last().StickerId);Assert.True(picker.IsVisible);
            w.SetBusy(true);Assert.False(picker.IsVisible);w.SetBusy(false);Assert.True(picker.IsVisible);w.Hide();Assert.False(picker.IsVisible);w.Show();Assert.True(picker.IsVisible);
            Tool(w,"Arrow");Assert.Empty(w.OwnedWindows.OfType<StickerPicker>());Tool(w,"Sticker");picker=w.OwnedWindows.OfType<StickerPicker>().Single();Assert.Equal(w.Model.SelectedSticker.Id,picker.Selected!.Id);Escape(w);Assert.Empty(w.OwnedWindows.OfType<StickerPicker>());
            Tool(w,"Sticker");picker=w.OwnedWindows.OfType<StickerPicker>().Single();Escape(picker);Assert.Empty(w.OwnedWindows.OfType<StickerPicker>());
            Tool(w,"Sticker");w.Close();Assert.False(w.IsVisible);Assert.Empty(w.OwnedWindows.OfType<StickerPicker>());
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
