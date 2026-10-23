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
using SnapBerry.App.Import;
using SnapBerry.App.Library;
using SnapBerry.App.Rendering;
using SnapBerry.App.Settings;
using SnapBerry.App.Stickers;
using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Windows.Tests;

// 실제 클립보드·키보드·창 표시 없이 이력 이미지 복사와 이미지 개체 붙여넣기를 검증한다.
public class HistoryClipboardTests
{
    private sealed class ClipboardSource(BitmapSource image) : IClipboardImageSource {public BitmapSource? Read()=>image;}
    private sealed class MemoryClipboard : IClipboardImageSource
    {
        public BitmapSource? Image;public int Writes;private IDataObject? data;
        public IDataObject? Data=>data;
        public BitmapSource? Read()=>ReadContent()?.Image;
        public ClipboardImageContent? ReadContent()=>data is null?(Image is {} image?new(image):null):ClipboardImageSource.ReadContent(data);
        public void Write(ClipboardImageContent content){data=ClipboardImageSource.CreateDataObject(content);Image=ReadContent()!.Image;Writes++;}
    }
    private static void Pump(Task task)
    {var dispatcher=Dispatcher.CurrentDispatcher;var frame=new DispatcherFrame();task.ContinueWith(_=>dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    private static BitmapSource Solid(int width,int height,Color color)
    {
        var bytes=new byte[width*height*4];
        for(var i=0;i<bytes.Length;i+=4){bytes[i]=color.B;bytes[i+1]=color.G;bytes[i+2]=color.R;bytes[i+3]=color.A;}
        var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,bytes,width*4);image.Freeze();return image;
    }
    private static byte[] Pixel(BitmapSource image,int x,int y)
    {var bytes=new byte[4];new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0).CopyPixels(new Int32Rect(x,y,1,1),bytes,4,0);return bytes;}
    private static void WaitUntilIdle(EditorWindow window)
    {
        async Task Wait(){var end=DateTime.UtcNow.AddSeconds(10);while(window.IsBusy){Assert.True(DateTime.UtcNow<end);await Task.Delay(1);}}
        Pump(Wait());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void PasteMovesLogicalFocusFromRibbonOrHistoryToTheSelectedObject(bool fromHistory)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-paste-focus-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),clipboardImages:new ClipboardSource(Solid(40,20,Colors.Red)));
        try
        {
            window.Model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
            var initial=(UIElement)window.FindName(fromHistory?"HistoryList":"PasteImageButton");
            FocusManager.SetFocusedElement(window,initial);Assert.Same(initial,FocusManager.GetFocusedElement(window));
            Pump(window.PasteImageAsync());
            var canvas=(EditorCanvas)((Grid)window.FindName("CanvasHost")).Children[0];
            Assert.Same(canvas,FocusManager.GetFocusedElement(window));Assert.NotNull(window.Model.Selected);
            Assert.True(canvas.IsEnabled);Assert.False(window.IsBusy);Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(AnnotationKind.Rectangle)] [InlineData(AnnotationKind.Arrow)] [InlineData(AnnotationKind.Sticker)]
    public void SelectedObjectsCopyAndPasteAsEditableObjectsAcrossDocuments(AnnotationKind kind)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-object-copy-"+Guid.NewGuid().ToString("N"));var clipboard=new MemoryClipboard();
        var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
        var window=new EditorWindow(store,new SettingsService(path),clipboardImages:clipboard,copyImage:clipboard.Write);
        try
        {
            window.Model.Open(new(){Width=200,Height=120,OriginalPng=PngFileWriter.Encode(Solid(200,120,Colors.Blue))});
            Annotation selected;
            if(kind==AnnotationKind.Sticker)
            {
                window.Model.InsertSticker(StickerCatalog.Load().First(x=>x.IsKeycap),new(10,20));
                window.Model.UpdateKeycapLabel(window.Model.SelectedId!.Value,"Ctrl+C");selected=window.Model.Selected!;
            }
            else selected=new(){Kind=kind,X=10,Y=20,Width=kind==AnnotationKind.Arrow?-40:40,Height=30,
                Style=new(){Color="#139887",Thickness=8,LineStyle=StrokePattern.DashDot}};
            selected=selected with{Width=kind==AnnotationKind.Arrow?-40:40,Height=30};
            if(kind==AnnotationKind.Sticker)window.Model.History!.Replace(selected);else window.Model.History!.Add(selected);
            window.Model.SelectedId=selected.Id;var token=window.Model.History!.CurrentStateToken;
            ApplicationCommands.Copy.Execute(null,window);WaitUntilIdle(window);
            Assert.True(clipboard.Data!.GetDataPresent("SnapBerry.AnnotationObject.v1",false));
            Assert.Equal(token,window.Model.History.CurrentStateToken);Assert.True(window.Model.History.IsDirty);
            // 복사 뒤 원본을 바꾸거나 다른 문서를 열어도 복사한 순간의 속성과 스티커 자원을 사용한다.
            window.Model.History.Replace(selected with{Width=5,Style=new(){Color="#FFFFFF"}});
            window.Model.Open(new(){Width=200,Height=120,OriginalPng=PngFileWriter.Encode(Solid(200,120,Colors.Red))});
            ApplicationCommands.Paste.Execute(null,window);WaitUntilIdle(window);
            var pasted=Assert.Single(window.Model.Document!.Annotations);
            Assert.Equal(kind,pasted.Kind);Assert.NotEqual(selected.Id,pasted.Id);
            Assert.Equal((selected.Width,selected.Height),(pasted.Width,pasted.Height));Assert.Equal(selected.Style,pasted.Style);
            Assert.Equal((100d,60d),(pasted.X+pasted.Width/2,pasted.Y+pasted.Height/2));
            Assert.Equal(selected.StickerId,pasted.StickerId);Assert.Equal(selected.Text,pasted.Text);
            if(kind==AnnotationKind.Sticker)Assert.NotNull(window.Model.Assets.GetBitmap(pasted.AssetHash));
            window.Model.History!.Undo();Assert.Empty(window.Model.Document.Annotations);
            window.Model.History.Redo();Assert.Equal(pasted,Assert.Single(window.Model.Document.Annotations));
            Pump(window.FlushAsync());var reopened=new EditorViewModel();reopened.Open(store.LoadAsync(window.Model.CaptureId!.Value).GetAwaiter().GetResult());
            Assert.Equal(pasted,Assert.Single(reopened.Document!.Annotations));Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(AnnotationKind.Rectangle,40,30,50,35)]
    [InlineData(AnnotationKind.Arrow,-40,-30,90,65)] [InlineData(AnnotationKind.Arrow,40,-30,50,65)]
    [InlineData(AnnotationKind.Sticker,40,30,50,35)]
    public void PasteCentersObjectsOnTheCursorAfterCropResizeAndZoom(AnnotationKind kind,double width,double height,double x,double y)=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=400,Height=300,CropBounds=new(40,30,200,100),ResizeSize=new(400,100),
            OriginalPng=PngFileWriter.Encode(Solid(400,300,Colors.Blue))});model.Zoom=1.5;
        var source=new Annotation{Kind=kind,X=150,Y=100,Width=width,Height=height};
        if(kind==AnnotationKind.Sticker)
        {
            model.InsertSticker(StickerCatalog.Load().First(x=>x.IsKeycap),new(150,100));
            source=model.Selected! with{Width=width,Height=height};
        }
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();
        var image=kind==AnnotationKind.Sticker?model.Assets.GetBitmap(source.AssetHash):Solid(40,30,Colors.Red);
        // 화면 (90,30) → 원본 (70,50). 비균등 크기 변경과 확대 배율을 각각 반영한다.
        var content=ClipboardImageSource.ReadContent(ClipboardImageSource.CreateDataObject(new(image,Annotation:source)))!;
        model.InsertAnnotation(content,canvas.ToDocumentPoint(new Point(90,30)));var pasted=model.Selected!;
        Assert.Equal((x,y,width,height),(pasted.X,pasted.Y,pasted.Width,pasted.Height));
        model.InsertAnnotation(content,canvas.ToDocumentPoint(new Point(150,60)));var next=model.Selected!;
        Assert.Equal((x+20,y+20,width,height),(next.X,next.Y,next.Width,next.Height));Assert.NotEqual(pasted.Id,next.Id);
        if(kind==AnnotationKind.Sticker){model.UpdateKeycapLabel(next.Id,"Ctrl+V");Assert.Equal("Ctrl+V",model.Selected!.Text);}
    });

    [Theory] [InlineData("null")] [InlineData("{")] [InlineData("{\"Kind\":999}")]
    [InlineData("{\"Kind\":2,\"Style\":null}")] [InlineData("{\"Kind\":2,\"Width\":-40}")]
    [InlineData("{\"Kind\":2,\"Style\":{\"Color\":null}}")]
    [InlineData("{\"Kind\":2,\"Style\":{\"Color\":\"not-a-color\"}}")]
    [InlineData("{\"Kind\":2,\"StickerId\":null}")]
    public void MalformedObjectMetadataFallsBackToTheClipboardImage(string metadata)=>Sta.Run(()=>
    {
        var data=ClipboardImageSource.CreateDataObject(new(Solid(40,20,Colors.Red)));
        data.SetData("SnapBerry.AnnotationObject.v1",metadata,false);var content=ClipboardImageSource.ReadContent(data)!;
        Assert.Null(content.Annotation);Assert.Equal((40,20),(content.Image.PixelWidth,content.Image.PixelHeight));
    });

    [Fact] public void RejectedImageAssetDoesNotChangeTheDocumentOrItsSavedCapture()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-paste-limits-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new CaptureLibraryStore(path);var original=new CaptureRecord{Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))};
            store.SaveAsync(original).GetAwaiter().GetResult();var model=new EditorViewModel();model.Open(original);var token=model.History!.CurrentStateToken;
            var png=PngFileWriter.Encode(Solid(40,20,Colors.Red));Array.Resize(ref png,16*1024*1024+1);
            Assert.Throws<InvalidDataException>(()=>model.InsertImage(png));
            Assert.Equal(token,model.History.CurrentStateToken);Assert.Empty(model.Document!.Annotations);Assert.Empty(model.Assets.Data);
            Assert.Empty(store.LoadAsync(original.Id).GetAwaiter().GetResult().Annotations);
        }
        finally{TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void SmallImagesOnEnlargedDocumentsKeepTheirFractionalDrawingBounds()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=100,Height=100,ResizeSize=new(1000,1000),OriginalPng=PngFileWriter.Encode(Solid(100,100,Colors.Blue))});
        model.InsertImage(PngFileWriter.Encode(Solid(5,5,Colors.Red)));var image=model.Selected!;
        Assert.Equal((49.75,49.75,.5,.5),(image.X,image.Y,image.Width,image.Height));
        var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())CompositeRenderer.DrawAnnotation(drawing,image,model.Assets);
        Assert.Equal(new Rect(49.75,49.75,.5,.5),visual.Drawing.Bounds);
    });

    [Theory] [InlineData(false)] [InlineData(true)]
    public void PastedImagesUseCanvasMoveResizeAndDeleteWithUndo(bool resize)=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
        model.InsertImage(PngFileWriter.Encode(Solid(40,20,Colors.Red)));var pasted=model.Selected!;
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();
        void Field(string name,object value)=>typeof(EditorCanvas).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(canvas,value);
        Field("before",pasted);Field("preview",pasted);Field("start",new PixelPoint(30,30));Field("dragging",true);Field("resizing",resize);
        canvas.MoveAnnotation(resize?new(110,70):new(40,35));
        typeof(EditorCanvas).GetMethod("OnMouseLeftButtonUp",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(canvas,
            [new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent}]);
        var changed=Assert.Single(model.Document!.Annotations);
        Assert.Equal(resize?(30d,30d,80d,40d):(40d,35d,40d,20d),(changed.X,changed.Y,changed.Width,changed.Height));
        model.History!.Undo();Assert.Equal(pasted,Assert.Single(model.Document.Annotations));
        model.History.Redo();Assert.Equal(changed,Assert.Single(model.Document.Annotations));
        canvas.DeleteSelected();Assert.Empty(model.Document.Annotations);model.History.Undo();Assert.Equal(changed,Assert.Single(model.Document.Annotations));
    });

    [Theory] [InlineData(5,5,20d)] [InlineData(100,1,.2)]
    public void FractionalAndThinPastedImagesKeepTheirRatioWhenResized(int width,int height,double expectedHeight)=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=100,Height=100,ResizeSize=new(1000,1000),OriginalPng=PngFileWriter.Encode(Solid(100,100,Colors.Blue))});
        model.InsertImage(PngFileWriter.Encode(Solid(width,height,Colors.Red)));var pasted=model.Selected!;
        var canvas=new EditorCanvas{Model=model};canvas.Refresh();
        void Field(string name,object value)=>typeof(EditorCanvas).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(canvas,value);
        Field("before",pasted);Field("preview",pasted);Field("dragging",true);Field("resizing",true);
        canvas.MoveAnnotation(new(pasted.X+20,pasted.Y+20));
        typeof(EditorCanvas).GetMethod("OnMouseLeftButtonUp",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(canvas,
            [new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent}]);
        var resized=Assert.Single(model.Document!.Annotations);Assert.Equal(20,resized.Width,8);Assert.Equal(expectedHeight,resized.Height,8);
        model.History!.Undo();Assert.Equal(pasted,Assert.Single(model.Document.Annotations));
    });

    [Fact] public void ImageCopyCommandDoesNotInterceptTextInput()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-copy-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            window.Model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
            var args=(CanExecuteRoutedEventArgs)Activator.CreateInstance(typeof(CanExecuteRoutedEventArgs),BindingFlags.Instance|BindingFlags.NonPublic,null,[ApplicationCommands.Copy,null],null)!;
            args.RoutedEvent=CommandManager.CanExecuteEvent;args.Source=window.FindName("HistorySearchBox");
            typeof(EditorWindow).GetMethod("CanCopyImageCommand",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[window,args]);
            Assert.False(args.CanExecute);Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(false,24,12)] [InlineData(true,24,12)] [InlineData(false,300,150)] [InlineData(true,24.25,12.5)]
    public void SelectedPastedImageCopiesOnlyItselfAtItsCurrentSizeAndCanBeDuplicated(bool resized,double width,double height)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-duplicate-"+Guid.NewGuid().ToString("N"));
        var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var clipboard=new MemoryClipboard{Image=Solid(40,20,Colors.Red)};
        var window=new EditorWindow(store,new SettingsService(path),clipboardImages:clipboard,copyImage:clipboard.Write);
        try
        {
            var target=new CaptureRecord{Width=200,Height=120,OriginalPng=PngFileWriter.Encode(Solid(200,120,Colors.Blue)),
                CropBounds=resized?new(20,10,100,80):null,ResizeSize=resized?new(200,80):null};
            window.Model.Open(target);Pump(window.PasteImageAsync());var pasted=window.Model.Selected!;
            var selected=pasted with{X=30,Y=30,Width=width,Height=height};window.Model.History!.Replace(selected);
            window.Model.History.Add(new(){Kind=AnnotationKind.StepMarker,X=30,Y=30,Width=24,Height=12,Text="",Style=new(){Fill="#00FF00"}});
            window.Model.SelectedId=selected.Id;var document=window.Model.Document;var token=window.Model.History.CurrentStateToken;
            var canvas=(EditorCanvas)typeof(EditorWindow).GetField("canvas",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            ApplicationCommands.Copy.Execute(null,canvas);WaitUntilIdle(window);
            Assert.NotNull(clipboard.Image);
            Assert.Equal(new byte[]{0,0,255,255},Pixel(clipboard.Image,10,5));
            Assert.Equal(token,window.Model.History.CurrentStateToken);Assert.True(window.Model.History.IsDirty);
            ApplicationCommands.Paste.Execute(null,canvas);WaitUntilIdle(window);
            Assert.Same(document,window.Model.Document);Assert.Equal(target.Id,window.Model.CaptureId);Assert.Equal(3,document!.Annotations.Count);
            var duplicate=window.Model.Selected!;Assert.NotEqual(selected.Id,duplicate.Id);Assert.Equal((width,height),(duplicate.Width,duplicate.Height));
            window.Model.History.Undo();Assert.Equal(2,document.Annotations.Count);window.Model.History.Redo();Assert.Equal(duplicate,document.Annotations.Last());
            Pump(window.FlushAsync());var reopened=new EditorViewModel();reopened.Open(store.LoadAsync(target.Id).GetAwaiter().GetResult());
            Assert.Equal(duplicate,reopened.Document!.Annotations.Last());Assert.Equal(3,reopened.Document.Annotations.Count);Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(false)] [InlineData(true)]
    public void CopyWithoutASelectedPastedImageStillCopiesTheFullDocument(bool otherSelection)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-copy-fallback-"+Guid.NewGuid().ToString("N"));var clipboard=new MemoryClipboard();
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),clipboardImages:clipboard,copyImage:clipboard.Write);
        try
        {
            window.Model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
            var annotation=new Annotation{Kind=AnnotationKind.StepMarker,X=30,Y=25,Width=40,Height=30,Text="",Style=new(){Fill="#00FF00"}};
            window.Model.History!.Add(annotation);window.Model.SelectedId=otherSelection?annotation.Id:null;
            ApplicationCommands.Copy.Execute(null,window);WaitUntilIdle(window);
            Assert.Equal((100,80),(clipboard.Image!.PixelWidth,clipboard.Image.PixelHeight));
            Assert.Equal(new byte[]{0,255,0,255},Pixel(clipboard.Image,50,40));Assert.Equal(new byte[]{255,0,0,255},Pixel(clipboard.Image,0,0));
            Assert.False(window.Model.History.IsDirty);Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void SelectedImageCopyPreservesTransparentPixelsAndSupportsRepeatedDuplication()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-copy-alpha-"+Guid.NewGuid().ToString("N"));
        var pixels=new byte[]{0,0,0,0,0,0,255,255,0,0,0,0,0,0,255,255};
        var source=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,pixels,8);source.Freeze();
        var clipboard=new MemoryClipboard{Image=source};
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),clipboardImages:clipboard,copyImage:clipboard.Write);
        try
        {
            window.Model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});Pump(window.PasteImageAsync());
            for(var i=0;i<2;i++)
            {
                ApplicationCommands.Copy.Execute(null,window);WaitUntilIdle(window);
                Assert.Equal((2,2),(clipboard.Image!.PixelWidth,clipboard.Image.PixelHeight));
                Assert.Equal(0,Pixel(clipboard.Image,0,0)[3]);Assert.Equal(new byte[]{0,0,255,255},Pixel(clipboard.Image,1,0));
                ApplicationCommands.Paste.Execute(null,window);WaitUntilIdle(window);
            }
            Assert.Equal(3,window.Model.Document!.Annotations.Select(x=>x.Id).Distinct().Count());
            Assert.All(window.Model.Document.Annotations,x=>Assert.Equal((2d,2d),(x.Width,x.Height)));
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void CopiedObjectKeepsItsOutputDimensionsOnAnotherDocument()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-copy-size-"+Guid.NewGuid().ToString("N"));var clipboard=new MemoryClipboard{Image=Solid(40,20,Colors.Red)};
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),clipboardImages:clipboard,copyImage:clipboard.Write);
        try
        {
            window.Model.Open(new(){Width=200,Height=120,CropBounds=new(20,10,100,80),ResizeSize=new(200,80),OriginalPng=PngFileWriter.Encode(Solid(200,120,Colors.Blue))});
            Pump(window.PasteImageAsync());window.Model.History!.Replace(window.Model.Selected! with{Width=24.25,Height=12.5});
            ApplicationCommands.Copy.Execute(null,window);WaitUntilIdle(window);
            window.Model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
            Pump(window.PasteImageAsync());Assert.Equal((48.5,12.5),(window.Model.Selected!.Width,window.Model.Selected.Height));
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData("NaN|20")] [InlineData("Infinity|20")] [InlineData("0|20")] [InlineData("-1|20")]
    [InlineData("20|0")] [InlineData("20")] [InlineData("1|2|3")]
    public void InvalidClipboardObjectSizeFallsBackToAnOrdinaryImage(string metadata)=>Sta.Run(()=>
    {
        var data=ClipboardImageSource.CreateDataObject(new(Solid(40,20,Colors.Red)));
        data.SetData("SnapBerry.ImageObject.v1",metadata,false);var content=ClipboardImageSource.ReadContent(data)!;
        Assert.Null(content.ObjectSize);var model=new EditorViewModel();
        model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
        model.InsertImage(PngFileWriter.Encode(content.Image),content.ObjectSize);
        Assert.Equal((40d,20d),(model.Selected!.Width,model.Selected.Height));
    });

    [Fact] public void ExcessiveObjectSizeCannotCreateAnUnreadableCapture()=>Sta.Run(()=>
    {
        var model=new EditorViewModel();model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
        var token=model.History!.CurrentStateToken;
        Assert.Throws<InvalidDataException>(()=>model.InsertImage(PngFileWriter.Encode(Solid(40,20,Colors.Red)),new(1000001,20)));
        Assert.Equal(token,model.History.CurrentStateToken);Assert.Empty(model.Document!.Annotations);Assert.Empty(model.Assets.Data);
    });

    [Theory] [InlineData(false)] [InlineData(true)]
    public void CopyFromHistoryUsesTheSelectedCompositeThenPastesOntoAnotherImage(bool sourceIsActive)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-copy-"+Guid.NewGuid().ToString("N"));
        var store=new CaptureLibraryStore(Path.Combine(path,"Library"));var clipboard=new MemoryClipboard();
        var window=new EditorWindow(store,new SettingsService(path),clipboardImages:clipboard,copyImage:clipboard.Write);
        try
        {
            var annotation=new Annotation{Kind=AnnotationKind.StepMarker,X=30,Y=25,Width=40,Height=30,Style=new(){Fill="#00FF00"},Text=""};
            var source=new CaptureRecord{Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Red)),
                CropBounds=new(10,10,80,60),ResizeSize=new(40,30),Annotations=sourceIsActive?[]:[annotation]};
            var target=new CaptureRecord{Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))};
            Pump(window.OpenNewAsync(source));Pump(window.OpenNewAsync(target));
            if(sourceIsActive){Pump(window.OpenRecordAsync(source.Id));window.Model.History!.Add(annotation);}
            var document=window.Model.Document;var token=window.Model.History!.CurrentStateToken;
            var list=(ListBox)window.FindName("HistoryList");list.SelectedItem=list.Items.Cast<CaptureEntry>().Single(x=>x.Item.Id==source.Id);
            Assert.True(ApplicationCommands.Copy.CanExecute(null,list));ApplicationCommands.Copy.Execute(null,list);WaitUntilIdle(window);
            Assert.Equal(1,clipboard.Writes);Assert.NotNull(clipboard.Image);
            Assert.Equal((40,30),(clipboard.Image!.PixelWidth,clipboard.Image.PixelHeight));
            Assert.Equal(new byte[]{0,255,0,255},Pixel(clipboard.Image,20,15));Assert.Equal(new byte[]{0,0,255,255},Pixel(clipboard.Image,1,1));
            Assert.Same(document,window.Model.Document);Assert.Equal(token,window.Model.History.CurrentStateToken);
            Pump(window.OpenRecordAsync(target.Id));var targetDocument=window.Model.Document;
            ApplicationCommands.Paste.Execute(null,window);WaitUntilIdle(window);
            Assert.Equal(target.Id,window.Model.CaptureId);Assert.Same(targetDocument,window.Model.Document);Assert.Single(targetDocument!.Annotations);
            Assert.Equal(new byte[]{0,255,0,255},Pixel(window.Model.Composite()!,50,40));
            Assert.Equal(new byte[]{255,0,0,255},Pixel(window.Model.Composite()!,0,0));Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void HistoryCopyCommandIsAvailableOnlyForASingleUndamagedSelection()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-copy-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path));
        try
        {
            var list=(ListBox)window.FindName("HistoryList");
            var first=new CaptureEntry(new(Guid.NewGuid(),DateTimeOffset.UtcNow,40,20),null);
            var second=new CaptureEntry(new(Guid.NewGuid(),DateTimeOffset.UtcNow,40,20),null);
            var damaged=new CaptureEntry(new(Guid.NewGuid(),DateTimeOffset.UtcNow,0,0,true),null);
            list.ItemsSource=new[]{first,second,damaged};
            Assert.False(ApplicationCommands.Copy.CanExecute(null,list));
            list.SelectedItem=first;Assert.True(ApplicationCommands.Copy.CanExecute(null,list));
            list.SelectedItems.Add(second);Assert.False(ApplicationCommands.Copy.CanExecute(null,list));
            list.SelectedItems.Clear();list.SelectedItem=damaged;Assert.False(ApplicationCommands.Copy.CanExecute(null,list));
            list.SelectedItem=first;window.SetBusy(true);Assert.False(ApplicationCommands.Copy.CanExecute(null,list));
            window.SetBusy(false);Assert.True(ApplicationCommands.Copy.CanExecute(null,list));Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Theory] [InlineData(false)] [InlineData(true)]
    public void PasteAddsASelectedImageToTheCurrentDocumentAndPersistsThroughUndoAndReload(bool resized)=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-paste-"+Guid.NewGuid().ToString("N"));
        var store=new CaptureLibraryStore(Path.Combine(path,"Library"));
        var source=Solid(40,20,Colors.Red);
        var window=new EditorWindow(store,new SettingsService(path),clipboardImages:new ClipboardSource(source));
        try
        {
            var target=new CaptureRecord{Width=resized?200:100,Height=resized?120:80,
                OriginalPng=PngFileWriter.Encode(Solid(resized?200:100,resized?120:80,Colors.Blue)),
                CropBounds=resized?new(20,10,100,80):null,ResizeSize=resized?new(200,80):null};
            window.Model.Open(target);window.Model.Tool=EditorTool.Arrow;
            var document=window.Model.Document;var history=window.Model.History;
            Pump(window.PasteImageAsync());
            Assert.Equal(target.Id,window.Model.CaptureId);Assert.Same(document,window.Model.Document);Assert.Same(history,window.Model.History);
            var pasted=Assert.Single(document!.Annotations);Assert.Equal(AnnotationKind.Sticker,pasted.Kind);
            Assert.Equal(pasted.Id,window.Model.SelectedId);Assert.Equal(EditorTool.Select,window.Model.Tool);
            Assert.Equal(resized?60:30,pasted.X);Assert.Equal(resized?40:30,pasted.Y);
            Assert.Equal(resized?20:40,pasted.Width);Assert.Equal(20,pasted.Height);
            Assert.Equal(new byte[]{0,0,255,255},Pixel(window.Model.Composite()!,resized?100:50,40));
            Assert.Equal(target.OriginalPng,window.Model.Snapshot().OriginalPng);
            history!.Undo();Assert.Empty(document.Annotations);Assert.Equal(new byte[]{255,0,0,255},Pixel(window.Model.Composite()!,resized?100:50,40));
            history.Redo();Assert.Equal(pasted,Assert.Single(document.Annotations));Pump(window.FlushAsync());
            var saved=store.LoadAsync(target.Id).GetAwaiter().GetResult();Assert.Single(saved.StickerAssets);
            var reopened=new EditorViewModel();reopened.Open(saved);Assert.Equal(pasted,Assert.Single(reopened.Document!.Annotations));
            Assert.Equal(new byte[]{0,0,255,255},Pixel(reopened.Composite()!,resized?100:50,40));
            Assert.Equal(target.Id,Assert.Single(store.ListAsync(0,50).GetAwaiter().GetResult()).Id);Assert.False(window.IsVisible);
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });

    [Fact] public void OversizedPastedImageFitsTheCanvasWithoutDistortingItsAspectRatio()=>Sta.Run(()=>
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-paste-"+Guid.NewGuid().ToString("N"));
        var window=new EditorWindow(new(Path.Combine(path,"Library")),new SettingsService(path),clipboardImages:new ClipboardSource(Solid(400,200,Colors.Red)));
        try
        {
            window.Model.Open(new(){Width=100,Height=80,OriginalPng=PngFileWriter.Encode(Solid(100,80,Colors.Blue))});
            Pump(window.PasteImageAsync());var pasted=Assert.Single(window.Model.Document!.Annotations);
            Assert.Equal((0d,15d,100d,50d),(pasted.X,pasted.Y,pasted.Width,pasted.Height));
        }
        finally{window.CloseForExit();TestFiles.DeleteDirectory(path);}
    });
}
