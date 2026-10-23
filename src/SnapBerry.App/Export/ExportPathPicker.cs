using System.Windows;
using System.IO;
using Microsoft.Win32;
namespace SnapBerry.App.Export;
// 저장 대화상자를 인터페이스로 분리하여 실제 UI 없이 내보내기 흐름을 테스트한다.
public interface IExportPathPicker {string? Pick(Window owner,string directory,string suggestedFileName);}
public sealed class ExportPathPicker : IExportPathPicker
{
    public string? Pick(Window owner,string directory,string suggestedFileName)
    {var dialog=CreateDialog(directory,suggestedFileName);return dialog.ShowDialog(owner)==true?dialog.FileName:null;}

    public static SaveFileDialog CreateDialog(string directory,string suggestedFileName)
    {
        // 추천 파일명과 같은 형식을 선택하여 표시 형식과 기본 확장자를 함께 복원한다.
        var format=ImageFileWriter.TryGetFormat(suggestedFileName,out var parsed)?parsed:ExportFormat.Png;
        return new()
        {
            Title="이미지 파일 저장",
            Filter="PNG 이미지 (*.png)|*.png|JPEG 이미지 (*.jpg;*.jpeg)|*.jpg;*.jpeg|WebP 이미지 (*.webp)|*.webp|BMP 이미지 (*.bmp)|*.bmp|TIFF 이미지 (*.tif;*.tiff)|*.tif;*.tiff|GIF 이미지 (*.gif)|*.gif",
            FileName=suggestedFileName,DefaultExt=ImageFileWriter.GetDefaultExtension(format),
            FilterIndex=format switch
            {
                ExportFormat.Png=>1,ExportFormat.Jpeg=>2,ExportFormat.WebP=>3,
                ExportFormat.Bmp=>4,ExportFormat.Tiff=>5,ExportFormat.Gif=>6,
                _=>throw new ArgumentOutOfRangeException(nameof(format))
            },
            AddExtension=true,InitialDirectory=directory,OverwritePrompt=true
        };
    }

    public static string SuggestFileName(string captureName,DateTime timestamp,ExportFormat format=ExportFormat.Png)
    {
        var extension=ImageFileWriter.GetDefaultExtension(format);
        // 이력 이름은 보존하고 파일명에만 Windows 제한을 적용한다.
        var invalid=Path.GetInvalidFileNameChars();
        var stem=new string(captureName.Trim().Select(c=>invalid.Contains(c)?'_':c).ToArray()).TrimEnd(' ','.');
        if(ImageFileWriter.TryGetFormat(stem,out _))stem=stem[..^Path.GetExtension(stem).Length].TrimEnd(' ','.');
        if(stem.Length==0)return $"Capture_{timestamp:yyyyMMdd_HHmmss}{extension}";
        var device=stem.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if(device is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"||
            (device.Length==4&&(device.StartsWith("COM",StringComparison.Ordinal)||device.StartsWith("LPT",StringComparison.Ordinal))&&"123456789¹²³".Contains(device[3])))stem="_"+stem;
        return stem+extension;
    }
}
