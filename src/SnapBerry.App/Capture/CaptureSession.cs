namespace SnapBerry.App.Capture;

// 전환 효과를 끈 상태를 캡처 요청 전체에 유지하고, 숨김 렌더링이 끝난 뒤에만 픽셀을 읽는다.
internal interface ICaptureDesktop
{
    IDisposable SuppressTransitions();
    void HideEditor();
    Task WaitForRenderAsync();
    void FlushComposition();
    bool IsEditorVisible {get;}
    CaptureFrame CaptureDesktop();
}

internal sealed class CaptureSession : IDisposable
{
    private readonly ICaptureDesktop desktop;
    private readonly IDisposable transitions;
    private bool disposed;
    public CaptureSession(ICaptureDesktop desktop)
    {this.desktop=desktop;transitions=desktop.SuppressTransitions();}

    public async Task<CaptureFrame> CaptureAsync()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        desktop.HideEditor();
        await desktop.WaitForRenderAsync();
        desktop.FlushComposition();
        if(desktop.IsEditorVisible)throw new InvalidOperationException("캡처 창을 완전히 숨기지 못했습니다. 다시 시도해 주세요.");
        return desktop.CaptureDesktop();
    }

    public void Dispose(){if(disposed)return;disposed=true;transitions.Dispose();}
}
