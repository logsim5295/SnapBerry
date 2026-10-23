using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapBerry.App.Editor;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.App.Export;
using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 도구 전환·마우스 캡처 상실·취소 후 미완료 제스처가 남지 않는지 검증한다.
public class GestureRegressionTests
{
    [Fact] public void DocumentSwitchCancelsActiveGesture()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            window.Show();var record=new CaptureRecord{Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))};window.Model.Open(record);
            window.Model.History!.Add(new(){Kind=AnnotationKind.Rectangle,X=-50000,Y=-50000,Width=100000,Height=100000});
            var canvas=(EditorCanvas)((Grid)window.FindName("CanvasHost")).Children[0];
            canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
            window.Model.Open(record with{Id=Guid.NewGuid(),Annotations=[]});
            canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
            Assert.Empty(window.Model.Document!.Annotations);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
