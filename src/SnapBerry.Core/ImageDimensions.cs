namespace SnapBerry.Core;

// Windows·macOS 저장소에서 같은 중간 버퍼 예산을 적용한다.
public static class ImageDimensions
{
    public static void GuardSize(int width,int height)
    {if(width<=0||height<=0||checked((long)width*height*20)>512L*1024*1024)throw new InvalidOperationException("Image is too large. Capture a smaller region.");}
}
