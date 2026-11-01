using Microsoft.Win32;

namespace SnapBerry.App.Settings;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // 이전 버전의 등록과 작업 관리자의 사용/사용 안 함 상태를 유지한다.
    private const string ValueName = "QuickCapture";
    private const string TrayArgument = " --tray";

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            RefreshIfEnabled(true);
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    internal static void RefreshIfEnabled(bool enabled) =>
        RefreshIfEnabled(enabled, Registry.CurrentUser, Environment.ProcessPath);

    internal static void RefreshIfEnabled(bool enabled, RegistryKey currentUser, string? executablePath)
    {
        if (!enabled) return;
        if (string.IsNullOrEmpty(executablePath))
            throw new InvalidOperationException("실행 경로를 확인할 수 없습니다.");

        var command = $"\"{executablePath}\"{TrayArgument}";
        using var existing = currentUser.OpenSubKey(RunKey);
        if (existing?.GetValue(ValueName) is string registered &&
            registered.Equals(command, StringComparison.OrdinalIgnoreCase) &&
            registered.EndsWith(TrayArgument, StringComparison.Ordinal))
            return;

        using var writable = currentUser.CreateSubKey(RunKey);
        writable.SetValue(ValueName, command, RegistryValueKind.String);
    }
}
