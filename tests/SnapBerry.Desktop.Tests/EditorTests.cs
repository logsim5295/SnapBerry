using SkiaSharp;
using SnapBerry.App.Library;
using SnapBerry.Core;
using SnapBerry.Desktop.Editing;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class EditorTests
{
    [Fact] public void CompositeConvertsProfiledClipboardColorsToSrgbInsteadOfDroppingTheProfile()
    {
        // Real macOS native pasteboard PNG. Its magenta maps to green ~64 in sRGB,
        // while discarding its calibrated RGB profile incorrectly yields green 0.
        using var fixture=typeof(EditorTests).Assembly.GetManifestResourceStream("SnapBerry.Desktop.Tests.Fixtures.calibrated-rgb.png")!;
        using var input=new MemoryStream();fixture.CopyTo(input);
        using var editor=new DesktopEditor();editor.OpenImage(input.ToArray());
        using var result=SKBitmap.Decode(editor.RenderPng());var magenta=result.GetPixel(10,10);
        Assert.InRange(magenta.Red,(byte)250,(byte)255);
        Assert.InRange(magenta.Green,(byte)50,(byte)80);
        Assert.InRange(magenta.Blue,(byte)250,(byte)255);
    }

    public static byte[] Png(int width=80,int height=60,SKColor? color=null)
    {using var image=new SKBitmap(width,height);image.Erase(color??SKColors.Blue);using var data=image.Encode(SKEncodedImageFormat.Png,100);return data.ToArray();}
    public static CaptureRecord Record()=>new(){Width=80,Height=60,OriginalPng=Png()};

    [Fact] public void FailedOpenKeepsTheCurrentDocumentAndUndo()
    {
        using var editor=new DesktopEditor();editor.Open(Record());editor.Commit(new(){Kind=AnnotationKind.Rectangle});
        var document=editor.Document;var token=editor.History!.CurrentStateToken;
        Assert.ThrowsAny<Exception>(()=>editor.Open(Record() with{OriginalPng=[1,2,3]}));
        Assert.Same(document,editor.Document);Assert.Equal(token,editor.History.CurrentStateToken);Assert.True(editor.History.CanUndo);
    }
    [Fact] public void PasteDuplicateMoveAndDeleteAreUndoable()
    {
        using var editor=new DesktopEditor();editor.Open(Record());editor.InsertImage(Png(20,10,SKColors.Red));
        var first=editor.Selected!;editor.DuplicateSelected();Assert.Equal(2,editor.Document!.Annotations.Count);
        Assert.NotEqual(first.Id,editor.Selected!.Id);Assert.Equal(first.AssetHash,editor.Selected.AssetHash);
        editor.ReplaceSelected(editor.Selected with{X=3,Y=4});editor.DeleteSelected();Assert.Single(editor.Document.Annotations);
        editor.History!.Undo();Assert.Equal(3,editor.Document.Annotations[1].X);editor.History.Undo();editor.History.Undo();Assert.Single(editor.Document.Annotations);
        editor.History.Redo();Assert.Equal(2,editor.Document.Annotations.Count);
    }
    [Fact] public void ExportAndCropUseOriginalCoordinatesWithoutSelectionChrome()
    {
        using var editor=new DesktopEditor();editor.Open(Record());editor.InsertImage(Png(20,10,SKColors.Red));
        editor.History!.Crop(new(30,25,20,10));using var result=SKBitmap.Decode(editor.RenderPng());
        Assert.Equal(20,result.Width);Assert.Equal(10,result.Height);Assert.Equal(SKColors.Red,result.GetPixel(10,5));
        editor.History.Undo();using var restored=SKBitmap.Decode(editor.RenderPng());Assert.Equal(80,restored.Width);
    }
    [Fact] public async Task SavedDocumentPreservesEditableHistoryAndAssetBytes()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-desktop-"+Guid.NewGuid());
        try
        {
            using var editor=new DesktopEditor();editor.Open(Record());editor.InsertImage(Png(20,10));editor.Rename("업무 결과");
            var store=new CaptureLibraryStore(path);await editor.SaveAsync(store);Assert.False(editor.History!.IsPersistenceDirty);
            using var reopened=new DesktopEditor();reopened.Open(await store.LoadAsync(editor.CaptureId!.Value));
            Assert.Equal("업무 결과",reopened.Name);Assert.Equal(editor.RenderPng(),reopened.RenderPng());
            Assert.Single(await store.ListAsync(0,50,"업무"));editor.Rename("");await editor.SaveAsync(store);
            Assert.Single(await store.ListAsync(0,50,editor.Snapshot().CapturedAtUtc.ToLocalTime().ToString("MM.dd")));
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [Fact] public async Task SaveFailureKeepsDirtyStateAndExistingDocument()
    {
        var path=Path.GetTempFileName();
        try
        {
            using var editor=new DesktopEditor();editor.Open(Record());editor.Commit(new(){Kind=AnnotationKind.Rectangle});var document=editor.Document;
            await Assert.ThrowsAnyAsync<IOException>(()=>editor.SaveAsync(new(path)));Assert.Same(document,editor.Document);Assert.True(editor.History!.IsPersistenceDirty);
        }
        finally{File.Delete(path);}
    }
    [Fact] public void CorruptOrMissingImageAssetsDoNotReplaceTheDocument()
    {
        using var editor=new DesktopEditor();editor.Open(Record());var document=editor.Document;
        Assert.ThrowsAny<Exception>(()=>editor.Open(Record() with{Annotations=[new(){Kind=AnnotationKind.Sticker,AssetHash=new string('a',64)}]}));
        Assert.Same(document,editor.Document);
        Assert.ThrowsAny<Exception>(()=>editor.Open(Record() with{Width=1000000,Height=1000000}));Assert.Same(document,editor.Document);
    }
    [Fact] public void PngAndPreviewUseTheSameCompositionAndResize()
    {
        using var editor=new DesktopEditor();editor.Open(Record());editor.Commit(new(){Kind=AnnotationKind.Rectangle,X=4,Y=4,Width=30,Height=20,Style=new(){Color="#00FF00",Thickness=4}});
        editor.History!.Resize(new(160,120));using var result=SKBitmap.Decode(editor.RenderPng());Assert.Equal(160,result.Width);Assert.Equal(120,result.Height);
        Assert.Equal(SKColors.Lime,result.GetPixel(8,20));Assert.Equal(SKColors.Blue,result.GetPixel(80,80));
    }
    [Fact] public void StepNumbersAdvanceAndUndoTogether()
    {
        using var editor=new DesktopEditor();editor.Open(Record());editor.InsertStep(new(5,5));Assert.Equal("1",Assert.Single(editor.Document!.Annotations).Text);
        Assert.Equal(2,editor.Document.Sequence.NextNumeric);editor.History!.Undo();Assert.Empty(editor.Document.Annotations);Assert.Equal(1,editor.Document.Sequence.NextNumeric);
    }
}
