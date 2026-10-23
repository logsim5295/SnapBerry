using System.Windows;
using Microsoft.Win32;

namespace SnapBerry.App.Import;

public interface IImagePathPicker {string? Pick(Window owner);}
public sealed class ImagePathPicker : IImagePathPicker
{
    public string? Pick(Window owner)
    {
        var dialog=new OpenFileDialog{Title="이미지 열기",Filter="이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",CheckFileExists=true,Multiselect=false};
        return dialog.ShowDialog(owner)==true?dialog.FileName:null;
    }
}
