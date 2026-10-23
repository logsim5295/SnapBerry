using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Media;
using SnapBerry.App.Editor;
using SnapBerry.App.Settings;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 버튼 및 리본의 둥근 모서리와 브랜드 강조 스타일을 검증한다.
public class ButtonAppearanceTests
{
    [Fact] public void NextNumberInputUsesPurpleBorderAndFocusAndKeepsEditing()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=SnapBerry.App.Export.PngFileWriter.Encode(RenderingTests.Image(100,100))});((StackPanel)w.FindName("NumberControls")).Visibility=Visibility.Visible;w.Show();w.Activate();w.UpdateLayout();
            var input=(TextBox)w.FindName("NextNumberBox");var chrome=Assert.IsType<Border>(input.Template.FindName("TextInputChrome",input));Assert.Equal("#FF923C83",chrome.BorderBrush.ToString());
            System.Windows.Input.Keyboard.Focus(input);w.UpdateLayout();Assert.True(input.IsKeyboardFocusWithin);Assert.Equal("#FFF5EAF3",chrome.Background.ToString());Assert.Equal("#FF923C83",input.CaretBrush.ToString());Assert.Equal("#FF923C83",input.SelectionBrush.ToString());
            input.Text="7";System.Windows.Input.Keyboard.Focus((Button)w.FindName("HistoryToggleButton"));w.UpdateLayout();Assert.Equal(7,w.Model.Document!.Sequence.NextNumeric);Assert.Equal("#FFFFFFFF",chrome.Background.ToString());
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData("FontSizeBox")] [InlineData("NumberModeBox")]
    public void ComboBoxesUsePurpleChromeAndSelectionWithoutChangingBehavior(string name)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=SnapBerry.App.Export.PngFileWriter.Encode(RenderingTests.Image(100,100))});
            ((StackPanel)w.FindName("FontControls")).Visibility=Visibility.Visible;((StackPanel)w.FindName("NumberControls")).Visibility=Visibility.Visible;w.Show();w.UpdateLayout();
            var combo=(ComboBox)w.FindName(name);var toggle=Assert.IsType<System.Windows.Controls.Primitives.ToggleButton>(combo.Template.FindName("DropDownToggle",combo));Assert.Contains(Descendants(toggle).OfType<Border>(),x=>x.BorderBrush?.ToString()=="#FF923C83");
            combo.IsDropDownOpen=true;w.UpdateLayout();Assert.True(toggle.IsChecked);combo.SelectedIndex=0;var item=(ComboBoxItem)combo.Items[0];item.ApplyTemplate();var chrome=Assert.IsType<Border>(item.Template.FindName("ComboItemChrome",item));Assert.Equal("#FFEBD5E6",chrome.Background.ToString());
            combo.SelectedIndex=1;if(name=="FontSizeBox")Assert.Equal(24,w.Model.Style.FontSize);else Assert.Equal(SnapBerry.Core.StepSequenceMode.Alphabetic,w.Model.Document!.Sequence.Mode);combo.IsDropDownOpen=false;
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData("ThicknessSlider")] [InlineData("MosaicSlider")]
    public void SlidersUsePurpleTrackAndThumbAndKeepValueCommands(string name)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=SnapBerry.App.Export.PngFileWriter.Encode(RenderingTests.Image(100,100))});
            var tool=name=="ThicknessSlider"?"Arrow":"Mosaic";
            ((ItemsControl)w.FindName("ToolsPanel")).Items.OfType<System.Windows.Controls.Primitives.ToggleButton>().Single(x=>(string)x.Tag==tool).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            var slider=(Slider)w.FindName(name);slider.Measure(new Size(160,24));slider.Arrange(new Rect(0,0,160,24));slider.UpdateLayout();var track=Assert.IsType<System.Windows.Controls.Primitives.Track>(slider.Template.FindName("PART_Track",slider));
            Assert.Contains(Descendants(track.Thumb).OfType<Border>(),x=>x.Background?.ToString()=="#FF923C83");
            Assert.Contains(Descendants(track.DecreaseRepeatButton).OfType<Border>(),x=>x.Background?.ToString()=="#FF923C83");
            var before=slider.Value;Slider.IncreaseSmall.Execute(null,slider);Assert.Equal(before+1,slider.Value);Assert.Equal(slider.Value,name=="ThicknessSlider"?w.Model.Style.Thickness:w.Model.MosaicSize);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void SelectedObjectOutlineAndResizeHandleUseBrandPurple()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=100,Height=100,OriginalPng=SnapBerry.App.Export.PngFileWriter.Encode(RenderingTests.Image(100,100))});
        var annotation=new SnapBerry.Core.Annotation{Kind=SnapBerry.Core.AnnotationKind.Rectangle,X=20,Y=20,Width=40,Height=40};model.History!.Add(annotation);model.SelectedId=annotation.Id;
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();canvas.Measure(new Size(100,100));canvas.Arrange(new Rect(0,0,100,100));canvas.UpdateLayout();
        var pens=GeometryDrawings(VisualTreeHelper.GetDrawing(canvas)).Where(x=>x.Pen is not null).Select(x=>x.Pen.Brush.ToString()).ToArray();Assert.Equal(2,pens.Length);Assert.All(pens,color=>Assert.Equal("#FF923C83",color));
    });
    private static IEnumerable<GeometryDrawing> GeometryDrawings(Drawing drawing)
    {if(drawing is GeometryDrawing geometry)yield return geometry;if(drawing is DrawingGroup group)foreach(var child in group.Children)foreach(var item in GeometryDrawings(child))yield return item;}
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}}
    [Fact] public void EditorButtonsHaveRoundedCornersWithoutLosingSelectionState()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Show();w.UpdateLayout();var ribbon=(Ribbon)w.FindName("MainRibbon");var selected=Descendants(ribbon).OfType<RibbonToggleButton>().First(x=>x.IsChecked==true);
            foreach(var button in new Control[]{(Button)w.FindName("HistoryToggleButton"),(RibbonButton)w.FindName("RegionCaptureButton"),selected})
                Assert.Contains(Descendants(button).OfType<Border>(),x=>x.CornerRadius.TopLeft==5);
            Assert.True(selected.IsChecked);Assert.Equal("#FFEBD5E6",selected.CheckedBackground.ToString());Assert.Equal("#FF923C83",selected.CheckedBorderBrush.ToString());
            Assert.Contains(Descendants(selected).OfType<Border>(),x=>x.Background?.ToString()=="#FFEBD5E6");
            w.SetBusy(true);Assert.False(((RibbonButton)w.FindName("RegionCaptureButton")).IsEnabled);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
