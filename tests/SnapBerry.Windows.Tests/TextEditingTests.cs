using System.Windows;
using System.Windows.Input;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.Core;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 텍스트 도구의 기존 객체 선택과 더블클릭 내용 편집 및 크기 변경을 검증한다.
public class TextEditingTests
{
    [Theory] [InlineData(true)] [InlineData(false)]
    public void TextDialogInputUsesPurpleBorderAndFocusWithoutLosingContent(bool multiline)=>Sta.Run(()=>
    {
        var value=multiline?"Existing text\nSecond line":"4";var w=new TextAnnotationEditor(value,multiline);
        try
        {
            w.Show();w.Activate();w.UpdateLayout();var input=(System.Windows.Controls.TextBox)w.FindName("TextEditorInput");
            Keyboard.Focus(input);w.UpdateLayout();var chrome=Assert.IsType<System.Windows.Controls.Border>(input.Template.FindName("TextInputChrome",input));Assert.Equal("#FF923C83",chrome.BorderBrush.ToString());Assert.Equal("#FFF5EAF3",chrome.Background.ToString());Assert.Equal("#FF923C83",input.CaretBrush.ToString());Assert.Equal(value,w.Value);Assert.Equal(multiline,input.AcceptsReturn);
            var buttons=(System.Windows.Controls.StackPanel)w.FindName("TextEditorButtons");Keyboard.Focus((System.Windows.Controls.Button)buttons.Children[1]);w.UpdateLayout();Assert.Equal("#FFFFFFFF",chrome.Background.ToString());Assert.Equal("#FF923C83",chrome.BorderBrush.ToString());Assert.Equal(value,w.Value);
        }
        finally{w.Close();}
    });
    [Theory] [InlineData(1)] [InlineData(2)]
    public void ExistingTextSelectsOnSingleClickAndEditsOnDoubleClick(int clickCount)=>Sta.Run(()=>
    {
        var model=new EditorViewModel{Tool=EditorTool.Text};model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});
        var a=new Annotation{Kind=AnnotationKind.Text,X=-50000,Y=-50000,Width=100000,Height=100000,Text="Existing text\nSecond line"};model.History!.Add(a);var canvas=new EditorCanvas{Model=model};Annotation? requested=null;var called=false;canvas.TextRequested+=(_,value)=>{called=true;requested=value;};
        var e=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent};typeof(MouseButtonEventArgs).GetProperty("ClickCount")!.SetValue(e,clickCount);canvas.RaiseEvent(e);Assert.Equal(a.Id,model.SelectedId);Assert.Equal(clickCount==2,called);
        if(clickCount==2){Assert.Equal(a,requested);var dialog=new TextAnnotationEditor(requested!.Text);try{Assert.Equal(a.Text,dialog.Value);}finally{dialog.Close();}}canvas.CancelGesture();Assert.Single(model.Document!.Annotations);
    });
    [Fact] public void ClickingEmptyAreaStillRequestsNewText()=>Sta.Run(()=>
    {
        var model=new EditorViewModel{Tool=EditorTool.Text};model.Open(new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))});var canvas=new EditorCanvas{Model=model};var called=false;canvas.TextRequested+=(_,value)=>{called=true;Assert.Null(value);};canvas.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});Assert.True(called);
    });
}
