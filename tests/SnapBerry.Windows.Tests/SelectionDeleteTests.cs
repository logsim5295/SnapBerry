using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 창 표시나 키 입력 없이 포커스 위치에 따른 선택 개체 삭제를 검증한다.
public class SelectionDeleteTests
{
    private sealed class Source : PresentationSource
    {
        public override Visual RootVisual {get;set;}=null!;
        protected override CompositionTarget GetCompositionTargetCore()=>null!;
        public override bool IsDisposed=>false;
    }
    private static KeyEventArgs Delete(UIElement source)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,new Source(),0,Key.Delete){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        source.RaiseEvent(args);return args;
    }
    private static void WithWindow(Action<EditorWindow> test)
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-delete-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            window.Model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,80))});
            test(window);Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    }

    [Theory]
    [InlineData(EditorTool.Arrow)] [InlineData(EditorTool.Text)] [InlineData(EditorTool.Rectangle)]
    [InlineData(EditorTool.Mosaic)] [InlineData(EditorTool.Number)] [InlineData(EditorTool.Sticker)]
    [InlineData(EditorTool.MagicWand)] [InlineData(EditorTool.Crop)]
    public void EscapeReturnsToSelectWithoutEditingTheDocument(EditorTool tool)=>Sta.Run(()=>WithWindow(window=>
    {
        var model=window.Model;var token=model.History!.CurrentStateToken;
        var buttons=((ItemsControl)window.FindName("ToolsPanel")).Items.OfType<System.Windows.Controls.Primitives.ToggleButton>().ToArray();
        // 스티커 창을 띄우지 않고 편집기 쪽 단축키만 검증한다.
        if(tool==EditorTool.Sticker)model.Tool=tool;
        else buttons.Single(button=>(string)button.Tag==tool.ToString()).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(tool==EditorTool.MagicWand)model.SelectMagicWand(new(1,1),0,false);
        var canvas=(EditorCanvas)((Grid)window.FindName("CanvasHost")).Children[0];
        canvas.BeginShape(new(1,1));
        if(tool==EditorTool.Crop){Assert.True(canvas.BeginCrop(new(0,0)));canvas.MoveCrop(new(10,10));}
        var key=new KeyEventArgs(Keyboard.PrimaryDevice,new Source(),0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};
        canvas.RaiseEvent(key);
        Assert.True(key.Handled);Assert.Equal(EditorTool.Select,model.Tool);
        Assert.Equal("Select",Assert.Single(buttons,button=>button.IsChecked==true).Tag);
        canvas.FinishCrop(new(10,10));
        Assert.Null(canvas.ShapePreview);Assert.False(model.HasPixelSelection);Assert.Equal(token,model.History.CurrentStateToken);
    }));

    [Theory]
    [InlineData(AnnotationKind.Arrow)] [InlineData(AnnotationKind.Rectangle)]
    [InlineData(AnnotationKind.Text)] [InlineData(AnnotationKind.StepMarker)]
    [InlineData(AnnotationKind.Mosaic)] [InlineData(AnnotationKind.Sticker)]
    public void DeleteRemovesOnlyTheSelectedObjectFromCanvasRibbonAndProperties(AnnotationKind kind)=>Sta.Run(()=>WithWindow(window=>
    {
        var model=window.Model;var other=new Annotation{Kind=AnnotationKind.Rectangle,X=1,Y=1,Width=10,Height=10};
        model.History!.Add(other);
        if(kind==AnnotationKind.Sticker)model.InsertImage(PngFileWriter.Encode(RenderingTests.Image(20,10)));
        else
        {
            var selected=new Annotation{Kind=kind,X=20,Y=20,Width=30,Height=20,Text=kind==AnnotationKind.StepMarker?"1":"text"};
            model.History.Add(selected);model.SelectedId=selected.Id;
        }
        var selectedObject=model.Selected!;
        var canvas=(EditorCanvas)((Grid)window.FindName("CanvasHost")).Children[0];
        foreach(var source in new UIElement[]{canvas,(UIElement)window.FindName("PasteImageButton"),(UIElement)window.FindName("ThicknessSlider"),window})
        {
            model.SelectedId=selectedObject.Id;
            Assert.True(Delete(source).Handled);
            Assert.Equal(other,Assert.Single(model.Document!.Annotations));Assert.Null(model.SelectedId);
            model.History.Undo();Assert.Equal(selectedObject,model.Document.Annotations.Last());
            model.History.Redo();Assert.Equal(other,Assert.Single(model.Document.Annotations));
            model.History.Undo();
        }
    }));

    [Theory] [InlineData("text")] [InlineData("combo")] [InlineData("password")] [InlineData("history")]
    public void DeleteLeavesTheSelectedObjectAloneInTextInputsAndHistory(string sourceKind)=>Sta.Run(()=>WithWindow(window=>
    {
        var selected=new Annotation{Kind=AnnotationKind.Text,Text="keep",Width=40,Height=20};
        window.Model.History!.Add(selected);window.Model.SelectedId=selected.Id;
        UIElement source=sourceKind switch
        {
            "text"=>(TextBox)window.FindName("NextNumberBox"),
            "combo"=>(ComboBox)window.FindName("FontSizeBox"),
            "history"=>(ListBox)window.FindName("HistoryList"),
            _=>new PasswordBox()
        };
        if(sourceKind=="password")((Grid)window.FindName("CanvasHost")).Children.Add(source);
        var token=window.Model.History.CurrentStateToken;
        Assert.False(Delete(source).Handled);Assert.Equal(selected,Assert.Single(window.Model.Document!.Annotations));
        Assert.Equal(token,window.Model.History.CurrentStateToken);Assert.Equal(selected.Id,window.Model.SelectedId);
    }));

    [Theory] [InlineData(false)] [InlineData(true)]
    public void DeleteDoesNothingWhileBusyOrWithoutASelection(bool busy)=>Sta.Run(()=>WithWindow(window=>
    {
        var selected=new Annotation{Kind=AnnotationKind.Arrow,Width=40,Height=20};
        window.Model.History!.Add(selected);window.Model.SelectedId=busy?selected.Id:null;window.SetBusy(busy);
        Assert.False(Delete(window).Handled);Assert.Equal(selected,Assert.Single(window.Model.Document!.Annotations));
        window.SetBusy(false);
    }));
}
