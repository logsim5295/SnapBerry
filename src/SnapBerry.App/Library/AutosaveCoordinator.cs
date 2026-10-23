namespace SnapBerry.App.Library;
// 짧은 연속 편집은 최신 스냅샷 하나로 합치고, 실제 쓰기는 세마포어로 한 번에 하나만 수행한다.
public sealed class AutosaveCoordinator(Func<CaptureRecord,Task> save) : IDisposable
{
    private readonly object gate=new();private readonly SemaphoreSlim writer=new(1,1);
    private readonly HashSet<Guid> suspended=[];private (CaptureRecord Record,Guid Token)? pending;
    private CancellationTokenSource? delay;private bool disposed;
    public event Action<Guid,Guid>? Saved;public event Action<Exception>? Failed;
    public void Queue(CaptureRecord record,Guid token)
    {
        lock(gate){if(disposed||suspended.Contains(record.Id))return;pending=(record,token);delay?.Cancel();delay?.Dispose();delay=new();_ = DebounceAsync(delay.Token);}
    }
    private async Task DebounceAsync(CancellationToken token)
    {try{await Task.Delay(500,token);await FlushAsync();}catch(OperationCanceledException){}catch(Exception e){Failed?.Invoke(e);}}
    public async Task FlushAsync()
    {
        // 문서 전환·복제·종료 전에 호출한다. 저장 중 새 편집이 들어오면 다음 반복에서 이어 저장한다.
        await writer.WaitAsync();try
        {
            while(true)
            {
                (CaptureRecord Record,Guid Token)? item;lock(gate){item=pending;pending=null;}
                if(item is null)return;var value=item.Value;bool blocked;lock(gate)blocked=suspended.Contains(value.Record.Id);if(blocked)continue;
                try{await save(value.Record);Saved?.Invoke(value.Record.Id,value.Token);}
                catch{lock(gate){if(pending is null&&!suspended.Contains(value.Record.Id))pending=value;}throw;}
            }
        }
        finally{writer.Release();}
    }
    public async Task SuspendAndDrainAsync(Guid id)
    {
        // 삭제 대상의 대기 저장을 제거하고 진행 중 쓰기까지 기다려 자동 저장이 삭제 파일을 되살리지 못하게 한다.
        lock(gate){suspended.Add(id);if(pending?.Record.Id==id)pending=null;delay?.Cancel();}
        await writer.WaitAsync();writer.Release();
    }
    public void Resume(Guid id){lock(gate)suspended.Remove(id);}
    public void Dispose(){lock(gate){disposed=true;pending=null;delay?.Cancel();delay?.Dispose();delay=null;}}
}
