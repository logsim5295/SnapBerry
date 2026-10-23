using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 글자 크기 직접 입력·검증·포커스 확정과 실행 취소 시 입력값 동기화를 검증한다.
public class EditableFontSizeTests
{
    [Theory]
    [InlineData("37.5",37.5)]
    [InlineData("1",1)]
    [InlineData("4096",4096)]
    [InlineData("0",24)]
    [InlineData("4097",24)]
    [InlineData("NaN",24)]
    [InlineData("abc",24)]
    [InlineData("",24)]
    public void TypedSizeAppliesOnEnterAndRejectsInvalidValues(string text,double expected)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        var w=new EditorWindow(new(Path.Combine(path,"Library")),new SnapBerry.App.Settings.SettingsService(path));
        try
        {
            w.Model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
            w.Model.Tool=EditorTool.Text;w.Model.Style=new(){FontSize=24};
            typeof(EditorWindow).GetMethod("UpdateProperties",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,null);w.Show();
            var box=(ComboBox)w.FindName("FontSizeBox");Assert.True(box.IsEditable);
            box.ApplyTemplate();Assert.NotNull(box.Template.FindName("PART_EditableTextBox",box));
            box.Text=text;
            box.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(box),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
            Assert.Equal(expected,w.Model.Style.FontSize);
            Assert.Equal(expected.ToString(System.Globalization.CultureInfo.CurrentCulture),box.Text);
            var a=new Annotation{Kind=AnnotationKind.Text,Text="sample",Style=new(){FontSize=37.5}};
            w.Model.SelectedId=a.Id;w.Model.History!.Add(a);
            Assert.Equal(37.5.ToString(System.Globalization.CultureInfo.CurrentCulture),box.Text);
            box.Text="42";
            box.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,box,null){RoutedEvent=Keyboard.LostKeyboardFocusEvent});
            Assert.Equal(42,w.Model.Selected!.Style.FontSize);
            w.Model.History.Undo();Assert.Equal(37.5,w.Model.Selected.Style.FontSize);
            Assert.Equal(37.5.ToString(System.Globalization.CultureInfo.CurrentCulture),box.Text);
            box.SelectedIndex=2;Assert.Equal(32,w.Model.Selected.Style.FontSize);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
