using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 전체 동작 점검에서 확인된 결함의 회귀 테스트다. 창이나 OS 입력을 사용하지 않는다.
public class ConsistencyFixTests
{
    private sealed class Source : PresentationSource
    {public override Visual RootVisual {get;set;}=new DrawingVisual();public override bool IsDisposed=>false;protected override CompositionTarget GetCompositionTargetCore()=>null!;}
    private static CaptureRecord Record()=>new(){Width=100,Height=100,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(100,100))};
    private static void Field(EditorCanvas canvas,string name,object value)=>typeof(EditorCanvas).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(canvas,value);
    private static object? Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,args);
    private static void Pump(Task task)
    {var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    [Fact] public void DeleteDuringDragCancelsTheGestureBeforeRemovingTheAnnotation()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(Record());var a=new Annotation{Kind=AnnotationKind.Rectangle,X=10,Y=10};model.History!.Add(a);model.SelectedId=a.Id;
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();model.Changed+=(_,_)=>canvas.Refresh();
        Field(canvas,"before",a);Field(canvas,"preview",a with{X=20});Field(canvas,"dragging",true);
        Call(canvas,"OnKeyDown",new KeyEventArgs(Keyboard.PrimaryDevice,new Source(),0,Key.Delete){RoutedEvent=Keyboard.KeyDownEvent});
        Assert.Empty(model.Document!.Annotations);Assert.Null(typeof(EditorCanvas).GetField("preview",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(canvas));
        Call(canvas,"OnMouseLeftButtonUp",new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
        Assert.Empty(model.Document.Annotations);Assert.Null(model.SelectedId);model.History.Undo();Assert.Equal(a,Assert.Single(model.Document.Annotations));
    });
    private static byte[] Pixel(BitmapSource bitmap,int x,int y)
    {var bytes=new byte[4];bitmap.CopyPixels(new Int32Rect(x,y,1,1),bytes,4,0);return bytes;}
    [Fact] public void DragPreviewAndCommittedCompositeHaveTheSameStackingOrder()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(Record());
        var old=new Annotation{Kind=AnnotationKind.StepMarker,X=10,Y=10,Width=40,Height=40,Style=new(){Fill="#FF0000"}};
        var newer=old with{Id=Guid.NewGuid(),X=30,Style=new(){Fill="#0000FF"}};model.History!.Add(old);model.History.Add(newer);
        var moved=old with{X=20};var canvas=new EditorCanvas{Model=model};canvas.Measure(new Size(100,100));canvas.Arrange(new Rect(0,0,100,100));Field(canvas,"preview",moved);
        var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())Call(canvas,"OnRender",drawing);
        var preview=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32);preview.Render(visual);
        model.History.Replace(moved);var committed=model.Composite()!;
        Assert.Equal(Pixel(committed,50,30),Pixel(preview,50,30));
        Assert.Equal(new byte[]{255,0,0,255},Pixel(committed,50,30));
    });
    [Fact] public void ChangedInactiveHistoryGetsANewThumbnail()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-audit-"+Guid.NewGuid().ToString("N"));var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
        var window=new EditorWindow(store,new SettingsService(path));
        try
        {
            var first=Record();var other=Record();Pump(window.OpenNewAsync(first));Pump(window.OpenNewAsync(other));
            var list=(ListBox)window.FindName("HistoryList");var before=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==first.Id);
            store.SaveAsync(first with{ResizeSize=new(100,50)}).GetAwaiter().GetResult();
            Pump((Task)Call(window,"RefreshLibraryAsync")!);
            var after=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==first.Id);
            Assert.Equal(50,after.Item.Height);Assert.NotSame(before.Thumbnail,after.Thumbnail);Assert.Equal(50,after.Thumbnail!.PixelHeight);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
    [Fact] public void LockedSettingsReadReportsTheErrorAndReleasesBusy()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-settings-audit-"+Guid.NewGuid().ToString("N"));var settings=new SettingsService(path);
        try
        {
            settings.Save(new());using var locked=new FileStream(Path.Combine(path,"settings.json"),FileMode.Open,FileAccess.Read,FileShare.None);
            var busy=false;Exception? reported=null;
            SnapBerry.App.Shell.AppHost.RunSettings(()=>settings.Load(),value=>busy=value,error=>reported=error);
            Assert.IsAssignableFrom<IOException>(reported);Assert.False(busy);
        }
        finally{TestFiles.DeleteDirectory(path);}
    }
    [Theory] [InlineData(2,48,"Resize text")] [InlineData(.1,2.4,"Resize text")] [InlineData(.001,1,"Resize text")] [InlineData(.001,1,"i")] [InlineData(1000,4096,"Resize text")]
    public void TextResizeMeasuresBothBoundsAndUsesTheInputFontLimits(double ratio,double fontSize,string value)=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(Record());var text=new Annotation{Kind=AnnotationKind.Text,Text=value,Style=new(){FontSize=24}};
        model.History!.Add(text);model.UpdateText(text.Id,text.Text);text=model.Document!.Annotations[0];
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();Field(canvas,"before",text);Field(canvas,"preview",text);Field(canvas,"dragging",true);Field(canvas,"resizing",true);
        var move=typeof(EditorCanvas).GetMethod("MoveAnnotation",BindingFlags.Instance|BindingFlags.NonPublic);Assert.NotNull(move);
        move.Invoke(canvas,[new PixelPoint(text.X+text.Width*ratio,text.Y+text.Height)]);
        var resized=(Annotation)typeof(EditorCanvas).GetField("preview",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(canvas)!;
        var measured=new FormattedText(text.Text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),fontSize,Brushes.Black,1);
        Assert.Equal(fontSize,resized.Style.FontSize,8);Assert.Equal(Math.Max(1,measured.Width),resized.Width,8);Assert.Equal(Math.Max(1,measured.Height),resized.Height,8);
        Call(canvas,"OnMouseLeftButtonUp",new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
        Assert.Equal(resized,model.Document.Annotations[0]);model.History.Undo();Assert.Equal(text,model.Document.Annotations[0]);model.History.Redo();Assert.Equal(resized,model.Document.Annotations[0]);
    });
    [Fact] public void ShortExistingTextCanBeMovedAtTheMinimumFontSize()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(Record());var text=new Annotation{Kind=AnnotationKind.Text,Text="i",Width=1,Height=1.2,Style=new(){FontSize=1}};
        model.History!.Add(text);var moved=text with{X=20};var canvas=new EditorCanvas{Model=model};canvas.Refresh();
        Field(canvas,"before",text);Field(canvas,"preview",moved);Field(canvas,"dragging",true);
        Call(canvas,"OnMouseLeftButtonUp",new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
        Assert.Equal(moved,Assert.Single(model.Document!.Annotations));model.History.Undo();Assert.Equal(text,Assert.Single(model.Document.Annotations));
    });
    [Fact] public void SameSizeExternalEditsRefreshTheThumbnailAndUnchangedFilesReuseIt()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-cache-"+Guid.NewGuid().ToString("N"));var directory=Path.Combine(path,"Library");var store=new CaptureLibraryStore(directory);
        var window=new EditorWindow(store,new SettingsService(path));
        try
        {
            var record=Record();Pump(window.OpenNewAsync(record));Pump(window.OpenNewAsync(Record()));
            var list=(ListBox)window.FindName("HistoryList");CaptureEntry Entry()=>list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==record.Id);
            var original=Entry();
            new CaptureLibraryStore(directory).SaveAsync(record with{Annotations=[new(){Kind=AnnotationKind.StepMarker,X=30,Y=30,Width=40,Height=40,Style=new(){Fill="#FF0000"}}]}).GetAwaiter().GetResult();
            Pump((Task)Call(window,"RefreshLibraryAsync")!);var updated=Entry();
            Assert.Equal(original.Item.Width,updated.Item.Width);Assert.Equal(original.Item.Height,updated.Item.Height);
            Assert.NotSame(original.Thumbnail,updated.Thumbnail);Assert.Equal(new byte[]{0,0,255,255},Pixel(updated.Thumbnail!,50,50));
            Pump((Task)Call(window,"RefreshLibraryAsync")!);Assert.Same(updated.Thumbnail,Entry().Thumbnail);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
