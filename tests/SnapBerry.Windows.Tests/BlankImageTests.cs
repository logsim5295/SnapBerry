using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Import;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using Xunit;
using CaptureMode=SnapBerry.Core.CaptureMode;

namespace SnapBerry.Windows.Tests;

// 창 표시·실제 캡처·클립보드 없이 새 문서와 실패 시 보존 동작을 검증한다.
public class BlankImageTests
{
    private static void Pump(Task task)
    {var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    private static byte[] Pixels(BitmapSource image)
    {var pixels=new byte[image.PixelWidth*image.PixelHeight*4];new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0).CopyPixels(pixels,image.PixelWidth*4,0);return pixels;}
    private static void WithEditor(Func<BlankImageOptions?> pick,Action<EditorWindow,CaptureLibraryStore,string> action)
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-blank-"+Guid.NewGuid().ToString("N"));
        var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var window=new EditorWindow(store,new SettingsService(path),pickBlankImage:pick);
        try{action(window,store,path);}finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    }

    [Theory] [InlineData(255,255,255,255)] [InlineData(255,12,34,56)] [InlineData(0,0,0,0)]
    public void GeneratedPngAndExportKeepDimensionsAndEveryBackgroundPixel(byte a,byte r,byte g,byte b)=>Sta.Run(()=>
    {
        var record=BlankImageService.Create(new(new(13,7),Color.FromArgb(a,r,g,b)));
        Assert.Equal(CaptureMode.Blank,record.Mode);Assert.Empty(record.Annotations);Assert.Equal((13,7),(record.Width,record.Height));
        var model=new EditorViewModel();model.Open(record);
        foreach(var image in new[]{PngFileWriter.Decode(record.OriginalPng),PngFileWriter.Decode(PngFileWriter.Encode(model.Composite()!))})
        {
            Assert.Equal((13,7),(image.PixelWidth,image.PixelHeight));var pixels=Pixels(image);
            for(var i=0;i<pixels.Length;i+=4)Assert.Equal(new byte[]{b,g,r,a},pixels[i..(i+4)]);
        }
    });

    [Theory] [InlineData(.5)] [InlineData(1)] [InlineData(2)]
    public void TransparentCanvasShowsCheckerboardAtStableSizeWithoutChangingExport(double zoom)=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(BlankImageService.Create(new(new(64,48),Colors.Transparent)));model.Zoom=zoom;
        var canvas=new EditorCanvas{Model=model};
        byte[] Render()
        {
            canvas.Refresh();canvas.Measure(new Size(canvas.Width,canvas.Height));canvas.Arrange(new Rect(0,0,canvas.Width,canvas.Height));canvas.UpdateLayout();
            var image=new RenderTargetBitmap((int)canvas.Width,(int)canvas.Height,96,96,PixelFormats.Pbgra32);image.Render(canvas);return Pixels(image);
        }
        var rendered=Render();var stride=(int)canvas.Width*4;
        byte[] Sample(int x,int y)=>rendered[(y*stride+x*4)..(y*stride+x*4+4)];
        Assert.Equal(new byte[]{245,245,245,255},Sample(2,2));Assert.Equal(new byte[]{220,220,220,255},Sample(10,2));
        Assert.Equal(Sample(2,2),Sample(10,10));Assert.Equal(Sample(10,2),Sample(2,10));
        Assert.All(Pixels(PngFileWriter.Decode(PngFileWriter.Encode(model.Composite()!))),pixel=>Assert.Equal((byte)0,pixel));
        // 흰색·유색 화면에서는 체크무늬가 원본 픽셀 뒤에 완전히 가려진다.
        foreach(var color in new[]{Colors.White,Colors.CornflowerBlue})
        {
            model.Open(BlankImageService.Create(new(new(64,48),color)));rendered=Render();
            Assert.Equal(new byte[]{color.B,color.G,color.R,255},Sample(2,2));Assert.Equal(Sample(2,2),Sample(10,2));
        }
    });

    [Theory] [InlineData(0,10)] [InlineData(-1,10)] [InlineData(10,0)] [InlineData(32768,1)] [InlineData(1,32768)] [InlineData(10000,10000)] [InlineData(int.MaxValue,int.MaxValue)]
    public void InvalidSizesAreRejectedBeforeAllocatingPixels(int width,int height)=>
        Assert.Throws<ArgumentOutOfRangeException>(()=>BlankImageService.Create(new(new(width,height),Colors.White)));

    [Fact] public void DialogValidatesIndependentDimensionsAndTogglesBackgroundChoiceWithoutShowing()=>Sta.Run(()=>
    {
        var dialog=new BlankImageDialog();
        try
        {
            var width=(TextBox)dialog.FindName("BlankWidthInput");var height=(TextBox)dialog.FindName("BlankHeightInput");
            var create=(Button)dialog.FindName("CreateBlankButton");var transparent=(CheckBox)dialog.FindName("BlankTransparent");var color=(Button)dialog.FindName("BlankColorButton");
            Assert.Equal("1280",width.Text);Assert.Equal("720",height.Text);Assert.True(create.IsEnabled);Assert.NotEqual(true,transparent.IsChecked);
            foreach(var invalid in new[]{"","-1","0","1.5","abc","32768","999999999999999"}){width.Text=invalid;Assert.False(create.IsEnabled);}
            width.Text="10000";height.Text="10000";Assert.False(create.IsEnabled);
            width.Text="800";height.Text="600";Assert.True(create.IsEnabled);width.Text="400";Assert.Equal("600",height.Text);
            transparent.IsChecked=true;Assert.False(color.IsEnabled);transparent.IsChecked=false;Assert.True(color.IsEnabled);
            var content=(FrameworkElement)dialog.Content;content.Measure(new Size(410,double.PositiveInfinity));content.Arrange(new Rect(content.DesiredSize));content.UpdateLayout();
            Assert.InRange(content.ActualHeight,250,500);Assert.Null(dialog.Result);Assert.False(dialog.IsVisible);
        }
        finally{dialog.Close();}
    });

    [Fact] public void NewCommandWorksWithoutDocumentAndIsBlockedDuringAnotherOperation()=>Sta.Run(()=>
    {
        var picks=0;WithEditor(()=>{picks++;return null;},(window,_,_)=>
        {
            var button=Assert.IsType<RibbonButton>(window.FindName("NewBlankImageButton"));
            Assert.Same(ApplicationCommands.New,button.Command);Assert.Same(window,button.CommandTarget);Assert.NotNull(button.LargeImageSource);
            Assert.Contains(ApplicationCommands.New.InputGestures.Cast<KeyGesture>(),x=>x.Key==Key.N&&x.Modifiers==ModifierKeys.Control);
            Assert.True(ApplicationCommands.New.CanExecute(null,window));
            window.SetBusy(true);Assert.False(ApplicationCommands.New.CanExecute(null,window));Pump(window.CreateBlankImageAsync());Assert.Equal(0,picks);
            window.SetBusy(false);Assert.True(ApplicationCommands.New.CanExecute(null,window));Pump(window.CreateBlankImageAsync());Assert.Equal(1,picks);
            Assert.Null(window.Model.Document);Assert.False(window.IsVisible);
        });
    });

    [Fact] public void CreatingFromEmptyEditorOpensAnEditableDocumentAndSelectsItsHistory()=>Sta.Run(()=>
        WithEditor(()=>new(new(32,24),Colors.White),(window,store,_)=>
        {
            Pump(window.CreateBlankImageAsync());Assert.NotNull(window.Model.Document);Assert.False(window.IsBusy);
            Assert.Equal(new PixelSize(32,24),window.Model.Document!.OutputSize);
            Assert.Equal(window.Model.CaptureId,Assert.IsType<CaptureEntry>(((ListBox)window.FindName("HistoryList")).SelectedItem).Item.Id);
            Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult());Assert.False(window.IsVisible);
        }));

    [Fact] public void CreationFlushesOldEditsAndNewTransparentDocumentSupportsEditingUndoAndReopen()=>Sta.Run(()=>
        WithEditor(()=>new(new(32,24),Colors.Transparent),(window,store,_)=>
        {
            var old=BlankImageService.Create(new(new(8,6),Colors.White));window.Model.Open(old);
            window.Model.History!.Add(new(){Kind=AnnotationKind.Text,Text="old edits"});
            ((TextBox)window.FindName("HistorySearchBox")).Text="missing";
            Pump(window.CreateBlankImageAsync());var id=window.Model.CaptureId!.Value;Assert.NotEqual(old.Id,id);
            Assert.Equal("old edits",Assert.Single(store.LoadAsync(old.Id).GetAwaiter().GetResult().Annotations).Text);
            Assert.Equal("",((TextBox)window.FindName("HistorySearchBox")).Text);
            Assert.Equal(id,Assert.IsType<CaptureEntry>(((ListBox)window.FindName("HistoryList")).SelectedItem).Item.Id);
            var mark=new Annotation{Kind=AnnotationKind.Rectangle,X=4,Y=4,Width=12,Height=12,Style=new(){Color="#FF0000",Thickness=2}};
            window.Model.History!.Add(mark);window.Model.History.Undo();Assert.Empty(window.Model.Document!.Annotations);window.Model.History.Redo();
            window.Model.InsertImage(BlankImageService.Create(new(new(2,2),Colors.Blue)).OriginalPng);
            Pump(window.FlushAsync());var saved=store.LoadAsync(id).GetAwaiter().GetResult();Assert.Equal(CaptureMode.Blank,saved.Mode);Assert.Equal(2,saved.Annotations.Length);
            var reopened=new EditorViewModel();reopened.Open(saved);Assert.Equal(Pixels(window.Model.Composite()!),Pixels(reopened.Composite()!));
            Assert.All(Pixels(PngFileWriter.Decode(saved.OriginalPng)),pixel=>Assert.Equal((byte)0,pixel));
            Assert.Equal(2,store.ListAsync(0,50).GetAwaiter().GetResult().Count);
        }));

    [Fact] public void CancelKeepsCurrentDocumentAndUndoStateAndBlocksNestedCreation()=>Sta.Run(()=>
    {
        EditorWindow? current=null;var picks=0;
        WithEditor(()=>{picks++;Assert.True(current!.IsBusy);Pump(current.CreateBlankImageAsync());return null;},(window,store,_)=>
        {
            current=window;window.Model.Open(BlankImageService.Create(new(new(8,6),Colors.White)));var id=window.Model.CaptureId;var token=window.Model.History!.CurrentStateToken;
            Pump(window.CreateBlankImageAsync());Assert.Equal(1,picks);Assert.Equal(id,window.Model.CaptureId);Assert.Equal(token,window.Model.History.CurrentStateToken);
            Assert.False(window.IsBusy);Assert.Empty(store.ListAsync(0,50).GetAwaiter().GetResult());
        });
    });

    [Fact] public void ValidationAndSaveFailuresKeepCurrentDocumentAndRestoreCommands()=>Sta.Run(()=>
    {
        var options=new BlankImageOptions(new(0,10),Colors.White);
        WithEditor(()=>options,(window,store,path)=>
        {
            window.Model.Open(BlankImageService.Create(new(new(8,6),Colors.White)));var id=window.Model.CaptureId;
            Assert.Throws<ArgumentOutOfRangeException>(()=>Pump(window.CreateBlankImageAsync()));Assert.Equal(id,window.Model.CaptureId);Assert.False(window.IsBusy);
            Pump(window.FlushAsync());var library=Path.Combine(path,"Library");TestFiles.DeleteDirectory(library);File.WriteAllText(library,"blocked");
            options=new(new(16,12),Colors.White);Assert.ThrowsAny<IOException>(()=>Pump(window.CreateBlankImageAsync()));
            Assert.Equal(id,window.Model.CaptureId);Assert.False(window.IsBusy);Assert.True(ApplicationCommands.New.CanExecute(null,window));
        });
    });
}
