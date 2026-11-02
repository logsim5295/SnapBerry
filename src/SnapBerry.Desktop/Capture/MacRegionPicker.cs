using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SnapBerry.Desktop.Tests")]

namespace SnapBerry.Desktop.Capture;

internal sealed class MacRegionPicker(Point initialPointer)
{
    private Point pointer = initialPointer;
    private readonly List<Window> windows = [];
    private readonly TaskCompletionSource<Rect?> selection = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Point? anchor;

    public static Task<Rect?> PickAsync(CancellationToken token)
    {
        Dispatcher.UIThread.VerifyAccess();
        var displays = MacDisplays.GetActive();
        return new MacRegionPicker(MacPickerNative.GetPointer()).SelectAsync(displays, MacPickerNative.ConfigureWindow, token);
    }

    internal async Task<Rect?> SelectAsync(IReadOnlyList<MacDisplay> displays, Action<Window> configureWindow, CancellationToken token)
    {
        if (displays.Count == 0) throw new IOException("캡처할 화면을 찾을 수 없습니다.");
        using var registration = token.Register(() => selection.TrySetCanceled(token));
        try
        {
            foreach (var display in displays)
            {
                token.ThrowIfCancellationRequested();
                var screen = new Rect(display.X, display.Y, display.Width, display.Height);
                var surface = new GuideSurface(this, screen);
                var window = new Window
                {
                    Title = "SnapBerry 영역 선택",
                    WindowDecorations = WindowDecorations.None,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Position = new((int)screen.X, (int)screen.Y),
                    Width = screen.Width, Height = screen.Height,
                    CanResize = false, ShowInTaskbar = false,
                    ShowActivated = screen.Contains(pointer),
                    TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
                    Background = Brushes.Transparent, Content = surface,
                    Cursor = new Cursor(StandardCursorType.Cross)
                };
                windows.Add(window);
                window.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Escape) selection.TrySetResult(null);
                };
                window.Closed += (_, _) => selection.TrySetResult(null);
                configureWindow(window);
                window.Show();
            }
            return await selection.Task;
        }
        finally
        {
            // Remove every guide window before screencapture receives the rectangle.
            foreach (var window in windows) window.Close();
        }
    }

    private void Move(Point point)
    {
        pointer = point;
        foreach (var window in windows) ((GuideSurface)window.Content!).InvalidateVisual();
    }

    private sealed class GuideSurface(MacRegionPicker picker, Rect screen) : Control
    {
        private static readonly IBrush Tint = new SolidColorBrush(Color.FromArgb(35, 0, 0, 0));
        private static readonly Pen Guide = new(Brushes.IndianRed, 1);
        private static readonly Pen Border = new(Brushes.White, 1);

        private Point GlobalPoint(PointerEventArgs e) => e.GetPosition(this) + (Vector)screen.TopLeft;

        protected override void OnPointerMoved(PointerEventArgs e) => picker.Move(GlobalPoint(e));

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var buttons = e.GetCurrentPoint(this).Properties;
            if (buttons.IsRightButtonPressed) { picker.selection.TrySetResult(null); return; }
            if (!buttons.IsLeftButtonPressed) return;
            picker.anchor = GlobalPoint(e);
            picker.Move(picker.anchor.Value);
            e.Pointer.Capture(this);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton != MouseButton.Left || picker.anchor is not { } start) return;
            var end = GlobalPoint(e);
            var region = new Rect(new Point(Math.Round(start.X), Math.Round(start.Y)),
                new Point(Math.Round(end.X), Math.Round(end.Y))).Normalize();
            picker.selection.TrySetResult(region.Width > 0 && region.Height > 0 ? region : null);
            e.Pointer.Capture(null);
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            context.DrawRectangle(Tint, null, bounds);
            var origin = (Vector)screen.TopLeft;
            var local = picker.pointer - origin;
            if (picker.anchor is { } start)
                context.DrawRectangle(null, Border, new Rect(start - origin, local).Normalize());
            context.DrawLine(Guide, new(0, local.Y), new(bounds.Width, local.Y));
            context.DrawLine(Guide, new(local.X, 0), new(local.X, bounds.Height));
        }
    }
}
