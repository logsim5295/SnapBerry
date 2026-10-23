using System.IO;
using SnapBerry.App.Library;
using Xunit;
namespace SnapBerry.Windows.Tests;
// 저장 직렬화, 최신 스냅샷 병합, 실패 재시도와 삭제 대상 저장 중단을 검증한다.
public class AutosaveTests
{
    [Fact] public async Task AutosaveStoresLatestCommittedState()
    {
        var stored=new List<int>();var c=new AutosaveCoordinator(r=>{stored.Add(r.Width);return Task.CompletedTask;});
        c.Queue(new(){Width=1},Guid.NewGuid());c.Queue(new(){Width=2},Guid.NewGuid());await c.FlushAsync();Assert.Equal(new[]{2},stored);
    }
    [Fact] public async Task DeletingActiveCaptureCannotRecreateRecord()
    {
        var stored=new List<Guid>();var id=Guid.NewGuid();var c=new AutosaveCoordinator(r=>{stored.Add(r.Id);return Task.CompletedTask;});
        c.Queue(new(){Id=id},Guid.NewGuid());await c.SuspendAndDrainAsync(id);c.Queue(new(){Id=id},Guid.NewGuid());await c.FlushAsync();Assert.Empty(stored);
    }
    [Fact] public async Task FailedSaveRemainsPending()
    {
        var fail=true;var stored=0;var c=new AutosaveCoordinator(r=>{if(fail)throw new IOException();stored++;return Task.CompletedTask;});
        c.Queue(new(),Guid.NewGuid());await Assert.ThrowsAsync<IOException>(()=>c.FlushAsync());fail=false;await c.FlushAsync();Assert.Equal(1,stored);
    }
}
