namespace SnapBerry.App.Capture;

internal interface IWindowTransitions
{
    void SetDisabled(nint handle,bool disabled);
}

// 이 앱은 평상시 전환 효과를 억제하지 않는다. 일부 창에서 실패해도 앞서 바꾼 창을 복구한다.
internal sealed class WindowTransitionScope : IDisposable
{
    private readonly IWindowTransitions native;
    private readonly List<nint> changed=[];
    private bool disposed;
    public WindowTransitionScope(IEnumerable<nint> handles,IWindowTransitions native)
    {
        this.native=native;
        try
        {
            foreach(var handle in handles.Distinct())
            {
                native.SetDisabled(handle,true);changed.Add(handle);
            }
        }
        catch(Exception error)
        {
            try{Dispose();}catch(Exception restoreError){throw new AggregateException(error,restoreError);}
            throw;
        }
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;var errors=new List<Exception>();
        foreach(var handle in changed.AsEnumerable().Reverse())
            try{native.SetDisabled(handle,false);}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("캡처 창 전환 설정을 복구하지 못했습니다.",errors);
    }
}
