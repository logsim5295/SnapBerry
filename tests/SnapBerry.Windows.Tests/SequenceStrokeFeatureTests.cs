using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Rendering;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 대문자 번호 UI와 다섯 선 모양의 편집·저장·합성 및 화살촉 채움을 검증한다.
public class SequenceStrokeFeatureTests
{
    private static void Tool(EditorWindow w,EditorTool tool)=>((ItemsControl)w.FindName("ToolsPanel")).Items.OfType<ToggleButton>().Single(x=>(string)x.Tag==tool.ToString()).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    private static CaptureRecord Record()=>new(){Width=240,Height=120,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(240,120))};

    [Fact] public void UppercaseComboAndNextValueContinueThroughZ()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SnapBerry.App.Settings.SettingsService(path));
        try
        {
            w.Model.Open(Record());w.Show();Tool(w,EditorTool.Number);
            var mode=(ComboBox)w.FindName("NumberModeBox");Assert.Equal(3,mode.Items.Count);mode.SelectedIndex=2;
            var next=(TextBox)w.FindName("NextNumberBox");Assert.Equal("A",next.Text);next.Text="Z";
            next.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(next),0,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});
            w.Model.History!.InsertStep(new(20,20),new());Assert.Equal("Z",w.Model.Document!.Annotations.Last().Text);Assert.Equal("AA",next.Text);
            w.Model.History.InsertStep(new(80,20),new());Assert.Equal("AA",w.Model.Document.Annotations.Last().Text);
            var id=w.Model.Document.Annotations[0].Id;w.Model.UpdateText(id,"A");Assert.Equal("B",next.Text);Assert.Equal(2,mode.SelectedIndex);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(EditorTool.Select,AnnotationKind.Arrow)] [InlineData(EditorTool.Select,AnnotationKind.Rectangle)] [InlineData(EditorTool.Arrow,AnnotationKind.Arrow)] [InlineData(EditorTool.Rectangle,AnnotationKind.Rectangle)]
    public void ComboEditsSelectedStrokeAndUndoRestoresIt(EditorTool tool,AnnotationKind kind)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SnapBerry.App.Settings.SettingsService(path));
        try
        {
            w.Model.Open(Record());Tool(w,tool);
            var a=new Annotation{Kind=kind,X=20,Y=20,Width=100,Height=50};w.Model.SelectedId=a.Id;w.Model.History!.Add(a);
            var box=(ComboBox)w.FindName("LineStyleBox");Assert.Equal(5,box.Items.Count);Assert.True(box.IsEnabled);
            Assert.All(box.Items.OfType<ComboBoxItem>(),item=>Assert.NotNull(((StackPanel)item.Content).Children.OfType<Image>().Single().Source));
            box.SelectedIndex=4;Assert.Equal(StrokePattern.DashDotDot,w.Model.Selected!.Style.LineStyle);
            w.Model.History.Undo();Assert.Equal(StrokePattern.Solid,w.Model.Selected.Style.LineStyle);Assert.Equal(0,box.SelectedIndex);
            w.Model.History.Redo();Assert.Equal(4,box.SelectedIndex);
            Tool(w,EditorTool.Text);Assert.Equal(Visibility.Collapsed,((FrameworkElement)w.FindName("LineStyleControls")).Visibility);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(EditorTool.Arrow,AnnotationKind.Arrow)] [InlineData(EditorTool.Rectangle,AnnotationKind.Rectangle)]
    public void DrawingModeSetsLineStyleForNewShapes(EditorTool tool,AnnotationKind kind)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));var w=new EditorWindow(new(Path.Combine(path,"Library")),new SnapBerry.App.Settings.SettingsService(path));
        try
        {
            w.Model.Open(Record());w.Show();Tool(w,tool);((ComboBox)w.FindName("LineStyleBox")).SelectedIndex=1;
            Assert.Equal(StrokePattern.Dash,w.Model.Style.LineStyle);Assert.False(w.Model.History!.CanUndo);
            var placement=new ShapePlacement();placement.PointerDown(new(10,10),kind,w.Model.Style);
            Assert.Equal(StrokePattern.Dash,placement.PointerUp(new(100,60))!.Style.LineStyle);
        }
        finally{w.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(AnnotationKind.Arrow)] [InlineData(AnnotationKind.Rectangle)]
    public void AllPatternsRenderDifferentlyAndArrowHeadStaysFilled(AnnotationKind kind)=>Sta.Run(()=>
    {
        var hashes=new HashSet<string>();
        foreach(var pattern in Enum.GetValues<StrokePattern>())
        {
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())CompositeRenderer.DrawAnnotation(dc,new(){Kind=kind,X=10,Y=kind==AnnotationKind.Arrow?30:10,Width=200,Height=kind==AnnotationKind.Arrow?0:40,Style=new(){LineStyle=pattern,Thickness=3}},new());
            var bitmap=new RenderTargetBitmap(240,60,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
            var bytes=PngFileWriter.Encode(bitmap);Assert.True(hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))),pattern.ToString());
            var pixels=new byte[240*60*4];bitmap.CopyPixels(pixels,240*4,0);var y=kind==AnnotationKind.Arrow?30:10;
            var count=Enumerable.Range(20,160).Count(x=>pixels[(y*240+x)*4+3]>100);Assert.True(count>0);
            if(pattern==StrokePattern.Solid)Assert.Equal(160,count);else Assert.True(count<160,pattern.ToString());
            if(kind==AnnotationKind.Arrow)Assert.True(pixels[(30*240+206)*4+3]>200);
            var artifacts=Environment.GetEnvironmentVariable("SNAPBERRY_TEST_ARTIFACTS");if(artifacts is not null){Directory.CreateDirectory(artifacts);File.WriteAllBytes(Path.Combine(artifacts,$"line-{kind}-{pattern}.png"),bytes);}
        }
    });

    [Fact] public void UppercaseCountersAndStrokeStylesSurviveHistoryReload()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        try
        {
            var record=Record() with{Sequence=new(){Mode=StepSequenceMode.UppercaseAlphabetic,NextUppercaseAlphabetic=27,NextNumeric=3,NextAlphabetic=4},Annotations=[new(){Kind=AnnotationKind.Arrow,Style=new(){LineStyle=StrokePattern.DashDotDot}}]};
            var store=new CaptureLibraryStore(path);store.SaveAsync(record).GetAwaiter().GetResult();var loaded=store.LoadAsync(record.Id).GetAwaiter().GetResult();
            Assert.Equal(StrokePattern.DashDotDot,loaded.Annotations[0].Style.LineStyle);
            var vm=new EditorViewModel();vm.Open(loaded);vm.History!.InsertStep(new(20,20),new());Assert.Equal("AA",vm.Document!.Annotations.Last().Text);
            Assert.Equal((3L,4L,28L),(vm.Document.Sequence.NextNumeric,vm.Document.Sequence.NextAlphabetic,vm.Document.Sequence.NextUppercaseAlphabetic));
            Assert.Throws<InvalidDataException>(()=>store.SaveAsync(record with{Sequence=record.Sequence with{NextUppercaseAlphabetic=0}}).GetAwaiter().GetResult());
            Assert.Throws<InvalidDataException>(()=>store.SaveAsync(record with{Annotations=[record.Annotations[0] with{Style=new(){LineStyle=(StrokePattern)999}}]}).GetAwaiter().GetResult());
            Assert.Equal(27,store.LoadAsync(record.Id).GetAwaiter().GetResult().Sequence.NextUppercaseAlphabetic);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
}
