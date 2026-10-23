using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.App.Stickers;
using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 색상이 불필요한 도구의 속성 숨김과 선택 색상의 보라색 강조를 검증한다.
public class ColorToolTests
{
    [Theory] [InlineData(EditorTool.Crop)] [InlineData(EditorTool.Sticker)] [InlineData(EditorTool.Mosaic)]
    public void ToolsWithoutColorHidePaletteIndicatorAndPicker(EditorTool tool)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
            var indicator=(FrameworkElement)w.FindName("CurrentColorControls");
            w.Model.Tool=tool;typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);w.UpdateLayout();
            Assert.Equal(Visibility.Collapsed,((StackPanel)w.FindName("ColorControls")).Visibility);
            Assert.Equal(Visibility.Collapsed,indicator.Visibility);Assert.False(((Button)w.FindName("ColorPickerButton")).IsEnabled);
            var color=w.Model.Style.Color;
            w.Model.Tool=EditorTool.Arrow;typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);w.UpdateLayout();
            Assert.Equal(Visibility.Visible,indicator.Visibility);Assert.True(((Button)w.FindName("ColorPickerButton")).IsEnabled);Assert.Equal(color,w.Model.Style.Color);Assert.False(w.IsVisible);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void ActiveColorUsesBrandPurpleOutline()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
            w.Model.Tool=EditorTool.Arrow;typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);
            var buttons=((StackPanel)w.FindName("ColorControls")).Children.OfType<Button>().ToArray();
            foreach(var selected in buttons)
            {
                typeof(EditorWindow).GetMethod("ColorClick",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,[selected,new RoutedEventArgs()]);
                foreach(var button in buttons){var active=button==selected;Assert.Equal(active?"#FF923C83":"#FFDCE2E7",button.BorderBrush.ToString());Assert.Equal(new Thickness(active?3:1),button.BorderThickness);}
                Assert.Equal((string)selected.Tag,((TextBlock)w.FindName("CurrentColorText")).Text);
                var swatch=Assert.IsType<DrawingImage>(((RibbonMenuButton)w.FindName("ColorMenuButton")).SmallImageSource);
                Assert.Equal("#FF"+((string)selected.Tag)[1..],Assert.IsType<GeometryDrawing>(swatch.Drawing).Brush.ToString());
            }
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void HistoryToggleUsesRibbonHistoryIcon()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try{w.UpdateLayout();var button=(Button)w.FindName("HistoryToggleButton");Assert.NotNull(button);var image=Assert.IsType<Image>(button.Content);var ribbon=(System.Windows.Controls.Ribbon.RibbonButton)w.FindName("RibbonHistory");Assert.Same(ribbon.SmallImageSource,image.Source);}
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData(AnnotationKind.Arrow,"",false)] [InlineData(AnnotationKind.Rectangle,"",false)] [InlineData(AnnotationKind.Mosaic,"",false)] [InlineData(AnnotationKind.Text,"",true)] [InlineData(AnnotationKind.StepMarker,"",true)] [InlineData(AnnotationKind.Sticker,"check-circle",false)] [InlineData(AnnotationKind.Sticker,"key-enter",true)]
    public void ContentEditEnabledOnlyForEditableSelection(AnnotationKind kind,string stickerId,bool enabled)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            var button=(System.Windows.Controls.Ribbon.RibbonButton)w.FindName("EditButton");Assert.False(button.IsEnabled);
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
            var a=new Annotation{Kind=kind,StickerId=stickerId};w.Model.SelectedId=a.Id;w.Model.History!.Add(a);Assert.Equal(enabled,button.IsEnabled);
            w.Model.SelectedId=null;typeof(EditorWindow).GetMethod("UpdateCommands",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,null);Assert.False(button.IsEnabled);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData(EditorTool.Arrow,AnnotationKind.Arrow)] [InlineData(EditorTool.Rectangle,AnnotationKind.Rectangle)] [InlineData(EditorTool.Mosaic,AnnotationKind.Mosaic)] [InlineData(EditorTool.Number,AnnotationKind.StepMarker)] [InlineData(EditorTool.Sticker,AnnotationKind.Sticker)] [InlineData(EditorTool.Text,AnnotationKind.Text)]
    public void ShapeModeClickSelectsExistingShapeWithoutCreatingOne(EditorTool tool,AnnotationKind kind)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Show();w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});w.UpdateLayout();
            var canvas=(EditorCanvas)((Grid)w.FindName("CanvasHost")).Children[0];var p=Mouse.GetPosition(canvas);w.Model.Tool=tool;
            var a=new Annotation{Kind=kind,X=p.X/w.Model.Zoom-20,Y=p.Y/w.Model.Zoom-20,Width=40,Height=kind==AnnotationKind.Arrow?40:80};
            if(kind==AnnotationKind.Sticker){var sticker=StickerCatalog.Load().First(x=>x.Id=="check-circle");a=a with{AssetHash=w.Model.Assets.AddPng(StickerCatalog.CreatePng(sticker)),StickerId=sticker.Id};}
            w.Model.History!.Add(a);w.Model.SelectedId=null;
            canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
            Assert.Equal(a.Id,w.Model.SelectedId);
            canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
            Assert.Single(w.Model.Document!.Annotations);Assert.Equal(a.Id,w.Model.Selected!.Id);Assert.Equal(a.Width,w.Model.Selected.Width);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Theory] [InlineData(EditorTool.Arrow,AnnotationKind.Arrow)] [InlineData(EditorTool.Rectangle,AnnotationKind.Rectangle)] [InlineData(EditorTool.Number,AnnotationKind.StepMarker)] [InlineData(EditorTool.Text,AnnotationKind.Text)]
    public void ShapeModeColorEditsSelectedShapeAndUpdatesIndicator(EditorTool tool,AnnotationKind kind)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
            var a=new Annotation{Kind=kind,Width=50,Style=new(){Thickness=8}};w.Model.Tool=tool;w.Model.SelectedId=a.Id;w.Model.History!.Add(a);
            typeof(EditorWindow).GetMethod("ColorClick",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,[new Button{Tag="#139887"},new RoutedEventArgs()]);
            Assert.Equal("#139887",w.Model.Selected!.Style.Color);Assert.Equal(8,w.Model.Selected.Style.Thickness);
            Assert.Equal("#139887",((TextBlock)w.FindName("CurrentColorText")).Text);
            var thickness=(RibbonMenuButton)w.FindName("ThicknessMenuButton");Assert.Equal("두께 8px",thickness.Label);
            w.Model.History.Undo();Assert.Equal(a.Style,w.Model.Selected.Style);
            typeof(EditorWindow).GetMethod("ThicknessChanged",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,[w,new RoutedPropertyChangedEventArgs<double>(8,5)]);
            Assert.Equal(5,w.Model.Selected.Style.Thickness);Assert.Equal(a.Style.Color,w.Model.Selected.Style.Color);
            Assert.Equal("두께 5px",thickness.Label);w.Model.History.Undo();Assert.Equal("두께 8px",thickness.Label);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void TextDialogPlacesApplyBeforeCancel()=>Sta.Run(()=>
    {
        var w=new TextAnnotationEditor("sample");try{var buttons=(StackPanel)w.FindName("TextEditorButtons");Assert.Equal("적용",((Button)buttons.Children[0]).Content);Assert.Equal("취소",((Button)buttons.Children[1]).Content);Assert.True(((Button)buttons.Children[1]).IsCancel);}finally{w.Close();}
    });
    [Fact] public void MosaicModeEditsSelectedBlockSizeAndSupportsUndo()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try{w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});w.Model.Tool=EditorTool.Mosaic;var a=new Annotation{Kind=AnnotationKind.Mosaic,Width=40,Height=40,MosaicBlockSize=20};w.Model.SelectedId=a.Id;w.Model.History!.Add(a);var slider=(Slider)w.FindName("MosaicSlider");Assert.Equal(20,slider.Value);slider.Value=30;Assert.Equal(30,w.Model.Selected!.MosaicBlockSize);w.Model.History.Undo();Assert.Equal(20,w.Model.Selected.MosaicBlockSize);}
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
