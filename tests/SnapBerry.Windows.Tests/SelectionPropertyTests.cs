using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 선택 종류별 속성 표시와 활성화, 선택 해제 후 숨김 및 비활성 입력 무시를 검증한다.
public class SelectionPropertyTests
{
    [Fact]
    public void DisabledControlsCannotChangeDefaultStyle()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            var before=w.Model.Style;
            var button=((StackPanel)w.FindName("ColorControls")).Children.OfType<Button>().Last();
            typeof(EditorWindow).GetMethod("ColorClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,[button,new RoutedEventArgs()]);
            ((Slider)w.FindName("ThicknessSlider")).Value=12;
            Assert.Equal(before,w.Model.Style);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory]
    [InlineData(null,false,false)]
    [InlineData(AnnotationKind.Arrow,true,true)]
    [InlineData(AnnotationKind.Rectangle,true,true)]
    [InlineData(AnnotationKind.Text,true,false)]
    [InlineData(AnnotationKind.StepMarker,true,false)]
    [InlineData(AnnotationKind.Sticker,false,false)]
    [InlineData(AnnotationKind.Mosaic,false,false)]
    public void SelectionEnablesOnlySupportedProperties(AnnotationKind? kind,bool color,bool thickness)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        var w=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            Assert.False(((StackPanel)w.FindName("ColorControls")).IsEnabled);
            Assert.False(((Button)w.FindName("ColorPickerButton")).IsEnabled);
            Assert.False(((Slider)w.FindName("ThicknessSlider")).IsEnabled);
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
            if(kind is AnnotationKind value)
            {
                var a=new Annotation{Kind=value,Text="sample"};
                w.Model.SelectedId=a.Id;w.Model.History!.Add(a);
            }
            Assert.Equal(color,((StackPanel)w.FindName("ColorControls")).IsEnabled);
            Assert.Equal(color,((Button)w.FindName("ColorPickerButton")).IsEnabled);
            Assert.Equal(thickness,((Slider)w.FindName("ThicknessSlider")).IsEnabled);
            Assert.Equal(color?Visibility.Visible:Visibility.Collapsed,((FrameworkElement)w.FindName("ColorControls")).Visibility);
            Assert.Equal(color?Visibility.Visible:Visibility.Collapsed,((FrameworkElement)w.FindName("CurrentColorControls")).Visibility);
            Assert.Equal(thickness?Visibility.Visible:Visibility.Collapsed,((FrameworkElement)w.FindName("ThicknessControls")).Visibility);
            w.Model.SelectedId=null;
            typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,null);
            Assert.False(((Button)w.FindName("ColorPickerButton")).IsEnabled);
            Assert.False(((Slider)w.FindName("ThicknessSlider")).IsEnabled);
            foreach(var name in new[]{"ColorControls","CurrentColorControls","ThicknessControls","LineStyleControls","FontControls","MosaicControls","NumberControls","NumberTextColorButton","StickerChooseButton"})
                Assert.Equal(Visibility.Collapsed,((FrameworkElement)w.FindName(name)).Visibility);
            w.Model.Tool=EditorTool.Arrow;
            typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,null);
            Assert.True(((Button)w.FindName("ColorPickerButton")).IsEnabled);
            Assert.True(((Slider)w.FindName("ThicknessSlider")).IsEnabled);
            Assert.Equal(Visibility.Visible,((FrameworkElement)w.FindName("ColorControls")).Visibility);
            Assert.Equal(Visibility.Visible,((FrameworkElement)w.FindName("ThicknessControls")).Visibility);
            w.SetBusy(true);Assert.False(((Button)w.FindName("ColorPickerButton")).IsEnabled);
            w.SetBusy(false);Assert.True(((Button)w.FindName("ColorPickerButton")).IsEnabled);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
