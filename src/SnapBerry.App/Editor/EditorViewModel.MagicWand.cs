using SnapBerry.Core;
using SnapBerry.Imaging;

namespace SnapBerry.App.Editor;

public sealed partial class EditorViewModel
{
    private MagicWandSelection? magicWand;
    public int MagicWandTolerance {get;set;}=15;public bool MagicWandGlobal {get;set;}
    public event EventHandler? MagicWandChanged;
    public bool HasPixelSelection=>magicWand?.Count>0;
    public int MagicWandCount=>magicWand?.Count??0;
    public byte[]? MagicWandPreview=>magicWand?.PreviewPng;
    public void SelectMagicWand(PixelPoint point,int tolerance,bool global)
    {
        if(Document is null||record is null)return;
        var selection=magicWand??new MagicWandSelection(Document.BackgroundAssetHash is {} hash?Assets.Data[hash]:record.OriginalPng,Document.CropBounds);
        try{selection.Select((int)Math.Floor(point.X),(int)Math.Floor(point.Y),tolerance,global);}catch{if(magicWand is null)selection.Dispose();throw;}
        magicWand=selection;SelectedId=null;MagicWandChanged?.Invoke(this,EventArgs.Empty);
    }
    public void UpdateMagicWand(int tolerance,bool global)
    {if(magicWand is not {} selection)return;selection.Select(selection.SeedX,selection.SeedY,tolerance,global);MagicWandChanged?.Invoke(this,EventArgs.Empty);}
    public void ClearMagicWand()
    {if(magicWand is null)return;magicWand.Dispose();magicWand=null;MagicWandChanged?.Invoke(this,EventArgs.Empty);}
    public bool DeleteMagicWandSelection()
    {if(!HasPixelSelection)return false;var png=magicWand!.Erase();ApplyBackgroundRemoval(png);ClearMagicWand();return true;}
}
