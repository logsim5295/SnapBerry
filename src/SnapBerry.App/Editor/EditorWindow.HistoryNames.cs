using System.Windows;
using System.Windows.Controls;
using SnapBerry.App.Library;

namespace SnapBerry.App.Editor;

public partial class EditorWindow
{
    private readonly Func<string,string?> historyNameEditor;
    private int historySearchVersion;

    private async void HistorySearchChanged(object sender,TextChangedEventArgs e)
    {
        if(!ready)return;
        historySearchVersion++;historyPages=1;hasMoreHistory=false;selectAfterRefresh=null;
        historyScroll?.ScrollToTop();await RefreshLibraryAsync();
    }
    private void ClearHistorySearchClick(object sender,RoutedEventArgs e)=>HistorySearchBox.Clear();
    private async void RenameHistoryMenuClick(object sender,RoutedEventArgs e)
    {if(((MenuItem)sender).DataContext is CaptureEntry entry&&!entry.Item.Damaged)await SafeAsync(()=>RenameHistoryAsync(entry.Item.Id));}

    private async Task RenameHistoryAsync(Guid id)
    {
        if(busy)return;SetBusy(true);
        try
        {
            // 예전 이름의 자동 저장이 뒤늦게 덮어쓰지 않도록 먼저 저장 대기를 해소한다.
            await FlushAsync();
            var active=Model.CaptureId==id;var source=active?Model.Snapshot():await store.LoadAsync(id);
            var value=historyNameEditor(source.Name);if(value is null)return;
            var name=CaptureName.Normalize(value);if(name==source.Name)return;
            await store.SaveAsync(source with{Name=name,UpdatedAtUtc=DateTimeOffset.UtcNow});
            if(active)Model.SetCaptureName(name);
            await RefreshLibraryAsync();StatusText.Text="이력 이름 저장됨";
        }
        finally{SetBusy(false);}
    }
}
