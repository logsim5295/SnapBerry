using Avalonia.Controls;
using SnapBerry.Desktop.Stickers;

namespace SnapBerry.Desktop.Views;

public sealed partial class MainWindow
{
    private StickerPickerWindow? stickerPicker;private bool restoreSticker;
    private Task ShowStickersAsync()
    {
        if(editor.Document is null)return Task.CompletedTask;
        if(stickerPicker is not null){stickerPicker.Show();stickerPicker.Activate();return Task.CompletedTask;}
        stickerPicker=new(definition=>_ =RunAsync(()=>
        {
            editor.InsertSticker(definition);Control<ComboBox>("ToolBox").SelectedIndex=0;editor.Tool=Editing.DesktopTool.Select;return Task.CompletedTask;
        })){RequestedThemeVariant=ActualThemeVariant};
        stickerPicker.Closed+=(_,_)=>stickerPicker=null;stickerPicker.Show(this);return Task.CompletedTask;
    }
    private Task UpdateKeycapAsync()
    {editor.UpdateKeycapLabel(Control<TextBox>("TextEntry").Text??"");return Task.CompletedTask;}
    private void HideForCapture(){surface.CancelGesture();restoreSticker=stickerPicker?.IsVisible==true;stickerPicker?.Hide();Hide();}
    private void RestoreAfterCapture(){Show();Activate();if(restoreSticker)stickerPicker?.Show(this);restoreSticker=false;}
    private void UpdateStickerCommands()
    {
        Control<Button>("StickerButton").IsEnabled=!busy&&editor.Document is not null;
        var isKeycap=editor.Selected is {} selected&&DesktopStickerCatalog.Load().Any(x=>x.Id==selected.StickerId&&x.IsKeycap);
        Control<Button>("ApplyKeycapButton").IsEnabled=!busy&&isKeycap;
        if(isKeycap&&syncingProperties)Control<TextBox>("TextEntry").Text=editor.Selected!.Text;
    }
}
