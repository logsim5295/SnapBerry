using SkiaSharp;
using SnapBerry.Core;
using SnapBerry.Desktop.Clipboard;
using SnapBerry.Desktop.Editing;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class ClipboardTests
{
    public sealed class MemoryClipboard : IEditorClipboard
    {
        public ImageClipboardContent? Content;public int Writes;
        public Task<ImageClipboardContent?> ReadAsync()=>Task.FromResult(Content);
        public Task WriteAsync(ImageClipboardContent content){Content=content;Writes++;return Task.CompletedTask;}
    }
    [Fact] public async Task CopyingAResizedImageAndPastingIntoAnotherDocumentPreservesPixelsAndDisplaySize()
    {
        var clipboard=new MemoryClipboard();var commands=new ClipboardCommands(clipboard);
        using var source=new DesktopEditor();source.Open(EditorTests.Record());source.InsertImage(EditorTests.Png(20,10,SKColors.Red));
        source.ReplaceSelected(source.Selected! with{Width=7.5,Height=3.75});source.History!.Resize(new(160,120));
        await commands.CopyAsync(source);var copy=clipboard.Content!;Assert.Equal(new ImageObjectSize(15,7.5),copy.ObjectSize);
        using var destination=new DesktopEditor();destination.Open(EditorTests.Record());var document=destination.Document;var undo=destination.History!.CurrentStateToken;
        Assert.True(await commands.PasteAsync(destination,()=>Task.CompletedTask));Assert.Same(document,destination.Document);Assert.Equal(15,destination.Selected!.Width);Assert.Equal(7.5,destination.Selected.Height);
        using var original=SKBitmap.Decode(destination.Assets[destination.Selected.AssetHash]);Assert.Equal(20,original.Width);Assert.Equal(10,original.Height);
        destination.History.Undo();Assert.Empty(destination.Document!.Annotations);Assert.Equal(undo,destination.History.CurrentStateToken);destination.History.Redo();Assert.Single(destination.Document.Annotations);
    }
    [Fact] public async Task EmptyEditorOpensClipboardAsANewDocumentAndEmptyClipboardDoesNothing()
    {
        var clipboard=new MemoryClipboard();var commands=new ClipboardCommands(clipboard);using var editor=new DesktopEditor();var flushes=0;
        Assert.False(await commands.PasteAsync(editor,()=>{flushes++;return Task.CompletedTask;}));Assert.Equal(0,flushes);
        clipboard.Content=new(EditorTests.Png());Assert.True(await commands.PasteAsync(editor,()=>{flushes++;return Task.CompletedTask;}));
        Assert.Equal(1,flushes);Assert.Equal(CaptureMode.Clipboard,editor.Snapshot().Mode);
    }
    [Fact] public async Task CorruptPasteAndClipboardWriteFailureKeepDocumentAndUndoUnchanged()
    {
        var clipboard=new MemoryClipboard{Content=new([0,1,2])};using var editor=new DesktopEditor();editor.Open(EditorTests.Record());var token=editor.History!.CurrentStateToken;
        await Assert.ThrowsAnyAsync<Exception>(()=>new ClipboardCommands(clipboard).PasteAsync(editor,()=>Task.CompletedTask));Assert.Equal(token,editor.History.CurrentStateToken);Assert.Empty(editor.Document!.Annotations);
        await Assert.ThrowsAsync<IOException>(()=>new ClipboardCommands(new BrokenClipboard()).CopyAsync(editor));Assert.True(editor.History.IsDirty);
    }
    private sealed class BrokenClipboard : IEditorClipboard
    {public Task<ImageClipboardContent?> ReadAsync()=>Task.FromResult<ImageClipboardContent?>(null);public Task WriteAsync(ImageClipboardContent content)=>throw new IOException("클립보드 실패");}
    [Theory] [InlineData("NaN|10")] [InlineData("Infinity|5")] [InlineData("-1|10")] [InlineData("1000001|10")] [InlineData("bad")]
    public void InvalidClipboardObjectSizeIsIgnored(string value)=>Assert.Null(ImageObjectSize.Parse(value));
    [Fact] public async Task DocumentCopyContainsVisibleAnnotationsAndDoesNotCopySelectionChrome()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.InsertImage(EditorTests.Png(20,10,SKColors.Red));var clipboard=new MemoryClipboard();
        await new ClipboardCommands(clipboard).CopyAsync(editor,true);Assert.Null(clipboard.Content!.ObjectSize);
        using var result=SKBitmap.Decode(clipboard.Content.Png);Assert.Equal(80,result.Width);Assert.Equal(SKColors.Red,result.GetPixel(40,30));Assert.False(editor.History!.IsDirty);
    }
}
