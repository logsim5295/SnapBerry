using SkiaSharp;
using SnapBerry.App.Library;
using SnapBerry.Desktop.Editing;
using SnapBerry.Desktop.Stickers;
using Xunit;

namespace SnapBerry.Desktop.Tests;

public class StickerTests
{
    [Fact] public void SharedCatalogAndMacKeysHaveUniqueIdsAndKoreanCategories()
    {
        var definitions=DesktopStickerCatalog.Load();Assert.True(definitions.Count>=80);Assert.Equal(definitions.Count,definitions.Select(x=>x.Id).Distinct().Count());
        Assert.Equal(new[]{"이모지","키보드","체크 / 엑스","기호","방향","업무"},definitions.Select(x=>DesktopStickerCatalog.CategoryName(x.Category)).Distinct());
        Assert.Contains(definitions,x=>x.Id=="key-mac-command");Assert.Contains(DesktopStickerCatalog.Search("Keyboard","Command"),x=>x.Id=="key-mac-command");
        Assert.All(DesktopStickerCatalog.Search("Work",""),x=>Assert.Equal("Work",x.Category));
    }
    [Fact] public void EveryStickerProducesReadableTransparentPngAndDifferentAssets()
    {
        var hashes=new HashSet<string>();
        foreach(var definition in DesktopStickerCatalog.Load())
        {
            var png=StickerPngRenderer.CreatePng(definition);hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(png)));
            using var bitmap=SKBitmap.Decode(png);Assert.NotNull(bitmap);Assert.Equal(256,bitmap.Height);
            Assert.Contains(bitmap.Pixels,x=>x.Alpha>0);Assert.Contains(bitmap.Pixels,x=>x.Alpha==0);
        }
        Assert.True(hashes.Count>=70);
    }
    [Fact] public async Task StickerAndChangedKeyboardLabelSurviveUndoAndReopen()
    {
        var path=Path.Combine(Path.GetTempPath(),"SnapBerry-mac-sticker-"+Guid.NewGuid());
        try
        {
            using var editor=new DesktopEditor();editor.Open(EditorTests.Record());var definition=DesktopStickerCatalog.Load().First(x=>x.Id=="key-mac-command");
            editor.InsertSticker(definition);var first=editor.Selected!;editor.UpdateKeycapLabel("Command+Shift+P");var changed=editor.Selected!;
            Assert.NotEqual(first.AssetHash,changed.AssetHash);Assert.Equal("Command+Shift+P",changed.Text);Assert.Equal(first.Id,changed.Id);
            editor.History!.Undo();Assert.Equal(first,editor.Selected);editor.History.Redo();Assert.Equal(changed,editor.Selected);
            var store=new CaptureLibraryStore(path);await editor.SaveAsync(store);using var reopened=new DesktopEditor();reopened.Open(await store.LoadAsync(editor.CaptureId!.Value));Assert.Equal(changed,Assert.Single(reopened.Document!.Annotations));
            editor.History.Undo();editor.History.Undo();Assert.Empty(editor.Document!.Annotations);
        }
        finally{if(Directory.Exists(path))Directory.Delete(path,true);}
    }
    [Fact] public void InvalidKeyboardLabelLeavesAnnotationAndUndoUnchanged()
    {
        using var editor=new DesktopEditor();editor.Open(EditorTests.Record());editor.InsertSticker(DesktopStickerCatalog.Load().First(x=>x.IsKeycap));var original=editor.Selected;var token=editor.History!.CurrentStateToken;
        Assert.Throws<ArgumentException>(()=>editor.UpdateKeycapLabel(new string('a',65)));Assert.Equal(original,editor.Selected);Assert.Equal(token,editor.History.CurrentStateToken);
    }
}
