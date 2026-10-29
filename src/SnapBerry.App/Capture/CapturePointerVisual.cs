using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapBerry.Core;

namespace SnapBerry.App.Capture;

// OS 커서 크기와 관계없이 잘 보이는 십자와 원본 픽셀 확대경을 오버레이 안에 그린다.
internal sealed class CapturePointerVisual : FrameworkElement
{
    private readonly PixelRect screen;
    private readonly CaptureFrame frame;
    private PixelPoint? pointer;
    private BitmapSource? sample;
    private Rect sampleDestination;
    private static readonly Pen CrossOutline = FrozenPen(Brushes.Black, 4);
    private static readonly Pen CrossForeground = FrozenPen(Brushes.White, 2);
    private static readonly Pen GridPen = FrozenPen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 0.5);
    private static readonly Pen CenterPen = FrozenPen(Brushes.Gold, 1);
    private static readonly Pen BorderPen = FrozenPen(Brushes.White, 1);
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    public CapturePointerVisual(PixelRect screen, CaptureFrame frame)
    {
        this.screen = screen;
        this.frame = frame;
        IsHitTestVisible = false;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public void SetPointer(PixelPoint point)
    {
        PixelPoint? next = WindowCaptureSelection.Contains(screen, point) ? point : null;
        if (pointer == next) return;
        pointer = next;
        sample = null;
        if (pointer is { } current)
        {
            var region = CaptureMagnifier.Sample(current, frame.DesktopBounds);
            sampleDestination = region.Destination;
            if (!region.Source.IsEmpty)
            {
                sample = new CroppedBitmap(frame.Image, region.Source);
                sample.Freeze();
            }
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (pointer is not { } current || ActualWidth <= 0 || ActualHeight <= 0) return;
        // 포인터와 샘플은 물리 픽셀, 표시 크기는 모니터 DPI에 따른 WPF 좌표다.
        var point = new Point((current.X - screen.X) * ActualWidth / screen.Width,
            (current.Y - screen.Y) * ActualHeight / screen.Height);
        var panel = CaptureMagnifier.Place(point, RenderSize);
        drawing.DrawRectangle(Brushes.Black, BorderPen, panel);
        drawing.PushTransform(new TranslateTransform(panel.X + CaptureMagnifier.Padding, panel.Y + CaptureMagnifier.Padding));
        drawing.DrawRectangle(Brushes.DimGray, null, new Rect(0, 0, CaptureMagnifier.ImageSize, CaptureMagnifier.ImageSize));
        if (sample is not null) drawing.DrawImage(sample, sampleDestination);
        for (var i = 0; i <= CaptureMagnifier.ImageSize; i += CaptureMagnifier.Zoom)
        {
            drawing.DrawLine(GridPen, new Point(i, 0), new Point(i, CaptureMagnifier.ImageSize));
            drawing.DrawLine(GridPen, new Point(0, i), new Point(CaptureMagnifier.ImageSize, i));
        }
        var center = new Rect(CaptureMagnifier.Radius * CaptureMagnifier.Zoom, CaptureMagnifier.Radius * CaptureMagnifier.Zoom,
            CaptureMagnifier.Zoom, CaptureMagnifier.Zoom);
        drawing.DrawRectangle(null, CrossOutline, center);
        drawing.DrawRectangle(null, CenterPen, center);
        drawing.Pop();
        var label = new FormattedText($"{current.X}, {current.Y}  ·  {CaptureMagnifier.Zoom}×", CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, LabelTypeface, 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { MaxTextWidth = CaptureMagnifier.ImageSize, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        drawing.DrawText(label, new Point(panel.X + CaptureMagnifier.Padding, panel.Y + CaptureMagnifier.PanelWidth + 5));
        DrawCross(drawing, point, CrossOutline);
        DrawCross(drawing, point, CrossForeground);
    }

    private static void DrawCross(DrawingContext drawing, Point point, Pen pen)
    {
        const int radius = 18, gap = 4;
        drawing.DrawLine(pen, new Point(point.X - radius, point.Y), new Point(point.X - gap, point.Y));
        drawing.DrawLine(pen, new Point(point.X + gap, point.Y), new Point(point.X + radius, point.Y));
        drawing.DrawLine(pen, new Point(point.X, point.Y - radius), new Point(point.X, point.Y - gap));
        drawing.DrawLine(pen, new Point(point.X, point.Y + gap), new Point(point.X, point.Y + radius));
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
