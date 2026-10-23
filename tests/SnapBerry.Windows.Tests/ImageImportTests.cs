using System.IO;
using System.Windows;
using System.Windows.Controls.Ribbon;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Reflection;
using SnapBerry.App.Editor;
using SnapBerry.App.Export;
using SnapBerry.App.Import;
using SnapBerry.App.Library;
using SnapBerry.App.Settings;
using SnapBerry.Core;
using CaptureMode=SnapBerry.Core.CaptureMode;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 파일 대화상자·실제 클립보드·앱 창을 사용하지 않는 이미지 가져오기 검증이다.
public class ImageImportTests
{
    private sealed class Picker(Func<Window,string?> pick) : IImagePathPicker {public string? Pick(Window owner)=>pick(owner);}
    private sealed class SavePicker(Func<Window,string?> pick) : IExportPathPicker {public string? Pick(Window owner,string directory,string suggestedFileName)=>pick(owner);}
    private sealed class ClipboardSource(Func<BitmapSource?> read) : IClipboardImageSource {public BitmapSource? Read()=>read();}
    private static string Temp()=>Path.Combine(Path.GetTempPath(),"SnapBerry-import-"+Guid.NewGuid().ToString("N"));
    private static void Pump(Task task)
    {var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    private static BitmapSource Pixels(byte[] pixels,int width=2,int height=1)
    {var image=BitmapSource.Create(width,height,144,144,PixelFormats.Bgra32,null,pixels,width*4);image.Freeze();return image;}
    private static string WriteImage(string path,BitmapEncoder encoder,BitmapSource? image=null)
    {encoder.Frames.Add(BitmapFrame.Create(image??RenderingTests.Image(8,6)));using var stream=File.Create(path);encoder.Save(stream);return path;}
    private static void WithWindow(Func<Window,string?>? pick,Func<BitmapSource?>? read,Action<EditorWindow,CaptureLibraryStore,string> action)
    {
        var path=Temp();Directory.CreateDirectory(path);var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
        var window=new EditorWindow(store,new SettingsService(path),imagePicker:new Picker(pick??(_=>null)),clipboardImages:new ClipboardSource(read??(()=>null)));
        try{action(window,store,path);}finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    }
    [Fact] public void ImportCommandsAreAvailableWithoutAnImageAndDisabledWhileBusy()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-import-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            var open=Assert.IsType<RibbonButton>(window.FindName("OpenImageButton"));
            var paste=Assert.IsType<RibbonButton>(window.FindName("PasteImageButton"));
            Assert.Same(ApplicationCommands.Open,open.Command);Assert.Same(ApplicationCommands.Paste,paste.Command);
            Assert.Same(window,open.CommandTarget);Assert.Same(window,paste.CommandTarget);
            Assert.NotNull(open.LargeImageSource);Assert.NotNull(paste.LargeImageSource);
            Assert.True(ApplicationCommands.Open.CanExecute(null,window));Assert.True(ApplicationCommands.Paste.CanExecute(null,window));
            window.SetBusy(true);Assert.False(ApplicationCommands.Open.CanExecute(null,window));Assert.False(ApplicationCommands.Paste.CanExecute(null,window));
            window.SetBusy(false);Assert.True(ApplicationCommands.Open.CanExecute(null,window));Assert.True(ApplicationCommands.Paste.CanExecute(null,window));
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ImportButtonsReactivateAfterOpeningOrSavingWithoutAnotherInputEvent(bool save)=>Sta.Run(()=>
    {
        var path=Temp();Directory.CreateDirectory(path);EditorWindow? window=null;
        void DrainCommands()
        {
            var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,()=>frame.Continue=false);Dispatcher.PushFrame(frame);
        }
        void QueryDuringWork()
        {
            Assert.True(window!.IsBusy);CommandManager.InvalidateRequerySuggested();DrainCommands();
            Assert.False(((RibbonButton)window.FindName("OpenImageButton")).IsEnabled);
            Assert.False(((RibbonButton)window.FindName("PasteImageButton")).IsEnabled);
        }
        try
        {
            var file=WriteImage(Path.Combine(path,"source.png"),new PngBitmapEncoder());
            var output=Path.Combine(path,"saved.png");
            window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),
                picker:new SavePicker(_=>{QueryDuringWork();return output;}),
                imagePicker:new Picker(_=>{QueryDuringWork();return file;}),clipboardImages:new ClipboardSource(()=>null));
            window.Model.Open(new(){Width=8,Height=6,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(8,6))});
            Pump(save?window.SaveAsync():window.OpenImageAsync());DrainCommands();
            Assert.False(window.IsBusy);Assert.True(ApplicationCommands.Open.CanExecute(null,window));
            Assert.True(((RibbonButton)window.FindName("OpenImageButton")).IsEnabled);
            Assert.True(((RibbonButton)window.FindName("PasteImageButton")).IsEnabled);
            Assert.False(window.IsVisible);if(save)Assert.True(File.Exists(output));
        }
        finally{window?.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData("png")] [InlineData("jpg")] [InlineData("bmp")] [InlineData("gif")] [InlineData("tiff")]
    public void SupportedFilesBecomeIndependentPngRecords(string format)=>Sta.Run(()=>
    {
        var path=Temp();Directory.CreateDirectory(path);try
        {
            BitmapEncoder encoder=format switch{"png"=>new PngBitmapEncoder(),"jpg"=>new JpegBitmapEncoder(),"bmp"=>new BmpBitmapEncoder(),"gif"=>new GifBitmapEncoder(),_=>new TiffBitmapEncoder()};
            var file=WriteImage(Path.Combine(path,"source."+format),encoder);var record=ImageImportService.FromFileAsync(file).GetAwaiter().GetResult();
            File.Delete(file);Assert.Equal((8,6),(record.Width,record.Height));Assert.Equal(CaptureMode.ImageFile,record.Mode);Assert.Empty(record.Annotations);
            Assert.Equal(new byte[]{137,80,78,71,13,10,26,10},record.OriginalPng[..8]);
            var store=new CaptureLibraryStore(Path.Combine(path,"Library"));store.SaveAsync(record).GetAwaiter().GetResult();
            var model=new EditorViewModel();model.Open(store.LoadAsync(record.Id).GetAwaiter().GetResult());Assert.Equal((8,6),(model.Composite()!.PixelWidth,model.Composite()!.PixelHeight));
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(false)] [InlineData(true)]
    public void MultiFrameFilesUseOnlyTheFirstFrame(bool tiff)=>Sta.Run(()=>
    {
        var path=Temp();Directory.CreateDirectory(path);try
        {
            BitmapEncoder encoder=tiff?new TiffBitmapEncoder():new GifBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Pixels([0,0,255,255,0,0,255,255])));
            encoder.Frames.Add(BitmapFrame.Create(Pixels([255,0,0,255,255,0,0,255])));
            var file=Path.Combine(path,tiff?"frames.tiff":"frames.gif");using(var stream=File.Create(file))encoder.Save(stream);
            var record=ImageImportService.FromFileAsync(file).GetAwaiter().GetResult();var sample=new byte[8];PngFileWriter.Decode(record.OriginalPng).CopyPixels(sample,8,0);
            Assert.Equal(new byte[]{0,0,255,255,0,0,255,255},sample);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void ClipboardPngTakesPriorityAndPreservesAlphaWithoutUsingSystemClipboard()=>Sta.Run(()=>
    {
        byte[] expected=[0,0,0,0,20,40,80,128];var data=new DataObject();
        using var png=new MemoryStream(PngFileWriter.Encode(Pixels(expected)));data.SetData("PNG",png);
        data.SetData(DataFormats.Bitmap,RenderingTests.Image(5,4));
        var image=ClipboardImageSource.ReadImage(data);Assert.NotNull(image);
        var record=ImageImportService.FromBitmap(image!);var converted=new FormatConvertedBitmap(PngFileWriter.Decode(record.OriginalPng),PixelFormats.Bgra32,null,0);
        var actual=new byte[8];converted.CopyPixels(actual,8,0);Assert.Equal(expected,actual);Assert.Equal(CaptureMode.Clipboard,record.Mode);
    });

    [Fact] public void ClipboardBitmapFallbackCopiesPixelsAndRejectsTextOnlyData()=>Sta.Run(()=>
    {
        var bitmap=new WriteableBitmap(2,1,96,96,PixelFormats.Bgra32,null);byte[] red=[0,0,255,255,0,0,255,255];bitmap.WritePixels(new Int32Rect(0,0,2,1),red,8,0);
        var data=new DataObject();data.SetData(DataFormats.Bitmap,bitmap);var record=ImageImportService.FromBitmap(ClipboardImageSource.ReadImage(data)!);
        bitmap.WritePixels(new Int32Rect(0,0,2,1),new byte[8],8,0);var actual=new byte[8];PngFileWriter.Decode(record.OriginalPng).CopyPixels(actual,8,0);Assert.Equal(red,actual);
        Assert.Null(ClipboardImageSource.ReadImage(new DataObject(DataFormats.Text,"plain text")));
    });

    [Fact] public void FileImportFlushesOldDocumentSelectsNewHistoryAndPreservesSourceFile()=>Sta.Run(()=>
    {
        string? file=null;WithWindow(_=>file,null,(window,store,path)=>
        {
            file=WriteImage(Path.Combine(path,"source.png"),new PngBitmapEncoder());var bytes=File.ReadAllBytes(file);
            var old=new CaptureRecord{Width=10,Height=10,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(10,10))};window.Model.Open(old);
            window.Model.History!.Add(new(){Kind=AnnotationKind.Text,Text="saved before import"});
            Pump(window.OpenImageAsync());Assert.NotEqual(old.Id,window.Model.CaptureId);Assert.Equal(CaptureMode.ImageFile,window.Model.Snapshot().Mode);
            Assert.Equal("saved before import",Assert.Single(store.LoadAsync(old.Id).GetAwaiter().GetResult().Annotations).Text);
            Assert.Equal(window.Model.CaptureId,Assert.IsType<CaptureEntry>(((ListBox)window.FindName("HistoryList")).SelectedItem).Item.Id);
            Assert.Equal(bytes,File.ReadAllBytes(file));Pump(window.FlushAsync());Assert.Equal(2,store.ListAsync(0,50).GetAwaiter().GetResult().Count);
        });
    });

    [Fact] public void ClipboardImportRetriesContentionAndCreatesAnEditableHistory()=>Sta.Run(()=>
    {
        var attempts=0;WithWindow(null,()=>{if(++attempts<3)throw new ExternalException("busy");return RenderingTests.Image(9,7);},(window,store,_)=>
        {
            Pump(window.PasteImageAsync());Assert.Equal(3,attempts);Assert.Equal(CaptureMode.Clipboard,window.Model.Snapshot().Mode);
            window.Model.History!.Add(new(){Kind=AnnotationKind.Text,Text="editable"});Pump(window.FlushAsync());
            var reopened=new EditorViewModel();reopened.Open(store.LoadAsync(window.Model.CaptureId!.Value).GetAwaiter().GetResult());Assert.Equal("editable",Assert.Single(reopened.Document!.Annotations).Text);
        });
    });

    [Theory] [InlineData(false)] [InlineData(true)]
    public void CancelledFileSelectionAndEmptyClipboardPreserveTheCurrentDocument(bool clipboard)=>Sta.Run(()=>WithWindow(null,null,(window,_,_)=>
    {
        var old=new CaptureRecord{Width=8,Height=6,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(8,6))};window.Model.Open(old);var token=window.Model.History!.CurrentStateToken;
        Pump(clipboard?window.PasteImageAsync():window.OpenImageAsync());Assert.Equal(old.Id,window.Model.CaptureId);Assert.Equal(token,window.Model.History.CurrentStateToken);Assert.False(window.IsBusy);
    }));

    [Fact] public void InvalidImageAndUnavailableHistoryPreserveTheCurrentDocument()=>Sta.Run(()=>
    {
        string? file=null;WithWindow(_=>file,null,(window,store,path)=>
        {
            file=Path.Combine(path,"bad.png");File.WriteAllText(file,"invalid");window.Model.Open(new(){Width=8,Height=6,OriginalPng=PngFileWriter.Encode(RenderingTests.Image(8,6))});var id=window.Model.CaptureId;
            Assert.ThrowsAny<Exception>(()=>Pump(window.OpenImageAsync()));Assert.Equal(id,window.Model.CaptureId);Assert.False(window.IsBusy);
            Pump(window.FlushAsync());Directory.Delete(Path.Combine(path,"Library"),true);File.WriteAllText(Path.Combine(path,"Library"),"blocked");
            file=WriteImage(Path.Combine(path,"good.png"),new PngBitmapEncoder());
            Assert.ThrowsAny<IOException>(()=>Pump(window.OpenImageAsync()));Assert.Equal(id,window.Model.CaptureId);Assert.False(window.IsBusy);
        });
    });

    [Fact] public void ModalFileSelectionBlocksReentryWithoutReadingTheClipboard()=>Sta.Run(()=>
    {
        EditorWindow? active=null;var reads=0;
        WithWindow(_=>{Assert.True(active!.IsBusy);Pump(active.PasteImageAsync());return null;},()=>{reads++;return null;},(window,_,_)=>
        {active=window;Pump(window.OpenImageAsync());Assert.Equal(0,reads);Assert.False(window.IsBusy);});
    });

    [Fact] public void ImagePasteCommandDoesNotInterceptTextInput()=>Sta.Run(()=>WithWindow(null,null,(window,_,_)=>
    {
        var source=new TextBox();var args=(CanExecuteRoutedEventArgs)Activator.CreateInstance(typeof(CanExecuteRoutedEventArgs),BindingFlags.Instance|BindingFlags.NonPublic,null,[ApplicationCommands.Paste,null],null)!;
        args.RoutedEvent=CommandManager.CanExecuteEvent;args.Source=source;
        typeof(EditorWindow).GetMethod("CanPasteImageCommand",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[window,args]);
        Assert.False(args.CanExecute);
        Assert.Contains(ApplicationCommands.Open.InputGestures.Cast<KeyGesture>(),x=>x.Key==Key.O&&x.Modifiers==ModifierKeys.Control);
        Assert.Contains(ApplicationCommands.Paste.InputGestures.Cast<KeyGesture>(),x=>x.Key==Key.V&&x.Modifiers==ModifierKeys.Control);
    }));

    [Fact] public void ExcessiveFileLengthIsRejectedBeforeDecoding()=>Sta.Run(()=>
    {
        var path=Temp();Directory.CreateDirectory(path);try
        {
            var file=Path.Combine(path,"huge.png");using(var stream=File.Create(file))stream.SetLength(128L*1024*1024+1);
            Assert.Throws<InvalidDataException>(()=>ImageImportService.FromFileAsync(file).GetAwaiter().GetResult());
        }
        finally{TestFiles.DeleteDirectory(path);}
    });
}
