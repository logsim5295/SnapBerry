using System.IO.Compression;
using SkiaSharp;
using SnapBerry.App.Library;
using SnapBerry.Core;
using SnapBerry.Desktop.Editing;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class BackgroundRemovalTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact] public void ToolbarCommandTracksDocumentStateInHeadlessBackend()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-cutout-ui-"+Guid.NewGuid());using var editor=new DesktopEditor();
        var window=new SnapBerry.Desktop.Views.MainWindow(editor,new NoCapture(),path);
        try
        {
            var button=Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Button>(window,"MagicWandButton")!;
            Assert.NotNull(button);Assert.False(button.IsEnabled);
            editor.Open(EditorTests.Record());Assert.True(button.IsEnabled);
            window.SetBusy(true);Assert.False(button.IsEnabled);window.SetBusy(false);
            editor.ApplyBackgroundRemoval(EditorTests.Png(color:SKColors.Transparent));Assert.True(button.IsEnabled);
            editor.History!.Undo();Assert.True(button.IsEnabled);
        }
        finally{window.Dispose();window.Close();if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    private sealed class NoCapture : SnapBerry.Desktop.Capture.ICaptureService
    {
        public Task<byte[]?> CaptureAsync(SnapBerry.Desktop.Capture.DesktopCaptureMode mode,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("실제 캡처 금지");
    }

    [Fact] public async Task CutoutPreservesOriginalAndAnnotationsAcrossUndoAndReopen()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-cutout-"+Guid.NewGuid());
        try
        {
            using var editor=new DesktopEditor();var original=EditorTests.Record();editor.Open(original);
            editor.Commit(new(){Kind=AnnotationKind.Rectangle,X=20,Y=15,Width=20,Height=15});
            editor.History!.Crop(new(10,10,40,30));editor.History.Resize(new(80,60));
            var cutout=EditorTests.Png(color:SKColors.Transparent);var before=editor.RenderPng();
            editor.ApplyBackgroundRemoval(cutout);
            using(var result=SKBitmap.Decode(editor.RenderPng())){Assert.Equal((80,60),(result.Width,result.Height));Assert.Equal(0,result.GetPixel(1,1).Alpha);}
            Assert.Equal(original.OriginalPng,editor.Snapshot().OriginalPng);Assert.Single(editor.Document!.Annotations);
            var after=editor.RenderPng();editor.History.Undo();Assert.Equal(before,editor.RenderPng());editor.History.Redo();Assert.Equal(after,editor.RenderPng());
            var store=new CaptureLibraryStore(path);await editor.SaveAsync(store);
            using var reopened=new DesktopEditor();reopened.Open(await store.LoadAsync(editor.CaptureId!.Value));
            Assert.Equal(after,reopened.RenderPng());Assert.Equal(original.OriginalPng,reopened.Snapshot().OriginalPng);
            Assert.Single(reopened.Snapshot().StickerAssets);
            using var zip=ZipFile.OpenRead(Directory.GetFiles(path,"*.qcapture").Single());
            Assert.NotNull(zip.GetEntry("assets/"+reopened.Document!.BackgroundAssetHash+".png"));
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }

    [Fact] public void BadCutoutDoesNotChangeDocumentOrHistory()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());var token=editor.History!.CurrentStateToken;
        Assert.Throws<InvalidDataException>(()=>editor.ApplyBackgroundRemoval(EditorTests.Png(10,10)));
        Assert.Throws<InvalidDataException>(()=>editor.ApplyBackgroundRemoval([1,2,3]));
        Assert.Equal(token,editor.History.CurrentStateToken);Assert.Null(editor.Document!.BackgroundAssetHash);Assert.Empty(editor.Snapshot().StickerAssets);
        var original=editor.Snapshot();
        Assert.Throws<InvalidDataException>(()=>editor.Open(original with{BackgroundAssetHash=new string('a',64)}));
        Assert.Equal(token,editor.History.CurrentStateToken);
    }
}
