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
    private readonly bool showGuides;
    private readonly DrawingVisual horizontal = new(), vertical = new(), cross = new(), magnifier = new();
    private readonly VisualCollection layers;
    private PixelPoint? pointer;
    private static readonly Pen CrossOutline = FrozenPen(Brushes.Black, 4);
    private static readonly Pen CrossForeground = FrozenPen(Brushes.White, 2);
    private static readonly Pen GuidePen = FrozenPen(Brushes.IndianRed, 1);
    private static readonly Pen GridPen = FrozenPen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 0.5);
    private static readonly Pen CenterPen = FrozenPen(Brushes.Gold, 1);
    private static readonly Pen BorderPen = FrozenPen(Brushes.White, 1);
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    public CapturePointerVisual(PixelRect screen, CaptureFrame frame, bool showGuides = true)
    {
        this.screen = screen;
        this.frame = frame;
        this.showGuides = showGuides;
        // 각 선을 별도 노드로 이동해야 두 선의 합집합인 전체 화면이 다시 그려지지 않는다.
        layers = new VisualCollection(this) { horizontal, vertical, magnifier, cross };
        cross.Opacity = magnifier.Opacity = horizontal.Opacity = vertical.Opacity = 0;
        using (var drawing = cross.RenderOpen())
        {
            DrawCross(drawing, new Point(), CrossOutline);
            DrawCross(drawing, new Point(), CrossForeground);
        }
        IsHitTestVisible = false;
        ClipToBounds = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    protected override int VisualChildrenCount => layers.Count;
    protected override Visual GetVisualChild(int index) => layers[index];

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (showGuides)
        {
            using (var drawing = horizontal.RenderOpen())
                drawing.DrawLine(GuidePen, new Point(), new Point(RenderSize.Width, 0));
            using (var drawing = vertical.RenderOpen())
                drawing.DrawLine(GuidePen, new Point(), new Point(0, RenderSize.Height));
        }
        if (pointer is { } current) PositionLayers(current);
    }

    public void SetPointer(PixelPoint point)
    {
        if (pointer == point) return;
        pointer = point;
        PositionLayers(point);
    }

    private void PositionLayers(PixelPoint current)
    {
        if (RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
        // 포인터와 샘플은 물리 픽셀, 표시 크기는 모니터 DPI에 따른 WPF 좌표다.
        var point = new Point((current.X - screen.X) * RenderSize.Width / screen.Width,
            (current.Y - screen.Y) * RenderSize.Height / screen.Height);
        if (showGuides)
        {
            horizontal.Offset = new Vector(0, point.Y);
            vertical.Offset = new Vector(point.X, 0);
            horizontal.Opacity = point.Y >= 0 && point.Y < RenderSize.Height ? 1 : 0;
            vertical.Opacity = point.X >= 0 && point.X < RenderSize.Width ? 1 : 0;
        }
        var onScreen = WindowCaptureSelection.Contains(screen, current);
        cross.Opacity = magnifier.Opacity = onScreen ? 1 : 0;
        if (!onScreen) return;
        cross.Offset = (Vector)point;
        var panel = CaptureMagnifier.Place(point, RenderSize);
        magnifier.Offset = (Vector)panel.TopLeft;
        using var drawing = magnifier.RenderOpen();
        drawing.DrawRectangle(Brushes.Black, BorderPen, new Rect(panel.Size));
        drawing.PushTransform(new TranslateTransform(CaptureMagnifier.Padding, CaptureMagnifier.Padding));
        drawing.DrawRectangle(Brushes.DimGray, null, new Rect(0, 0, CaptureMagnifier.ImageSize, CaptureMagnifier.ImageSize));
        var region = CaptureMagnifier.Sample(current, frame.DesktopBounds);
        if (!region.Source.IsEmpty)
        {
            var sample = new CroppedBitmap(frame.Image, region.Source);
            sample.Freeze();
            drawing.DrawImage(sample, region.Destination);
        }
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
        drawing.DrawText(label, new Point(CaptureMagnifier.Padding, CaptureMagnifier.PanelWidth + 5));
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
