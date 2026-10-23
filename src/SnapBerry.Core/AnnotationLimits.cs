namespace SnapBerry.Core;

// 입력과 이력 파일 검증이 같은 제한을 사용한다.
public static class AnnotationLimits
{
    public const int MaxTextLength=8192;
    public static void ValidateText(string text)
    {
        if(string.IsNullOrWhiteSpace(text))throw new ArgumentException("텍스트를 입력해 주세요.",nameof(text));
        if(text.Length>MaxTextLength)throw new ArgumentException($"텍스트는 {MaxTextLength}자까지 입력할 수 있습니다.",nameof(text));
    }
}
