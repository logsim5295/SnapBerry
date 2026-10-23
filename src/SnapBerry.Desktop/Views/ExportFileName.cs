namespace SnapBerry.Desktop.Views;

public static class ExportFileName
{
    public static string Suggest(string captureName,DateTime timestamp)
    {
        var stem=new string(captureName.Trim().Select(c=>c<32||"<>:\"/\\|?*".Contains(c)?'_':c).ToArray()).TrimEnd(' ','.');
        if(stem.EndsWith(".png",StringComparison.OrdinalIgnoreCase))stem=stem[..^4].TrimEnd(' ','.');
        if(stem.Length==0)return $"Capture_{timestamp:yyyyMMdd_HHmmss}.png";
        var device=stem.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if(device is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"||device.Length==4&&(device.StartsWith("COM",StringComparison.Ordinal)||device.StartsWith("LPT",StringComparison.Ordinal))&&"123456789¹²³".Contains(device[3]))stem="_"+stem;
        return stem+".png";
    }
}
