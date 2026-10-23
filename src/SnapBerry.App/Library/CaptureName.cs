namespace SnapBerry.App.Library;

public static class CaptureName
{
    public const int MaxLength=100;
    public static bool IsValid(string? value)=>value is not null&&value.Length<=MaxLength&&!value.Any(char.IsControl);
    public static string Normalize(string value)
    {
        if(!IsValid(value))throw new ArgumentException($"이름은 줄바꿈 없이 {MaxLength}자까지 입력해 주세요.");
        return value.Trim();
    }
}
