using System.Runtime.InteropServices;
using SkiaSharp;
using SnapBerry.Core;

namespace SnapBerry.Imaging;

// 선택은 임시 마스크이며 Erase를 확정하기 전에는 이미지 바이트나 문서를 바꾸지 않는다.
public sealed class MagicWandSelection : IDisposable
{
    private byte[] pixels,mask;private readonly PixelRect bounds;private byte[]? preview;
    public int Width {get;}public int Height {get;}public int Count {get;private set;}
    public int SeedX {get;private set;}public int SeedY {get;private set;}
    public MagicWandSelection(byte[] png,PixelRect bounds)
    {
        using var data=SKData.CreateCopy(png);using var codec=SKCodec.Create(data)??throw new InvalidDataException("이미지를 읽을 수 없습니다.");
        Width=codec.Info.Width;Height=codec.Info.Height;ImageDimensions.GuardSize(Width,Height);
        if(!CropPlacement.IsWithin(bounds,Width,Height))throw new ArgumentOutOfRangeException(nameof(bounds));this.bounds=bounds;
        using var space=SKColorSpace.CreateSrgb();using var image=new SKBitmap(new SKImageInfo(Width,Height,SKColorType.Rgba8888,SKAlphaType.Unpremul,space));
        if(codec.GetPixels(image.Info,image.GetPixels())!=SKCodecResult.Success)throw new InvalidDataException("이미지가 손상되었습니다.");
        pixels=new byte[checked(Width*Height*4)];Marshal.Copy(image.GetPixels(),pixels,0,pixels.Length);mask=new byte[Width*Height];
    }

    public void Select(int x,int y,int tolerance,bool global)
    {
        ObjectDisposedException.ThrowIf(pixels.Length==0,this);
        if(tolerance is <0 or >100)throw new ArgumentOutOfRangeException(nameof(tolerance));
        Array.Clear(mask);Count=0;preview=null;SeedX=x;SeedY=y;
        if(x<bounds.X||y<bounds.Y||x>=bounds.X+bounds.Width||y>=bounds.Y+bounds.Height)return;
        var seed=(y*Width+x)*4;if(pixels[seed+3]==0)return;
        var threshold=(int)Math.Round(tolerance*255d/100);var red=pixels[seed];var green=pixels[seed+1];var blue=pixels[seed+2];
        bool Matches(int index)
        {
            var offset=index*4;return pixels[offset+3]!=0&&Math.Abs(pixels[offset]-red)<=threshold&&Math.Abs(pixels[offset+1]-green)<=threshold&&Math.Abs(pixels[offset+2]-blue)<=threshold;
        }
        if(global)
        {
            for(var yy=bounds.Y;yy<bounds.Y+bounds.Height;yy++)for(var xx=bounds.X;xx<bounds.X+bounds.Width;xx++)
            {var index=yy*Width+xx;if(Matches(index)){mask[index]=1;Count++;}}
            return;
        }
        var pending=new Stack<int>();
        void Visit(int index){if(mask[index]==0&&Matches(index)){mask[index]=1;Count++;pending.Push(index);}}
        Visit(y*Width+x);
        while(pending.TryPop(out var index))
        {
            var xx=index%Width;var yy=index/Width;
            if(xx>bounds.X)Visit(index-1);if(xx<bounds.X+bounds.Width-1)Visit(index+1);
            if(yy>bounds.Y)Visit(index-Width);if(yy<bounds.Y+bounds.Height-1)Visit(index+Width);
        }
    }

    public byte[]? PreviewPng
    {
        get
        {
            if(Count==0)return null;if(preview is not null)return preview;
            var overlay=new byte[pixels.Length];
            for(var i=0;i<mask.Length;i++)if(mask[i]!=0){var offset=i*4;overlay[offset]=146;overlay[offset+1]=60;overlay[offset+2]=180;overlay[offset+3]=110;}
            return preview=Encode(overlay,Width,Height);
        }
    }
    public byte[] Erase()
    {
        ObjectDisposedException.ThrowIf(pixels.Length==0,this);var result=pixels.ToArray();
        for(var i=0;i<mask.Length;i++)if(mask[i]!=0)Array.Clear(result,i*4,4);
        return Encode(result,Width,Height);
    }
    private static byte[] Encode(byte[] rgba,int width,int height)
    {
        using var space=SKColorSpace.CreateSrgb();using var image=new SKBitmap(new SKImageInfo(width,height,SKColorType.Rgba8888,SKAlphaType.Unpremul,space));
        Marshal.Copy(rgba,0,image.GetPixels(),rgba.Length);using var data=image.Encode(SKEncodedImageFormat.Png,100)??throw new InvalidDataException("이미지를 저장할 수 없습니다.");return data.ToArray();
    }
    public static byte[] CheckerboardPng()
    {
        var pixels=new byte[16*16*4];
        for(var y=0;y<16;y++)for(var x=0;x<16;x++){var index=(y*16+x)*4;var shade=(byte)(((x/8+y/8)%2==0)?245:220);pixels[index]=pixels[index+1]=pixels[index+2]=shade;pixels[index+3]=255;}
        return Encode(pixels,16,16);
    }
    public void Dispose(){pixels=[];mask=[];preview=null;Count=0;}
}
