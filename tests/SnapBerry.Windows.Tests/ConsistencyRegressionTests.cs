using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 창 표시·실제 입력·화면 캡처 없이 편집과 이력의 일관성을 검증한다.
public class ConsistencyRegressionTests
{
    private static CaptureRecord Record(int width=80,int height=60)=>new()
    {Width=width,Height=height,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(width,height))};
    private static string Temp()=>Path.Combine(Path.GetTempPath(),"SnapBerry-consistency-"+Guid.NewGuid().ToString("N"));
    private static void Pump(Task task)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();
        task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));
        Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
    }
    private static void Refresh(EditorWindow window)=>Pump(RefreshWhenIdle(window));
    private static async Task RefreshWhenIdle(EditorWindow window)
    {
        var flag=typeof(EditorWindow).GetField("refreshing",BindingFlags.Instance|BindingFlags.NonPublic)!;
        while((bool)flag.GetValue(window)!)await Task.Delay(10);
        await (Task)typeof(EditorWindow).GetMethod("RefreshLibraryAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null)!;
    }
    private static void WithWindow(Action<EditorWindow,CaptureLibraryStore> action)
    {
        var path=Temp();var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
        var window=new EditorWindow(store,new SettingsService(path));
        try{action(window,store);}
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    }

    [Fact] public void OversizedTextIsRejectedBeforeChangingDocument()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(Record());
        var annotation=new Annotation{Kind=AnnotationKind.Text,Text="before"};model.History!.Add(annotation);
        var token=model.History.CurrentStateToken;
        Assert.Throws<ArgumentException>(()=>model.UpdateText(annotation.Id,new string('x',8193)));
        Assert.Equal(annotation,Assert.Single(model.Document!.Annotations));Assert.Equal(token,model.History.CurrentStateToken);
    });

    [Fact] public void TextEditorBlocksOverLimitValuesAndAcceptsTheStorageBoundary()=>Sta.Run(()=>
    {
        var dialog=new TextAnnotationEditor("");
        try
        {
            var input=(TextBox)dialog.FindName("TextEditorInput");
            var buttons=(StackPanel)dialog.FindName("TextEditorButtons");var apply=(Button)buttons.Children[0];
            input.Text=new string('x',8193);Assert.False(apply.IsEnabled);
            Assert.Contains("8192",((TextBlock)dialog.FindName("TextLengthStatus")).Text);
            input.Text=new string('x',8192);Assert.True(apply.IsEnabled);
            var model=new EditorViewModel();model.Open(Record());var a=new Annotation{Kind=AnnotationKind.Text,Text="x"};model.History!.Add(a);
            model.UpdateText(a.Id,input.Text);var path=Temp();
            try{var store=new CaptureLibraryStore(path);store.SaveAsync(model.Snapshot()).GetAwaiter().GetResult();Assert.Equal(input.Text,store.LoadAsync(model.CaptureId!.Value).GetAwaiter().GetResult().Annotations[0].Text);}
            finally{TestFiles.DeleteDirectory(path);}
        }
        finally{dialog.Close();}
    });

    [Fact] public void NewCaptureSelectsItsOwnHistoryItemAndRefreshPreservesMultiselection()=>Sta.Run(()=>WithWindow((window,store)=>
    {
        var first=Record();Pump(window.OpenNewAsync(first));Refresh(window);
        var list=(ListBox)window.FindName("HistoryList");list.SelectedItem=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==first.Id);
        var second=Record();Pump(window.OpenNewAsync(second));Refresh(window);
        Assert.Equal(second.Id,window.Model.CaptureId);Assert.Equal(second.Id,Assert.IsType<CaptureEntry>(list.SelectedItem).Item.Id);
        list.SelectedItems.Add(list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==first.Id));Refresh(window);
        Assert.Equal(2,list.SelectedItems.Count);Assert.Equal(second.Id,window.Model.CaptureId);
    }));

    [Theory] [InlineData(false)] [InlineData(true)]
    public void SliderDragHasOneUndoStepAndPersistsItsFinalValue(bool mosaic)=>Sta.Run(()=>WithWindow((window,store)=>
    {
        window.Model.Open(Record());var annotation=new Annotation{Kind=mosaic?AnnotationKind.Mosaic:AnnotationKind.Rectangle,MosaicBlockSize=12,Style=new(){Thickness=3}};
        window.Model.SelectedId=annotation.Id;window.Model.History!.Add(annotation);
        var slider=(Slider)window.FindName(mosaic?"MosaicSlider":"ThicknessSlider");
        slider.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});
        for(var i=1;i<=5;i++)slider.Value=(mosaic?12:3)+i;
        slider.RaiseEvent(new DragCompletedEventArgs(0,0,false){RoutedEvent=Thumb.DragCompletedEvent});
        Pump(window.FlushAsync());var saved=store.LoadAsync(window.Model.CaptureId!.Value).GetAwaiter().GetResult().Annotations[0];
        Assert.Equal(mosaic?17:8,mosaic?saved.MosaicBlockSize:saved.Style.Thickness);
        window.Model.History.Undo();Assert.Equal(annotation,window.Model.Selected);
        window.Model.History.Redo();Assert.Equal(mosaic?17:8,mosaic?window.Model.Selected!.MosaicBlockSize:window.Model.Selected!.Style.Thickness);
    }));

    [Fact] public void SelectionMadeWhileHistoryLoadsIsPreserved()=>Sta.Run(()=>WithWindow((window,store)=>
    {
        var first=Record();var second=Record();Pump(window.OpenNewAsync(first));Pump(window.OpenNewAsync(second));Refresh(window);
        var list=(ListBox)window.FindName("HistoryList");var entries=list.Items.Cast<CaptureEntry>().ToArray();
        list.SelectedItem=entries.Single(x=>x.Item.Id==first.Id);
        // 저장소의 기존 직렬화 경계를 잠시 잡아 목록 읽기만 지연한다. 창이나 OS 입력은 사용하지 않는다.
        var gate=typeof(CaptureLibraryStore).GetField("gate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(store)!;
        Task refresh;lock(gate)
        {
            refresh=(Task)typeof(EditorWindow).GetMethod("RefreshLibraryAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null)!;
            list.SelectedItem=entries.Single(x=>x.Item.Id==second.Id);
        }
        Pump(refresh);Assert.Equal(second.Id,Assert.IsType<CaptureEntry>(list.SelectedItem).Item.Id);
    }));

    [Fact] public void SliderDragBackToOriginalDoesNotAddAnUndoStep()=>Sta.Run(()=>WithWindow((window,_)=>
    {
        window.Model.Open(Record());var annotation=new Annotation{Kind=AnnotationKind.Rectangle};window.Model.SelectedId=annotation.Id;window.Model.History!.Add(annotation);
        var token=window.Model.History.CurrentStateToken;var slider=(Slider)window.FindName("ThicknessSlider");
        slider.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});slider.Value=8;slider.Value=3;
        slider.RaiseEvent(new DragCompletedEventArgs(0,0,false){RoutedEvent=Thumb.DragCompletedEvent});
        Assert.Equal(token,window.Model.History.CurrentStateToken);window.Model.History.Undo();Assert.Empty(window.Model.Document!.Annotations);
    }));

    [Fact] public void HistoryThumbnailIsAnIndependentSmallBitmap()=>Sta.Run(()=>WithWindow((window,_)=>
    {
        Pump(window.OpenNewAsync(Record(320,200)));Refresh(window);
        var entry=Assert.Single(((ListBox)window.FindName("HistoryList")).Items.Cast<CaptureEntry>());
        var bitmap=Assert.IsType<RenderTargetBitmap>(entry.Thumbnail);
        Assert.InRange(bitmap.PixelWidth,1,120);Assert.InRange(bitmap.PixelHeight,1,120);Assert.True(bitmap.IsFrozen);
    }));

    [Fact] public void UnchangedHistoryMetadataDoesNotReopenTheArchive()=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var record=Record();var store=new CaptureLibraryStore(path);store.SaveAsync(record).GetAwaiter().GetResult();
            var first=Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult());
            using var locked=new FileStream(Path.Combine(path,record.Id.ToString("N")+".qcapture"),FileMode.Open,FileAccess.Read,FileShare.None);
            var second=Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult());
            Assert.False(second.Damaged);Assert.Equal(first,second);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void MetadataCacheTracksExternalChangesAndDeletion()=>Sta.Run(()=>
    {
        var path=Temp();try
        {
            var record=Record();var store=new CaptureLibraryStore(path);store.SaveAsync(record).GetAwaiter().GetResult();
            Assert.Equal(80,Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult()).Width);
            new CaptureLibraryStore(path).SaveAsync(record with{ResizeSize=new(123,45)}).GetAwaiter().GetResult();
            var file=Path.Combine(path,record.Id.ToString("N")+".qcapture");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddSeconds(2));
            Assert.Equal(123,Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult()).Width);
            File.Delete(file);Assert.Empty(store.ListAsync(0,50).GetAwaiter().GetResult());
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
}
