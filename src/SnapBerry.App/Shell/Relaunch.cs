using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using SnapBerry.App.Interop;

namespace SnapBerry.App.Shell;

internal sealed record RunningEditor(nint Window, bool SameBuild, string? Path, string? Version);

internal static class Relaunch
{
    internal static readonly Guid BuildId = typeof(App).Module.ModuleVersionId;
    private static readonly string RequestedVersion =
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(App).Assembly.GetName().Version!.ToString();

    // 기존 단일 인스턴스 잠금은 구버전과 공유한다. 이 표식은 실행 중인 빌드를 구분한다.
    private static string MarkerName(int pid, nint window, Guid build) => $@"Local\SnapBerry.Editor.{pid}.{window:X}.{build:N}";
    internal static Mutex MarkEditor(int pid, nint window, Guid build) => new(false, MarkerName(pid, window, build));

    internal static bool IsSameBuild(int pid, nint window, Guid build)
    {
        if (!Mutex.TryOpenExisting(MarkerName(pid, window, build), out var marker)) return false;
        marker.Dispose();
        return true;
    }

    internal static string? Handle(string? requestedPath, StartupDiagnostics diagnostics)
    {
        // 동시에 실행하면 먼저 시작한 프로세스가 아직 편집기 HWND를 만들고 있을 수 있다.
        var timeout = Stopwatch.StartNew();
        RunningEditor? editor;
        while (true)
        {
            editor = FindEditor();
            if (editor is { SameBuild: true } || timeout.Elapsed >= TimeSpan.FromSeconds(2)) break;
            if (editor is not null && editor.Version != RequestedVersion) break;
            Thread.Sleep(100);
        }

        diagnostics.Write($"instance.relaunch hwnd={editor?.Window} sameBuild={editor?.SameBuild} exe={editor?.Path} version={editor?.Version}");
        var notice = Notice(editor, requestedPath, RequestedVersion);
        if (notice is not null) return notice;
        return NativeMethods.PostMessage(editor!.Window, InstanceActivation.Message, 0, 0)
            ? null : "기존 편집기 창을 열 수 없습니다. 트레이 메뉴에서 편집기 열기를 선택해 주세요.";
    }

    private static RunningEditor? FindEditor()
    {
        RunningEditor? otherBuild = null;
        foreach (var title in new[] { "SnapBerry", "QuickCapture" })
        {
            nint window = 0;
            while ((window = NativeMethods.FindWindowEx(0, window, null, title)) != 0)
            {
                if (NativeMethods.GetWindow(window, 4 /* GW_OWNER */) != 0) continue;
                NativeMethods.GetWindowThreadProcessId(window, out var pid);
                if (pid == 0 || pid == Environment.ProcessId) continue;
                try
                {
                    if (IsSameBuild((int)pid, window, BuildId))
                        return new(window, true, null, null);
                    using var process = Process.GetProcessById((int)pid);
                    using var module = process.MainModule;
                    if (module is null) continue;
                    var info = module.FileVersionInfo;
                    if (IsAppProduct(info.ProductName))
                        otherBuild ??= new(window, false, module.FileName, info.ProductVersion ?? info.FileVersion);
                }
                catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException)
                {
                    // 창을 찾는 동안 프로세스가 종료되거나 조회 권한이 없을 수 있다.
                }
            }
        }
        return otherBuild;
    }

    internal static bool IsAppProduct(string? product) =>
        string.Equals(product, "SnapBerry", StringComparison.OrdinalIgnoreCase)
        || string.Equals(product, "QuickCapture", StringComparison.OrdinalIgnoreCase);

    internal static string? Notice(RunningEditor? editor, string? requestedPath, string requestedVersion)
    {
        if (editor?.SameBuild == true) return null;
        const string unknown = "확인할 수 없음";
        var reason = editor is null
            ? "실행 중인 SnapBerry의 정보를 확인할 수 없습니다."
            : "다른 빌드의 SnapBerry가 실행 중입니다.";
        return $"{reason}\n\n실행 중인 버전  {editor?.Version ?? unknown}\n실행 경로  {editor?.Path ?? unknown}\n\n"
            + $"지금 실행한 버전  {requestedVersion}\n실행 경로  {requestedPath ?? unknown}\n\n"
            + "작업 표시줄의 SnapBerry 트레이 아이콘을 우클릭해 ‘종료’를 선택한 뒤, 원하는 실행 파일을 다시 실행해 주세요.";
    }
}
