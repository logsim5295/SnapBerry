using SnapBerry.Core;
using Xunit;

namespace SnapBerry.Core.Tests;

public class BackgroundRemovalTests
{
    [Fact] public void BackgroundReplacementCountsOnlyAssetsKeptByTheNewDocument()
    {
        var before=new string('a',64);var after=new string('b',64);var document=new AnnotationDocument(20,20){BackgroundAssetHash=before};
        Assert.Equal(new[]{after},document.AssetHashesWithBackground(after));
        document.Annotations.Add(new(){Kind=AnnotationKind.Sticker,AssetHash=before});
        document.Annotations.Add(new(){Kind=AnnotationKind.Sticker,AssetHash=after});
        Assert.Equal(new[]{before,after},document.AssetHashesWithBackground(after));
    }
    [Fact] public void BackgroundChangeIsOneUndoableCommandAndKeepsOtherEdits()
    {
        var document=new AnnotationDocument(80,60);var history=new CommandHistory(document);
        var annotation=new Annotation{Kind=AnnotationKind.Text,Text="메모"};history.Add(annotation);
        history.Crop(new(10,10,40,30));history.Resize(new(160,120));
        var before=history.CurrentStateToken;var hash=new string('a',64);
        history.SetBackground(hash);
        Assert.Equal(hash,document.BackgroundAssetHash);Assert.Single(document.Annotations);Assert.Equal(new PixelSize(160,120),document.OutputSize);
        history.Undo();Assert.Null(document.BackgroundAssetHash);Assert.Equal(before,history.CurrentStateToken);
        history.Redo();Assert.Equal(hash,document.BackgroundAssetHash);Assert.Equal(annotation,Assert.Single(document.Annotations));
    }

    [Fact] public void InvalidOrDuplicateBackgroundDoesNotAddUndoStates()
    {
        var document=new AnnotationDocument(20,20);var history=new CommandHistory(document);
        Assert.Throws<ArgumentException>(()=>history.SetBackground("../bad"));Assert.False(history.CanUndo);
        history.SetBackground(new string('b',64));var token=history.CurrentStateToken;
        history.SetBackground(new string('b',64));Assert.Equal(token,history.CurrentStateToken);
        history.Undo();Assert.False(history.CanUndo);
    }
}
