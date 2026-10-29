using System.Windows;
using SnapBerry.Core;

namespace SnapBerry.App.Capture;

internal static class CaptureMagnifier
{
    public const int Radius = 8;
    public const int Zoom = 8;
    public const int ImageSize = (Radius * 2 + 1) * Zoom;
    public const int Padding = 4;
    public const int PanelWidth = ImageSize + Padding * 2;
    public const int PanelHeight = PanelWidth + 26;

    // 화면 가장자리에서도 중심 픽셀을 이동하거나 늘리지 않고, 없는 픽셀은 빈칸으로 남긴다.
    public static (Int32Rect Source, Rect Destination) Sample(PixelPoint pointer, PixelRect desktop)
    {
        var requested = new PixelRect((int)Math.Floor(pointer.X) - Radius, (int)Math.Floor(pointer.Y) - Radius, Radius * 2 + 1, Radius * 2 + 1);
        if (WindowCaptureSelection.Clip(requested, desktop) is not { } clipped) return (Int32Rect.Empty, Rect.Empty);
        return (new(clipped.X - desktop.X, clipped.Y - desktop.Y, clipped.Width, clipped.Height),
            new((clipped.X - requested.X) * Zoom, (clipped.Y - requested.Y) * Zoom, clipped.Width * Zoom, clipped.Height * Zoom));
    }

    public static Rect Place(Point pointer, Size viewport)
    {
        const int gap = 28;
        var x = pointer.X + gap;
        var y = pointer.Y + gap;
        if (x + PanelWidth > viewport.Width) x = pointer.X - gap - PanelWidth;
        if (y + PanelHeight > viewport.Height) y = pointer.Y - gap - PanelHeight;
        return new(Math.Clamp(x, 0, Math.Max(0, viewport.Width - PanelWidth)),
            Math.Clamp(y, 0, Math.Max(0, viewport.Height - PanelHeight)), PanelWidth, PanelHeight);
    }
}
