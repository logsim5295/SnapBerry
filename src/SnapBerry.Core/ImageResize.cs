namespace SnapBerry.Core;

public readonly record struct PixelSize(int Width,int Height);

public static class ImageResize
{
    // 렌더링 중간 버퍼를 고려한 면적 제한과 입력 가능한 한 변의 상한이다.
    public static void Validate(PixelSize size)
    {
        if(size.Width<1||size.Height<1||size.Width>32767||size.Height>32767||(long)size.Width*size.Height>512L*1024*1024/20)
            throw new ArgumentOutOfRangeException(nameof(size),"가로·세로는 1~32767 px이며 전체 이미지 크기는 약 2684만 픽셀 이하여야 합니다.");
    }
    public static PixelSize KeepAspectRatio(PixelSize original,int value,bool editWidth)
    {
        if(original.Width<1||original.Height<1||value<1||value>32767)throw new ArgumentOutOfRangeException(nameof(value));
        var other=Math.Max(1,(int)Math.Min(int.MaxValue,Math.Round((double)value*(editWidth?original.Height:original.Width)/(editWidth?original.Width:original.Height),MidpointRounding.AwayFromZero)));
        var size=editWidth?new PixelSize(value,other):new PixelSize(other,value);Validate(size);return size;
    }
}
