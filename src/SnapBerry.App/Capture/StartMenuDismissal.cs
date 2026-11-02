using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SnapBerry.App.Interop;

namespace SnapBerry.App.Capture;

internal static class StartMenuDismissal
{
    private static readonly string SystemApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SystemApps");

    internal static Func<Task>? Find()
    {
        var foreground = GetForegroundWindow();
        var window = foreground;
        // 작업 표시줄 등에 포커스가 남은 경우에는 실제 화면을 덮고 있는 셸 창만 후보로 삼는다.
        if (!IsLauncherWindow(window))
        {
            window = 0;
            while ((window = NativeMethods.FindWindowEx(0, window, "Windows.UI.Core.CoreWindow", null)) != 0)
                if (IsVisible(window) && CoversItsCenter(window) && IsLauncherWindow(window)) break;
            if (window == 0) return null;
        }
        NativeMethods.GetWindowThreadProcessId(window, out var frozenOwner);
        return async () =>
        {
            await DismissWindowAsync(window, frozenOwner, foreground);
            // 검색 화면에서 Esc를 누르면 시작 메뉴가 다시 전면에 나타나는 구성도 있다.
            var next = GetForegroundWindow();
            if (next != window && IsLauncherWindow(next))
            {
                NativeMethods.GetWindowThreadProcessId(next, out var nextOwner);
                await DismissWindowAsync(next, nextOwner, next);
            }
        };
    }

    private static Task DismissWindowAsync(nint window, uint pid, nint foreground)
    {
        bool Visible()
        {
            NativeMethods.GetWindowThreadProcessId(window, out var owner);
            return owner == pid && IsVisible(window) && (GetForegroundWindow() == window || CoversItsCenter(window));
        }
        var requestedFocus = false;
        bool CanDismiss()
        {
            if (Down(0x10) || Down(0x11) || Down(0x12) || Down(0x5B) || Down(0x5C) || Down(0x1B)) return false;
            var active = GetForegroundWindow();
            if (active != window && active == foreground && foreground != window && !requestedFocus && CoversItsCenter(window))
            {
                requestedFocus = true;
                NativeMethods.SetForegroundWindow(window);
            }
            return GetForegroundWindow() == window;
        }
        return WaitForCloseAsync(Visible, CanDismiss, SendEscape, () => Task.Delay(25));
    }

    private static bool IsVisible(nint window) => NativeMethods.IsWindowVisible(window) && !NativeMethods.IsIconic(window)
        && NativeMethods.CloakState(window) == 0;

    private static bool CoversItsCenter(nint window)
    {
        if (!NativeMethods.GetWindowRect(window, out var r) || r.Right <= r.Left || r.Bottom <= r.Top) return false;
        var hit = WindowFromPoint(new NativeMethods.Point { X = r.Left + (r.Right - r.Left) / 2, Y = r.Top + (r.Bottom - r.Top) / 2 });
        return GetAncestor(hit, 2) == window; // GA_ROOT
    }

    private static bool IsLauncherWindow(nint window)
    {
        if (window == 0 || NativeMethods.GetWindowThreadProcessId(window, out var pid) == 0) return false;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName is ("StartMenuExperienceHost" or "SearchHost" or "SearchApp")
                && IsShellLauncher(process.MainModule?.FileName);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or ArgumentException) { return false; }
    }

    internal static bool IsShellLauncher(string? path)
    {
        if (path is null) return false;
        var relative = Path.GetRelativePath(SystemApps, path);
        return relative.Equals(@"Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe", StringComparison.OrdinalIgnoreCase)
            || relative.Equals(@"MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe", StringComparison.OrdinalIgnoreCase)
            || relative.Equals(@"Microsoft.Windows.Search_cw5n1h2txyewy\SearchApp.exe", StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task WaitForCloseAsync(Func<bool> visible, Func<bool> canDismiss, Action escape, Func<Task> delay)
    {
        // 단축키의 Ctrl+Shift를 누른 상태로 Esc를 보내면 작업 관리자가 열릴 수 있다.
        // 시작 폴더·검색에서 첫 Esc가 한 단계만 뒤로 가는 경우에만 한 번 더 시도한다.
        var sent = 0;
        var nextAttempt = 0;
        for (var tick = 0; tick < 80; tick++)
        {
            if (!visible()) return;
            if (sent < 2 && tick >= nextAttempt && canDismiss())
            {
                escape();
                sent++;
                nextAttempt = tick + 16;
            }
            await delay();
        }
        if (visible()) throw new InvalidOperationException("시작 메뉴가 영역 선택 화면을 가리고 있습니다. 캡처 단축키에서 손을 뗀 뒤 다시 시도해 주세요.");
    }

    private static bool Down(int key) => GetAsyncKeyState(key) < 0;
    private static void SendEscape()
    {
        var down = new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Key = 0x1B } } };
        var up = down;
        up.Data.Keyboard.Flags = 2; // KEYEVENTF_KEYUP
        var size = Marshal.SizeOf<Input>();
        var count = SendInput(2, [down, up], size);
        if (count == 1) SendInput(1, [up], size);
        if (count != 2) throw new InvalidOperationException("시작 메뉴에 닫기 요청을 전달하지 못했습니다.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse; // INPUT 공용체의 크기와 정렬은 MOUSEINPUT을 포함한다.
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint ExtraInfo; }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativeMethods.Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
